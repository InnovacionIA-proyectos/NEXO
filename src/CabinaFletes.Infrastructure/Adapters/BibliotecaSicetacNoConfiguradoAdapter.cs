using CabinaFletes.Application.CasosDeUso.Biblioteca.DTOs;
using CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;
using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>
/// Se registra cuando NO hay cadena de conexión al Data Warehouse
/// (ConnectionStrings:DwSice): todo endpoint del módulo responde 503 con un
/// mensaje accionable, en vez de fallar la inyección de dependencias porque
/// DwSiceDbContext no está registrado. Portado de BibliotecaSicetacNoConfigurado
/// del proyecto original.
/// </summary>
public sealed class BibliotecaSicetacNoConfiguradoAdapter : IBibliotecaSicetacPort
{
    private static ServiceUnavailableException Error() => new(
        "DWSICE_NO_CONFIGURADO",
        "La conexión al Data Warehouse SICE-TAC no está configurada. Define ConnectionStrings:DwSice.");

    public Task<MatrizSicetacResultDto> BuscarAsync(
        string? q, int? periodo, string? configuracion, string? tipoCarga, string? unidadTransporte,
        string? condicionCarga, string? viaEstandar, int top, CancellationToken ct = default)
        => throw Error();

    public Task<IReadOnlyList<int>> PeriodosAsync(CancellationToken ct = default)
        => throw Error();

    public Task<SicetacFiltrosDto> FiltrosAsync(CancellationToken ct = default)
        => throw Error();
}
