using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

/// <summary>Carga masiva de tarifas de plaza desde un Excel (.xlsx) ya validado por Api.</summary>
public sealed record clsImportarTarifasPlazaExcelCommand(Stream Archivo) : ICommand<clsImportarTarifasPlazaResultDto>;

public sealed class clsImportarTarifasPlazaExcelCommandHandler(ITarifasPlazaRepository tarifas, ICurrentUserPort usuarioActual)
    : ICommandHandler<clsImportarTarifasPlazaExcelCommand, clsImportarTarifasPlazaResultDto>
{
    public Task<clsImportarTarifasPlazaResultDto> Handle(clsImportarTarifasPlazaExcelCommand command, CancellationToken ct = default) =>
        tarifas.ImportarExcelAsync(command.Archivo, usuarioActual.Id, ct);
}
