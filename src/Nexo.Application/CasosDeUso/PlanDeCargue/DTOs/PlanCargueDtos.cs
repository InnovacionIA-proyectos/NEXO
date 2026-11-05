using Nexo.Application.CasosDeUso.Solicitudes.DTOs;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;

/// <summary>Respuesta del 202 del patrón de trabajos pesados, aplicado a la cubicación.</summary>
public record clsSolicitudDeCubicacionAceptadaDto(Guid PlanCargueId, string Estado, string ConsultarEn);

/// <summary>Respuesta 202 genérica de los comandos de escritura sin recurso propio que consultar. <c>CorrelationId</c> es el id del sobre de la cola, para cruzar con logs/DLQ.</summary>
public record clsComandoAceptadoDto(Guid CorrelationId, string Estado);

/// <summary>Lo que el Cubicador necesita de una línea de solicitud para armar el plan 3D. clsRuta puede venir sin resolver (Origen/Destino nulos) si la línea no tiene ruta.</summary>
public record clsLineaParaCubicacionDto(
    Guid LineaId,
    IReadOnlyList<clsPosicionCargaDto> Posiciones,
    string? Origen,
    string? Destino);

/// <summary>Consulta de respaldo: reconstruye el estado completo sin haber recibido ningún aviso.</summary>
public record clsPlanCargueDto(
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

/// <summary>Lo que viaja por el canal de avisos cuando un plan cambia de estado. Sin la lista de colocaciones a propósito (pueden ser cientos): el aviso dice que hay desenlace y la consulta trae el detalle.</summary>
public record clsAvisoPlanCargueDto(Guid PlanCargueId, string Estado, decimal Puntaje, string? Motivo = null)
{
    public static clsAvisoPlanCargueDto Desde(clsPlanCargueDto p) => new(p.Id, p.Estado, p.Puntaje);
}

// Lectura completa del visor 3D, toda contra la proyección local; los shapes espejan el contrato de Cubicador.

public record clsEjeVehiculoDto(decimal PosicionMm, decimal CapacidadKg);

public record clsVehiculoCubicacionDto(
    Guid Id, string Nombre, string? Descripcion,
    decimal LargoInternoMm, decimal AnchoInternoMm, decimal AltoInternoMm,
    decimal CapacidadKg, decimal TaraKg, decimal PuertaAnchoMm, decimal PuertaAltoMm, string PuertaPosicion,
    IReadOnlyList<clsEjeVehiculoDto> Ejes, decimal ToleranciaParedesMm, decimal ToleranciaEntreUnidadesMm,
    bool EsPreset);

public record clsItemPlanCargueDetalleDto(
    Guid Id, Guid PlanCargueId, string Sku, string Descripcion, string? Pedido, string? clsCliente,
    string? Destino, int SecuenciaEntrega, decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg,
    int Cantidad, string? Color, int Prioridad, string? TipoEmbalaje, bool Apilable, int MaxNiveles,
    decimal MaxPesoEncimaKg, bool Rotable, bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado,
    string? GrupoCompatibilidad, string? Observaciones);

public record clsColocacionCargueDto(
    Guid Id, Guid PlanCargueId, Guid ItemPlanCargueId, int IndiceUnidad, bool Cargada,
    decimal X, decimal Y, decimal Z, decimal LargoColocadoMm, decimal AnchoColocadoMm, decimal AltoColocadoMm,
    int OrientacionIndice, int SecuenciaCargue, Guid? SoporteColocacionId, decimal PorcentajeSoporte,
    decimal PesoEncimaKg, string EstadoEstabilidad, string Accesibilidad, bool Bloqueada, bool AjusteManual,
    string? CodigoRechazo, string? RazonRechazo);

/// <summary>Detalle completo del plan (a diferencia del resumen de <see cref="clsPlanCargueDto"/>), con sus ítems completos.</summary>
public record clsPlanCargueDetalleDto(
    Guid Id, string Nombre, Guid VehiculoId, Guid? LineaSolicitudId, int NumeroVehiculo,
    string Estado, string Estrategia, string Algoritmo, int? Semilla,
    decimal PesoTotalKg, decimal VolumenUtilizadoM3, decimal UtilizacionVolumen, decimal UtilizacionPiso,
    decimal Puntaje, IReadOnlyList<clsItemPlanCargueDetalleDto> Items,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>GET .../resultado y /exportar, leído de la proyección local. <c>Metricas</c> es <c>null</c> si el plan nunca se optimizó.</summary>
public record clsResultadoPlanDto(
    clsPlanCargueDetalleDto Plan,
    clsVehiculoCubicacionDto Vehiculo,
    IReadOnlyList<clsColocacionCargueDto> Colocaciones,
    Mensajes.Cubicacion.clsMetricasCubicacionEvento? Metricas,
    IReadOnlyList<Mensajes.Cubicacion.clsIncidenciaValidacionEvento> Incidencias);

/// <summary>GET .../flota — los vehículos (planes) hermanos de una misma línea de solicitud (una carga que no cabe en uno se reparte en varios).</summary>
public record clsPlanFlotaItemDto(Guid Id, int NumeroVehiculo, int TotalVehiculos, int UnidadesCargadas, int UnidadesTotales);

/// <summary>
/// Estado del "crear plan desde línea": <paramref name="Estado"/> es <c>procesando</c> (aún sin flota ni rechazo), <c>listo</c> (hay flota) o
/// <c>rechazado</c> (<paramref name="Codigo"/>/<paramref name="Mensaje"/> traen el motivo del Cubicador; manda sobre una flota vieja porque es del último intento).
/// </summary>
public record clsEstadoCubicacionLineaDto(
    string Estado, string? Codigo, string? Mensaje, DateTimeOffset? RechazadoAt, IReadOnlyList<clsPlanFlotaItemDto> Vehiculos);
