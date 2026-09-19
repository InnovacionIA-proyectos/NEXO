using System.Net.Http.Json;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Infrastructure.Notificaciones.Adaptadores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace CabinaFletes.Api.Tests.Notificaciones;

/// <summary>
/// Marca una prueba que habla con el MailSender de verdad y manda un correo real.
///
/// SE SALTA SALVO QUE SE PIDA EXPLÍCITAMENTE, con las dos variables de abajo. No es
/// pereza: una prueba que manda correos a alguien cada vez que corre la suite acaba
/// silenciada o, peor, ignorada por quien recibe los correos. Así queda repetible para
/// quien quiera comprobar la integración, y muda para todos los demás.
/// </summary>
public sealed class HechoConCorreoRealAttribute : FactAttribute
{
    public const string VarUrl = "MAILSENDER_BASE_URL";
    public const string VarDestinatario = "MAILSENDER_PRUEBA_DESTINATARIO";

    public HechoConCorreoRealAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VarUrl)) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VarDestinatario)))
            Skip =
                $"Manda un correo REAL. Para correrla: {VarUrl}=<url del MailSender> y " +
                $"{VarDestinatario}=<tu correo>.";
    }
}

/// <summary>
/// El adaptador contra el MailSender real.
///
/// POR QUÉ HACE FALTA, teniendo doce pruebas del adaptador contra un servidor de mentira:
/// esas afirman que traducimos bien lo que el servicio responde, pero no que el servicio
/// acepte la forma de nuestro multipart. Eso solo lo dice el servicio.
///
/// Es la misma clase de hueco que ya nos costó una vez: el arranque de la cadena de
/// priorización publicaba al Outbox sin guardar, todas las pruebas en verde, y solo se vio
/// levantando la aplicación de verdad.
/// </summary>
public class MailSenderRealTests(ITestOutputHelper salida)
{
    private static string Url => Environment.GetEnvironmentVariable(HechoConCorreoRealAttribute.VarUrl)!;
    private static string Destinatario =>
        Environment.GetEnvironmentVariable(HechoConCorreoRealAttribute.VarDestinatario)!;

    private static (MailSenderCorreoAdapter adaptador, HttpClient http) Armar()
    {
        var http = new HttpClient { BaseAddress = new Uri(Url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MailSenderCorreoAdapter.ClaveBaseUrl] = Url,
                [MailSenderCorreoAdapter.ClaveRemitente] =
                    Environment.GetEnvironmentVariable("MAILSENDER_REMITENTE"),
            })
            .Build();
        return (new MailSenderCorreoAdapter(http, config, NullLogger<MailSenderCorreoAdapter>.Instance), http);
    }

    [HechoConCorreoReal]
    public async Task El_MailSender_acepta_nuestro_multipart_con_adjunto()
    {
        var (adaptador, http) = Armar();
        using var _ = http;

        // Un PDF mínimo pero válido: si el relay valida el tipo, unos bytes cualquiera no
        // sirven para saber si el problema es nuestro o suyo.
        var pdf = System.Text.Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
            "2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

        var id = await adaptador.EnviarAsync(new CorreoParaEnviar(
            [Destinatario],
            "NEXO — prueba de integración del MailSender (HU-06.1)",
            """
            <p>Correo de prueba de la integración de Nexo con el MailSender.</p>
            <p>Si llegó con el PDF adjunto, el multipart que arma
            <code>MailSenderCorreoAdapter</code> es el que el servicio espera.</p>
            <p style="color:#777;font-size:12px">Generado por la prueba
            MailSenderRealTests. No requiere respuesta.</p>
            """,
            [new AdjuntoDeCorreo("prueba-nexo.pdf", pdf)],
            Metadatos: new Dictionary<string, string> { ["origen"] = "prueba-integracion-hu-061" }));

        salida.WriteLine($"El MailSender aceptó el correo con id: {id ?? "(sin id)"}");
        Assert.False(string.IsNullOrWhiteSpace(id),
            "El 202 tiene que traer un identificador: es con lo que se consulta el estado.");

        // El servicio expone el estado y los intentos. Consultarlo cierra el círculo: no
        // solo aceptó el correo, lo tiene registrado y se puede auditar después.
        var estado = await http.GetStringAsync($"api/v1/emails/{id}");
        salida.WriteLine($"Estado en el relay: {estado}");

        Assert.Contains(id!, estado);
        Assert.Contains(Destinatario, estado, StringComparison.OrdinalIgnoreCase);

        // Y el detalle intento por intento, que es lo que permite responder "¿por qué no
        // llegó?" sin entrar al servidor de correo.
        var intentos = await http.GetStringAsync($"api/v1/emails/{id}/attempts");
        salida.WriteLine($"Intentos: {intentos}");
    }

    [HechoConCorreoReal]
    public async Task Un_correo_sin_destinatarios_lo_rechaza_el_relay_y_no_se_reintenta()
    {
        // Comprueba contra el servicio real que un rechazo suyo se traduce en la clase
        // irreparable. Si el MailSender cambiara ese 400 por un 500, esta prueba lo dice —
        // y ahí sí importaría, porque un 500 se reintentaría cuatro veces para nada.
        var (adaptador, http) = Armar();
        using var _ = http;

        await Assert.ThrowsAsync<CorreoRechazadoException>(() => adaptador.EnviarAsync(
            new CorreoParaEnviar([], "NEXO — no debería salir", "<p>x</p>", [])));
    }
}
