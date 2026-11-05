using System.ComponentModel.DataAnnotations;

namespace Nexo.Api.Cotizaciones;

// DTOs de entrada: la validación HTTP vive aquí y el controller los mapea 1:1 a un Command de Application.

public class clsCrearCotizacionRequest
{
    [Required]
    public Guid SolicitudId { get; set; }
}

public class clsAgregarLineaCotizacionRequest
{
    public Guid? LineaSolicitudId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorFlete { get; set; }

    /// <summary>Devolución del contenedor vacío (línea 6 exclusiva); suma al total como concepto aparte.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorRetornoContenedor { get; set; }

    [Required]
    [RegularExpression("(?i)^(sicetac|historica|manual|tarifario)$",
        ErrorMessage = "Fuente debe ser 'sicetac', 'historica', 'manual' o 'tarifario'.")]
    public string Fuente { get; set; } = null!;
}

/// <summary>Editar el flete de una línea. No lleva Fuente a propósito: al guardar, la fuente pasa a Manual.</summary>
public class clsEditarLineaCotizacionRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorFlete { get; set; }

    /// <summary>Si no se envía, no se toca el valor que ya tenía.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorRetornoContenedor { get; set; }

    /// <summary>
    /// <c>true</c> = guardar aunque no se haya podido verificar el piso SICE-TAC (ruta sin piso, período distinto del mes actual o consulta fallida); si no, 409 PISO_SICETAC_NO_VERIFICABLE.
    /// No salta un valor bajo un piso encontrado (siempre 400 FLETE_BAJO_PISO_SICETAC). Default false.
    /// </summary>
    public bool ConfirmarSinPiso { get; set; }
}

/// <summary>Rechazar una línea de la cotización. El motivo es obligatorio (hasta 500).</summary>
public class clsRechazarLineaRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Indica el motivo del rechazo."), MaxLength(500)]
    public string Motivo { get; set; } = null!;
}

/// <summary>Cambiar el vehículo de una línea. Se envía UNO: <c>configVehiculoId</c> (configuración oficial) o <c>tipoVehiculo</c> (texto o código, con la <c>carroceria</c> opcional).</summary>
public class clsCambiarVehiculoLineaRequest
{
    public Guid? ConfigVehiculoId { get; set; }

    [MaxLength(100)]
    public string? TipoVehiculo { get; set; }

    [MaxLength(100)]
    public string? Carroceria { get; set; }
}

public class clsAgregarConexosRequest
{
    [Required, MinLength(1, ErrorMessage = "Debe incluir al menos un servicio conexo.")]
    public List<clsConexoItemRequest> Items { get; set; } = new();
}

public class clsConexoItemRequest
{
    /// <summary>Línea de la cotización a la que aplica. Obligatorio.</summary>
    [Required]
    public Guid LineaId { get; set; }

    [Required]
    public Guid ServicioConexoId { get; set; }

    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Cantidad { get; set; } = 1;

    /// <summary>Si no se envía, se calcula como ValorBase * Cantidad del servicio.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAplicado { get; set; }

    /// <summary>"individual" (default) si se agregó a esta línea puntualmente, "general" si vino de "agregar a todas las líneas" (un ítem por línea, cada uno con su LineaId).</summary>
    [RegularExpression("(?i)^(individual|general)$", ErrorMessage = "Origen debe ser 'individual' o 'general'.")]
    public string Origen { get; set; } = "individual";
}

public class clsEditarConexoRequest
{
    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Cantidad { get; set; }

    /// <summary>Si no se envía, se recalcula como ValorBase * Cantidad del servicio.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAplicado { get; set; }
}

public class clsMarcarResultadoRequest
{
    [Required]
    [RegularExpression("(?i)^(ganada|perdida)$", ErrorMessage = "Resultado debe ser 'ganada' o 'perdida'.")]
    public string Resultado { get; set; } = null!;

    [MaxLength(100)]
    public string? Motivo { get; set; }

    [MaxLength(500)]
    public string? Comentario { get; set; }
}
