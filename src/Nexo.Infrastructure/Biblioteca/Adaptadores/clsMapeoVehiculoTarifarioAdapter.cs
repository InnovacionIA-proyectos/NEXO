using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Lee <c>tbl_mapeo_vehiculo_tarifario</c> y la guarda en memoria. Es un servicio ÚNICO con su propio alcance de base de datos porque lo
/// consultan los costeos en paralelo (cada trabajador con su propio contexto) y un contexto compartido no es seguro entre hilos.
///
/// Sin la tabla (el script aún no se aplicó) o con la base caída, responde "sin mapeo" por un minuto y registra el aviso: el costeo sigue
/// como antes de existir el mapeo, en lugar de fallar.
/// </summary>
public sealed class clsMapeoVehiculoTarifarioAdapter(
    IServiceScopeFactory scopes, IMemoryCache cache, ILogger<clsMapeoVehiculoTarifarioAdapter> log) : IMapeoVehiculoTarifarioPort
{
    private const string Clave = "mapeo-vehiculo-tarifario";
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VigenciaSinTabla = TimeSpan.FromMinutes(1);

    public async Task<clsMapeoVehiculoTarifarioDto?> ObtenerAsync(string? tipoVehiculo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo)) return null;

        var mapa = await cache.GetOrCreateAsync(Clave, async entrada =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();
                var filas = await db.MapeosVehiculoTarifario.AsNoTracking().Where(m => m.Activo).ToListAsync(ct);
                entrada.AbsoluteExpirationRelativeToNow = Vigencia;
                return filas.ToDictionary(
                    m => clsCodigosDeVehiculo.Normalizar(m.TipoVehiculo),
                    m => new clsMapeoVehiculoTarifarioDto(m.TipoVehiculo.Trim(), m.CodigoSicetac.Trim(), string.IsNullOrWhiteSpace(m.CodigoPlazaSap) ? null : m.CodigoPlazaSap.Trim()));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                entrada.AbsoluteExpirationRelativeToNow = VigenciaSinTabla;
                log.LogWarning(ex, "No se pudo leer tbl_mapeo_vehiculo_tarifario: el costeo usa el código del vehículo sin mapear");
                return new Dictionary<string, clsMapeoVehiculoTarifarioDto>();
            }
        });

        return mapa!.GetValueOrDefault(clsCodigosDeVehiculo.Normalizar(tipoVehiculo));
    }
}
