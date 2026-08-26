using CabinaFletes.Application.CasosDeUso.ServiciosConexos.DTOs;
using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.CasosDeUso.ServiciosConexos.Interfaces;

public interface IServiciosConexosRepository
{
    Task<PaginaServiciosDto> ListarAsync(bool soloActivos, string? q, string? categoria, int page, int pageSize, CancellationToken ct);

    Task<bool> ExisteNombreAsync(string nombre, CancellationToken ct);

    void Agregar(ServicioConexo servicio);

    Task<ServicioConexo?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Carga masiva desde Excel (.xlsx): Nombre, Valor base, Vigente hasta, Categoría, Activo.
    /// Todo el análisis del archivo (detección de encabezado tolerante, parseo de celdas) y la
    /// persistencia viven aquí — como en Biblioteca (ver MIGRACION.md), separar el parseo de
    /// ClosedXML de la decisión "insertar/duplicado/inválida" no gana testabilidad real.
    /// </summary>
    Task<ImportarServiciosResultDto> ImportarExcelAsync(Stream archivo, Guid usuarioId, CancellationToken ct);
}
