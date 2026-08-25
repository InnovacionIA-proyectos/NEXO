using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>
/// Catálogo de rutas (origen/destino como texto libre del comercial). La captura
/// reutiliza la ruta existente (comparación normalizada) o crea una nueva.
/// </summary>
public interface IRutasCatalogoPort
{
    /// <summary>Carga el catálogo completo a memoria (es pequeño) para resolver muchas líneas sin ida y vuelta a la BD.</summary>
    Task<IReadOnlyList<Ruta>> ListarAsync(CancellationToken ct);

    void Agregar(Ruta ruta);
}
