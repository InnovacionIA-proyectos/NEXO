using Nexo.Application.CasosDeUso.Biblioteca.DTOs;

namespace Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>
/// Motor tarifario SAP TM (replica getConsultarTarifaFlete). Null cuando la
/// conexión al datacenter (DbSolucionesTi) no está configurada — se degrada,
/// nunca falla. Implementado por Infrastructure/Adapters/clsTarifarioSapAdapter.cs.
/// </summary>
public interface ITarifarioSapPort
{
    Task<clsResultadoTarifarioSap?> ConsultarAsync(clsConsultaTarifarioSap consulta, CancellationToken ct = default);
}
