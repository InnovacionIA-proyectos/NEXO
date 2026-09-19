using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>
/// Carga masiva desde Excel. La lectura/parseo de celdas (ClosedXML) y la deduplicación
/// contra la BD viven en el repositorio (Infrastructure): es lógica de acceso a datos
/// entrelazada con el formato de archivo, sin regla de negocio separable con provecho
/// real de testabilidad (mismo criterio pragmático usado en Biblioteca/ReglasSoloSicetac).
/// </summary>
public sealed record clsImportarServiciosConexosExcelCommand(Stream Archivo) : ICommand<clsImportarServiciosResultDto>;

public sealed class clsImportarServiciosConexosExcelCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual)
    : ICommandHandler<clsImportarServiciosConexosExcelCommand, clsImportarServiciosResultDto>
{
    public Task<clsImportarServiciosResultDto> Handle(clsImportarServiciosConexosExcelCommand command, CancellationToken ct = default) =>
        repo.ImportarExcelAsync(command.Archivo, usuarioActual.Id, ct);
}
