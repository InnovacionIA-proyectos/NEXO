using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;

/// <summary>
/// Consulta de SOLO LECTURA a las tarifas SAP TM del datacenter: histórico desde tblSAPTarifasLog y plaza vigente desde tblSAPTarifas.
/// Lanza clsServiceUnavailableException si la conexión no está configurada.
/// </summary>
public interface ISapHistoricoPort
{
    /// <summary>
    /// <paramref name="page"/> (1-based, def. 1) y <paramref name="top"/> como tamaño de página, igual que <see cref="BuscarPlazaAsync"/>.
    /// <c>Total</c> es el total SIN paginar.
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
