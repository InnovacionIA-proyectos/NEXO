using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.Common.Dtos;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// S5-HU-19.1/#281: implementa <see cref="ITarifaPlazaInternaPort"/>/
/// <see cref="IHistoricoPlazaInternoPort"/> contra <c>tbl_tarifas_plaza</c>/
/// <c>tbl_tarifas_plaza_historico</c> — reemplaza a los antiguos métodos privados de
/// <c>clsPropuestaTarifaAdapter</c> (<c>CandidatoPlazaPropiaAsync</c>/
/// <c>CandidatoHistoricoPropioAsync</c>, S5-HU-16.3/#256), que solo filtraban por
/// municipio+vehículo+peso, sin línea de negocio/clase de viaje/clase de contenedor ni
/// concepto de nivel exacto-luego-tolerante.
///
/// Fila de 7 dimensiones unificada entre <see cref="clsTarifaPlaza"/> (el campo de
/// dedup/vigencia es <see cref="clsTarifaPlaza.Vigencia"/>) y
/// <see cref="clsTarifaPlazaHistorico"/> (el de dedup es <see cref="clsTarifaPlazaHistorico.TarifaPlazaId"/>,
/// el de vigencia/ventana es <see cref="clsTarifaPlazaHistorico.ReemplazadoAt"/>).
/// </summary>
public sealed class clsTarifaPlazaInternaAdapter(clsNexoDbContext db, TimeProvider clock, IMemoryCache cache, IMapeoVehiculoTarifarioPort mapeo)
    : ITarifaPlazaInternaPort, IHistoricoPlazaInternoPort
{
    private const int VentanaMesesHistorico = 3;

    internal sealed record clsFila(
        Guid? TarifaPlazaId, decimal Valor, string DetallesTarifa,
        string LineaNegocio, string ClaseViaje, string? ClaseContenedor, int? PesoId, string? TipoVehiculo);

    public async Task<clsResultadoPlazaInterna> BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct = default)
    {
        var (origenIds, destinoIds) = await ResolverMunicipiosAsync(consulta, ct);
        if (origenIds.Count == 0 || destinoIds.Count == 0) return new clsResultadoPlazaInterna(null, null);

        var hoy = DateOnly.FromDateTime(clock.GetUtcNow().Date);
        var filas = await db.TarifasPlaza.AsNoTracking()
            .Where(t => origenIds.Contains(t.OrigenMunicipioId) && destinoIds.Contains(t.DestinoMunicipioId) && t.Vigencia <= hoy)
            .Select(t => new clsFila(null, t.Valor, t.DetallesTarifa, t.LineaNegocio, t.ClaseViaje, t.ClaseContenedor, t.PesoId, t.TipoVehiculo))
            .ToListAsync(ct);

        var pesoId = await PesoIdAsync(consulta.Peso, ct);
        var top = await SeleccionarAsync(filas, consulta, pesoId, ct);

        // Vencida siempre null — ver el doc-comment de clsResultadoPlazaInterna: esta
        // tabla no tiene el concepto SAP de "vigencia hasta" vencida.
        return new clsResultadoPlazaInterna(
            top is null ? null : new clsCandidatoTarifaDto(
                "plaza", top.Value.Valor,
                $"vigencia {hoy:yyyy-MM-dd}" + (top.Value.Fila.DetallesTarifa is { Length: > 0 } ? $" · {top.Value.Fila.DetallesTarifa}" : ""),
                $"tbl_tarifas_plaza (propia) · {top.Value.Criterio}", filas.Count,
                Advertencia: top.Value.VehiculoRelajado ? AdvertenciaVehiculo : null, TablaOrigen: "NEXO",
                VehiculoRelajado: top.Value.VehiculoRelajado),
            null);
    }

    Task<clsCandidatoTarifaDto?> IHistoricoPlazaInternoPort.BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct) =>
        BuscarHistoricoAsync(consulta, ct);

    private async Task<clsCandidatoTarifaDto?> BuscarHistoricoAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct)
    {
        var (origenIds, destinoIds) = await ResolverMunicipiosAsync(consulta, ct);
        if (origenIds.Count == 0 || destinoIds.Count == 0) return null;

        var hoy = clock.GetUtcNow();
        var desde = hoy.AddMonths(-VentanaMesesHistorico);

        // Dedup al ÚLTIMO reemplazo por TarifaPlazaId antes de aplicar el nivel de
        // matching: una misma tarifa pudo archivarse varias veces, solo la más reciente
        // compite (igual criterio que clsPropuestaTarifaAdapter.CandidatoSapAsync con el
        // log SAP: "último registro por tarifa"). Materializa ANTES del GroupBy/First: EF
        // Core no traduce "GroupBy(...).Select(g => g.OrderBy(...).First())" encadenado con
        // una proyección más (confirmado al verificar esta issue —
        // KeyNotFoundException: "EmptyProjectionMember"); la tabla es chica (~cientos de
        // filas), dedup en memoria es barato.
        var enVentana = await db.TarifasPlazaHistorico.AsNoTracking()
            .Where(t => origenIds.Contains(t.OrigenMunicipioId) && destinoIds.Contains(t.DestinoMunicipioId) && t.ReemplazadoAt >= desde)
            .ToListAsync(ct);
        var ultimos = enVentana
            .GroupBy(t => t.TarifaPlazaId)
            .Select(g => g.OrderByDescending(x => x.ReemplazadoAt).First())
            .Select(t => new clsFila(t.TarifaPlazaId, t.Valor, t.DetallesTarifa, t.LineaNegocio, t.ClaseViaje, t.ClaseContenedor, t.PesoId, t.TipoVehiculo))
            .ToList();

        var pesoId = await PesoIdAsync(consulta.Peso, ct);
        var top = await SeleccionarAsync(ultimos, consulta, pesoId, ct);
        if (top is null) return null;

        return new clsCandidatoTarifaDto(
            "historico_sap", top.Value.Valor,
            $"últimos {VentanaMesesHistorico} meses" + (top.Value.Fila.DetallesTarifa is { Length: > 0 } ? $" · {top.Value.Fila.DetallesTarifa}" : ""),
            $"tbl_tarifas_plaza_historico (propia) · {top.Value.Criterio} · último registro por tarifa", ultimos.Count,
            Advertencia: top.Value.VehiculoRelajado ? AdvertenciaVehiculo : null, TablaOrigen: "NEXO",
            VehiculoRelajado: top.Value.VehiculoRelajado);
    }

    /// <summary>
    /// Nivel exacto (las 5 dimensiones además de municipio): si deja 0 filas, nivel
    /// tolerante — cada filtro se aplica solo si, aplicado, deja al menos una fila (mismo
    /// patrón que <c>clsPropuestaTarifaAdapter.CandidatoSapAsync</c>). Descarta Valor &lt;= 0
    /// y toma el de mayor valor.
    /// </summary>
    private async Task<(clsFila Fila, decimal Valor, string Criterio, bool VehiculoRelajado)?> SeleccionarAsync(
        List<clsFila> filas, clsConsultaTarifaPlazaInterna c, int? pesoId, CancellationToken ct)
    {
        filas = filas.Where(f => f.Valor > 0).ToList();
        if (filas.Count == 0) return null;

        var (exactas, nivel, vehiculoRelajado) = NivelDeMatching(filas, c, pesoId, await TiposDeVehiculoAceptadosAsync(c.TipoVehiculo, ct));
        var top = exactas.OrderByDescending(f => f.Valor).FirstOrDefault();
        if (top is null) return null;

        return (top, top.Valor, nivel, vehiculoRelajado);
    }

    /// <summary>
    /// Los códigos con los que una fila de <c>tbl_tarifas_plaza</c> puede nombrar el vehículo de la línea: el de Plaza/SAP según el mapeo (el
    /// Sencillo C2 es 2; el Turbo 2L1/2L3 se tarifa como 2L2) y también el propio del vehículo, porque la carga inicial de esta tabla sacó el
    /// vehículo con códigos de SICE-TAC (3S2, 2L3) y todavía no se decidió unificarla. Es una ampliación: lo que antes coincidía sigue coincidiendo.
    /// </summary>
    private async Task<HashSet<string>?> TiposDeVehiculoAceptadosAsync(string? tipoVehiculo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo)) return null;

        var aceptados = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tipoVehiculo.Trim() };
        var plaza = await mapeo.PlazaAsync(tipoVehiculo, ct);
        if (plaza.Codigo is not null) aceptados.Add(plaza.Codigo);
        return aceptados;
    }

    private const string AdvertenciaVehiculo =
        "No hay tarifas del vehículo de la línea en esta ruta: el valor corresponde a otro vehículo y solo sirve de referencia.";

    internal static (List<clsFila> Filas, string Criterio, bool VehiculoRelajado) NivelDeMatching(
        List<clsFila> filas, clsConsultaTarifaPlazaInterna c, int? pesoId, IReadOnlySet<string>? tiposDeVehiculo = null)
    {
        bool LineaNegocioOk(clsFila f) => c.LineaNegocio is null || Normalizar(f.LineaNegocio) == Normalizar(c.LineaNegocio);
        bool ClaseViajeOk(clsFila f) => c.ClaseViaje is null || Normalizar(f.ClaseViaje) == Normalizar(c.ClaseViaje);
        bool ClaseContenedorOk(clsFila f) => c.ClaseContenedor is null || Normalizar(f.ClaseContenedor ?? "") == Normalizar(c.ClaseContenedor);
        bool TipoVehiculoOk(clsFila f) => c.TipoVehiculo is null
            || (tiposDeVehiculo is null ? f.TipoVehiculo == c.TipoVehiculo : f.TipoVehiculo is not null && tiposDeVehiculo.Contains(f.TipoVehiculo));
        bool PesoOk(clsFila f) => pesoId is null || f.PesoId == pesoId;

        // En una devolución de contenedor la clase de contenedor es obligatoria: una fila de otra clase no cuenta.
        if (c.ClaseContenedorEstricta && c.ClaseContenedor is not null)
        {
            filas = filas.Where(ClaseContenedorOk).ToList();
            if (filas.Count == 0) return (filas, "sin filas de la clase de contenedor de la línea", false);
        }

        var exactas = filas.Where(f => LineaNegocioOk(f) && ClaseViajeOk(f) && ClaseContenedorOk(f) && TipoVehiculoOk(f) && PesoOk(f)).ToList();
        if (exactas.Count > 0) return (exactas, "nivel exacto · municipios + línea de negocio + clase de viaje + clase de contenedor + vehículo + peso", false);

        // Tolerante: cada filtro se relaja si, aplicado sobre lo que queda, no deja nada.
        var candidatos = filas;
        var aplicados = new List<string>();
        void Relajar(Func<clsFila, bool> filtro, string nombre)
        {
            var f = candidatos.Where(filtro).ToList();
            if (f.Count > 0) { candidatos = f; aplicados.Add(nombre); }
        }
        Relajar(LineaNegocioOk, "línea de negocio");
        Relajar(ClaseViajeOk, "clase de viaje");
        Relajar(ClaseContenedorOk, "clase de contenedor");
        Relajar(TipoVehiculoOk, "vehículo");
        Relajar(PesoOk, "peso");

        var criterio = aplicados.Count > 0 ? $"nivel tolerante · municipios + {string.Join(" + ", aplicados)}" : "nivel tolerante · solo municipios";
        // El vehículo se pidió pero ninguna fila lo trae: el valor es de otro vehículo.
        var vehiculoRelajado = c.TipoVehiculo is not null && !aplicados.Contains("vehículo");
        return (candidatos, criterio, vehiculoRelajado);
    }

    private async Task<int?> PesoIdAsync(decimal? peso, CancellationToken ct)
    {
        if (peso is null) return null;
        return await db.RangosPeso.AsNoTracking()
            .Where(r => r.PesoMinimo <= peso && (r.PesoMaximo == null || peso <= r.PesoMaximo))
            .Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// S5-HU-19.1/#281: si la ruta ya trae <see cref="clsConsultaTarifaPlazaInterna.OrigenMunicipioId"/>/
    /// <see cref="clsConsultaTarifaPlazaInterna.DestinoMunicipioId"/> (S5-HU-16.1/#254),
    /// se usan DIRECTO — es el caso exacto, sin ambigüedad posible. Resolver por texto
    /// cuando ya hay un Id sería estrictamente peor: dos ciudades reales pueden calzar por
    /// contención con el mismo texto de ruta (confirmado al verificar esta issue contra
    /// clsPropuestaPlazaPropiaTests, que agrega un sufijo aleatorio al texto de la ruta —
    /// resolver por texto ahí cruzó con una fila de OTRO par de municipios).
    ///
    /// Solo sin Id resuelto cae a buscar el municipio cuyo nombre normalizado está
    /// contenido en el texto de la ruta, o al revés — misma regla de contención en las dos
    /// direcciones que <c>clsTarifarioSapAdapter.CodigosDaneAsync</c>. Puede devolver más
    /// de un Id (nombres cortos que calzan con varias filas); se usan todos como
    /// candidatos, nunca se adivina un único "mejor" municipio.
    /// </summary>
    private async Task<(IReadOnlyList<int> Origen, IReadOnlyList<int> Destino)> ResolverMunicipiosAsync(
        clsConsultaTarifaPlazaInterna consulta, CancellationToken ct)
    {
        // Atajo: con los dos Id ya resueltos no hace falta ni cargar el catálogo de
        // municipios en caché.
        if (consulta.OrigenMunicipioId is { } oId && consulta.DestinoMunicipioId is { } dId)
            return ([oId], [dId]);

        var municipios = await cache.GetOrCreateAsync("plaza-interna:municipios", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await db.Municipios.AsNoTracking()
                .Select(m => new { m.Id, m.Municipio })
                .ToListAsync(ct);
        });

        // S5-HU-19.1/#281: EXACTO primero, contención como último recurso. "BUENAVENTURA"
        // (texto limpio) matchea por contención, además de la fila correcta, "VENTURA"
        // (Cañar, Ecuador) y "SAN BUENAVENTURA" (Cotopaxi, Ecuador) — verificado contra la
        // base real: tbl_municipios cubre Colombia/Venezuela/Ecuador, y nombres cortos o
        // compuestos colisionan por contención con municipios de OTRO país. Un municipio
        // exacto nunca es ambiguo; se intenta primero y, si hay alguno, los de contención
        // ni se consideran.
        List<int> Coincide(string textoNorm)
        {
            var exactos = municipios!.Where(m => Normalizar(m.Municipio) == textoNorm).Select(m => m.Id).ToList();
            if (exactos.Count > 0) return exactos;

            return municipios!
                .Where(m => { var n = Normalizar(m.Municipio); return n.Length > 0 && (textoNorm.Contains(n) || n.Contains(textoNorm)); })
                .Select(m => m.Id)
                .ToList();
        }

        var origenIds = consulta.OrigenMunicipioId is { } idOrigen ? [idOrigen] : Coincide(Normalizar(consulta.Origen));
        var destinoIds = consulta.DestinoMunicipioId is { } idDestino ? [idDestino] : Coincide(Normalizar(consulta.Destino));
        return (origenIds, destinoIds);
    }

    private static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var limpio = string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return limpio.ToUpperInvariant();
    }
}
