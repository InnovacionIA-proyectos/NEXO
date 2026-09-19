using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.PlanDeCargue.Entidades;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// S3-HU-03 (issue #112): <see cref="ResultadoPlanMapper"/> es el único lugar que
/// traduce la proyección local (entidades EF) al shape que consume el visor 3D —
/// probado directo porque desarma JSON (MetricasJson/IncidenciasJson/EjesJson) sin
/// pasar por un handler completo.
/// </summary>
public class ResultadoPlanMapperTests
{
    private static PlanCargue PlanConVehiculo() => new()
    {
        Id = Guid.NewGuid(), Nombre = "Plan 1", VehiculoId = Guid.NewGuid(), Estado = "optimizado",
        Vehiculo = new VehiculoCubicacion
        {
            Nombre = "Furgón", EjesJson = """[{"posicionMm":1500,"capacidadKg":8000}]""",
        },
    };

    [Fact]
    public void ADto_lanza_NotFound_si_el_vehiculo_no_esta_cargado()
    {
        var plan = PlanConVehiculo();
        plan.Vehiculo = null;

        Assert.Throws<NotFoundException>(() => ResultadoPlanMapper.ADto(plan));
    }

    [Fact]
    public void ADto_con_metricas_e_incidencias_nulas_no_revienta()
    {
        var plan = PlanConVehiculo();
        plan.MetricasJson = null;
        plan.IncidenciasJson = "[]";

        var dto = ResultadoPlanMapper.ADto(plan);

        Assert.Null(dto.Metricas);
        Assert.Empty(dto.Incidencias);
    }

    [Fact]
    public void ADto_deserializa_metricas_e_incidencias_guardadas()
    {
        var plan = PlanConVehiculo();
        plan.MetricasJson = """
            {"volumenInternoM3":90,"volumenUtilizadoM3":1.2,"porcentajeVolumen":1.3,
             "areaPisoM2":33,"areaPisoUtilizadaM2":1.2,"porcentajePiso":3.6,
             "pesoMaximoKg":28000,"pesoCargadoKg":500,"porcentajePeso":1.8,
             "unidadesTotales":1,"unidadesCargadas":1,"unidadesNoCargadas":0,
             "pedidosCompletos":1,"pedidosParciales":0,"destinos":1,"alturaMaximaMm":1000,
             "centroGravedadMm":[600,500,500],"pesoPorEjeKg":[500],"diferenciaLadosKg":0,
             "advertencias":0,"violacionesCriticas":0,"puntaje":75.1,
             "puntajesParciales":{"volumen":0.9},"espacioVacioUtilM3":0,"espacioFragmentadoM3":0}
            """;
        plan.IncidenciasJson = """
            [{"severidad":"advertencia","codigoRegla":"R1","titulo":"T","descripcion":"D",
              "accionSugerida":null,"colocacionItemId":null}]
            """;

        var dto = ResultadoPlanMapper.ADto(plan);

        Assert.NotNull(dto.Metricas);
        Assert.Equal(1, dto.Metricas!.UnidadesCargadas);
        Assert.Equal(75.1m, dto.Metricas.Puntaje);
        Assert.Single(dto.Incidencias);
        Assert.Equal("R1", dto.Incidencias[0].CodigoRegla);
    }

    [Fact]
    public void AVehiculo_deserializa_los_ejes_del_json()
    {
        var vehiculo = new VehiculoCubicacion { Nombre = "Furgón", EjesJson = """[{"posicionMm":1500,"capacidadKg":8000}]""" };

        var dto = ResultadoPlanMapper.AVehiculo(vehiculo);

        Assert.Single(dto.Ejes);
        Assert.Equal(1500, dto.Ejes[0].PosicionMm);
        Assert.Equal(8000, dto.Ejes[0].CapacidadKg);
    }

    [Fact]
    public void AVehiculo_con_ejesjson_vacio_no_revienta()
    {
        var vehiculo = new VehiculoCubicacion { Nombre = "Furgón", EjesJson = "" };

        var dto = ResultadoPlanMapper.AVehiculo(vehiculo);

        Assert.Empty(dto.Ejes);
    }
}
