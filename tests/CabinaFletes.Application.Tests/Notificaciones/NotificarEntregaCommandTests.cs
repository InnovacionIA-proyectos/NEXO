using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.CasosDeUso.Notificaciones;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using Moq;

namespace CabinaFletes.Application.Tests.Notificaciones;

/// <summary>
/// El caso de uso que manda el correo, ya fuera de la petición.
///
/// Lo que se protege aquí es la diferencia entre "no había correo que mandar" y "el correo
/// falló". La primera se resuelve sin error: sin cliente, sin comercial responsable o sin
/// relay configurado no hay avería, y reintentarlo cuatro veces no lo va a cambiar. La
/// segunda tiene que subir como excepción para que el bus la reintente o la lleve a la
/// cola de error — devolverla como resultado la dejaría enterrada en una línea de log.
/// </summary>
public class NotificarEntregaCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IComercialResponsablePort> _comercial = new();
    private readonly Mock<ICorreoPort> _correo = new();

    private static readonly Guid CotizacionId = Guid.Parse("aaaa0000-0000-0000-0000-000000000001");
    private static readonly Guid SolicitudId = Guid.Parse("bbbb0000-0000-0000-0000-000000000001");

    private NotificarEntregaCommandHandler Handler() =>
        new(_cotizaciones.Object, _solicitudes.Object, _comercial.Object, _correo.Object);

    private static NotificarEntregaCommand Comando(byte[]? pdf = null, string? nombre = null) =>
        new(CotizacionId, SolicitudId, pdf, nombre);

    /// <summary>Deja el camino completo listo: cotización, cliente, responsable y relay.</summary>
    private void TodoListo(string correoResponsable = "comercial@boterosoto.com.co")
    {
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(CotizacionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cotizacion
            {
                Id = CotizacionId,
                SolicitudId = SolicitudId,
                Total = 1_800_000m,
                Lineas = { new CotizacionLinea { ValorFlete = 1_800_000m, Fuente = FuenteFlete.Sicetac } },
            });
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Solicitud
            {
                Id = SolicitudId,
                Cliente = new Cliente { Nombre = "Colanta", Nit = "890900608-9" },
            });
        _comercial.SetupGet(c => c.Configurado).Returns(true);
        _comercial.Setup(c => c.BuscarPorNitAsync("890900608-9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResponsableComercialDto("Ana Gómez", correoResponsable));
        _correo.SetupGet(c => c.Configurado).Returns(true);
        _correo.Setup(c => c.EnviarAsync(It.IsAny<CorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("id-del-relay-123");
    }

    [Fact]
    public async Task Manda_el_correo_al_comercial_responsable_con_el_id_del_relay()
    {
        TodoListo();

        var r = await Handler().Handle(Comando());

        Assert.True(r.Enviado);
        Assert.Equal("comercial@boterosoto.com.co", r.Destinatario);
        Assert.Equal("id-del-relay-123", r.IdEnElRelay);
    }

    [Fact]
    public async Task El_formato_va_como_adjunto()
    {
        TodoListo();
        var pdf = new byte[] { 9, 8, 7 };

        var r = await Handler().Handle(Comando(pdf, "formato.pdf"));

        Assert.Equal(1, r.Adjuntos);
        _correo.Verify(c => c.EnviarAsync(
            It.Is<CorreoParaEnviar>(x =>
                x.Adjuntos.Count == 1 &&
                x.Adjuntos[0].Nombre == "formato.pdf" &&
                x.Adjuntos[0].Contenido == pdf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_formato_el_correo_sale_igual_sin_adjunto()
    {
        // Entregar sin archivo es válido: el comercial recibe el aviso con los totales.
        TodoListo();

        var r = await Handler().Handle(Comando());

        Assert.True(r.Enviado);
        Assert.Equal(0, r.Adjuntos);
    }

    [Fact]
    public async Task El_correo_lleva_metadatos_con_la_cotizacion()
    {
        // Es lo que permite responder "¿salió la notificación de esta cotización?"
        // consultando el relay, en vez de cruzar logs a mano.
        TodoListo();

        await Handler().Handle(Comando());

        _correo.Verify(c => c.EnviarAsync(
            It.Is<CorreoParaEnviar>(x => x.Metadatos != null && x.Metadatos["cotizacion"] == CotizacionId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("sin cotización")]
    [InlineData("sin cliente")]
    [InlineData("sin DW")]
    [InlineData("sin responsable")]
    [InlineData("sin relay")]
    public async Task Cuando_no_hay_a_quien_escribirle_no_es_un_fallo(string caso)
    {
        TodoListo();
        switch (caso)
        {
            case "sin cotización":
                _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(CotizacionId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Cotizacion?)null);
                break;
            case "sin cliente":
                _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Solicitud?)null);
                break;
            case "sin DW":
                _comercial.SetupGet(c => c.Configurado).Returns(false);
                break;
            case "sin responsable":
                _comercial.Setup(c => c.BuscarPorNitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ResponsableComercialDto?)null);
                break;
            case "sin relay":
                _correo.SetupGet(c => c.Configurado).Returns(false);
                break;
        }

        // No lanza: reintentar esto cuatro veces y acabar en la cola de error sería una
        // alerta al equipo por una configuración incompleta, no por una avería.
        var r = await Handler().Handle(Comando());

        Assert.False(r.Enviado);
        Assert.False(string.IsNullOrWhiteSpace(r.Motivo));
        _correo.Verify(c => c.EnviarAsync(It.IsAny<CorreoParaEnviar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_fallo_de_la_salida_de_correo_SUBE()
    {
        // Al contrario que los casos de arriba: aquí sí hay avería, y tragársela dejaría
        // la notificación perdida sin que nadie se entere. El bus tiene que verla.
        TodoListo();
        _correo.Setup(c => c.EnviarAsync(It.IsAny<CorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CorreoTransitorioException("El relay no responde."));

        await Assert.ThrowsAsync<CorreoTransitorioException>(() => Handler().Handle(Comando()));
    }

    [Fact]
    public async Task Un_rechazo_definitivo_tambien_SUBE()
    {
        TodoListo();
        _correo.Setup(c => c.EnviarAsync(It.IsAny<CorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CorreoRechazadoException("El adjunto pesa más de lo permitido."));

        await Assert.ThrowsAsync<CorreoRechazadoException>(() => Handler().Handle(Comando()));
    }
}
