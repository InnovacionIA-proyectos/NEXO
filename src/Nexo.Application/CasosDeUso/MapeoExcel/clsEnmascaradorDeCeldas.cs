using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Quita de lo que sale hacia el modelo los datos que nunca deben salir: NIT, nombres de personas, correos, teléfonos y valores
/// comerciales. El enmascarado se hace aquí, por reglas y ANTES de armar el mensaje; el modelo no decide qué se oculta.
///
/// Para saber a qué campo corresponde una columna el modelo no necesita el dato, solo de qué tipo es, así que el marcador
/// (<c>&lt;NIT&gt;</c>, <c>&lt;CORREO&gt;</c>…) conserva esa información. Dos vías: por el encabezado (toda la columna) y por el contenido
/// de la celda (un correo o un NIT escrito en una columna cualquiera).
/// </summary>
public static class clsEnmascaradorDeCeldas
{
    public const int MaxCaracteresPorCelda = 200;

    private static readonly Regex Correo = new(@"[\w.+\-]+@[\w\-]+(\.[\w\-]+)+", RegexOptions.Compiled);
    private static readonly Regex NitConDigito = new(@"\b\d{5,12}-\d\b", RegexOptions.Compiled);
    // Nueve dígitos seguidos o más (cédula, NIT sin dígito de verificación, celular): un peso o unas dimensiones no llegan a tanto.
    private static readonly Regex NumeroLargo = new(@"(?<![\d.,])\d{9,}(?![\d])", RegexOptions.Compiled);
    private static readonly Regex Control = new(@"[\p{C}]", RegexOptions.Compiled);

    // Palabras del encabezado (ya normalizado, separadas por no letras ni dígitos) que delatan una columna sensible.
    private static readonly (string Marcador, HashSet<string> Palabras)[] PorEncabezado =
    [
        ("<NIT>", ["NIT", "IDENTIFICACION", "CEDULA", "CC", "DOCUMENTO", "RUT"]),
        ("<CORREO>", ["CORREO", "EMAIL", "MAIL", "E"]),
        ("<TELEFONO>", ["TELEFONO", "TEL", "CELULAR", "MOVIL", "WHATSAPP", "FAX"]),
        ("<NOMBRE>", ["CONTACTO", "CONDUCTOR", "SOLICITANTE", "RESPONSABLE", "NOMBRE", "REMITENTE", "DESTINATARIO", "COMERCIAL", "VENDEDOR", "ASESOR", "PROVEEDOR", "CLIENTE"]),
        ("<VALOR>", ["VALOR", "PRECIO", "TARIFA", "COSTO", "FLETE", "MONTO", "IVA", "ANTICIPO"]),
    ];

    /// <summary>El marcador de una columna sensible por su encabezado, o null si el encabezado no delata nada.</summary>
    public static string? MarcadorDeColumna(string? encabezado)
    {
        var palabras = Regex.Split(clsResolucionDeCatalogos.Normalizar(encabezado), @"[^A-Z0-9]+").Where(p => p.Length > 0).ToHashSet();
        foreach (var (marcador, claves) in PorEncabezado)
        {
            if (!claves.Overlaps(palabras)) continue;
            // "E" solo cuenta junto a "MAIL" ("E MAIL"): suelto es una letra cualquiera.
            if (marcador == "<CORREO>" && !palabras.Overlaps(["CORREO", "EMAIL", "MAIL"])) continue;
            return marcador;
        }
        return null;
    }

    /// <summary>Una celda lista para salir: sin caracteres de control, sin correos ni números largos y recortada.</summary>
    public static string Celda(string? valor, string? marcadorDeLaColumna)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        if (marcadorDeLaColumna is not null) return marcadorDeLaColumna;

        var texto = Control.Replace(valor.Trim(), " ");
        texto = Correo.Replace(texto, "<CORREO>");
        texto = NitConDigito.Replace(texto, "<NIT>");
        texto = NumeroLargo.Replace(texto, "<NUMERO>");
        return texto.Length <= MaxCaracteresPorCelda ? texto : texto[..MaxCaracteresPorCelda];
    }
}
