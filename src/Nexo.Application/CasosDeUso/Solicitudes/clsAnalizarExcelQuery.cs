using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Analiza un Excel de licitación (cualquier plantilla de cliente) y devuelve las
/// hojas con encabezados y filas para que el usuario mapee las columnas. No persiste nada.
/// </summary>
public sealed record clsAnalizarExcelQuery(Stream Archivo, string NombreArchivo) : IQuery<clsAnalisisExcelDto>;

public sealed class clsAnalizarExcelQueryHandler(IExcelSolicitudesPort excel) : IQueryHandler<clsAnalizarExcelQuery, clsAnalisisExcelDto>
{
    public Task<clsAnalisisExcelDto> Handle(clsAnalizarExcelQuery query, CancellationToken ct = default) =>
        excel.AnalizarAsync(query.Archivo, query.NombreArchivo, ct);
}
