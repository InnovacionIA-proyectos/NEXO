using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>Lo que un cliente escribe distinto a NEXO en una columna categórica (los términos de tipo <c>valor</c> de su diccionario).</summary>
public static class clsValoresDelCliente
{
    /// <summary>
    /// <c>(campo, término normalizado) → valor de NEXO</c>. Lo propio del cliente manda sobre lo general; sin cliente solo valen los generales,
    /// porque «todos» son los de todos los clientes.
    /// </summary>
    public static async Task<Dictionary<(string Campo, string Termino), string>> CargarAsync(
        IDiccionarioClienteRepository diccionario, Guid? clienteId, CancellationToken ct)
    {
        var terminos = await diccionario.ListarAsync(clienteId, incluirGenerales: true, tipo: clsTerminoCliente.TipoValor, buscar: null, soloActivos: true, ct);
        var validos = terminos.Where(t => t.Contexto is not null).ToList();

        var resultado = new Dictionary<(string, string), string>();
        // Los generales primero, para que los del cliente los reemplacen.
        foreach (var t in validos.Where(t => t.ClienteId is null).Concat(validos.Where(t => clienteId is not null && t.ClienteId == clienteId)))
            resultado[(t.Contexto!, t.TerminoNormalizado)] = t.Concepto;
        return resultado;
    }
}
