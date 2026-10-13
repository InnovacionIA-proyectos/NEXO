using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.Costeo.Interfaces;

/// <summary>
/// S5-HU-19.1/#281: consulta de tarifa de plaza/histórico contra las tablas propias de
/// NEXO (<c>tbl_tarifas_plaza</c>/<c>tbl_tarifas_plaza_historico</c>) — el reemplazo
/// tipado de los antiguos métodos privados <c>CandidatoPlazaPropiaAsync</c>/
/// <c>CandidatoHistoricoPropioAsync</c> de <c>clsPropuestaTarifaAdapter</c>, extraídos a
/// un puerto propio para que el motor de costeo no sea el único lugar donde esta consulta
/// se puede probar.
///
/// <paramref name="OrigenMunicipioId"/>/<paramref name="DestinoMunicipioId"/> (si la ruta
/// ya los resolvió, S5-HU-16.1/#254) se usan DIRECTO, sin ninguna resolución por texto —
/// es el caso común y el exacto. Solo cuando son null el adaptador cae a resolver el
/// municipio por nombre contenido en <paramref name="Origen"/>/<paramref name="Destino"/>
/// (misma regla de contención en las dos direcciones que ya usa
/// <c>clsTarifarioSapAdapter.CodigosDaneAsync</c>), para que rutas viejas sin Id resuelto
/// también puedan encontrar tarifa. <paramref name="Origen"/>/<paramref name="Destino"/>
/// son SIEMPRE obligatorios (hacen falta para el fallback y para el texto de
/// <c>Detalle</c>), aunque los Id ya vengan resueltos.
///
/// LineaNegocio/ClaseViaje viajan ya resueltos al NOMBRE del catálogo externo (no al
/// enum ni a ningún Id) — <c>clsTarifaPlaza.LineaNegocio/ClaseViaje</c> guardan el nombre,
/// nunca un Id de otra base de datos (ver el doc-comment de esa entidad).
/// </summary>
public sealed record clsConsultaTarifaPlazaInterna(
    string Origen, string Destino, string? LineaNegocio, string? ClaseViaje, string? ClaseContenedor,
    string? TipoVehiculo, decimal? Peso, int? OrigenMunicipioId = null, int? DestinoMunicipioId = null);

/// <summary>
/// <paramref name="Vencida"/>: a propósito SIEMPRE null hoy. <c>tbl_tarifas_plaza.Vigencia</c>
/// es la fecha DESDE la que el precio aplica (confirmado contra los 271 registros reales
/// de S4-HU-10.1: todos traen Vigencia ya pasada) — lo opuesto a las tablas SAP externas,
/// donde Vigencia es la fecha HASTA la que el precio sigue siendo válido. No hay ningún
/// campo que marque una fila de <c>tbl_tarifas_plaza</c> como "vencida" en el sentido SAP
/// (expirada): una actualización SOBRESCRIBE la fila vigente y el valor anterior se
/// archiva aparte, en <c>tbl_tarifas_plaza_historico</c>, vía el trigger de BD — nunca
/// convive una fila vigente y una vencida del mismo lado de NEXO. Inventar una regla de
/// "vencida" para esta tabla sería forzar la semántica de otra tabla distinta sobre una
/// columna que no la tiene.
/// </summary>
public sealed record clsResultadoPlazaInterna(clsCandidatoTarifaDto? Vigente, clsCandidatoTarifaDto? Vencida);

public interface ITarifaPlazaInternaPort
{
    Task<clsResultadoPlazaInterna> BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct = default);
}

/// <summary>
/// Mismo criterio de matching que <see cref="ITarifaPlazaInternaPort"/>, contra
/// <c>tbl_tarifas_plaza_historico</c> — ventana de 3 meses por <c>ReemplazadoAt</c> y
/// deduplicado al último registro por <c>TarifaPlazaId</c> (una misma tarifa pudo
/// reemplazarse varias veces; solo el reemplazo más reciente compite). No separa
/// vigente/vencida — el histórico de SAP (<c>CandidatoSapAsync</c> con
/// <c>soloVigentes: false</c>) tampoco lo hace.
/// </summary>
public interface IHistoricoPlazaInternoPort
{
    Task<clsCandidatoTarifaDto?> BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct = default);
}
