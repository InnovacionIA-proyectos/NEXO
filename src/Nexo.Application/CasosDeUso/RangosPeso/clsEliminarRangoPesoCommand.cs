using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.RangosPeso;

/// <summary>Devuelve true si había una banda y se eliminó; false si no existía (para un 404).</summary>
public sealed record clsEliminarRangoPesoCommand(int Id) : ICommand<bool>;

public sealed class clsEliminarRangoPesoCommandHandler(IRangosPesoRepository rangos)
    : ICommandHandler<clsEliminarRangoPesoCommand, bool>
{
    public async Task<bool> Handle(clsEliminarRangoPesoCommand command, CancellationToken ct = default) =>
        await rangos.EliminarAsync(command.Id, ct) > 0;
}
