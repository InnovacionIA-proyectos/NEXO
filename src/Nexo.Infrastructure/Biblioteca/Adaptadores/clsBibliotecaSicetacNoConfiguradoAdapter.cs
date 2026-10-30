using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Se registra cuando NO hay cadena de conexión al Data Warehouse
/// (ConnectionStrings:DwSice): todo endpoint del módulo responde 503 con un
/// mensaje accionable, en vez de fallar la inyección de dependencias porque
/// clsDwSiceDbContext no está registrado. Portado de BibliotecaSicetacNoConfigurado
/// del proyecto original.
/// </summary>
public sealed class clsBibliotecaSicetacNoConfiguradoAdapter : IBibliotecaSicetacPort
{
    private static clsServiceUnavailableException Error() => new(
        "DWSICE_NO_CONFIGURADO",
        "La conexión al Data Warehouse SICE-TAC no está configurada. Define ConnectionStrings:DwSice.");

    public Task<clsMatrizSicetacResultDto> BuscarAsync(
        string? q, int? periodo, string? configuracion, string? tipoCarga, string? unidadTransporte,
        string? condicionCarga, string? viaEstandar, int top, int page = 1, CancellationToken ct = default)
        => throw Error();

    public Task<IReadOnlyList<int>> PeriodosAsync(CancellationToken ct = default)
        => throw Error();

    public Task<clsSicetacFiltrosDto> FiltrosAsync(CancellationToken ct = default)
        => throw Error();
}
