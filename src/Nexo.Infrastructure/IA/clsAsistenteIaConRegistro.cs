using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.IA.Entidades;

namespace Nexo.Infrastructure.IA;

/// <summary>
/// Envuelve al adaptador del proveedor para controlar su uso. Antes de llamar, aplica el tope diario de la persona; después,
/// registra la llamada (función, usuario, modelo, tokens, costo estimado, latencia y resultado) en <c>tbl_ia_uso</c>, con éxito
/// o con error. No guarda el contenido enviado ni la respuesta.
///
/// TOPE DIARIO. El día es el de Colombia (medianoche en UTC-5) y se mide en tokens (entrada, salida y caché). El tope es
/// BLANDO: se verifica antes de llamar con lo ya registrado, así que dos llamadas simultáneas pueden pasarlo por una llamada.
/// Un tope de 0 lo desactiva, y una llamada sin usuario (proceso interno) no tiene tope por persona. Al superarlo responde
/// <c>429 IA_TOPE_DIARIO</c> con el consumo y la hora en que se reinicia.
///
/// NO SE REGISTRA lo que nunca llegó al proveedor (sin llave, función o IA apagada): no consumió nada. Si guardar el registro
/// falla, se avisa en el log y la respuesta al usuario no se pierde.
/// </summary>
public sealed class clsAsistenteIaConRegistro(
    IAsistenteIaPort interno,
    IUsoIaRepository uso,
    IOptions<clsOpcionesAnthropic> opciones,
    TimeProvider reloj,
    ILogger<clsAsistenteIaConRegistro> log) : IAsistenteIaPort
{
    private static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(-5);

    private readonly clsOpcionesAnthropic _opciones = opciones.Value;

    public bool Habilitada(clsFuncionIa funcion) => interno.Habilitada(funcion);

    public async Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default)
    {
        // Si el adaptador va a negarse sin llamar al proveedor, que lo diga él: no hay nada que topar ni registrar.
        if (!interno.Habilitada(peticion.Funcion))
            return await interno.CompletarAsync(peticion, ct);

        await VerificarTopeAsync(peticion, ct);

        var cronometro = Stopwatch.StartNew();
        try
        {
            var respuesta = await interno.CompletarAsync(peticion, ct);
            await RegistrarAsync(peticion, new clsUsoDeTokens(
                respuesta.Modelo, respuesta.TokensEntrada, respuesta.TokensSalida, respuesta.TokensCacheLectura, respuesta.TokensCacheEscritura),
                clsUsoIa.ResultadoOk, cronometro);
            return respuesta;
        }
        catch (clsIaRespuestaNoUtilException ex)
        {
            await RegistrarAsync(peticion, ex.Uso, ex.Code, cronometro);
            throw;
        }
        catch (clsServiceUnavailableException ex) when (!ct.IsCancellationRequested)
        {
            // El proveedor falló antes de responder: no hay cifras de tokens, pero el intento y su causa quedan anotados.
            await RegistrarAsync(peticion, new clsUsoDeTokens(_opciones.ModeloEfectivo, 0, 0, 0, 0), ex.Code, cronometro);
            throw;
        }
    }

    private async Task VerificarTopeAsync(clsPeticionIa peticion, CancellationToken ct)
    {
        var limite = _opciones.LimiteTokensPorUsuarioDiaEfectivo;
        if (limite <= 0 || peticion.UsuarioId is not { } usuarioId) return;

        var inicioDelDia = InicioDelDia(reloj.GetUtcNow());
        var consumidos = await uso.TokensConsumidosDesdeAsync(usuarioId, inicioDelDia, ct);
        if (consumidos < limite) return;

        throw new clsLimiteExcedidoException(
            "IA_TOPE_DIARIO",
            $"Alcanzaste el tope diario de uso de la IA ({limite:N0} tokens). Se reinicia a la medianoche, hora de Colombia.",
            new { limiteTokensPorDia = limite, tokensConsumidosHoy = consumidos, reiniciaEn = inicioDelDia.AddDays(1) });
    }

    /// <summary>Medianoche de Colombia (UTC-5) del día al que pertenece <paramref name="ahora"/>.</summary>
    internal static DateTimeOffset InicioDelDia(DateTimeOffset ahora)
    {
        var local = ahora.ToOffset(DesfaseColombia);
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, DesfaseColombia);
    }

    private async Task RegistrarAsync(clsPeticionIa peticion, clsUsoDeTokens tokens, string resultado, Stopwatch cronometro)
    {
        try
        {
            await uso.RegistrarAsync(new clsRegistroUsoIa(
                peticion.Funcion.ToString(), peticion.UsuarioId, tokens.Modelo,
                tokens.Entrada, tokens.Salida, tokens.CacheLectura, tokens.CacheEscritura,
                clsPreciosIa.Estimar(tokens.Modelo, tokens.Entrada, tokens.Salida, tokens.CacheLectura, tokens.CacheEscritura),
                (int)Math.Min(cronometro.ElapsedMilliseconds, int.MaxValue), resultado, reloj.GetUtcNow()),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Perder un renglón de uso es mejor que perder la respuesta que el usuario ya esperó; pero no debe pasar callado.
            log.LogError(ex, "No se pudo registrar el uso de IA ({Funcion}, {Resultado})", peticion.Funcion, resultado);
        }
    }
}
