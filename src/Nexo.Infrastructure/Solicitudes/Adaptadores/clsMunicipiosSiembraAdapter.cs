using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Construye el catálogo completo de municipios (S4-HU-09.1) leyendo tblSAPDANE +
/// tblZonasTarificacionRelacion + tblSAPTarifas/tblSAPTarifasLog del datacenter
/// corporativo. Sin caché a propósito — esto corre una sola vez, no repetidamente
/// como <see cref="clsProspectosDwAdapter"/>. Toda la transformación vive aquí (no en
/// Application) porque depende por completo del esquema real de esas tablas — ver
/// <see cref="clsResolucionMunicipios"/> para las reglas puras, ya verificadas contra
/// datos reales.
/// </summary>
public sealed class clsMunicipiosSiembraAdapter(IServiceProvider sp) : IMunicipiosSiembraPort
{
    private readonly clsDbSolucionesTiDbContext? _db = sp.GetService<clsDbSolucionesTiDbContext>();

    public bool Configurado => _db is not null;

    public async Task<IReadOnlyList<clsMunicipio>> ConstruirCatalogoAsync(CancellationToken ct)
    {
        if (_db is null) return [];

        var centrosPoblados = await _db.CentrosPobladosDane
            .Select(d => new { d.CodigoCentroPoblado, d.NombreMunicipio, d.NombreDepartamento, d.NombreMostrar })
            .ToListAsync(ct);

        var zonasOficiales = await _db.ZonasTarificacion
            .Select(z => new { z.IdZonaTransporte, z.IdZonaTarificacion })
            .ToListAsync(ct);

        var codigosZc = await ListarCodigosZcTarifarioAsync(ct);

        // Mapa código-zona-normalizado -> ZTC oficial (cruce con la relación oficial), y
        // mapa código-zona-original -> variantes normalizadas/expandidas (para el match
        // contra el nombre de municipio) — reutiliza exactamente la regla de
        // abreviaturas del motor real de costeo (clsResolucionZonaSap), no una nueva.
        var zonaOficialPorCodigoNormalizado = zonasOficiales
            .GroupBy(z => clsTexto.Normalizar(z.IdZonaTransporte))
            .ToDictionary(g => g.Key, g => g.First().IdZonaTarificacion);

        var variantesPorCodigoZc = codigosZc.ToDictionary(
            codigo => codigo,
            codigo => (IReadOnlyList<string>)clsResolucionZonaSap.ExpandirAbreviaturas(
                clsResolucionZonaSap.SinPrefijoZona(clsTexto.Normalizar(codigo))));

        var nuevos = new List<clsMunicipio>();

        foreach (var fila in centrosPoblados)
        {
            var pais = clsResolucionMunicipios.DetectarPais(fila.NombreMostrar);
            if (!clsResolucionMunicipios.EsMunicipioColombiano(fila.CodigoCentroPoblado, pais)) continue;

            string? nombreTarifasPlaza = null;
            string? zonaTarificacion = null;
            if (pais == "CO")
            {
                nombreTarifasPlaza = clsResolucionMunicipios.BuscarCodigoZc(clsTexto.Normalizar(fila.NombreMunicipio), variantesPorCodigoZc);
                if (nombreTarifasPlaza is not null)
                    zonaOficialPorCodigoNormalizado.TryGetValue(clsTexto.Normalizar(nombreTarifasPlaza), out zonaTarificacion);
            }

            nuevos.Add(new clsMunicipio
            {
                CodigoDane = fila.CodigoCentroPoblado,
                Municipio = clsTexto.Normalizar(fila.NombreMunicipio),
                Departamento = clsTexto.Normalizar(fila.NombreDepartamento),
                Pais = pais,
                NombreTarifasPlaza = nombreTarifasPlaza,
                ZonaTarificacion = zonaTarificacion,
            });
        }

        foreach (var (codigoDane, municipio, departamento, zonaZc) in clsResolucionMunicipios.ExcepcionesCorregimiento)
        {
            zonaOficialPorCodigoNormalizado.TryGetValue(clsTexto.Normalizar(zonaZc), out var zonaTarificacion);
            nuevos.Add(new clsMunicipio
            {
                CodigoDane = codigoDane,
                Municipio = clsTexto.Normalizar(municipio),
                Departamento = clsTexto.Normalizar(departamento),
                Pais = "CO",
                NombreTarifasPlaza = zonaZc,
                ZonaTarificacion = zonaTarificacion,
            });
        }

        zonaOficialPorCodigoNormalizado.TryGetValue(clsTexto.Normalizar(clsResolucionMunicipios.ZonaZcJerusalen), out var zonaJerusalen);
        nuevos.Add(new clsMunicipio
        {
            CodigoDane = clsResolucionMunicipios.CodigoDaneJerusalen,
            Municipio = clsTexto.Normalizar(clsResolucionMunicipios.MunicipioJerusalen),
            Departamento = clsTexto.Normalizar(clsResolucionMunicipios.DepartamentoSonson),
            Pais = "CO",
            NombreTarifasPlaza = clsResolucionMunicipios.ZonaZcJerusalen,
            ZonaTarificacion = zonaJerusalen,
        });

        return nuevos;
    }

    private async Task<IReadOnlyList<string>> ListarCodigosZcTarifarioAsync(CancellationToken ct)
    {
        if (_db is null) return [];

        // Solo ZC_ (ciudad puntual) — no ZTC_ (zona agregada), tal como pide la HU:
        // el cruce contra tblZonasTarificacionRelacion es lo que resuelve la zona
        // agregada a partir del ZC_, no al revés. "ZC" nunca es prefijo de "ZTC_..."
        // (el tercer carácter difiere: C vs T), así que StartsWith("ZC") no confunde
        // los dos.
        var deTarifas = _db.TarifasPlazaSap
            .Where(t => t.Origen.StartsWith("ZC") || t.Destino.StartsWith("ZC"))
            .Select(t => new { t.Origen, t.Destino });
        var deLog = _db.TarifasLogSap
            .Where(t => t.Origen.StartsWith("ZC") || t.Destino.StartsWith("ZC"))
            .Select(t => new { t.Origen, t.Destino });

        var codigos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fila in await deTarifas.ToListAsync(ct))
        {
            if (fila.Origen.StartsWith("ZC", StringComparison.Ordinal)) codigos.Add(fila.Origen);
            if (fila.Destino.StartsWith("ZC", StringComparison.Ordinal)) codigos.Add(fila.Destino);
        }
        foreach (var fila in await deLog.ToListAsync(ct))
        {
            if (fila.Origen.StartsWith("ZC", StringComparison.Ordinal)) codigos.Add(fila.Origen);
            if (fila.Destino.StartsWith("ZC", StringComparison.Ordinal)) codigos.Add(fila.Destino);
        }
        return [.. codigos];
    }
}
