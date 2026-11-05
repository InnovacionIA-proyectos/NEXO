using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.LecturaExcel.Interfaces;

/// <summary>Las recetas de lectura que cada cliente ya confirmó (<c>tbl_recetas_lectura_cliente</c>).</summary>
public interface IRecetasLecturaRepository
{
    /// <summary>Sin seguimiento, con el nombre del cliente. Con <paramref name="clienteId"/> las de ese cliente; sin él, las de todos.</summary>
    Task<IReadOnlyList<clsRecetaLecturaCliente>> ListarAsync(Guid? clienteId, CancellationToken ct);

    /// <summary>Con seguimiento, para actualizarla o borrarla; null si no existe.</summary>
    Task<clsRecetaLecturaCliente?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct);

    void Agregar(clsRecetaLecturaCliente receta);
    void Eliminar(clsRecetaLecturaCliente receta);
}
