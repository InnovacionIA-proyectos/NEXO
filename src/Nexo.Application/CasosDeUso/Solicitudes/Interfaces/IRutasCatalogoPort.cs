using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>Catálogo de rutas; la captura reutiliza la existente (comparación normalizada) o crea una nueva.</summary>
public interface IRutasCatalogoPort
{
    /// <summary>Carga el catálogo completo a memoria (es pequeño) para resolver muchas líneas sin ida y vuelta a la BD.</summary>
    Task<IReadOnlyList<clsRuta>> ListarAsync(CancellationToken ct);

    void Agregar(clsRuta ruta);
}
