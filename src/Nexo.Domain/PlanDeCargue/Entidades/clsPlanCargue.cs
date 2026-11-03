using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Domain.PlanDeCargue.Entidades;

// ---------------------------------------------------------------------------
// Cubicador 3D — plan de cargue con posiciones calculadas por el motor de bin
// packing. Convención: dimensiones internas en MILÍMETROS y pesos en KG.
// ---------------------------------------------------------------------------

/// <summary>Tipo de vehículo/contenedor (VehicleType). Los presets son filas editables, no constantes.</summary>
public class clsVehiculoCubicacion : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = null!;

    [MaxLength(300)]
    public string? Descripcion { get; set; }

    public decimal LargoInternoMm { get; set; }
    public decimal AnchoInternoMm { get; set; }
    public decimal AltoInternoMm { get; set; }
    public decimal CapacidadKg { get; set; }
    public decimal TaraKg { get; set; }
    public decimal PuertaAnchoMm { get; set; }
    public decimal PuertaAltoMm { get; set; }

    /// <summary>trasera | frontal | costado_izquierdo | costado_derecho</summary>
    [MaxLength(20)]
    public string PuertaPosicion { get; set; } = "trasera";

    /// <summary>JSON: [{"posicionMm":1500,"capacidadKg":8000}, …]</summary>
    public string EjesJson { get; set; } = "[]";

    public decimal ToleranciaParedesMm { get; set; }
    public decimal ToleranciaEntreUnidadesMm { get; set; }

    /// <summary>Preset sembrado por el sistema (editable por el usuario).</summary>
    public bool EsPreset { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Plan de cargue (LoadPlan).</summary>
public class clsPlanCargue : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    public string Nombre { get; set; } = null!;

    [Required]
    public Guid VehiculoId { get; set; }

    [JsonIgnore]
    public clsVehiculoCubicacion? Vehiculo { get; set; }

    /// <summary>Línea de solicitud que originó este plan (botón Cubicar de la Mesa de Costeo).</summary>
    public Guid? LineaSolicitudId { get; set; }

    /// <summary>
    /// Posición (1-based) de este vehículo dentro de la FLOTA de la línea: una carga que
    /// no cabe en un vehículo se reparte en varios planes (Vehículo 1..N) con la misma
    /// LineaSolicitudId. Vale 1 para planes de un solo vehículo o standalone.
    /// </summary>
    public int NumeroVehiculo { get; set; } = 1;

    /// <summary>borrador | procesando | optimizado | ajustado | error</summary>
    [MaxLength(20)]
    public string Estado { get; set; } = "borrador";

    [MaxLength(40)]
    public string Estrategia { get; set; } = "Combinada";

    /// <summary>JSON de PonderacionesCombinada cuando la estrategia es combinada.</summary>
    public string? PonderacionesJson { get; set; }

    [MaxLength(10)]
    public string Algoritmo { get; set; } = "bfd";

    public int? Semilla { get; set; }

    public decimal PesoTotalKg { get; set; }
    public decimal VolumenUtilizadoM3 { get; set; }
    public decimal UtilizacionVolumen { get; set; }
    public decimal UtilizacionPiso { get; set; }
    public decimal Puntaje { get; set; }

    /// <summary>JSON de IncidenciaValidacion[] (concepto ValidationIssue).</summary>
    public string IncidenciasJson { get; set; } = "[]";

    /// <summary>
    /// JSON de MetricasCubicacionDto (cubicador/CONTRATO.md §3.2), tal cual llega en el
    /// evento enriquecido de plan-de-cargue.generado — igual que <see cref="IncidenciasJson"/>,
    /// se guarda verbatim (NEXO no recalcula métricas, solo proyecta lo que Cubicador ya
    /// calculó). Nulo hasta la primera optimización.
    /// </summary>
    public string? MetricasJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<clsItemPlanCargue> Items { get; set; } = new List<clsItemPlanCargue>();
    public ICollection<clsColocacionCargue> Colocaciones { get; set; } = new List<clsColocacionCargue>();
}

/// <summary>Unidad de carga registrada en el plan (CargoItem). Cantidad se expande al optimizar.</summary>
public class clsItemPlanCargue : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PlanCargueId { get; set; }

    [MaxLength(60)]
    public string Sku { get; set; } = "";

    [Required, MaxLength(200)]
    public string Descripcion { get; set; } = null!;

    [MaxLength(60)]
    public string? Pedido { get; set; }

    [MaxLength(120)]
    public string? clsCliente { get; set; }

    [MaxLength(120)]
    public string? Destino { get; set; }

    /// <summary>1 = se entrega primero.</summary>
    public int SecuenciaEntrega { get; set; } = 1;

    public decimal LargoMm { get; set; }
    public decimal AnchoMm { get; set; }
    public decimal AltoMm { get; set; }
    public decimal PesoKg { get; set; }
    public int Cantidad { get; set; } = 1;

    /// <summary>Color hex de representación (opcional; la leyenda puede recolorear).</summary>
    [MaxLength(9)]
    public string? Color { get; set; }

    /// <summary>1 = crítica … 5 = baja.</summary>
    public int Prioridad { get; set; } = 3;

    [MaxLength(40)]
    public string? TipoEmbalaje { get; set; }

    public bool Apilable { get; set; } = true;
    public int MaxNiveles { get; set; }               // 0 = sin límite
    public decimal MaxPesoEncimaKg { get; set; } = -1; // -1 = sin límite
    public bool Rotable { get; set; } = true;
    public bool MantenerVertical { get; set; }
    public bool Fragil { get; set; }
    public bool Peligroso { get; set; }
    public bool Refrigerado { get; set; }

    [MaxLength(40)]
    public string? GrupoCompatibilidad { get; set; }

    [MaxLength(300)]
    public string? Observaciones { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Resultado por unidad física (CargoPlacement): una fila por instancia expandida
/// de cada ítem. Las NO cargadas se persisten con Cargada = false y su razón.
/// </summary>
public class clsColocacionCargue : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PlanCargueId { get; set; }

    [Required]
    public Guid ItemPlanCargueId { get; set; }

    [JsonIgnore]
    public clsItemPlanCargue? Item { get; set; }

    /// <summary>Índice de la instancia dentro de la cantidad del ítem (0-based).</summary>
    public int IndiceUnidad { get; set; }

    public bool Cargada { get; set; }

    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Z { get; set; }
    public decimal LargoColocadoMm { get; set; }
    public decimal AnchoColocadoMm { get; set; }
    public decimal AltoColocadoMm { get; set; }
    public int OrientacionIndice { get; set; }
    public int SecuenciaCargue { get; set; }

    public Guid? SoporteColocacionId { get; set; }
    public decimal PorcentajeSoporte { get; set; }
    public decimal PesoEncimaKg { get; set; }

    /// <summary>estable | advertencia | inestable</summary>
    [MaxLength(20)]
    public string EstadoEstabilidad { get; set; } = "estable";

    /// <summary>accesible | parcialmente_bloqueada | bloqueada</summary>
    [MaxLength(30)]
    public string Accesibilidad { get; set; } = "accesible";

    public bool Bloqueada { get; set; }
    public bool AjusteManual { get; set; }

    [MaxLength(30)]
    public string? CodigoRechazo { get; set; }

    [MaxLength(500)]
    public string? RazonRechazo { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
