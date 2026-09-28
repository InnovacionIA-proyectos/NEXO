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
