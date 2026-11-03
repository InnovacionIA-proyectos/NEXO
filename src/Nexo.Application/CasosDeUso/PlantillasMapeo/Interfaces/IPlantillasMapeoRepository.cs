using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.PlantillasMapeo.Interfaces;

/// <summary>Los mapeos de columnas que cada cliente ya confirmó (<c>tbl_plantillas_mapeo_cliente</c>).</summary>
public interface IPlantillasMapeoRepository
{
    /// <summary>Sin seguimiento, con el nombre del cliente. Con <paramref name="clienteId"/> las de ese cliente; sin él, las de todos.</summary>
    Task<IReadOnlyList<clsPlantillaMapeoCliente>> ListarAsync(Guid? clienteId, CancellationToken ct);

    /// <summary>La plantilla del cliente con esa firma de encabezados, sin seguimiento; null si no hay.</summary>
    Task<clsPlantillaMapeoCliente?> BuscarPorFirmaAsync(Guid clienteId, string firma, CancellationToken ct);

    /// <summary>Con seguimiento, para actualizarla; null si el cliente no tiene una con esa firma.</summary>
    Task<clsPlantillaMapeoCliente?> ObtenerParaActualizarAsync(Guid clienteId, string firma, CancellationToken ct);

    /// <summary>Con seguimiento, para borrarla; null si no existe.</summary>
    Task<clsPlantillaMapeoCliente?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct);

    void Agregar(clsPlantillaMapeoCliente plantilla);
    void Eliminar(clsPlantillaMapeoCliente plantilla);

    /// <summary>¿El error de guardar es el índice único (cliente + firma)? Otra confirmación del mismo formato se adelantó.</summary>
    bool EsChoqueDeFirma(Exception error);

    /// <summary>Descarta lo que quedó pendiente de guardar (la plantilla nueva y su auditoría) para volver a empezar tras un choque de firma.</summary>
    void DescartarCambiosPendientes();
}
