using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>
/// Catálogo de tipos de vehículo: tblSAPTiposVehiculoTarifario (dbSolucionesTI) más los
/// casos reales de negocio que ese catálogo todavía no cubre — confirmados con datos
/// reales del seeder de S4-HU-10.1 (docs/Database/seeder/README.md,
/// "reglas_solo_sicetac.csv"): 80 filas de devolución de contenedor vacío sin tipo de
/// vehículo real (solo tamaño+modalidad) y 33 filas de la banda "PBV 7500-8000 kg" sin
/// código confirmado en ningún catálogo. Se agregan aquí, no en la BD, hasta que se
/// formalicen con código real (issue pendiente, ver comentario de Sara en #210).
/// </summary>
public sealed class clsTiposVehiculoAdapter(IServiceProvider sp, IMemoryCache cache) : ITiposVehiculoPort
{
    private readonly clsDbSolucionesTiDbContext? _dc = sp.GetService<clsDbSolucionesTiDbContext>();

    public static readonly IReadOnlyList<clsTipoVehiculoDto> CasosEspeciales =
    [
        new("CNT20/Consolidado", "Devolución contenedor 20' — Consolidado", false),
        new("CNT20/Exclusivo", "Devolución contenedor 20' — Exclusivo", false),
        new("CNT40/Consolidado", "Devolución contenedor 40' — Consolidado", false),
        new("CNT40/Exclusivo", "Devolución contenedor 40' — Exclusivo", false),
        new("PBV 7500-8000", "Camión liviano PBV 7500-8000 kg (nuevo)", false),
    ];

    public async Task<IReadOnlyList<clsTipoVehiculoDto>> ListarAsync(CancellationToken ct)
    {
        if (_dc is null) return CasosEspeciales;

        var oficiales = await cache.GetOrCreateAsync("catalogos:tipos-vehiculo", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _dc.TiposVehiculo.AsNoTracking()
                .Where(v => v.Estado)
                .Select(v => new clsTipoVehiculoDto(v.Codigo.Trim(), v.Descripcion.Trim(), true))
                .ToListAsync(ct);
        });

        return oficiales!.Concat(CasosEspeciales).ToList();
    }
}
