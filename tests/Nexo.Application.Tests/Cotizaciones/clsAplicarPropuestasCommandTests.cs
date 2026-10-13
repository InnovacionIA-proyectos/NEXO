using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S5-HU-19.2/#282: <c>soloAutomaticas</c> decide si "Aplicar propuestas" se salta las
/// líneas en modo ConsultaCompleta (necesitan que el Administrador de Tarifas elija) o
/// las aplica igual que siempre.
/// </summary>
public class clsAplicarPropuestasCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ILineasSolicitudPort> _lineasSolicitud = new();
    private readonly Mock<IPropuestaTarifaPort> _propuestas = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsAplicarPropuestasCommandHandler Handler() => new(
        _cotizaciones.Object, _lineasSolicitud.Object, _propuestas.Object, _solicitudes.Object,
        _auditoria.Object, _usuarioActual.Object, _uow.Object);

    private clsCotizacion PrepararCotizacionVacia(Guid solicitudId)
    {
        var cotizacion = new clsCotizacion { SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador };
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cotizacion);
        _solicitudes.Setup(s => s.ContarLineasAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _lineasSolicitud.Setup(l => l.ObtenerPorSolicitudAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, clsLineaSolicitud>)new Dictionary<Guid, clsLineaSolicitud>());
        return cotizacion;
    }

    [Fact]
    public async Task Sin_soloAutomaticas_aplica_tanto_las_automaticas_como_ConsultaCompleta()
    {
        var solicitudId = Guid.NewGuid();
        var cotizacion = PrepararCotizacionVacia(solicitudId);
        var lineaAuto = Guid.NewGuid();
        var lineaManual = Guid.NewGuid();
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(solicitudId, [
                new clsPropuestaLineaDto(lineaAuto, "A → B", "C2", 1000, null, null, null, 1_000_000m, "plaza", "Plaza vigente mayor.", ModoDecision: nameof(ModoDecision.PlazaVigenteMayor)),
                new clsPropuestaLineaDto(lineaManual, "C → D", "C2", 1000, null, null, null, 2_500_000m, "sicetac", "Solo piso.", ModoDecision: nameof(ModoDecision.ConsultaCompleta)),
            ], []));

        var resultado = await Handler().Handle(new clsAplicarPropuestasCommand(cotizacion.Id, SoloAutomaticas: false));

        Assert.Equal(2, resultado.Agregadas);
        Assert.Equal(2, cotizacion.Lineas.Count);
    }

    [Fact]
    public async Task Con_soloAutomaticas_salta_las_lineas_en_ConsultaCompleta()
    {
        var solicitudId = Guid.NewGuid();
        var cotizacion = PrepararCotizacionVacia(solicitudId);
        var lineaAuto = Guid.NewGuid();
        var lineaManual = Guid.NewGuid();
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(solicitudId, [
                new clsPropuestaLineaDto(lineaAuto, "A → B", "C2", 1000, null, null, null, 1_000_000m, "plaza", "Plaza vigente mayor.", ModoDecision: nameof(ModoDecision.PlazaVigenteMayor)),
                new clsPropuestaLineaDto(lineaManual, "C → D", "C2", 1000, null, null, null, 2_500_000m, "sicetac", "Solo piso.", ModoDecision: nameof(ModoDecision.ConsultaCompleta)),
            ], []));

        var resultado = await Handler().Handle(new clsAplicarPropuestasCommand(cotizacion.Id, SoloAutomaticas: true));

        Assert.Equal(1, resultado.Agregadas);
        var linea = Assert.Single(cotizacion.Lineas);
        Assert.Equal(lineaAuto, linea.LineaSolicitudId);
    }
}
