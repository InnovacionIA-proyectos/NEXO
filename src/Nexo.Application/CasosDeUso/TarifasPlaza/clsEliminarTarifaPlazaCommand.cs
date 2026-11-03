using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

/// <summary>Devuelve true si había una tarifa y se eliminó; false si no existía (para un 404).</summary>
public sealed record clsEliminarTarifaPlazaCommand(Guid Id) : ICommand<bool>;

public sealed class clsEliminarTarifaPlazaCommandHandler(ITarifasPlazaRepository tarifas)
    : ICommandHandler<clsEliminarTarifaPlazaCommand, bool>
{
    public async Task<bool> Handle(clsEliminarTarifaPlazaCommand command, CancellationToken ct = default) =>
        await tarifas.EliminarAsync(command.Id, ct) > 0;
}
