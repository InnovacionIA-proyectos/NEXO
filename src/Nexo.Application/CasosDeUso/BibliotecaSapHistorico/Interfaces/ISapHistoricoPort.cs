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
    /// <summary>
    /// S6-HU-29.2/#312: <paramref name="page"/> (1-based, def. 1) y <paramref name="top"/> como tamaño de página, igual que
    /// <see cref="BuscarPlazaAsync"/>. <c>Total</c> sigue siendo el total SIN paginar. Antes solo había <c>top</c> y las filas más
    /// allá de las primeras eran inalcanzables (el histórico trae ~26 mil).
    /// </summary>
    Task<clsSapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, int page = 1, CancellationToken ct = default);

    Task<clsSapHistoricoFiltrosDto> FiltrosAsync(CancellationToken ct = default);

    Task<clsSapHistoricoResultDto> BuscarPlazaAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, int page = 1, CancellationToken ct = default);

    Task<clsSapHistoricoFiltrosDto> FiltrosPlazaAsync(CancellationToken ct = default);
}
