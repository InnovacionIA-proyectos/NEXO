using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IClientesPort
{
    Task<bool> ExisteAsync(Guid id, CancellationToken ct);

    Task<Cliente?> ObtenerPorNitAsync(string nit, CancellationToken ct);

    void Agregar(Cliente cliente);

    Task<IReadOnlyList<Cliente>> BuscarAsync(string? terminoNormalizado, int top, CancellationToken ct);
}
