namespace Nexo.Domain.IA.Entidades;

/// <summary>
/// Una llamada a un modelo de lenguaje (éxito o error): gasto por función y mes, tope diario por usuario y auditoría.
/// No guarda el contenido enviado ni la respuesta, solo cifras y resultado (regla de seguridad).
/// </summary>
public class clsUsoIa
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nombre de la función de NEXO que llamó al modelo (<c>ImportacionExcel</c>, <c>AsistenteCotizacion</c>…).</summary>
    public string Funcion { get; set; } = null!;

    /// <summary>Quién la pidió; null cuando no la dispara una persona.</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Identificador del modelo tal como lo devuelve el proveedor.</summary>
    public string Modelo { get; set; } = null!;

    public int TokensEntrada { get; set; }
    public int TokensSalida { get; set; }

    /// <summary>Tokens leídos del caché de prompt y escritos en él: se facturan distinto que los de entrada normales.</summary>
    public int TokensCacheLectura { get; set; }
    public int TokensCacheEscritura { get; set; }

    /// <summary>Costo en dólares con el precio vigente al llamar; se guarda para que un cambio de precios no reescriba el gasto pasado. Null si el modelo no tiene precio conocido.</summary>
    public decimal? CostoEstimadoUsd { get; set; }

    public int LatenciaMs { get; set; }

    /// <summary><c>ok</c> o el código con el que terminó la llamada (<c>IA_LIMITE_PROVEEDOR</c>, <c>IA_RECHAZADA</c>…).</summary>
    public string Resultado { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public const string ResultadoOk = "ok";
}
