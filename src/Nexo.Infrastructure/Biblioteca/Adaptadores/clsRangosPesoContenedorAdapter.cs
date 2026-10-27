using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Lee <c>tbl_rangos_peso_contenedor</c> y la guarda en memoria (servicio único con su propio alcance de base de datos, porque lo consultan
/// costeos en paralelo). Sin la tabla, vacía o con la base caída, informa que no está disponible por un minuto y deja un aviso en el log: quien
/// llama usa entonces la regla que tenía antes en lugar de dejar de costear.
/// </summary>
public sealed class clsRangosPesoContenedorAdapter(
    IServiceScopeFactory scopes, IMemoryCache cache, ILogger<clsRangosPesoContenedorAdapter> log) : IRangosPesoContenedorPort
{
    private const string Clave = "rangos-peso-contenedor";
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VigenciaSinTabla = TimeSpan.FromMinutes(1);

    public async Task<clsVehiculoDeContenedor> VehiculoParaAsync(int contenedorPies, decimal pesoKg, CancellationToken ct = default)
    {
        var rangos = await cache.GetOrCreateAsync(Clave, async entrada =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();
                var filas = await db.RangosPesoContenedor.AsNoTracking().Where(r => r.Activo).ToListAsync(ct);
                entrada.AbsoluteExpirationRelativeToNow = filas.Count > 0 ? Vigencia : VigenciaSinTabla;
                return (IReadOnlyList<clsRangoPesoContenedorDto>)filas
                    .Select(r => new clsRangoPesoContenedorDto(r.Contenedor, r.TipoVehiculo.Trim(), r.PesoMaximo)).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                entrada.AbsoluteExpirationRelativeToNow = VigenciaSinTabla;
                log.LogWarning(ex, "No se pudo leer tbl_rangos_peso_contenedor: se usa la regla de rangos de SAP");
                return [];
            }
        });

        return rangos!.Count == 0
            ? new clsVehiculoDeContenedor(false, null)
            : new clsVehiculoDeContenedor(true, clsRecomendadorDeVehiculoDeContenedor.Elegir(rangos, contenedorPies, pesoKg));
    }
}
