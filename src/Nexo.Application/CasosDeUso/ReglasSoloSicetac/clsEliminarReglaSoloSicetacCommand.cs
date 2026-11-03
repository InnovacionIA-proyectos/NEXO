using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Devuelve true si había una regla y se eliminó; false si no existía (para un 404).</summary>
public sealed record clsEliminarReglaSoloSicetacCommand(Guid Id) : ICommand<bool>;

public sealed class clsEliminarReglaSoloSicetacCommandHandler(IReglasSoloSicetacRepository reglas)
    : ICommandHandler<clsEliminarReglaSoloSicetacCommand, bool>
{
    // La eliminación en sí es un ExecuteDeleteAsync (ver clsEfReglasSoloSicetacRepository): no
    // pasa por el UnitOfWork porque no hay nada más que guardar en la misma operación.
    public async Task<bool> Handle(clsEliminarReglaSoloSicetacCommand command, CancellationToken ct = default) =>
        await reglas.EliminarAsync(command.Id, ct) > 0;
}
