using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;
using Moq;

namespace Nexo.Application.Tests.Notificaciones;

/// <summary>
/// El caso de uso que manda el correo, ya fuera de la petición (S2-HU-06.1) y con el
/// destinatario resuelto de la sesión de quien solicitó la consulta (S2-HU-06.2), no de
/// un "comercial responsable" sacado de un DW por NIT del cliente — ese mecanismo se
/// retiró por completo (IComercialResponsablePort/DwCubosSap ya no existen).
///
/// Lo que se protege aquí es la diferencia entre "no había correo que mandar" y "el correo
/// falló". La primera se resuelve sin error: sin cliente, sin creador o sin relay
/// configurado no hay avería, y reintentarlo cuatro veces no lo va a cambiar. La segunda
/// tiene que subir como excepción para que el bus la reintente o la lleve a la cola de
/// error — devolverla como resultado la dejaría enterrada en una línea de log.
/// </summary>
public class clsNotificarEntregaCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<ICorreoPort> _correo = new();

    private static readonly Guid CotizacionId = Guid.Parse("aaaa0000-0000-0000-0000-000000000001");
    private static readonly Guid SolicitudId = Guid.Parse("bbbb0000-0000-0000-0000-000000000001");

    private clsNotificarEntregaCommandHandler Handler() =>
        new(_cotizaciones.Object, _solicitudes.Object, _correo.Object);

    private static clsNotificarEntregaCommand Comando(byte[]? pdf = null, string? nombre = null) =>
        new(CotizacionId, SolicitudId, pdf, nombre);

    /// <summary>
    /// Deja el camino completo listo: cotización, cliente y creador (quien solicitó la
    /// consulta). El cliente y el creador usan dominios de correo DELIBERADAMENTE
    /// distintos — si algún día alguien vuelve a colar un dato del cliente como
    /// destinatario, una prueba que solo usara el mismo dominio para los dos no lo
    /// detectaría.
    /// </summary>
    private void TodoListo(string correoCreador = "cherrera@boterosoto.com.co")
    {
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(CotizacionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsCotizacion
            {
                Id = CotizacionId,
                SolicitudId = SolicitudId,
                Total = 1_800_000m,
                Lineas = { new clsCotizacionLinea { ValorFlete = 1_800_000m, Fuente = FuenteFlete.Sicetac } },
            });
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud
            {
                Id = SolicitudId,
                clsCliente = new clsCliente { Nombre = "Colanta", Nit = "890900608-9" },
                Creador = new clsUsuario { Nombre = "Carlos Herrera", Email = correoCreador, Rol = RolUsuario.AdminBiblioteca },
            });
        _correo.SetupGet(c => c.Configurado).Returns(true);
        _correo.Setup(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("id-del-relay-123");
    }

    [Fact]
    public async Task Manda_el_correo_a_quien_solicito_la_consulta_con_el_id_del_relay()
    {
        TodoListo();

        var r = await Handler().Handle(Comando());

        Assert.True(r.Enviado);
        Assert.Equal("cherrera@boterosoto.com.co", r.Destinatario);
        Assert.Equal("id-del-relay-123", r.IdEnElRelay);
    }

    /// <summary>
    /// La salvaguarda de la #22: aunque el cliente tuviera algún dato de contacto, esta
    /// prueba fija que el destinatario real es el correo del creador de la solicitud, no
    /// nada derivado del cliente. clsCliente ni siquiera tiene columna de correo (salvaguarda
    /// estructural adicional) — esto verifica el comportamiento del caso de uso.
    /// </summary>
    [Fact]
    public async Task Ninguna_direccion_del_cliente_puede_ser_destinataria()
    {
        TodoListo(correoCreador: "yzapata@boterosoto.com.co");

        await Handler().Handle(Comando());

        _correo.Verify(c => c.EnviarAsync(
            It.Is<clsCorreoParaEnviar>(x =>
                x.Destinatarios.Count == 1 &&
                x.Destinatarios[0] == "yzapata@boterosoto.com.co" &&
                x.Destinatarios.All(d => d.EndsWith("@boterosoto.com.co"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Salvaguarda estructural, no solo de comportamiento: aunque el handler tuviera un
    /// bug mañana, no hay NINGÚN campo en clsCliente que pudiera colarse como destinatario —
    /// el tipo mismo no tiene de dónde sacar un correo de cliente.
    /// </summary>
    [Fact]
    public void Cliente_no_tiene_ninguna_columna_de_correo()
    {
        var propiedades = typeof(clsCliente).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain(propiedades, nombre => nombre.Contains("correo", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task El_formato_va_como_adjunto()
    {
        TodoListo();
        var pdf = new byte[] { 9, 8, 7 };

        var r = await Handler().Handle(Comando(pdf, "formato.pdf"));

        Assert.Equal(1, r.Adjuntos);
        _correo.Verify(c => c.EnviarAsync(
            It.Is<clsCorreoParaEnviar>(x =>
                x.Adjuntos.Count == 1 &&
                x.Adjuntos[0].Nombre == "formato.pdf" &&
                x.Adjuntos[0].Contenido == pdf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_formato_el_correo_sale_igual_sin_adjunto()
    {
        // Entregar sin archivo es válido: el solicitante recibe el aviso con los totales.
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
            It.Is<clsCorreoParaEnviar>(x => x.Metadatos != null && x.Metadatos["cotizacion"] == CotizacionId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("sin cotización")]
    [InlineData("sin cliente")]
    [InlineData("sin creador")]
    [InlineData("sin relay")]
    public async Task Cuando_no_hay_a_quien_escribirle_no_es_un_fallo(string caso)
    {
        TodoListo();
        switch (caso)
        {
            case "sin cotización":
                _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(CotizacionId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((clsCotizacion?)null);
                break;
            case "sin cliente":
                _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((clsSolicitud?)null);
                break;
            case "sin creador":
                _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new clsSolicitud
                    {
                        Id = SolicitudId,
                        clsCliente = new clsCliente { Nombre = "Colanta", Nit = "890900608-9" },
                        Creador = null,
                    });
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
        _correo.Verify(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_fallo_de_la_salida_de_correo_SUBE()
    {
        // Al contrario que los casos de arriba: aquí sí hay avería, y tragársela dejaría
        // la notificación perdida sin que nadie se entere. El bus tiene que verla.
        TodoListo();
        _correo.Setup(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsCorreoTransitorioException("El relay no responde."));

        await Assert.ThrowsAsync<clsCorreoTransitorioException>(() => Handler().Handle(Comando()));
    }

    [Fact]
    public async Task Un_rechazo_definitivo_tambien_SUBE()
    {
        TodoListo();
        _correo.Setup(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsCorreoRechazadoException("El adjunto pesa más de lo permitido."));

        await Assert.ThrowsAsync<clsCorreoRechazadoException>(() => Handler().Handle(Comando()));
    }
}
