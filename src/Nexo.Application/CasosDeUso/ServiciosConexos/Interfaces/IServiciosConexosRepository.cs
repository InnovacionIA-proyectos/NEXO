using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;

public interface IServiciosConexosRepository
{
    Task<clsPaginaServiciosDto> ListarAsync(bool soloActivos, string? q, string? categoria, int page, int pageSize, CancellationToken ct);

    /// <summary>Duplicado real de la tabla (S4-HU-15.1/#223): las 6 dimensiones juntas
    /// (CodigoServicio, Origen, Destino, Puerto, Ruta, TipoMaquina), no solo el nombre —
    /// el mismo CodigoServicio puede repetirse con dimensiones distintas (Almacenamiento
    /// en dos ciudades). <paramref name="excluirId"/> se pasa al editar.</summary>
    Task<bool> ExisteDimensionesAsync(
        string codigoServicio, string origen, string destino, string puerto, string ruta, string tipoMaquina,
        Guid? excluirId, CancellationToken ct);

    void Agregar(clsServicioConexo servicio);

    /// <summary>Deja en la auditoría el precio de venta que tenía el servicio y el que tiene ahora (se guarda con el resto de los cambios).</summary>
    void RegistrarCambioDePrecioVenta(clsServicioConexo servicio, decimal? precioVentaAntes);

    Task<clsServicioConexo?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Carga masiva desde Excel (.xlsx). Todo el análisis del archivo (detección de
    /// encabezado tolerante, parseo de celdas) y la persistencia viven aquí: separar el
    /// parseo de ClosedXML de la decisión "insertar/duplicado/inválida" no gana
    /// testabilidad real.
    /// </summary>
    Task<clsImportarServiciosResultDto> ImportarExcelAsync(Stream archivo, Guid usuarioId, CancellationToken ct);
}
