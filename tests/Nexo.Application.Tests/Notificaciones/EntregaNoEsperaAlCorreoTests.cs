using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Notificaciones;
using Moq;

namespace Nexo.Application.Tests.Notificaciones;

/// <summary>
/// El criterio central de la HU-06.1: la entrega se confirma de inmediato aunque el correo
/// no pueda salir.
///
/// La forma de garantizarlo no es un try/catch alrededor del envío —eso ya estaba, y aun
/// así quien entregaba esperaba a que el relay respondiera— sino que el caso de uso de
/// entrega NO TENGA de dónde mandar un correo. Estas pruebas fijan justamente eso: que la
/// salida de correo no sea ni una dependencia del handler.
/// </summary>
public class EntregaNoEsperaAlCorreoTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private static readonly TimeProvider Reloj = TimeProvider.System;

    private EntregarCotizacionCommandHandler Handler() => new(
        _cotizaciones.Object, _solicitudes.Object, _auditoria.Object, _usuarioActual.Object,
        _publicador.Object, Reloj, _uow.Object);

    private Cotizacion Preparar(Guid cotizacionId, Guid solicitudId)
    {
        var c = new Cotizacion
        {
            Id = cotizacionId,
            SolicitudId = solicitudId,
            Estado = EstadoCotizacion.Borrador,
            Lineas = { new CotizacionLinea { ValorFlete = 1_800_000m, Fuente = FuenteFlete.Sicetac } },
        };
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(cotizacionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(c);
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.EnCosteo });
        return c;
    }

    [Fact]
    public void El_caso_de_uso_de_entrega_ya_no_depende_de_la_salida_de_correo()
    {
        // Es la prueba estructural, y la más difícil de deshacer sin darse cuenta. Si
        // alguien vuelve a inyectar ICorreoPort aquí, esto se rompe — y con ella volvería
        // la espera al relay dentro de la petición.
        var dependencias = typeof(EntregarCotizacionCommandHandler)
            .GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType).ToList();

        // IComercialResponsablePort ni siquiera existe ya en el código (S2-HU-06.2 lo
        // retiró por completo: el destinatario sale de la sesión de quien solicitó la
        // consulta, no de un DW por NIT del cliente) — no hay tipo que verificar aquí.
        Assert.DoesNotContain(typeof(ICorreoPort), dependencias);
        Assert.Contains(typeof(IPublicadorDeMensajes), dependencias);
    }

    [Fact]
    public async Task Entregar_publica_el_evento_y_no_manda_ningun_correo()
    {
        var cotizacionId = Guid.NewGuid();
        var solicitudId = Guid.NewGuid();
        Preparar(cotizacionId, solicitudId);

        var r = await Handler().Handle(new EntregarCotizacionCommand(cotizacionId, null, null));

        Assert.Equal("encolada", r.EstadoNotificacion);
        _publicador.Verify(p => p.PublicarAsync(
            It.Is<CotizacionEntregada>(m => m.CotizacionId == cotizacionId && m.SolicitudId == solicitudId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_evento_se_publica_antes_de_guardar()
    {
        // Con el Outbox eso es lo que lo mete en la misma transacción. Al revés podría
        // quedar la entrega firme sin notificación pendiente.
        var cotizacionId = Guid.NewGuid();
        Preparar(cotizacionId, Guid.NewGuid());
        var orden = new List<string>();
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<CotizacionEntregada>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Handler().Handle(new EntregarCotizacionCommand(cotizacionId, null, null));

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }

    [Fact]
    public async Task El_formato_viaja_en_el_evento_porque_no_esta_en_la_base()
    {
        // Es la única pieza que el consumidor no puede leer de ningún sitio: llega como
        // archivo en esta petición y no se persiste.
        var cotizacionId = Guid.NewGuid();
        Preparar(cotizacionId, Guid.NewGuid());
        var pdf = new byte[] { 1, 2, 3, 4, 5 };

        await Handler().Handle(new EntregarCotizacionCommand(cotizacionId, pdf, "formato.pdf"));

        _publicador.Verify(p => p.PublicarAsync(
            It.Is<CotizacionEntregada>(m => m.FormatoPdf == pdf && m.NombrePdf == "formato.pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_entrega_rechazada_no_publica_nada()
    {
        // Notificar una entrega que no ocurrió sería peor que no notificar.
        var cotizacionId = Guid.NewGuid();
        var c = Preparar(cotizacionId, Guid.NewGuid());
        c.Estado = EstadoCotizacion.Entregada;   // ya entregada: el caso de uso corta

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(new EntregarCotizacionCommand(cotizacionId, null, null)));

        _publicador.Verify(p => p.PublicarAsync(
            It.IsAny<CotizacionEntregada>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task La_respuesta_ya_no_afirma_que_el_correo_salio()
    {
        // Los campos viejos —emailEnviado, el correo del responsable— habrían quedado
        // mintiendo: al responder no ha salido nada.
        var cotizacionId = Guid.NewGuid();
        Preparar(cotizacionId, Guid.NewGuid());

        var r = await Handler().Handle(new EntregarCotizacionCommand(cotizacionId, null, null));

        var propiedades = r.GetType().GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("EmailEnviado", propiedades);
        Assert.DoesNotContain("CorreoResponsable", propiedades);
    }
}
