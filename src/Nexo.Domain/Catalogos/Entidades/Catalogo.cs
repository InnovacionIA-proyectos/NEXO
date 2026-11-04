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

    /// <summary>Municipio (código DANE) del Origen. Nulo si el texto no resolvió contra el catálogo (ciudad fuera de Colombia, typo).</summary>
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

    /// <summary>true cuando la fila corresponde a un código canónico del Cubicador (2L2, C2, C3, 2S2, 3S3). Toda fila nueva nace en false (cuarentena, fuera de los desplegables) hasta que se mapee o se descarte.</summary>
    public bool EsCatalogoOficial { get; set; }

    /// <summary>Descartada de la bandeja de normalización. No se borra: las líneas que la referencian la conservan y, si el mismo texto vuelve a llegar, se reutiliza.</summary>
    public bool Descartada { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Catálogo de servicios conexos con tarifas variables por ciudad/ruta/puerto. Cada fila usa solo las
/// dimensiones que le corresponden; las demás quedan en "NO APLICA" (nunca NULL). Única por
/// (CodigoServicio, Origen, Destino, Puerto, Ruta, TipoMaquina). Las dimensiones son texto plano y no
/// catálogos porque hay casos sin municipio único ("Eje Cafetero", corredores viales).
/// </summary>
public class clsServicioConexo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Código corto del servicio (p. ej. "PRE_BSAT", "AVT_AUTO"); si no trae uno propio, es igual a <see cref="TipoServicio"/>.</summary>
    [Required, MaxLength(30)]
    public string CodigoServicio { get; set; } = null!;

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

    [Required, Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal CostoUnitario { get; set; }

    /// <summary>Precio al cliente con margen incluido. Null: al cotizar se usa el costo. <see cref="CostoUnitario"/> es interno y nunca sale al cliente.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? PrecioVenta { get; set; }

    /// <summary>Unidad de cobro ("Por estiba" en Almacenamiento/ITR, "Por unidad" en el resto).</summary>
    [Required, MaxLength(30)]
    public string Unidad { get; set; } = null!;

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
