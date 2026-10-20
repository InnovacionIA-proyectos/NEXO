using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Common;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Domain.Catalogos.Entidades;

public class clsRuta : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    /// <summary>S5-HU-16.1/#254: código DANE real del Origen/Destino, vía tbl_municipios —
    /// nulo en rutas creadas antes de esta issue o con texto que no resolvió contra el
    /// catálogo (ej. ciudad fuera de Colombia, typo). Base para que SICE-TAC (#255) y
    /// plaza/histórico (#256) dejen de comparar por texto.</summary>
    public int? OrigenMunicipioId { get; set; }
    [JsonIgnore]
    public clsMunicipio? OrigenMunicipio { get; set; }

    public int? DestinoMunicipioId { get; set; }
    [JsonIgnore]
    public clsMunicipio? DestinoMunicipio { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class clsConfiguracionVehiculo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string TipoVehiculo { get; set; } = null!;

    [MaxLength(100)]
    public string? Carroceria { get; set; }

    [MaxLength(50)]
    public string? RangoPeso { get; set; }

    /// <summary>
    /// S5-HU-18.4/#276: true cuando esta fila corresponde a uno de los códigos canónicos
    /// del Cubicador (2L2, C2, C3, 2S2, 3S3). ResolverVehiculo ya NO crea filas oficiales
    /// en silencio — toda fila nueva nace en false (en cuarentena), fuera de cualquier
    /// desplegable de selección, hasta que alguien la mapee a un código canónico o la
    /// descarte (pantalla de normalización, S5-HU-19.3, fuera de este repo).
    /// </summary>
    public bool EsCatalogoOficial { get; set; }

    /// <summary>
    /// S6-HU-27.3/#322: un humano descartó esta fila de la bandeja de normalización (no corresponde a ningún vehículo real). Sale de la
    /// bandeja pero NO se borra: las líneas que ya apuntan a ella siguen apuntándole, y si el mismo texto vuelve a llegar se reutiliza
    /// la fila descartada en vez de crear otra en cuarentena.
    /// </summary>
    public bool Descartada { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Catálogo de servicios conexos mantenido por Servicios Logísticos (S4-HU-15,
/// reestructurado en la Issue #223 por Sara: tarifas variables por ciudad/ruta/puerto en
/// vez de un valor único genérico). Cada fila usa SOLO el subconjunto de dimensiones que
/// le corresponde según su tipo de servicio — las que no aplican quedan en "NO APLICA"
/// (default de columna, nunca NULL): Almacenamiento usa <see cref="Origen"/>; ITR usa
/// <see cref="Origen"/>+<see cref="Puerto"/>+<see cref="TipoMaquina"/>; un adicional "por
/// ruta" usa <see cref="Ruta"/>; uno "por origen-destino" usa <see cref="Origen"/>+
/// <see cref="Destino"/>; los servicios de valor único ya sembrados (Cargue, Descargue,
/// Stand-by...) no usan ninguna. Única por (CodigoServicio, Origen, Destino, Puerto,
/// Ruta, TipoMaquina) — ver <c>UQ_servicios_conexos_dimensiones</c>.
///
/// Origen/Destino/Puerto/Ruta/TipoMaquina son texto plano, NO catálogos: se intentó FK a
/// municipios y se abandonó porque hay casos reales sin municipio único ("Eje Cafetero",
/// peajes/corredores viales como "Peaje De Occidente").
/// </summary>
public class clsServicioConexo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Código corto del servicio (p. ej. "PRE_BSAT", "AVT_AUTO") — antes no existía, se
    /// derivó del Excel de costos 2026; cuando el servicio no traía uno propio, es igual a <see cref="TipoServicio"/>.</summary>
    [Required, MaxLength(30)]
    public string CodigoServicio { get; set; } = null!;

    /// <summary>Antes "Nombre".</summary>
    [Required, MaxLength(120)]
    public string TipoServicio { get; set; } = null!;

    public const string NoAplica = "NO APLICA";

    [Required, MaxLength(150)]
    public string Origen { get; set; } = NoAplica;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = NoAplica;

    [Required, MaxLength(50)]
    public string Puerto { get; set; } = NoAplica;

    [Required, MaxLength(50)]
    public string Ruta { get; set; } = NoAplica;

    [Required, MaxLength(60)]
    public string TipoMaquina { get; set; } = NoAplica;

    /// <summary>Antes "ValorBase" — ahora obligatorio, ya no hay ReglaCobro genérica.</summary>
    [Required, Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal CostoUnitario { get; set; }

    /// <summary>Unidad de cobro real ("Por estiba" en Almacenamiento/ITR, "Por unidad" en el resto)
    /// — el front la muestra junto a la cantidad para que quede claro qué se está contando.</summary>
    [Required, MaxLength(30)]
    public string Unidad { get; set; } = null!;

    /// <summary>Antes "VigenteHasta" (nullable) — ahora obligatoria.</summary>
    public DateOnly Vigencia { get; set; }

    /// <summary>Segmento del catálogo: "logistico" (por defecto) o "adicional".</summary>
    [Required, MaxLength(20)]
    public string Categoria { get; set; } = "logistico";

    public bool Activo { get; set; } = true;

    public Guid? ActualizadoPor { get; set; }

    [JsonIgnore]
    public clsUsuario? ActualizadoPorUsuario { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
