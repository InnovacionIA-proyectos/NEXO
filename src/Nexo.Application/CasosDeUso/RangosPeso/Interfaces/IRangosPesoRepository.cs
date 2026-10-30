using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.RangosPeso.Interfaces;

public interface IRangosPesoRepository
{
    Task<IReadOnlyList<clsRangoPeso>> ListarAsync(CancellationToken ct);

    Task<clsRangoPeso?> ObtenerAsync(int id, CancellationToken ct);

    /// <summary>Para validar duplicados al crear/editar; <paramref name="excluirId"/> se
    /// pasa al editar, para no chocar contra la fila que se está editando.</summary>
    Task<bool> ExisteAsync(int pesoMinimo, int? pesoMaximo, int? excluirId, CancellationToken ct);

    void Agregar(clsRangoPeso rango);

    /// <summary>Elimina por Id; devuelve cuántas filas borró (0 o 1).</summary>
    Task<int> EliminarAsync(int id, CancellationToken ct);
}
