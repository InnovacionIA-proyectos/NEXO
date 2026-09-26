using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>
/// Fuente del datacenter corporativo (dbSolucionesTI) para la siembra única del
/// catálogo de municipios (S4-HU-09.1). Sin cachear (a diferencia de
/// <see cref="IClientesDwPort"/>/<see cref="IProspectosDwPort"/>): esto se corre una
/// sola vez, no en cada request. Null/Configurado=false cuando
/// ConnectionStrings:DbSolucionesTi no está configurada.
/// </summary>
public interface IMunicipiosSiembraPort
{
    bool Configurado { get; }

    /// <summary>
    /// Lee tblSAPDANE + tblZonasTarificacionRelacion + tblSAPTarifas/tblSAPTarifasLog,
    /// aplica el filtro/match/excepciones ya verificados (ver
    /// <c>clsResolucionMunicipios</c> en Infrastructure — el detalle de esa
    /// transformación vive ahí, no en Application, porque depende por completo del
    /// esquema del datacenter corporativo) y devuelve el catálogo completo, listo
    /// para insertar tal cual.
    /// </summary>
    Task<IReadOnlyList<clsMunicipio>> ConstruirCatalogoAsync(CancellationToken ct);
}
