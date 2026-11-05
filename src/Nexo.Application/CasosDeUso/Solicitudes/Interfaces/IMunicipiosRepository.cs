using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IMunicipiosRepository
{
    /// <summary>Catálogo completo, sin filtro ni paginación, para poblar el desplegable de Origen/Destino.</summary>
    Task<IReadOnlyList<clsMunicipio>> ListarAsync(CancellationToken ct);
}
