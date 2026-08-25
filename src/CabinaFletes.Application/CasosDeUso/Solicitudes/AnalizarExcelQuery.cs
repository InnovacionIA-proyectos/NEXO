using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Analiza un Excel de licitación (cualquier plantilla de cliente) y devuelve las
/// hojas con encabezados y filas para que el usuario mapee las columnas. No persiste nada.
/// </summary>
public sealed record AnalizarExcelQuery(Stream Archivo, string NombreArchivo) : IQuery<AnalisisExcelDto>;

public sealed class AnalizarExcelQueryHandler(IExcelSolicitudesPort excel) : IQueryHandler<AnalizarExcelQuery, AnalisisExcelDto>
{
    public Task<AnalisisExcelDto> Handle(AnalizarExcelQuery query, CancellationToken ct = default) =>
        excel.AnalizarAsync(query.Archivo, query.NombreArchivo, ct);
}
