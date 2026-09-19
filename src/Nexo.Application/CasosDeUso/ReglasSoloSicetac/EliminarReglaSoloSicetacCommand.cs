using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Devuelve true si había una regla y se eliminó; false si no existía (para un 404).</summary>
public sealed record EliminarReglaSoloSicetacCommand(Guid Id) : ICommand<bool>;

public sealed class EliminarReglaSoloSicetacCommandHandler(IReglasSoloSicetacRepository reglas)
    : ICommandHandler<EliminarReglaSoloSicetacCommand, bool>
{
    // La eliminación en sí es un ExecuteDeleteAsync (ver EfReglasSoloSicetacRepository): no
    // pasa por el UnitOfWork porque no hay nada más que guardar en la misma operación.
    public async Task<bool> Handle(EliminarReglaSoloSicetacCommand command, CancellationToken ct = default) =>
        await reglas.EliminarAsync(command.Id, ct) > 0;
}
