using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.TarifasSicetacAdmin.Dtos;
using CabinaFletes.Application.TarifasSicetacAdmin.Ports;

namespace CabinaFletes.Application.TarifasSicetacAdmin.Commands;

public sealed record ImportarTarifasSicetacExcelCommand(Stream Excel) : ICommand<ImportResumenDto>;

public sealed class ImportarTarifasSicetacExcelCommandHandler(
    ITarifasSicetacRepository repo, ICurrentUserPort usuarioActual)
    : ICommandHandler<ImportarTarifasSicetacExcelCommand, ImportResumenDto>
{
    public Task<ImportResumenDto> Handle(ImportarTarifasSicetacExcelCommand command, CancellationToken ct = default) =>
        repo.ImportarExcelAsync(command.Excel, usuarioActual.Id, ct);
}
