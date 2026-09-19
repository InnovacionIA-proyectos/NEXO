using Nexo.Application.CasosDeUso.Solicitudes.DTOs;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;

/// <summary>Respuesta del 202: el patrón de trabajos pesados del contrato de
/// integración, aplicado a la cubicación (S2-HU-08.1).</summary>
public record SolicitudDeCubicacionAceptadaDto(Guid PlanCargueId, string Estado, string ConsultarEn);

/// <summary>
/// Respuesta 202 genérica de los comandos de escritura de S3-HU-02 que NO generan (ni
/// tienen, ver issue #108) un recurso propio de NEXO para consultar — guardar_vehiculo,
/// crear_plan_demo, guardar_item, importar_items, eliminar_item, eliminar_plan,
/// ajustar_colocacion. <c>CorrelationId</c> es el mismo id que viaja en el sobre de la
/// cola, útil para cruzar con logs/DLQ si el worker rechaza el comando.
/// </summary>
public record ComandoAceptadoDto(Guid CorrelationId, string Estado);

/// <summary>
/// Lo que el Cubicador (microservicio Python) necesita de una línea de solicitud para
/// armar el plan de cargue 3D — puente Mesa de Costeo → Cubicador (crear-desde-línea,
/// ver cubicador/CONTRATO.md sección 7). Ruta puede venir sin resolver (Origen/Destino
/// nulos) si la línea no tiene ruta asignada.
/// </summary>
public record LineaParaCubicacionDto(
    Guid LineaId,
    IReadOnlyList<PosicionCargaDto> Posiciones,
    string? Origen,
    string? Destino);

/// <summary>Consulta de respaldo: reconstruye el estado completo sin haber recibido
/// ningún aviso — igual criterio que GET /api/trabajos/{id}.</summary>
public record PlanCargueDto(
    Guid Id,
    string Nombre,
    string Estado,
    string Estrategia,
    string Algoritmo,
    decimal Puntaje,
    decimal PesoTotalKg,
    decimal VolumenUtilizadoM3,
    decimal UtilizacionVolumen,
    decimal UtilizacionPiso,
    int TotalItems,
    int TotalColocaciones,
    DateTimeOffset UpdatedAt);

/// <summary>Lo que viaja por el canal de avisos cuando un plan cambia de estado. Sin la
/// lista de colocaciones a propósito (puede haber cientos): el aviso dice que hay
/// desenlace, la consulta trae el detalle — mismo reparto que /hubs/trabajos.</summary>
public record AvisoPlanCargueDto(Guid PlanCargueId, string Estado, decimal Puntaje, string? Motivo = null)
{
    public static AvisoPlanCargueDto Desde(PlanCargueDto p) => new(p.Id, p.Estado, p.Puntaje);
}

// ---------------------------------------------------------------------------------
// S3-HU-03 (issue #112): lectura completa del visor 3D — TODAS contra la proyección
// local que llena PlanDeCargueGeneradoConsumer/VehiculoCatalogoConsumer (issue #111).
// Ninguna de estas consultas llama a Cubicador (criterio no-negociable de la HU). Los
// shapes espejan cubicador/CONTRATO.md §3.2 (ResultadoPlanDto/VehiculoCubicacionDto/etc.)
// para que el frontend (S3-HU-04) reciba lo mismo que ya conoce del contrato de Cubicador.
// ---------------------------------------------------------------------------------

public record EjeVehiculoDto(decimal PosicionMm, decimal CapacidadKg);

public record VehiculoCubicacionDto(
    Guid Id, string Nombre, string? Descripcion,
    decimal LargoInternoMm, decimal AnchoInternoMm, decimal AltoInternoMm,
    decimal CapacidadKg, decimal TaraKg, decimal PuertaAnchoMm, decimal PuertaAltoMm, string PuertaPosicion,
    IReadOnlyList<EjeVehiculoDto> Ejes, decimal ToleranciaParedesMm, decimal ToleranciaEntreUnidadesMm,
    bool EsPreset);

public record ItemPlanCargueDetalleDto(
    Guid Id, Guid PlanCargueId, string Sku, string Descripcion, string? Pedido, string? Cliente,
    string? Destino, int SecuenciaEntrega, decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg,
    int Cantidad, string? Color, int Prioridad, string? TipoEmbalaje, bool Apilable, int MaxNiveles,
    decimal MaxPesoEncimaKg, bool Rotable, bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado,
    string? GrupoCompatibilidad, string? Observaciones);

public record ColocacionCargueDto(
    Guid Id, Guid PlanCargueId, Guid ItemPlanCargueId, int IndiceUnidad, bool Cargada,
    decimal X, decimal Y, decimal Z, decimal LargoColocadoMm, decimal AnchoColocadoMm, decimal AltoColocadoMm,
    int OrientacionIndice, int SecuenciaCargue, Guid? SoporteColocacionId, decimal PorcentajeSoporte,
    decimal PesoEncimaKg, string EstadoEstabilidad, string Accesibilidad, bool Bloqueada, bool AjusteManual,
    string? CodigoRechazo, string? RazonRechazo);

/// <summary>Detalle completo del plan (a diferencia de <see cref="PlanCargueDto"/>, que es
/// el resumen de la consulta de respaldo de S2-HU-08.1) — con sus ítems completos.</summary>
public record PlanCargueDetalleDto(
    Guid Id, string Nombre, Guid VehiculoId, Guid? LineaSolicitudId, int NumeroVehiculo,
    string Estado, string Estrategia, string Algoritmo, int? Semilla,
    decimal PesoTotalKg, decimal VolumenUtilizadoM3, decimal UtilizacionVolumen, decimal UtilizacionPiso,
    decimal Puntaje, IReadOnlyList<ItemPlanCargueDetalleDto> Items,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>GET /api/planes-cargue/{id}/resultado y /exportar (issue #112) — mismo shape
/// que <c>ResultadoPlanDto</c> de Cubicador, leído de la proyección local. <c>Metricas</c>
/// es <c>null</c> si el plan nunca se optimizó (nadie ha publicado un
/// plan-de-cargue.generado con métricas todavía).</summary>
public record ResultadoPlanDto(
    PlanCargueDetalleDto Plan,
    VehiculoCubicacionDto Vehiculo,
    IReadOnlyList<ColocacionCargueDto> Colocaciones,
    Mensajes.Cubicacion.MetricasCubicacionEvento? Metricas,
    IReadOnlyList<Mensajes.Cubicacion.IncidenciaValidacionEvento> Incidencias);

/// <summary>GET /api/planes-cargue/{id}/flota (issue #112) — los vehículos (planes)
/// hermanos de una misma línea de solicitud (S2-HU-08: una carga que no cabe en un
/// vehículo se reparte en varios planes con la misma LineaSolicitudId).</summary>
public record PlanFlotaItemDto(Guid Id, int NumeroVehiculo, int TotalVehiculos, int UnidadesCargadas, int UnidadesTotales);
