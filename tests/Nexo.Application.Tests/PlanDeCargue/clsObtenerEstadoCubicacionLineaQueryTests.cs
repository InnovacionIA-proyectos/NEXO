using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.PlanDeCargue.Entidades;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// #304: GET /api/planes-cargue/por-linea/{lineaId}/estado. El sondeo de la flota solo devolvía una lista (vacía = "seguir
/// esperando"), así que un rechazo del Cubicador —FLOTA_INSUFICIENTE, VEHICULO_SIN_PRESET...— dejaba al front esperando para
/// siempre. Este estado distingue procesando / listo / rechazado.
/// </summary>
public class clsObtenerEstadoCubicacionLineaQueryTests
{
    private readonly Mock<IPlanCargueRepository> _planes = new();
    private readonly Mock<IRechazoCubicacionLineaPort> _rechazos = new();

    private static readonly Guid LineaId = Guid.Parse("dddd0000-0000-0000-0000-000000000001");

    private clsObtenerEstadoCubicacionLineaQueryHandler Handler() => new(_planes.Object, _rechazos.Object);

    private void Flota(params clsPlanCargue[] planes) =>
        _planes.Setup(p => p.ObtenerPorLineaAsync(LineaId, It.IsAny<CancellationToken>())).ReturnsAsync(planes);

    private void Rechazo(string codigo, string mensaje) =>
        _rechazos.Setup(r => r.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsRechazoCubicacionLinea
            {
                LineaSolicitudId = LineaId, Codigo = codigo, Mensaje = mensaje, RechazadoAt = DateTimeOffset.UtcNow,
            });

    [Fact]
    public async Task Sin_flota_ni_rechazo_sigue_procesando()
    {
        Flota();

        var r = await Handler().Handle(new clsObtenerEstadoCubicacionLineaQuery(LineaId));

        Assert.Equal("procesando", r.Estado);
        Assert.Null(r.Codigo);
        Assert.Null(r.Mensaje);
        Assert.Empty(r.Vehiculos);
    }

    [Fact]
    public async Task Con_flota_y_sin_rechazo_esta_listo_con_los_conteos_de_cada_vehiculo()
    {
        Flota(new clsPlanCargue
        {
            Id = Guid.NewGuid(), LineaSolicitudId = LineaId, NumeroVehiculo = 1,
            Colocaciones = new List<clsColocacionCargue> { new() { Cargada = true }, new() { Cargada = false } },
        });

        var r = await Handler().Handle(new clsObtenerEstadoCubicacionLineaQuery(LineaId));

        Assert.Equal("listo", r.Estado);
        var vehiculo = Assert.Single(r.Vehiculos);
        Assert.Equal(1, vehiculo.TotalVehiculos);
        Assert.Equal((1, 2), (vehiculo.UnidadesCargadas, vehiculo.UnidadesTotales));
    }

    // Lo que faltaba: el motivo del Cubicador llega al front en vez de perderse en el log.
    [Fact]
    public async Task Con_rechazo_dice_rechazado_con_el_codigo_y_el_mensaje_del_Cubicador()
    {
        Flota();
        Rechazo("FLOTA_INSUFICIENTE", "Quedó carga sin ubicar tras el último vehículo.");

        var r = await Handler().Handle(new clsObtenerEstadoCubicacionLineaQuery(LineaId));

        Assert.Equal("rechazado", r.Estado);
        Assert.Equal("FLOTA_INSUFICIENTE", r.Codigo);
        Assert.Equal("Quedó carga sin ubicar tras el último vehículo.", r.Mensaje);
        Assert.NotNull(r.RechazadoAt);
    }

    // El rechazo es del ÚLTIMO intento (pedir el plan otra vez lo borra): manda sobre una flota vieja.
    [Fact]
    public async Task El_rechazo_manda_sobre_una_flota_de_un_intento_anterior()
    {
        Flota(new clsPlanCargue { Id = Guid.NewGuid(), LineaSolicitudId = LineaId, NumeroVehiculo = 1 });
        Rechazo("VEHICULO_SIN_PRESET", "El vehículo recomendado no tiene preset.");

        var r = await Handler().Handle(new clsObtenerEstadoCubicacionLineaQuery(LineaId));

        Assert.Equal("rechazado", r.Estado);
        Assert.Single(r.Vehiculos);
    }
}
