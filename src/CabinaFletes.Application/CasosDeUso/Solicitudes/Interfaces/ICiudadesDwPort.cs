namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>
/// Municipio "costeable": existe en <c>tblSAPDANE</c> y su nombre resuelve (directo o vía
/// zona) contra <c>tblSAPTarifas</c>/<c>tblZonasTarificacionRelacion</c>. Departamento va
/// aparte (no concatenado) porque 256 nombres de municipio se repiten en más de un
/// departamento (HU-005 parte 3) — el frontend decide cómo mostrarlo.
/// </summary>
public sealed record CiudadDwDto(string Municipio, string Departamento);

/// <summary>
/// Catálogo de ciudades para Origen/Destino del formulario de captura, filtrado a solo
/// las que el motor de tarifas (<see cref="CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces.ITarifarioSapPort"/>)
/// puede efectivamente resolver — misma regla de resolución (directo, o vía zona de
/// tarificación) que ya usa ese motor, para no ofrecer ciudades que después no costeen.
/// Null/Configurado=false cuando ConnectionStrings:DbSolucionesTi no está configurada.
/// </summary>
public interface ICiudadesDwPort
{
    bool Configurado { get; }

    /// <summary>Catálogo completo, cacheado 1 h.</summary>
    Task<IReadOnlyList<CiudadDwDto>> ListarAsync(CancellationToken ct);
}
