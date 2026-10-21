using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.BibliotecaSapHistorico.Adaptadores;

/// <summary>
/// Se registra cuando NO hay cadena de conexión al datacenter corporativo
/// (ConnectionStrings:DbSolucionesTi): todo endpoint del módulo responde 503
/// con un mensaje accionable, en vez de fallar la inyección de dependencias
/// porque clsDbSolucionesTiDbContext no está registrado. Portado de
/// BibliotecaSapHistoricoNoConfigurado del proyecto original.
/// </summary>
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
