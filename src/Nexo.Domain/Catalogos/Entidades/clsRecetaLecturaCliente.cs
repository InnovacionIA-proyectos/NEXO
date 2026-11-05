namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Cómo leer el Excel de un cliente (la «receta» que la persona confirmó): dónde están los encabezados y los datos de cada hoja y cómo se despivotan las tablas
/// cruzadas. Un archivo nuevo del cliente con la misma estructura se lee con esta receta sin llamar a la IA.
/// </summary>
public class clsRecetaLecturaCliente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClienteId { get; set; }
    public clsCliente? Cliente { get; set; }

    /// <summary>Un nombre para reconocerla en una lista (opcional).</summary>
    public string? Nombre { get; set; }

    /// <summary>La receta como JSON (<c>clsRecetaLectura</c>).</summary>
    public string Receta { get; set; } = null!;

    /// <summary>Títulos y rótulos de la parte alta de las hojas que usa la receta, como un arreglo JSON de textos normalizados. Sirve para reconocer el mismo formato.</summary>
    public string Huella { get; set; } = null!;

    /// <summary>Lo que la persona explicó sobre cómo está armado el archivo; se le da de contexto a la IA cuando el formato cambia.</summary>
    public string? Instrucciones { get; set; }

    public int Usos { get; set; }
    public DateTimeOffset? UltimoUsoAt { get; set; }
    public Guid? CreadoPor { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
