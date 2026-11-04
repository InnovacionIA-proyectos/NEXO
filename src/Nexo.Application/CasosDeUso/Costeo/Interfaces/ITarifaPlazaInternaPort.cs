using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.Costeo.Interfaces;

/// <summary>
/// Consulta de tarifa de plaza/histórico contra las tablas propias de NEXO. Si la ruta trae municipio Id se usan directo;
/// solo si son null se resuelve por nombre contenido en Origen/Destino (siempre obligatorios, también para el Detalle).
/// LineaNegocio/ClaseViaje viajan ya resueltos al NOMBRE del catálogo externo, nunca a un Id.
/// </summary>
public sealed record clsConsultaTarifaPlazaInterna(
    string Origen, string Destino, string? LineaNegocio, string? ClaseViaje, string? ClaseContenedor,
    string? TipoVehiculo, decimal? Peso, int? OrigenMunicipioId = null, int? DestinoMunicipioId = null,
    bool ClaseContenedorEstricta = false);

/// <summary>
/// <paramref name="Vencida"/> es siempre null: Vigencia es la fecha DESDE la que aplica (no HASTA, como en SAP) y una
/// actualización sobrescribe la fila vigente y archiva la anterior en el histórico.
/// </summary>
public sealed record clsResultadoPlazaInterna(clsCandidatoTarifaDto? Vigente, clsCandidatoTarifaDto? Vencida);

public interface ITarifaPlazaInternaPort
{
    Task<clsResultadoPlazaInterna> BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct = default);
}

/// <summary>
/// Mismo matching contra <c>tbl_tarifas_plaza_historico</c>: ventana de 3 meses por <c>ReemplazadoAt</c>, deduplicado
/// al último registro por <c>TarifaPlazaId</c>; no separa vigente/vencida.
/// </summary>
public interface IHistoricoPlazaInternoPort
{
    Task<clsCandidatoTarifaDto?> BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct = default);
}
