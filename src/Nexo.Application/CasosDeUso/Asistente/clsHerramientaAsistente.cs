using System.Globalization;
using System.Text.Json;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente;

/// <summary>
/// Lo que una herramienta le devuelve al asistente. Siempre trae la <paramref name="Fuente"/> de donde salió el dato: ningún valor
/// llega al modelo sin ella. Cuando no hay dato, <paramref name="Encontrado"/> es false y <paramref name="Nota"/> lo dice, para que el
/// asistente informe que no hay valor en lugar de inventarlo.
/// </summary>
/// <param name="Valor">El resultado de la consulta tal como lo expone el caso de uso existente; null si no hay dato.</param>
/// <param name="Nota">Aclaración para el asistente: por qué no hay dato, o un límite del resultado (por ejemplo que se recortó).</param>
public sealed record clsResultadoHerramienta(bool Encontrado, object? Valor, string Fuente, string? Nota = null)
{
    public static clsResultadoHerramienta Con(object valor, string fuente, string? nota = null) => new(true, valor, fuente, nota);

    public static clsResultadoHerramienta SinDatos(string fuente, string nota) => new(false, null, fuente, nota);
}

/// <summary>Lo que se le muestra al modelo de una herramienta: nombre, para qué sirve y la forma de su entrada (JSON Schema).</summary>
public sealed record clsDefinicionHerramientaDto(string Nombre, string Descripcion, JsonElement EsquemaEntrada);

/// <summary>
/// Una consulta de solo lectura que el asistente puede pedir. Envuelve un caso de uso o un puerto que ya existe y se ejecuta con la
/// identidad del usuario que conversa: el rol decide si la herramienta está disponible (<see cref="Roles"/>) y las reglas de acceso
/// propias de cada consulta (por ejemplo, que un Comercial solo vea sus solicitudes) se mantienen. Ninguna herramienta escribe.
/// </summary>
public interface IHerramientaAsistente
{
    /// <summary>Identificador estable en minúsculas con guion bajo; es el nombre con el que el modelo la pide.</summary>
    string Nombre { get; }

    string Descripcion { get; }

    /// <summary>JSON Schema de la entrada (un objeto).</summary>
    string EsquemaEntradaJson { get; }

    /// <summary>Roles que pueden usarla: los mismos que ya tiene el endpoint que envuelve. Vacío = cualquier usuario autenticado.</summary>
    IReadOnlyCollection<RolUsuario> Roles { get; }

    /// <exception cref="clsValidationFailedException"><c>HERRAMIENTA_ENTRADA_INVALIDA</c>: la entrada no cumple el esquema.</exception>
    /// <exception cref="clsForbiddenException">El usuario no puede ver lo pedido (por ejemplo <c>SOLICITUD_AJENA</c>).</exception>
    /// <exception cref="clsNotFoundException">Lo pedido no existe.</exception>
    Task<clsResultadoHerramienta> EjecutarAsync(JsonElement entrada, CancellationToken ct = default);
}

/// <summary>
/// Base de las herramientas: valida que la entrada sea un objeto, entrega lectores con error claro para cada campo y traduce la
/// caída de una fuente externa en un resultado "sin datos" (el asistente debe poder decir que la fuente no responde, no fallar).
/// </summary>
public abstract class clsHerramientaBase : IHerramientaAsistente
{
    public abstract string Nombre { get; }
    public abstract string Descripcion { get; }
    public abstract string EsquemaEntradaJson { get; }
    public virtual IReadOnlyCollection<RolUsuario> Roles => [];

    /// <summary>Fuente que se informa cuando la consulta no puede responder.</summary>
    protected abstract string FuenteNoDisponible { get; }

    public async Task<clsResultadoHerramienta> EjecutarAsync(JsonElement entrada, CancellationToken ct = default)
    {
        if (entrada.ValueKind is not JsonValueKind.Object)
            throw new clsValidationFailedException("HERRAMIENTA_ENTRADA_INVALIDA", $"{Nombre}: la entrada debe ser un objeto JSON.");

        try
        {
            return await LeerAsync(new clsEntradaHerramienta(Nombre, entrada), ct);
        }
        catch (clsServiceUnavailableException)
        {
            return clsResultadoHerramienta.SinDatos(FuenteNoDisponible, "La fuente no está disponible en este momento; no hay valor que informar.");
        }
    }

    protected abstract Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct);
}

/// <summary>Lectura tipada de la entrada de una herramienta. Un campo ausente, nulo o vacío cuenta como no enviado.</summary>
public sealed class clsEntradaHerramienta(string herramienta, JsonElement json)
{
    private const int MaxTexto = 200;

    public string? Texto(string campo, bool requerido = false)
    {
        var elemento = Buscar(campo);
        if (elemento is null)
        {
            if (requerido) throw Invalida($"falta \"{campo}\".");
            return null;
        }

        if (elemento.Value.ValueKind is not JsonValueKind.String) throw Invalida($"\"{campo}\" debe ser texto.");
        var texto = elemento.Value.GetString()!.Trim();
        if (texto.Length > MaxTexto) throw Invalida($"\"{campo}\" supera {MaxTexto} caracteres.");
        return texto;
    }

    public int? Entero(string campo, int minimo, int maximo)
    {
        var elemento = Buscar(campo);
        if (elemento is null) return null;

        // El modelo a veces manda el número como texto ("5"); se acepta, pero nada más.
        var valor = elemento.Value.ValueKind switch
        {
            JsonValueKind.Number when elemento.Value.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(elemento.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => throw Invalida($"\"{campo}\" debe ser un número entero."),
        };
        if (valor < minimo || valor > maximo) throw Invalida($"\"{campo}\" debe estar entre {minimo} y {maximo}.");
        return valor;
    }

    public decimal? Decimal(string campo, decimal minimo, decimal maximo)
    {
        var elemento = Buscar(campo);
        if (elemento is null) return null;

        var valor = elemento.Value.ValueKind switch
        {
            JsonValueKind.Number when elemento.Value.TryGetDecimal(out var n) => n,
            JsonValueKind.String when decimal.TryParse(elemento.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) => n,
            _ => throw Invalida($"\"{campo}\" debe ser un número."),
        };
        if (valor < minimo || valor > maximo) throw Invalida($"\"{campo}\" debe estar entre {minimo:0.##} y {maximo:0.##}.");
        return valor;
    }

    /// <summary>Los objetos de una lista del cuerpo; exige al menos uno y no más de <paramref name="maximo"/>.</summary>
    public IReadOnlyList<clsEntradaHerramienta> Objetos(string campo, int maximo)
    {
        if (!json.TryGetProperty(campo, out var lista) || lista.ValueKind is not JsonValueKind.Array)
            throw Invalida($"\"{campo}\" debe ser una lista.");
        var items = lista.EnumerateArray().ToList();
        if (items.Count is 0 || items.Count > maximo) throw Invalida($"\"{campo}\" debe tener entre 1 y {maximo} elementos.");
        if (items.Any(i => i.ValueKind is not JsonValueKind.Object)) throw Invalida($"cada elemento de \"{campo}\" debe ser un objeto.");
        return items.Select(i => new clsEntradaHerramienta(herramienta, i)).ToList();
    }

    /// <summary>Un tope de filas: sin valor, <paramref name="porDefecto"/>; con valor, entre 1 y <paramref name="maximo"/>.</summary>
    public int Tope(string campo, int porDefecto, int maximo) => Entero(campo, 1, maximo) ?? porDefecto;

    public Guid Identificador(string campo)
    {
        var texto = Texto(campo, requerido: true)!;
        return Guid.TryParse(texto, out var id) ? id : throw Invalida($"\"{campo}\" debe ser un identificador (GUID).");
    }

    public TEnum? Enumeracion<TEnum>(string campo) where TEnum : struct, Enum
    {
        var texto = Texto(campo);
        if (texto is null) return null;
        return Enum.TryParse<TEnum>(texto, ignoreCase: true, out var valor) && Enum.IsDefined(valor)
            ? valor
            : throw Invalida($"\"{campo}\" no es válido. Valores: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    /// <summary>Exige que venga al menos uno de los campos: una consulta sin ningún filtro devolvería una muestra sin sentido.</summary>
    public void ExigirAlgunoDe(params string[] campos)
    {
        if (campos.All(c => Buscar(c) is null))
            throw Invalida($"envía al menos uno de: {string.Join(", ", campos)}.");
    }

    private JsonElement? Buscar(string campo)
    {
        if (!json.TryGetProperty(campo, out var elemento)) return null;
        if (elemento.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (elemento.ValueKind is JsonValueKind.String && string.IsNullOrWhiteSpace(elemento.GetString())) return null;
        return elemento;
    }

    private clsValidationFailedException Invalida(string motivo) =>
        new("HERRAMIENTA_ENTRADA_INVALIDA", $"{herramienta}: {motivo}");
}
