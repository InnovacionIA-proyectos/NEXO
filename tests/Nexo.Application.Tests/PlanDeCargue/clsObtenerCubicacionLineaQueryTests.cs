using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Exceptions;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// Puente Mesa de Costeo → Cubicador (crear-desde-línea): lo que el Cubicador (Python)
/// necesita de una línea, servido por GET /api/lineas-solicitud/{id}/cubicacion — ver
/// cubicador/CONTRATO.md sección 7.
/// </summary>
public class clsObtenerCubicacionLineaQueryTests
{
    private readonly Mock<ILineaSolicitudCubicacionPort> _lineas = new();

    private static readonly Guid LineaId = Guid.Parse("dddd0000-0000-0000-0000-000000000001");

    private clsObtenerCubicacionLineaQueryHandler Handler() => new(_lineas.Object);

    [Fact]
    public async Task Con_la_linea_existente_devuelve_sus_posiciones_y_ruta()
    {
        var esperado = new clsLineaParaCubicacionDto(
            LineaId,
            [new clsPosicionCargaDto("Pallet", 3, 120, 100, 100, 500)],
            "Bogotá", "Medellín");
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var r = await Handler().Handle(new clsObtenerCubicacionLineaQuery(LineaId));

        Assert.Equal(esperado, r);
    }

    [Fact]
    public async Task Con_la_linea_inexistente_lanza_LINEA_NO_ENCONTRADA()
    {
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsLineaParaCubicacionDto?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsObtenerCubicacionLineaQuery(LineaId)));

        Assert.Equal("LINEA_NO_ENCONTRADA", ex.Code);
    }
}
