using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;

/// <summary>
/// Consulta de SOLO LECTURA a las tarifas SAP TM del datacenter corporativo
/// (clsDbSolucionesTiDbContext): el histórico sale del log de cambios
/// (dbsolucionesTI.dbo.tblSAPTarifasLog) y las de plaza (vigentes) de
/// dbo.tblSAPTarifas. Lanza clsServiceUnavailableException si la conexión al
/// datacenter no está configurada. Implementado por
/// Infrastructure/Adapters/clsSapHistoricoAdapter.cs (o
/// clsSapHistoricoNoConfiguradoAdapter si falta ConnectionStrings:DbSolucionesTi).
/// </summary>
public interface ISapHistoricoPort
{
    Task<clsSapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, CancellationToken ct = default);

    Task<clsSapHistoricoFiltrosDto> FiltrosAsync(CancellationToken ct = default);

    Task<clsSapHistoricoResultDto> BuscarPlazaAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, int page = 1, CancellationToken ct = default);

    Task<clsSapHistoricoFiltrosDto> FiltrosPlazaAsync(CancellationToken ct = default);
}
