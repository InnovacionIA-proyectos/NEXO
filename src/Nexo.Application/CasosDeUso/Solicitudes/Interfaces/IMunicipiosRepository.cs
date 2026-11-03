using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IMunicipiosRepository
{
    /// <summary>Catálogo completo (~3.804 filas) — sin filtro ni paginación, para
    /// poblar el desplegable de Origen/Destino de una sola vez en el cliente.</summary>
    Task<IReadOnlyList<clsMunicipio>> ListarAsync(CancellationToken ct);
}
