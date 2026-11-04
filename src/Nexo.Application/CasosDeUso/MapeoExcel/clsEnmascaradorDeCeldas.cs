using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Quita de lo que sale hacia el modelo NIT, nombres de personas, correos, teléfonos y valores comerciales, por reglas y ANTES de armar el
/// mensaje. El marcador (<c>&lt;NIT&gt;</c>…) conserva el tipo del dato. Vías: encabezado, contenido de la columna y de cada celda.
/// </summary>
public static class clsEnmascaradorDeCeldas
{
    public const int MaxCaracteresPorCelda = 200;

    /// <summary>Cuántas filas de una columna se miran para decidir de qué trata: más que las 20 que salen, para no depender de que las primeras sean raras.</summary>
    public const int MaxFilasParaClasificar = 200;

    private static readonly Regex Correo = new(@"[\w.+\-]+@[\w\-]+(\.[\w\-]+)+", RegexOptions.Compiled);
    private static readonly Regex NitConDigito = new(@"\b\d{5,12}-\d\b", RegexOptions.Compiled);
    // Nueve dígitos seguidos o más (cédula, NIT sin dígito de verificación, celular): un peso o unas dimensiones no llegan a tanto.
    private static readonly Regex NumeroLargo = new(@"(?<![\d.,])\d{9,}(?![\d])", RegexOptions.Compiled);
    // Un celular o un fijo con indicativo, con o sin separadores: "300 123 4567", "+57 300-123-4567", "(601) 555 1234". Lo bastante específico
    // para que UNA celda así oculte la columna entera.
    private static readonly Regex Telefono = new(
        @"(?<![\w.,\-])(?:\+?57[\s.\-]?)?(?:3\d{2}[\s.\-]?\d{3}[\s.\-]?\d{4}|\(?60\d\)?[\s.\-]?\d{3}[\s.\-]?\d{4})(?!\d)",
        RegexOptions.Compiled);
    // Un fijo local de siete dígitos ("555-1234"): sirve para ocultar esa celda, pero no para decidir sobre toda la columna, porque
    // "100 1500" también lo cumple.
    private static readonly Regex FijoLocal = new(@"(?<![\w.,\-])\d{3}[\s\-]\d{4}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex Control = new(@"[\p{C}]", RegexOptions.Compiled);
    // "NombreContacto", "NITCliente", "NIT2": parte el encabezado pegado en cambios de minúscula a mayúscula, fin de sigla y letra/número.
    private static readonly Regex CortePorMayuscula = new(@"(?<=\p{Ll})(?=\p{Lu})|(?<=\p{Lu})(?=\p{Lu}\p{Ll})|(?<=\p{L})(?=\d)|(?<=\d)(?=\p{L})", RegexOptions.Compiled);

    private const string MarcadorNit = "<NIT>";
    private const string MarcadorCorreo = "<CORREO>";
    private const string MarcadorTelefono = "<TELEFONO>";
    private const string MarcadorNombre = "<NOMBRE>";
    private const string MarcadorValor = "<VALOR>";

    // Palabras del encabezado (ya normalizado, separadas por no letras ni dígitos) que delatan una columna sensible.
    private static readonly (string Marcador, HashSet<string> Palabras)[] PorEncabezado =
    [
        (MarcadorNit, ["NIT", "NITS", "IDENTIFICACION", "CEDULA", "CC", "DOCUMENTO", "RUT", "NUIP", "PASAPORTE"]),
        (MarcadorCorreo, ["CORREO", "CORREOS", "EMAIL", "EMAILS", "MAIL", "E"]),
        (MarcadorTelefono, ["TELEFONO", "TELEFONOS", "TEL", "TELF", "TLF", "CEL", "CELULAR", "CELULARES", "MOVIL", "MOVILES", "WHATSAPP", "FAX", "PBX"]),
        (MarcadorNombre, [
            "CONTACTO", "CONDUCTOR", "SOLICITANTE", "RESPONSABLE", "NOMBRE", "NOMBRES", "APELLIDO", "APELLIDOS", "REMITENTE", "DESTINATARIO",
            "COMERCIAL", "VENDEDOR", "ASESOR", "PROVEEDOR", "CLIENTE", "PERSONA", "USUARIO", "TITULAR", "PROPIETARIO", "REPRESENTANTE",
            "ENCARGADO", "CONSIGNATARIO", "RECIBE", "ATIENDE", "AUTORIZA", "TRANSPORTISTA", "OPERADOR"]),
        (MarcadorValor, ["VALOR", "PRECIO", "TARIFA", "COSTO", "FLETE", "MONTO", "IVA", "ANTICIPO"]),
    ];

    // Frases de encabezado que dicen "una persona" sin que ninguna palabra suelta lo diga ("Quien recibe", "A cargo de").
    private static readonly (string Marcador, string[] Frases)[] FrasesPorEncabezado =
    [
        (MarcadorNombre, ["QUIEN RECIBE", "QUIEN ENTREGA", "QUIEN SOLICITA", "QUIEN AUTORIZA", "A CARGO", "ATENDIDO POR", "RECIBIDO POR", "ENTREGADO A",
            "AUTORIZADO POR", "SOLICITADO POR", "ELABORADO POR", "RAZON SOCIAL", "PERSONA DE CONTACTO"]),
    ];

    // Raíces que se buscan dentro del encabezado sin separar, para el que viene pegado y en minúsculas ("nombrecontacto", "telefonocelular").
    // Largas a propósito: una raíz corta ("nit", "tel") aparecería dentro de "unitario" o "hotel".
    private static readonly (string Marcador, string[] Raices)[] RaicesPorEncabezado =
    [
        (MarcadorCorreo, ["CORREO", "EMAIL"]),
        (MarcadorTelefono, ["TELEFON", "CELULAR", "WHATSAPP"]),
        (MarcadorNit, ["CEDULA", "IDENTIFICACION"]),
        (MarcadorNombre, ["NOMBRE", "APELLIDO", "CONTACTO", "CONDUCTOR", "SOLICITANTE", "DESTINATARIO", "REMITENTE", "VENDEDOR", "RESPONSABLE", "REPRESENTANTE", "CLIENTE"]),
    ];

    /// <summary>El marcador de una columna sensible por su encabezado, o null si el encabezado no delata nada.</summary>
    public static string? MarcadorDeColumna(string? encabezado)
    {
        var normalizado = clsResolucionDeCatalogos.Normalizar(CortePorMayuscula.Replace(encabezado ?? string.Empty, " "));
        var lista = Regex.Split(normalizado, @"[^A-Z0-9]+").Where(p => p.Length > 0).ToList();
        var palabras = lista.ToHashSet();
        var frase = " " + string.Join(' ', lista) + " ";

        foreach (var (marcador, claves) in PorEncabezado)
        {
            if (!claves.Overlaps(palabras)) continue;
            // "E" solo cuenta junto a "MAIL" ("E MAIL"): suelto es una letra cualquiera.
            if (marcador == MarcadorCorreo && !palabras.Overlaps(["CORREO", "CORREOS", "EMAIL", "EMAILS", "MAIL"])) continue;
            return marcador;
        }

        foreach (var (marcador, frases) in FrasesPorEncabezado)
            if (frases.Any(f => frase.Contains($" {f} ", StringComparison.Ordinal))) return marcador;

        var pegado = string.Concat(lista);
        foreach (var (marcador, raices) in RaicesPorEncabezado)
            if (raices.Any(r => pegado.Contains(r, StringComparison.Ordinal))) return marcador;

        return null;
    }

    /// <summary>Marcador de cada columna: el del encabezado y, si este no dice nada, el del contenido; una columna sensible se oculta ENTERA, no solo las celdas reconocidas.</summary>
    public static IReadOnlyList<string?> MarcadoresDeColumnas(IReadOnlyList<string> encabezados, IReadOnlyList<IReadOnlyList<string>>? filas)
    {
        var marcadores = new string?[encabezados.Count];
        for (var i = 0; i < encabezados.Count; i++)
        {
            marcadores[i] = MarcadorDeColumna(encabezados[i]);
            if (marcadores[i] is null && filas is not null)
                marcadores[i] = MarcadorPorContenido(filas.Take(MaxFilasParaClasificar).Select(f => f is not null && i < f.Count ? f[i] : null));
        }
        return marcadores;
    }

    /// <summary>Marcador por contenido: un correo, un NIT con dígito o un teléfono en cualquier celda bastan; un nombre de persona, en al menos una de cada diez celdas con texto.</summary>
    public static string? MarcadorPorContenido(IEnumerable<string?> celdas)
    {
        var conTexto = 0;
        var nombres = 0;
        var correo = false;
        var nit = false;
        var telefono = false;
        foreach (var celda in celdas)
        {
            if (string.IsNullOrWhiteSpace(celda)) continue;
            conTexto++;
            correo |= Correo.IsMatch(celda);
            nit |= NitConDigito.IsMatch(celda);
            telefono |= Telefono.IsMatch(celda);
            if (clsNombresDePersona.Parece(celda)) nombres++;
        }

        if (correo) return MarcadorCorreo;
        if (nit) return MarcadorNit;
        if (telefono) return MarcadorTelefono;
        return nombres > 0 && nombres * 10 >= conTexto ? MarcadorNombre : null;
    }

    /// <summary>
    /// Celda lista para salir: sin caracteres de control, correos, teléfonos ni números largos, y recortada. Con <paramref name="detectarNombres"/>
    /// oculta también nombres de persona (no en columnas categóricas, donde «Santa Marta» debe verse).
    /// </summary>
    public static string Celda(string? valor, string? marcadorDeLaColumna, bool detectarNombres = false)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        if (marcadorDeLaColumna is not null) return marcadorDeLaColumna;
        if (detectarNombres && clsNombresDePersona.Parece(valor)) return MarcadorNombre;

        var texto = Control.Replace(valor.Trim(), " ");
        texto = Correo.Replace(texto, MarcadorCorreo);
        texto = NitConDigito.Replace(texto, MarcadorNit);
        texto = NumeroLargo.Replace(texto, "<NUMERO>");
        texto = Telefono.Replace(texto, MarcadorTelefono);
        texto = FijoLocal.Replace(texto, MarcadorTelefono);
        return texto.Length <= MaxCaracteresPorCelda ? texto : texto[..MaxCaracteresPorCelda];
    }
}
