using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;

/// <summary>Los términos que usa cada cliente en sus archivos (<c>tbl_diccionario_cliente</c>).</summary>
public interface IDiccionarioClienteRepository
{
    /// <summary>
    /// Sin seguimiento, con el nombre del cliente. Con <paramref name="clienteId"/> trae los de ese cliente y, si
    /// <paramref name="incluirGenerales"/>, también los que aplican a todos; sin él, los de todos los clientes.
    /// <paramref name="buscar"/> filtra por una parte del término o del concepto.
    /// </summary>
    Task<IReadOnlyList<clsTerminoCliente>> ListarAsync(
        Guid? clienteId, bool incluirGenerales, string? tipo, string? buscar, bool soloActivos, CancellationToken ct);

    /// <summary>Con seguimiento, para editarlo o borrarlo; null si no existe.</summary>
    Task<clsTerminoCliente?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>El término que ya ocupa esa combinación (mismo cliente, texto normalizado, tipo y contexto), distinto de <paramref name="excluirId"/>.</summary>
    Task<clsTerminoCliente?> BuscarDuplicadoAsync(
        Guid? clienteId, string terminoNormalizado, string tipo, string? contexto, Guid? excluirId, CancellationToken ct);

    Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct);

    void Agregar(clsTerminoCliente termino);
    void Eliminar(clsTerminoCliente termino);
}
