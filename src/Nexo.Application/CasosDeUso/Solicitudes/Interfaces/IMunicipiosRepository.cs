using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IMunicipiosRepository
{
    /// <summary>La siembra es de una sola vez (foto fija, sin sincronización periódica) —
    /// esto es lo que hace que el comando falle si ya se corrió antes, en vez de
    /// duplicar o mezclar corridas distintas.</summary>
    Task<bool> ExisteAlgunoAsync(CancellationToken ct);

    void AgregarRango(IEnumerable<clsMunicipio> municipios);

    /// <summary>Catálogo completo (~3.804 filas) — sin filtro ni paginación, para
    /// poblar el desplegable de Origen/Destino de una sola vez en el cliente.</summary>
    Task<IReadOnlyList<clsMunicipio>> ListarAsync(CancellationToken ct);
}
