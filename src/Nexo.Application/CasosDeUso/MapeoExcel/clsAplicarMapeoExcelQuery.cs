using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using static Nexo.Application.CasosDeUso.MapeoExcel.clsConversoresDeCelda;
using L = Nexo.Application.CasosDeUso.MapeoExcel.clsLimitesDeLineaDeCaptura;

namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Aplica a TODAS las filas de un Excel el mapeo que el usuario confirmó y devuelve las líneas listas para capturar, con los errores por fila.
/// Recibe las columnas y las filas que ya devolvió el análisis del archivo; no vuelve a leerlo ni guarda nada.
/// </summary>
/// <param name="ClienteId">El cliente dueño del archivo: su diccionario traduce los valores (lo que el cliente escribe → lo que NEXO entiende).</param>
/// <param name="PrimeraFila">El número que tiene en el archivo la primera fila recibida (2 si hay un solo encabezado), para que los errores digan la fila de Excel.</param>
public sealed record clsAplicarMapeoExcelQuery(
    Guid? ClienteId, IReadOnlyList<clsColumnaAAplicar>? Columnas, IReadOnlyList<IReadOnlyList<string>>? Filas, int PrimeraFila = 2)
    : IQuery<clsResultadoAplicarMapeoDto>;

/// <summary>
/// Es determinista: no usa la IA. Cada columna pide una conversión de una lista cerrada y el código la aplica; una celda que no se entiende se
/// reporta con su fila y su campo en vez de adivinarse. Una fila convertida pasa además por los mismos límites de longitud y rango del cuerpo de
/// la captura y por sus reglas (<see cref="clsCapturarSolicitudValidador.ValidarLinea"/>), de modo que toda línea que sale de aquí la captura
/// la acepta. Las filas con errores no se devuelven como línea: se reportan (todas sus fallas juntas) para corregirlas de una vez.
/// </summary>
public sealed class clsAplicarMapeoExcelQueryHandler(IDiccionarioClienteRepository diccionario)
    : IQueryHandler<clsAplicarMapeoExcelQuery, clsResultadoAplicarMapeoDto>
{
    public const int MaxFilas = 5000;
    public const int MaxColumnas = 100;
    public const int MaxErroresDevueltos = 100;
    private const string Ignorar = "ignorar";

    private static readonly string[] CamposDePosicion = ["descripcion", "cantidad", "largoCm", "anchoCm", "altoCm", "pesoKg", "codigoTipoMercancia"];
    private static readonly string[] CamposSiNo = ["exclusivo", "consolidado", "retornaContenedor", "levanteContenedorDiferente"];

    public async Task<clsResultadoAplicarMapeoDto> Handle(clsAplicarMapeoExcelQuery query, CancellationToken ct = default)
    {
        var filas = query.Filas ?? [];
        if (filas.Count > MaxFilas)
            throw new clsValidationFailedException("DEMASIADAS_FILAS", $"El archivo trae más de {MaxFilas:N0} filas; divídelo y súbelo por partes.");

        var columnas = ValidarColumnas(query.Columnas);
        var valores = await clsValoresDelCliente.CargarAsync(diccionario, query.ClienteId, ct);

        var lineas = new List<clsLineaImportadaDto>();
        var errores = new List<clsErrorDeFilaDto>();
        int totalErrores = 0, filasConErrores = 0, filasVacias = 0;

        for (var i = 0; i < filas.Count; i++)
        {
            var numeroFila = query.PrimeraFila + i;
            var erroresFila = new List<clsErrorDeFilaDto>();
            var linea = ConvertirFila(numeroFila, filas[i] ?? [], columnas, valores, erroresFila);

            if (linea is null && erroresFila.Count == 0) { filasVacias++; continue; }

            if (linea is not null && erroresFila.Count == 0)
                ValidarConReglasDeCaptura(linea, erroresFila);

            if (erroresFila.Count == 0)
            {
                lineas.Add(linea!);
                continue;
            }

            filasConErrores++;
            totalErrores += erroresFila.Count;
            errores.AddRange(erroresFila.Take(Math.Max(0, MaxErroresDevueltos - errores.Count)));
        }

        return new clsResultadoAplicarMapeoDto(filas.Count, filasVacias, filasConErrores, lineas, errores, totalErrores);
    }

    // ---------------------------------------------------------------- el mapeo

    internal sealed record ColumnaAplicable(int Indice, string Campo, string Transformacion);

    /// <summary>El mapeo es una entrada externa: campos y conversiones de las listas cerradas, sin columnas repetidas ni campos duplicados.</summary>
    internal static List<ColumnaAplicable> ValidarColumnas(IReadOnlyList<clsColumnaAAplicar>? entrada)
    {
        if (entrada is null || entrada.Count == 0)
            throw new clsValidationFailedException("MAPEO_REQUERIDO", "Envía el mapeo de las columnas del archivo.");
        if (entrada.Count > MaxColumnas)
            throw new clsValidationFailedException("DEMASIADAS_COLUMNAS", $"El mapeo trae más de {MaxColumnas} columnas.");

        var resultado = new List<ColumnaAplicable>();
        var indices = new HashSet<int>();
        foreach (var c in entrada)
        {
            if (c.Indice < 0 || !indices.Add(c.Indice))
                throw new clsValidationFailedException("COLUMNA_INVALIDA", $"La columna {c.Indice} está repetida o su posición no es válida.");

            var campo = clsTerminoCliente.Canonico(clsTerminoCliente.CamposDestino, c.Campo)
                ?? throw new clsValidationFailedException("CAMPO_INVALIDO", $"«{c.Campo}» no es un campo de NEXO (columna {c.Indice}).");
            var transformacion = string.IsNullOrWhiteSpace(c.Transformacion)
                ? clsTransformacionesMapeo.Ninguna
                : clsTerminoCliente.Canonico(clsTransformacionesMapeo.Todas, c.Transformacion)
                    ?? throw new clsValidationFailedException("TRANSFORMACION_INVALIDA", $"«{c.Transformacion}» no es una conversión conocida (columna {c.Indice}).");

            if (campo == Ignorar) continue;
            if (!clsTransformacionesMapeo.Aplica(campo, transformacion))
                throw new clsValidationFailedException("TRANSFORMACION_NO_APLICA", $"La conversión «{transformacion}» no se puede usar con «{campo}» (columna {c.Indice}).");
            resultado.Add(new(c.Indice, campo, transformacion));
        }

        // Dos columnas para el mismo campo no se pueden resolver sin adivinar; una columna «ORIGEN - DESTINO» ocupa los dos.
        var ocupados = resultado
            .SelectMany(c => c.Transformacion == clsTransformacionesMapeo.DividirOrigenDestino ? new[] { "origen", "destino" } : new[] { c.Campo })
            .ToList();
        var repetido = ocupados.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new clsValidationFailedException("CAMPO_DUPLICADO", $"Más de una columna apunta a «{repetido.Key}»; deja solo una.");

        foreach (var obligatorio in new[] { "origen", "destino" })
            if (!ocupados.Contains(obligatorio))
                throw new clsValidationFailedException("CAMPO_REQUERIDO", $"Falta la columna de «{obligatorio}», que es obligatoria.");

        return resultado;
    }

    // ---------------------------------------------------------------- una fila

    /// <summary>Los valores ya convertidos de una fila, a medida que se leen sus celdas.</summary>
    private sealed class Fila(int numero, List<clsErrorDeFilaDto> errores)
    {
        public readonly Dictionary<string, string> Textos = [];
        public readonly Dictionary<string, decimal> Numeros = [];
        public readonly Dictionary<string, bool> SiNo = [];
        public DateTimeOffset? Deadline;
        public bool HayDatos;

        public void Error(string? campo, string codigo, string mensaje) => errores.Add(new(numero, campo, codigo, mensaje));

        public void Texto(string campo, string valor)
        {
            var max = MaxTexto(campo);
            if (valor.Length > max) Error(campo, "TEXTO_DEMASIADO_LARGO", $"«{campo}» admite máximo {max} caracteres y trae {valor.Length}.");
            else Textos[campo] = valor;
        }
    }

    /// <summary>La línea de la fila, o null si la fila no tiene ningún dato en las columnas usadas o si alguna celda no se pudo convertir (hay errores).</summary>
    private static clsLineaImportadaDto? ConvertirFila(
        int numero, IReadOnlyList<string> celdas, IReadOnlyList<ColumnaAplicable> columnas,
        Dictionary<(string Campo, string Termino), string> valores, List<clsErrorDeFilaDto> errores)
    {
        var f = new Fila(numero, errores);

        foreach (var c in columnas)
        {
            var celda = c.Indice < celdas.Count ? clsTexto.Limpiar(celdas[c.Indice]) : string.Empty;
            if (string.IsNullOrEmpty(celda)) continue;
            f.HayDatos = true;
            LeerCelda(f, c, celda, valores);
        }

        if (!f.HayDatos) return null;
        if (errores.Count > 0) return null;

        var posicion = ArmarPosicion(f);
        if (errores.Count > 0) return null;

        string? T(string campo) => f.Textos.GetValueOrDefault(campo);
        decimal? N(string campo) => f.Numeros.TryGetValue(campo, out var n) ? n : null;
        bool? B(string campo) => f.SiNo.TryGetValue(campo, out var b) ? b : null;

        return new clsLineaImportadaDto(
            numero, T("origen") ?? string.Empty, T("destino") ?? string.Empty, T("tipoVehiculo"), T("carroceria"), T("tipologia"),
            T("lineaNegocio"), T("claseViaje"), T("claseContenedor"), B("exclusivo"), B("consolidado"), B("retornaContenedor"),
            T("ciudadDevolucionContenedor"), B("levanteContenedorDiferente"), T("ciudadLevanteContenedor"),
            N("horasCargue"), N("horasDescargue"), N("peso"), N("valorMercancia"),
            posicion is null ? null : [posicion], T("referencia"), f.Deadline);
    }

    private static void LeerCelda(Fila f, ColumnaAplicable c, string celda, Dictionary<(string Campo, string Termino), string> valores)
    {
        if (c.Transformacion == clsTransformacionesMapeo.DividirOrigenDestino)
        {
            if (TryDividirOrigenDestino(celda, out var origen, out var destino)) { f.Texto("origen", origen); f.Texto("destino", destino); }
            else f.Error(c.Campo, "ORIGEN_DESTINO_NO_SEPARABLE", $"No se pudo separar origen y destino de «{celda}»: se esperaba «ORIGEN - DESTINO».");
        }
        else if (CamposSiNo.Contains(c.Campo))
        {
            if (TryBooleano(celda, out var valor)) f.SiNo[c.Campo] = valor;
            else f.Error(c.Campo, "SI_NO_INVALIDO", $"«{celda}» no es un sí o un no (en «{c.Campo}»).");
        }
        else if (c.Campo == "deadline")
        {
            if (TryFecha(celda, out var fecha)) f.Deadline = fecha;
            else f.Error(c.Campo, "FECHA_INVALIDA", $"«{celda}» no es una fecha válida; se espera día/mes/año (31/10/2026) o año-mes-día.");
        }
        else if (clsTransformacionesMapeo.CamposNumericos.Contains(c.Campo))
            LeerNumero(f, c, celda);
        else
            f.Texto(c.Campo, valores.TryGetValue((c.Campo, clsResolucionDeCatalogos.Normalizar(celda)), out var traducido) ? traducido : celda);
    }

    private static void LeerNumero(Fila f, ColumnaAplicable c, string celda)
    {
        var modo = c.Transformacion switch
        {
            clsTransformacionesMapeo.PesoToneladasAKg => ModoNumero.Toneladas,
            clsTransformacionesMapeo.NumeroComaDecimal => ModoNumero.ComaDecimal,
            _ => ModoNumero.Invariante,
        };

        if (!TryNumero(celda, modo, out var n))
        {
            var pista = modo == ModoNumero.Invariante ? " Si usa coma decimal (1.234,56), marca la columna con «numero_coma_decimal»." : string.Empty;
            f.Error(c.Campo, "NUMERO_INVALIDO", $"«{celda}» no es un número válido (en «{c.Campo}»).{pista}");
            return;
        }

        if (c.Transformacion == clsTransformacionesMapeo.PesoToneladasAKg) n *= 1000m;

        if (c.Campo == "cantidad" && n != decimal.Truncate(n))
        {
            f.Error(c.Campo, "CANTIDAD_INVALIDA", $"La cantidad debe ser un número entero y trae «{celda}».");
            return;
        }

        var (min, max) = RangoDe(c.Campo);
        if (n < min || n > max)
        {
            f.Error(c.Campo, "FUERA_DE_RANGO", $"«{c.Campo}» debe estar entre {min:0.##} y {max:0.##} y trae {n:0.##}.");
            return;
        }

        f.Numeros[c.Campo] = n;
    }

    /// <summary>La posición de carga de la fila: solo si trae algún dato de carga. La cantidad vale 1 si no se indica; las dimensiones y el peso unitario son obligatorios.</summary>
    private static clsPosicionCargaDto? ArmarPosicion(Fila f)
    {
        if (!CamposDePosicion.Any(c => f.Textos.ContainsKey(c) || f.Numeros.ContainsKey(c))) return null;

        var faltan = new[] { "largoCm", "anchoCm", "altoCm", "pesoKg" }.Where(c => !f.Numeros.ContainsKey(c)).ToList();
        if (faltan.Count > 0)
        {
            f.Error(faltan[0], "POSICION_INCOMPLETA", $"La carga de la fila necesita largo, ancho, alto y peso por unidad; falta: {string.Join(", ", faltan)}.");
            return null;
        }

        return new clsPosicionCargaDto(
            f.Textos.GetValueOrDefault("descripcion"), f.Numeros.TryGetValue("cantidad", out var cantidad) ? (int)cantidad : 1,
            f.Numeros["largoCm"], f.Numeros["anchoCm"], f.Numeros["altoCm"], f.Numeros["pesoKg"], f.Textos.GetValueOrDefault("codigoTipoMercancia"));
    }

    // ---------------------------------------------------------------- límites y reglas de la captura

    private static int MaxTexto(string campo) => campo switch
    {
        "origen" or "destino" or "ciudadDevolucionContenedor" or "ciudadLevanteContenedor" => L.MaxCiudad,
        "tipoVehiculo" => L.MaxTipoVehiculo,
        "carroceria" => L.MaxCarroceria,
        "lineaNegocio" => L.MaxLineaNegocio,
        "claseViaje" => L.MaxClaseViaje,
        "claseContenedor" => L.MaxClaseContenedor,
        "descripcion" => L.MaxDescripcionPosicion,
        "codigoTipoMercancia" => L.MaxCodigoTipoMercancia,
        _ => int.MaxValue,
    };

    private static (decimal Min, decimal Max) RangoDe(string campo) => campo switch
    {
        "peso" => (0m, L.MaxPeso),
        "valorMercancia" => (0m, L.MaxValor),
        "horasCargue" or "horasDescargue" => (0m, L.MaxHoras),
        "cantidad" => ((decimal)L.MinCantidad, (decimal)L.MaxCantidad),
        "largoCm" or "anchoCm" or "altoCm" => (L.MinDimensionCm, L.MaxDimensionCm),
        "pesoKg" => (L.MinPesoPosicionKg, L.MaxPesoPosicionKg),
        _ => (decimal.MinValue, decimal.MaxValue),
    };

    /// <summary>Las reglas de la captura sobre la línea ya convertida: origen y destino, tope de unidades y peso consistente.</summary>
    private static void ValidarConReglasDeCaptura(clsLineaImportadaDto l, List<clsErrorDeFilaDto> errores)
    {
        var entrada = new clsCapturaLineaInput(
            l.Origen, l.Destino, l.TipoVehiculo, l.Carroceria, l.Tipologia, l.LineaNegocio, l.ClaseViaje, l.ClaseContenedor,
            l.Exclusivo, l.Consolidado, l.RetornaContenedor, l.CiudadDevolucionContenedor, l.LevanteContenedorDiferente,
            l.CiudadLevanteContenedor, null, l.Peso, l.ValorMercancia, l.Posiciones, l.HorasCargue, l.HorasDescargue);
        try
        {
            clsCapturarSolicitudValidador.ValidarLinea(entrada, l.Fila);
        }
        catch (clsValidationFailedException ex)
        {
            errores.Add(new(l.Fila, null, ex.Code, ex.Message));
        }
    }
}
