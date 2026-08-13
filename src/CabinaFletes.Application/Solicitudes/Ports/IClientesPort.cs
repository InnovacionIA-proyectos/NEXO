using CabinaFletes.Domain.Entities;

namespace CabinaFletes.Application.Solicitudes.Ports;

public interface IClientesPort
{
    Task<bool> ExisteAsync(Guid id, CancellationToken ct);

    Task<Cliente?> ObtenerPorNitAsync(string nit, CancellationToken ct);

    void Agregar(Cliente cliente);

    Task<IReadOnlyList<Cliente>> BuscarAsync(string? terminoNormalizado, int top, CancellationToken ct);
}
