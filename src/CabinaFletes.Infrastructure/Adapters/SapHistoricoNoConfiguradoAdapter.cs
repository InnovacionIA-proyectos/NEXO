using CabinaFletes.Application.BibliotecaSapHistorico.Dtos;
using CabinaFletes.Application.BibliotecaSapHistorico.Ports;
using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>
/// Se registra cuando NO hay cadena de conexión al datacenter corporativo
/// (ConnectionStrings:DbSolucionesTi): todo endpoint del módulo responde 503
/// con un mensaje accionable, en vez de fallar la inyección de dependencias
/// porque DbSolucionesTiDbContext no está registrado. Portado de
/// BibliotecaSapHistoricoNoConfigurado del proyecto original.
/// </summary>
public sealed class SapHistoricoNoConfiguradoAdapter : ISapHistoricoPort
{
    private static ServiceUnavailableException Error() => new(
        "SOLUCIONESTI_NO_CONFIGURADO",
        "La conexión al histórico/plaza SAP no está configurada. Define ConnectionStrings:DbSolucionesTi (ver .env.example).");

    public Task<SapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, CancellationToken ct = default)
        => throw Error();

    public Task<SapHistoricoFiltrosDto> FiltrosAsync(CancellationToken ct = default)
        => throw Error();

    public Task<SapHistoricoResultDto> BuscarPlazaAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, CancellationToken ct = default)
        => throw Error();

    public Task<SapHistoricoFiltrosDto> FiltrosPlazaAsync(CancellationToken ct = default)
        => throw Error();
}
