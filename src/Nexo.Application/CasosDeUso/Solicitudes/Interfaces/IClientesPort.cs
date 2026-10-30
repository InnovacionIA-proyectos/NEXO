using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IClientesPort
{
    Task<bool> ExisteAsync(Guid id, CancellationToken ct);

    Task<clsCliente?> ObtenerPorNitAsync(string nit, CancellationToken ct);

    void Agregar(clsCliente cliente);

    Task<IReadOnlyList<clsCliente>> BuscarAsync(string? terminoNormalizado, int top, CancellationToken ct);
}
