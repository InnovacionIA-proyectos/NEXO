using CabinaFletes.Application.Cotizaciones.Ports;
using CabinaFletes.Infrastructure.External;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>
/// Comercial responsable del cliente, resuelto contra el DW de cubos SAP
/// (DWCubosEBSSAP.DimCliente, SOLO LECTURA). El DbContext es OPCIONAL: si
/// ConnectionStrings:DwCubosSap no está configurado, Configurado = false.
/// </summary>
public sealed class ComercialResponsableSapAdapter(IServiceProvider sp) : IComercialResponsablePort
{
    private readonly DwCubosSapDbContext? _db = sp.GetService<DwCubosSapDbContext>();

    public bool Configurado => _db is not null;

    public async Task<ResponsableComercialDto?> BuscarPorNitAsync(string nit, CancellationToken ct)
    {
        if (_db is null) return null;
        var responsable = await _db.ClientesSap
            .Where(x => x.Nit == nit && x.CorreoResponsable.Contains("@"))
            .Select(x => new { x.NombreResponsable, x.CorreoResponsable })
            .FirstOrDefaultAsync(ct);
        return responsable is null ? null : new ResponsableComercialDto(responsable.NombreResponsable, responsable.CorreoResponsable);
    }
}
