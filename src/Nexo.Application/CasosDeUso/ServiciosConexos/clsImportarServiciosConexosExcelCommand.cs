using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>Carga masiva desde Excel. El parseo (ClosedXML) y la deduplicación viven en el repositorio: es acceso a datos entrelazado con el formato de archivo.</summary>
public sealed record clsImportarServiciosConexosExcelCommand(Stream Archivo) : ICommand<clsImportarServiciosResultDto>;

public sealed class clsImportarServiciosConexosExcelCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual)
    : ICommandHandler<clsImportarServiciosConexosExcelCommand, clsImportarServiciosResultDto>
{
    public Task<clsImportarServiciosResultDto> Handle(clsImportarServiciosConexosExcelCommand command, CancellationToken ct = default) =>
        repo.ImportarExcelAsync(command.Archivo, usuarioActual.Id, ct);
}
