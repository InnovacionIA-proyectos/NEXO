using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.BibliotecaSapHistorico.Adaptadores;

/// <summary>Se registra sin ConnectionStrings:DbSolucionesTi: todo endpoint responde 503 accionable en vez de fallar la DI.</summary>
public sealed class clsSapHistoricoNoConfiguradoAdapter : ISapHistoricoPort
{
    private static clsServiceUnavailableException Error() => new(
        "SOLUCIONESTI_NO_CONFIGURADO",
        "La conexión al histórico/plaza SAP no está configurada. Define ConnectionStrings:DbSolucionesTi (ver .env.example).");

    public Task<clsSapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, int page = 1, CancellationToken ct = default)
        => throw Error();

    public Task<clsSapHistoricoFiltrosDto> FiltrosAsync(CancellationToken ct = default)
        => throw Error();

    public Task<clsSapHistoricoResultDto> BuscarPlazaAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, int page = 1, CancellationToken ct = default)
        => throw Error();

    public Task<clsSapHistoricoFiltrosDto> FiltrosPlazaAsync(CancellationToken ct = default)
        => throw Error();
}
