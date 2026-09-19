using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;

namespace CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;

/// <summary>
/// Consulta de SOLO LECTURA a las tarifas SAP TM del datacenter corporativo
/// (DbSolucionesTiDbContext): el histórico sale del log de cambios
/// (dbsolucionesTI.dbo.tblSAPTarifasLog) y las de plaza (vigentes) de
/// dbo.tblSAPTarifas. Lanza ServiceUnavailableException si la conexión al
/// datacenter no está configurada. Implementado por
/// Infrastructure/Adapters/SapHistoricoAdapter.cs (o
/// SapHistoricoNoConfiguradoAdapter si falta ConnectionStrings:DbSolucionesTi).
/// </summary>
public interface ISapHistoricoPort
{
    Task<SapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, CancellationToken ct = default);

    Task<SapHistoricoFiltrosDto> FiltrosAsync(CancellationToken ct = default);

    Task<SapHistoricoResultDto> BuscarPlazaAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, int page = 1, CancellationToken ct = default);

    Task<SapHistoricoFiltrosDto> FiltrosPlazaAsync(CancellationToken ct = default);
}
