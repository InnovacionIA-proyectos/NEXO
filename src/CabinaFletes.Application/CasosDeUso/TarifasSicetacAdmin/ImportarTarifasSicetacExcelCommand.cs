using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record ImportarTarifasSicetacExcelCommand(Stream Excel) : ICommand<ImportResumenDto>;

public sealed class ImportarTarifasSicetacExcelCommandHandler(
    ITarifasSicetacRepository repo, ICurrentUserPort usuarioActual)
    : ICommandHandler<ImportarTarifasSicetacExcelCommand, ImportResumenDto>
{
    public Task<ImportResumenDto> Handle(ImportarTarifasSicetacExcelCommand command, CancellationToken ct = default) =>
        repo.ImportarExcelAsync(command.Excel, usuarioActual.Id, ct);
}
