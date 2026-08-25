using CabinaFletes.Application.CasosDeUso.ServiciosConexos.DTOs;
using CabinaFletes.Application.CasosDeUso.ServiciosConexos.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.ServiciosConexos;

/// <summary>
/// Carga masiva desde Excel. La lectura/parseo de celdas (ClosedXML) y la deduplicación
/// contra la BD viven en el repositorio (Infrastructure): es lógica de acceso a datos
/// entrelazada con el formato de archivo, sin regla de negocio separable con provecho
/// real de testabilidad (mismo criterio pragmático usado en Biblioteca/ReglasSoloSicetac).
/// </summary>
public sealed record ImportarServiciosConexosExcelCommand(Stream Archivo) : ICommand<ImportarServiciosResultDto>;

public sealed class ImportarServiciosConexosExcelCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual)
    : ICommandHandler<ImportarServiciosConexosExcelCommand, ImportarServiciosResultDto>
{
    public Task<ImportarServiciosResultDto> Handle(ImportarServiciosConexosExcelCommand command, CancellationToken ct = default) =>
        repo.ImportarExcelAsync(command.Archivo, usuarioActual.Id, ct);
}
