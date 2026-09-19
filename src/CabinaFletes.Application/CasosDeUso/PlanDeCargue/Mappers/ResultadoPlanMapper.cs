using System.Text.Json;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue.Mappers;

/// <summary>
/// S3-HU-03 (issue #112): arma <see cref="ResultadoPlanDto"/> (GET .../resultado,
/// /exportar, y la base de /metricas y /no-cargadas) desde la proyección local — nunca
/// consulta a Cubicador. Un solo mapeo compartido para no repetirlo en cada endpoint
/// (mismo criterio que <c>ResultadoPlanReader</c> del lado de Cubicador).
/// </summary>
public static class ResultadoPlanMapper
{
    private static readonly JsonSerializerOptions JsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static ResultadoPlanDto ADto(PlanCargue plan)
    {
        if (plan.Vehiculo is null)
            throw new NotFoundException("VEHICULO_NO_ENCONTRADO", "El vehículo del plan ya no existe en la proyección.");

        return new ResultadoPlanDto(
            APlanDetalle(plan),
            AVehiculo(plan.Vehiculo),
            plan.Colocaciones.Select(AColocacion).ToList(),
            DeserializarMetricas(plan.MetricasJson),
            DeserializarIncidencias(plan.IncidenciasJson));
    }

    public static PlanCargueDetalleDto APlanDetalle(PlanCargue p) => new(
        p.Id, p.Nombre, p.VehiculoId, p.LineaSolicitudId, p.NumeroVehiculo,
        p.Estado, p.Estrategia, p.Algoritmo, p.Semilla,
        p.PesoTotalKg, p.VolumenUtilizadoM3, p.UtilizacionVolumen, p.UtilizacionPiso, p.Puntaje,
        p.Items.Select(AItem).ToList(), p.CreatedAt, p.UpdatedAt);

    public static ItemPlanCargueDetalleDto AItem(ItemPlanCargue i) => new(
        i.Id, i.PlanCargueId, i.Sku, i.Descripcion, i.Pedido, i.Cliente, i.Destino, i.SecuenciaEntrega,
        i.LargoMm, i.AnchoMm, i.AltoMm, i.PesoKg, i.Cantidad, i.Color, i.Prioridad, i.TipoEmbalaje,
        i.Apilable, i.MaxNiveles, i.MaxPesoEncimaKg, i.Rotable, i.MantenerVertical, i.Fragil, i.Peligroso,
        i.Refrigerado, i.GrupoCompatibilidad, i.Observaciones);

    public static ColocacionCargueDto AColocacion(ColocacionCargue c) => new(
        c.Id, c.PlanCargueId, c.ItemPlanCargueId, c.IndiceUnidad, c.Cargada, c.X, c.Y, c.Z,
        c.LargoColocadoMm, c.AnchoColocadoMm, c.AltoColocadoMm, c.OrientacionIndice, c.SecuenciaCargue,
        c.SoporteColocacionId, c.PorcentajeSoporte, c.PesoEncimaKg, c.EstadoEstabilidad, c.Accesibilidad,
        c.Bloqueada, c.AjusteManual, c.CodigoRechazo, c.RazonRechazo);

    public static VehiculoCubicacionDto AVehiculo(VehiculoCubicacion v)
    {
        var ejes = string.IsNullOrWhiteSpace(v.EjesJson)
            ? new List<EjeVehiculoInput>()
            : JsonSerializer.Deserialize<List<EjeVehiculoInput>>(v.EjesJson, JsonCamelCase) ?? new();

        return new VehiculoCubicacionDto(
            v.Id, v.Nombre, v.Descripcion, v.LargoInternoMm, v.AnchoInternoMm, v.AltoInternoMm,
            v.CapacidadKg, v.TaraKg, v.PuertaAnchoMm, v.PuertaAltoMm, v.PuertaPosicion,
            ejes.Select(e => new EjeVehiculoDto(e.PosicionMm, e.CapacidadKg)).ToList(),
            v.ToleranciaParedesMm, v.ToleranciaEntreUnidadesMm, v.EsPreset);
    }

    private static MetricasCubicacionEvento? DeserializarMetricas(string? metricasJson) =>
        string.IsNullOrWhiteSpace(metricasJson)
            ? null
            : JsonSerializer.Deserialize<MetricasCubicacionEvento>(metricasJson, JsonCamelCase);

    private static IReadOnlyList<IncidenciaValidacionEvento> DeserializarIncidencias(string incidenciasJson) =>
        string.IsNullOrWhiteSpace(incidenciasJson)
            ? Array.Empty<IncidenciaValidacionEvento>()
            : JsonSerializer.Deserialize<List<IncidenciaValidacionEvento>>(incidenciasJson, JsonCamelCase) ?? new();
}
