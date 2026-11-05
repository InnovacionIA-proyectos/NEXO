using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Variante de texto libre ("TB", "T.B", "40HC") que el diccionario traduce al código oficial. Una tabla para dos
/// catálogos según <see cref="TipoElemento"/>: tipos de vehículo (2L2, C2, C3...) y clases de contenedor (CNT40.HC...).
/// <see cref="Codigo"/> es texto, no FK. <see cref="AliasNormalizado"/> es único en toda la tabla y usa el normalizador de la captura.
/// </summary>
public class clsAliasElemento
{
    public const string TipoVehiculo = "Vehiculo";
    public const string TipoContenedor = "Contenedor";

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>El texto tal como se escribió (limpio de espacios), para mostrarlo.</summary>
    [Required, MaxLength(100)]
    public string Alias { get; set; } = null!;

    [Required, MaxLength(100)]
    public string AliasNormalizado { get; set; } = null!;

    /// <summary><see cref="TipoVehiculo"/> o <see cref="TipoContenedor"/>.</summary>
    [Required, MaxLength(20)]
    public string TipoElemento { get; set; } = null!;

    [Required, MaxLength(20)]
    public string Codigo { get; set; } = null!;

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Texto de la query (<c>tipo=</c>) → valor canónico de <see cref="TipoElemento"/>; sin importar mayúsculas ni tildes.</summary>
    public static bool TryParseTipo(string? texto, out string tipo)
    {
        tipo = string.Empty;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var t = texto.Trim();
        if (t.Equals("vehiculo", StringComparison.OrdinalIgnoreCase) || t.Equals("vehículo", StringComparison.OrdinalIgnoreCase)) tipo = TipoVehiculo;
        else if (t.Equals("contenedor", StringComparison.OrdinalIgnoreCase)) tipo = TipoContenedor;
        return tipo.Length > 0;
    }
}
