using Microsoft.EntityFrameworkCore;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>Los códigos de vehículo de NEXO (<c>tbl_mapeo_vehiculo_tarifario</c>) y las carrocerías del catálogo (<c>tbl_tipos_carroceria_catalogo</c>), activos.</summary>
public sealed class clsVehiculosNexoAdapter(clsNexoDbContext db) : IVehiculosNexoPort
{
    public async Task<IReadOnlyList<clsCodigoVehiculoNexo>> ListarCodigosAsync(CancellationToken ct)
    {
        var filas = await db.MapeosVehiculoTarifario.AsNoTracking()
            .Where(m => m.Activo)
            .Select(m => new { m.TipoVehiculo, m.NombreSicetac })
            .ToListAsync(ct);
        return filas.Select(m => new clsCodigoVehiculoNexo(m.TipoVehiculo.Trim(), string.IsNullOrWhiteSpace(m.NombreSicetac) ? m.TipoVehiculo.Trim() : m.NombreSicetac.Trim())).ToList();
    }

    public async Task<IReadOnlyList<string>> ListarCarroceriasOficialesAsync(CancellationToken ct)
    {
        var filas = await db.TiposCarroceriaCatalogo.AsNoTracking()
            .Where(c => c.Activo)
            .Select(c => new { c.CodigoCarroceria, c.NombreCarroceria })
            .ToListAsync(ct);
        return filas.SelectMany(c => new[] { c.CodigoCarroceria, c.NombreCarroceria }).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
    }
}
