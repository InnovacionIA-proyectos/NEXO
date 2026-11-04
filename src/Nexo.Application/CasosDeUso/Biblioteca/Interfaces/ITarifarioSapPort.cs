using Nexo.Application.CasosDeUso.Biblioteca.DTOs;

namespace Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>Motor tarifario SAP TM (replica getConsultarTarifaFlete). Null cuando DbSolucionesTi no está configurada: degrada, nunca falla.</summary>
public interface ITarifarioSapPort
{
    Task<clsResultadoTarifarioSap?> ConsultarAsync(clsConsultaTarifarioSap consulta, CancellationToken ct = default);
}
