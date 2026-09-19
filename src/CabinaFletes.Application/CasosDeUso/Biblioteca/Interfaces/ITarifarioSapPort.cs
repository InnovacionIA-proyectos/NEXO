using CabinaFletes.Application.CasosDeUso.Biblioteca.DTOs;

namespace CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>
/// Motor tarifario SAP TM (replica getConsultarTarifaFlete). Null cuando la
/// conexión al datacenter (DbSolucionesTi) no está configurada — se degrada,
/// nunca falla. Implementado por Infrastructure/Adapters/TarifarioSapAdapter.cs.
/// </summary>
public interface ITarifarioSapPort
{
    Task<ResultadoTarifarioSap?> ConsultarAsync(ConsultaTarifarioSap consulta, CancellationToken ct = default);
}
