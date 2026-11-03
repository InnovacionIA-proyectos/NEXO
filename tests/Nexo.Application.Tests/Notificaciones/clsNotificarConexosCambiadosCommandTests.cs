using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.Plantillas;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;
using Nexo.Mensajes.Notificaciones;
using Moq;

namespace Nexo.Application.Tests.Notificaciones;

/// <summary>
/// S5-HU-20.1/#286: el correo "Cotización actualizada" a quien creó la solicitud cuando otra
/// persona cambia los servicios conexos de una cotización ya entregada. Mismas garantías que
/// la entrega: el destinatario es el creador (nunca el cliente), "no había a quién escribirle"
/// se resuelve sin error y un fallo de la salida de correo SUBE como excepción.
/// </summary>
public class clsNotificarConexosCambiadosCommandTests
{
    private static readonly Guid SolicitudId = Guid.Parse("bbbb0000-0000-0000-0000-000000000002");

    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<ICorreoPort> _correo = new();

    private clsNotificarConexosCambiadosCommandHandler Handler() => new(_solicitudes.Object, _correo.Object);

    private static clsConexosCotizacionCambiados Evento(string accion = "agregado", params clsConexoCambiado[] items) => new(
        Guid.NewGuid(), SolicitudId, accion,
        items.Length > 0 ? items : [new clsConexoCambiado("Cargue", 2, 100_000m)],
        NuevoTotal: 1_900_000m, ModificadoPor: Guid.NewGuid(), ModificadoEn: DateTimeOffset.UtcNow);

    private void TodoListo(string correoCreador = "cherrera@boterosoto.com.co")
    {
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud
            {
                Id = SolicitudId,
                clsCliente = new clsCliente { Nombre = "Colanta", Nit = "890900608-9" },
                Creador = new clsUsuario { Nombre = "Carlos Herrera", Email = correoCreador, Rol = RolUsuario.Comercial },
            });
        _correo.SetupGet(c => c.Configurado).Returns(true);
        _correo.Setup(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>())).ReturnsAsync("id-relay-9");
    }

    [Fact]
    public async Task Manda_el_correo_al_creador_con_el_resumen_y_el_nuevo_total()
    {
        TodoListo();
        clsCorreoParaEnviar? enviado = null;
        _correo.Setup(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .Callback<clsCorreoParaEnviar, CancellationToken>((m, _) => enviado = m).ReturnsAsync("id-relay-9");

        var r = await Handler().Handle(new clsNotificarConexosCambiadosCommand(Evento()));

        Assert.True(r.Enviado);
        Assert.Equal("cherrera@boterosoto.com.co", r.Destinatario);
        Assert.Equal("id-relay-9", r.IdEnElRelay);
        Assert.Equal(["cherrera@boterosoto.com.co"], enviado!.Destinatarios);
        Assert.Contains("Cotización actualizada", enviado.Asunto);
        Assert.Contains("1.900.000 COP", enviado.Asunto);
        Assert.Contains("se agregó Cargue x 2", enviado.CuerpoHtml);
        Assert.Contains("1.900.000 COP", enviado.CuerpoHtml);
    }

    [Fact]
    public async Task Sin_creador_identificable_no_hay_a_quien_notificar_y_no_envia()
    {
        TodoListo();
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = SolicitudId, clsCliente = new clsCliente { Nombre = "Colanta", Nit = "1" }, Creador = null });

        var r = await Handler().Handle(new clsNotificarConexosCambiadosCommand(Evento()));

        Assert.False(r.Enviado);
        _correo.Verify(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_cliente_no_envia()
    {
        TodoListo();
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = SolicitudId, clsCliente = null });

        Assert.False((await Handler().Handle(new clsNotificarConexosCambiadosCommand(Evento()))).Enviado);
    }

    [Fact]
    public async Task Con_el_relay_sin_configurar_no_envia_ni_lanza()
    {
        TodoListo();
        _correo.SetupGet(c => c.Configurado).Returns(false);

        var r = await Handler().Handle(new clsNotificarConexosCambiadosCommand(Evento()));

        Assert.False(r.Enviado);
        _correo.Verify(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_fallo_de_la_salida_de_correo_sube_como_excepcion()
    {
        TodoListo();
        _correo.Setup(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("relay caído"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(new clsNotificarConexosCambiadosCommand(Evento())));
    }

    // ------------------------------------------------------------ plantilla

    [Fact]
    public void El_resumen_dice_agregado_o_quitado_y_lista_varios_servicios_en_una_frase()
    {
        Assert.Equal("se agregó Cargue x 2", clsPlantillaConexosCambiados.Resumen(Evento("agregado")));
        Assert.Equal("se quitó Cargue x 1,5", clsPlantillaConexosCambiados.Resumen(
            Evento("quitado", new clsConexoCambiado("Cargue", 1.5m, 10m))));
        Assert.Equal("se agregó Cargue x 1, Descargue x 2 y Escolta x 3", clsPlantillaConexosCambiados.Resumen(
            Evento("agregado", new clsConexoCambiado("Cargue", 1, 1m), new clsConexoCambiado("Descargue", 2, 1m), new clsConexoCambiado("Escolta", 3, 1m))));
    }

    [Fact]
    public void El_nombre_del_servicio_se_escapa_en_el_cuerpo_html()
    {
        var html = clsPlantillaConexosCambiados.CuerpoHtml(
            Evento("agregado", new clsConexoCambiado("<b>X</b>", 1, 1m)),
            new clsCliente { Nombre = "A & B", Nit = "1" }, "Ana");

        Assert.DoesNotContain("<b>X</b>", html);
        Assert.Contains("&lt;b&gt;X&lt;/b&gt;", html);
        Assert.Contains("A &amp; B", html);
    }

    // ------------------------------------------------------------ validador del contrato

    [Fact]
    public void El_validador_acepta_un_evento_completo()
    {
        Assert.Empty(new clsConexosCotizacionCambiadosValidador().Validar(Evento()));
    }

    [Fact]
    public void El_validador_rechaza_accion_desconocida_items_vacios_ids_vacios_y_fecha_por_defecto()
    {
        var malo = new clsConexosCotizacionCambiados(
            Guid.Empty, Guid.Empty, "borrado", [], 0m, Guid.Empty, default);

        var motivos = new clsConexosCotizacionCambiadosValidador().Validar(malo);

        Assert.Equal(6, motivos.Count); // 3 ids + acción + items + fecha
    }
}
