using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S5-HU-19.3/#283: (1) nunca se guarda un flete por debajo del piso SICE-TAC, sea cual
/// sea la fuente; (2) un valor Histórico se acepta si hay "histórica sugerida" vigente O
/// coincide con el histórico de la propuesta recalculada en el servidor.
/// </summary>
public class clsAgregarLineaCotizacionPisoTests
{
    private const decimal Piso = 2_500_000m;

    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ILineasSolicitudPort> _lineasSolicitud = new();
    private readonly Mock<ITarifasHistoricasPort> _historicas = new();
    private readonly Mock<IPropuestaTarifaPort> _propuestas = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsAgregarLineaCotizacionCommandHandler Handler() => new(
        _cotizaciones.Object, _lineasSolicitud.Object, _historicas.Object, _propuestas.Object,
        _auditoria.Object, _usuario.Object, TimeProvider.System, _uow.Object);

    private (Guid CotizacionId, Guid LineaId) Preparar(decimal? piso, decimal? historicoDeLaPropuesta = null, bool hayHistoricaSugeridaVigente = false)
    {
        var cotizacionId = Guid.NewGuid();
        var solicitudId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsCotizacion { Id = cotizacionId, SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador });
        _lineasSolicitud.Setup(l => l.ObtenerDeSolicitudAsync(lineaId, solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaSolicitud { Id = lineaId, SolicitudId = solicitudId });
        _historicas.Setup(h => h.HayVigenteAsync(It.IsAny<Guid>(), It.IsAny<TipologiaCarga?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hayHistoricaSugeridaVigente);
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(solicitudId, [
                new clsPropuestaLineaDto(lineaId, "A → B", "C2", 1000,
                    piso is null ? null : new clsCandidatoTarifaDto("sicetac", piso.Value, "", "", 1),
                    null,
                    historicoDeLaPropuesta is null ? null : new clsCandidatoTarifaDto("historico_sap", historicoDeLaPropuesta.Value, "", "", 1),
                    null, null, ""),
            ], []));
        return (cotizacionId, lineaId);
    }

    private static clsAgregarLineaCotizacionCommand Comando(Guid cotizacionId, Guid lineaId, decimal valor, string fuente) =>
        new(cotizacionId, lineaId, valor, null, fuente);

    // ------------------------------------------------ bajo el piso

    [Theory]
    [InlineData("Tarifario")]
    [InlineData("Historica")]
    [InlineData("Manual")]
    public async Task Un_flete_bajo_el_piso_falla_con_cualquier_fuente(string fuente)
    {
        var (cot, lin) = Preparar(Piso, historicoDeLaPropuesta: 2_000_000m, hayHistoricaSugeridaVigente: true);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(Comando(cot, lin, 2_000_000m, fuente)));

        Assert.Equal("FLETE_BAJO_PISO_SICETAC", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2_500_000)]   // igual al piso
    [InlineData(3_000_000)]   // mayor
    public async Task Un_flete_igual_o_mayor_al_piso_se_guarda(int valor)
    {
        var (cot, lin) = Preparar(Piso);

        var dto = await Handler().Handle(Comando(cot, lin, valor, "Manual"));

        Assert.Single(dto.Lineas);
    }

    [Fact]
    public async Task Una_linea_sin_piso_se_guarda_normal()
    {
        var (cot, lin) = Preparar(piso: null);

        var dto = await Handler().Handle(Comando(cot, lin, 100_000m, "Manual"));

        Assert.Single(dto.Lineas);
    }

    // ------------------------------------------------ histórico de la propuesta

    [Fact]
    public async Task Historica_que_coincide_con_la_propuesta_se_guarda_sin_historicas_sugeridas_vigentes()
    {
        var (cot, lin) = Preparar(Piso, historicoDeLaPropuesta: 2_800_000m, hayHistoricaSugeridaVigente: false);

        var dto = await Handler().Handle(Comando(cot, lin, 2_800_000m, "Historica"));

        Assert.Single(dto.Lineas);
    }

    [Fact]
    public async Task Historica_con_sugerida_vigente_sigue_aceptandose_aunque_no_coincida_con_la_propuesta()
    {
        var (cot, lin) = Preparar(Piso, historicoDeLaPropuesta: 2_800_000m, hayHistoricaSugeridaVigente: true);

        var dto = await Handler().Handle(Comando(cot, lin, 2_900_000m, "Historica"));

        Assert.Single(dto.Lineas);
    }

    [Fact]
    public async Task Historica_ajena_a_ambas_falla_con_HISTORICA_VENCIDA()
    {
        var (cot, lin) = Preparar(Piso, historicoDeLaPropuesta: 2_800_000m, hayHistoricaSugeridaVigente: false);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(Comando(cot, lin, 2_900_000m, "Historica")));

        Assert.Equal("HISTORICA_VENCIDA", ex.Code);
    }
}
