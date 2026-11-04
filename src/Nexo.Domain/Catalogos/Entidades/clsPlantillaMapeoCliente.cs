namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Mapeo de columnas de un Excel ya confirmado por un cliente, para reusarlo con archivos de los mismos encabezados.
/// Se reconoce por <see cref="FirmaEncabezados"/>: cambiar, agregar, quitar o reordenar una columna da otra firma.
/// </summary>
public class clsPlantillaMapeoCliente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClienteId { get; set; }
    public clsCliente? Cliente { get; set; }

    /// <summary>Un nombre para reconocerla en una lista (opcional).</summary>
    public string? Nombre { get; set; }

    /// <summary>SHA-256 en hexadecimal (64 caracteres) de los encabezados normalizados y en orden. Es única por cliente.</summary>
    public string FirmaEncabezados { get; set; } = null!;

    /// <summary>Los encabezados tal como llegaron, como un arreglo JSON de textos (para mostrarlos).</summary>
    public string Encabezados { get; set; } = null!;

    /// <summary>El mapeo: un arreglo JSON de <c>{ indice, campo, transformacion }</c>, una entrada por columna.</summary>
    public string Columnas { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
