using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Auditoria.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.TarifasPlaza.Repositorios;

public sealed class clsEfTarifasPlazaRepository(clsNexoDbContext db, ILineasNegocioPort lineasNegocio, IClasesViajePort clasesViaje)
    : ITarifasPlazaRepository
{
    private const int TopeErrores = 20;

    public async Task<IReadOnlyList<clsTarifaPlaza>> ListarAsync(CancellationToken ct) =>
        await db.TarifasPlaza.AsNoTracking().Include(t => t.Origen).Include(t => t.Destino).Include(t => t.Peso).ToListAsync(ct);

    public Task<clsTarifaPlaza?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.TarifasPlaza.Include(t => t.Origen).Include(t => t.Destino).Include(t => t.Peso).FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> ExisteAsync(
        string lineaNegocio, string claseViaje, int origenMunicipioId, int destinoMunicipioId,
        string? claseContenedor, int? pesoId, string? tipoVehiculo, Guid? excluirId, CancellationToken ct) =>
        db.TarifasPlaza.AnyAsync(t =>
            t.LineaNegocio == lineaNegocio && t.ClaseViaje == claseViaje &&
            t.OrigenMunicipioId == origenMunicipioId && t.DestinoMunicipioId == destinoMunicipioId &&
            t.ClaseContenedor == claseContenedor && t.PesoId == pesoId && t.TipoVehiculo == tipoVehiculo &&
            (excluirId == null || t.Id != excluirId), ct);

    public void Agregar(clsTarifaPlaza tarifa) => db.TarifasPlaza.Add(tarifa);

    public async Task<int> EliminarAsync(Guid id, CancellationToken ct) =>
        await db.TarifasPlaza.Where(t => t.Id == id).ExecuteDeleteAsync(ct);

    public async Task<clsImportarTarifasPlazaResultDto> ImportarExcelAsync(Stream excel, Guid? usuarioId, CancellationToken ct)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(excel); }
        catch { throw new clsValidationFailedException("EXCEL_INVALIDO", "El archivo no es un Excel (.xlsx) válido."); }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault()
                ?? throw new clsValidationFailedException("EXCEL_VACIO", "El Excel no tiene hojas.");
            var header = ws.FirstRowUsed()
                ?? throw new clsValidationFailedException("EXCEL_VACIO", "El Excel no tiene encabezados.");

            var cols = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in header.CellsUsed()) cols[cell.GetString().Trim()] = cell.Address.ColumnNumber;

            foreach (var requerida in new[] { "LINEA DE NEGOCIO", "CLASE DE VIAJE", "ORIGEN", "DESTINO", "VALOR", "VIGENCIA", "DETALLES" })
                if (!cols.ContainsKey(requerida))
                    throw new clsValidationFailedException("EXCEL_COLUMNAS", $"Falta la columna obligatoria '{requerida}'.");

            // Catálogos en memoria antes de procesar filas: municipios por nombre normalizado
            // (con Colombia como desempate si el nombre choca entre países — las tarifas de
            // plaza reales son todas de origen colombiano, ver docs/Database/seeder/README.md),
            // rangos de peso por su formato de texto ("3-15000" / "10500+"), línea de negocio y
            // clase de viaje por descripción normalizada.
            var municipiosPorNombre = (await db.Municipios.AsNoTracking().ToListAsync(ct))
                .GroupBy(m => clsTexto.Normalizar(m.Municipio))
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Pais == "CO" ? 0 : 1).First());
            var rangosPorTexto = (await db.RangosPeso.AsNoTracking().ToListAsync(ct))
                .ToDictionary(FormatoRango, r => r, StringComparer.OrdinalIgnoreCase);
            var lineasNegocioValidas = (await lineasNegocio.ListarAsync(ct))
                .ToDictionary(l => clsTexto.Normalizar(l.Descripcion), l => l.Descripcion);
            var clasesViajeValidas = (await clasesViaje.ListarAsync(ct))
                .ToDictionary(c => clsTexto.Normalizar(c.Descripcion), c => c.Descripcion);

            int insertadas = 0, actualizadas = 0, rechazadas = 0, fila = 1;
            var errores = new List<string>();
            // Deduplicación DENTRO del mismo Excel: la última fila con la misma llave gana,
            // no cuenta doble (mismo criterio que clsEfTarifasSicetacRepository).
            var pendientes = new Dictionary<string, clsTarifaPlaza>();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                fila++;
                try
                {
                    var lineaTxt = Str(row, cols, "LINEA DE NEGOCIO");
                    var claseTxt = Str(row, cols, "CLASE DE VIAJE");
                    var origenTxt = Str(row, cols, "ORIGEN");
                    var destinoTxt = Str(row, cols, "DESTINO");
                    var claseContenedor = ColOpcional(cols, "CLASE CONTENEDOR") is { } cc ? Str(row, cols, cc) : null;
                    var rangoTxt = ColOpcional(cols, "RANGO PESO") is { } rp ? Str(row, cols, rp) : null;
                    var tipoVehiculo = ColOpcional(cols, "TIPO VEHICULO") is { } tv ? Str(row, cols, tv) : null;
                    var valor = Num(row, cols, "VALOR");
                    var vigenciaTxt = Str(row, cols, "VIGENCIA");
                    var detalles = Str(row, cols, "DETALLES");

                    if (string.IsNullOrWhiteSpace(lineaTxt) && string.IsNullOrWhiteSpace(origenTxt) && valor is null)
                        continue; // fila vacía: se ignora

                    if (string.IsNullOrWhiteSpace(lineaTxt) || string.IsNullOrWhiteSpace(claseTxt)
                        || string.IsNullOrWhiteSpace(origenTxt) || string.IsNullOrWhiteSpace(destinoTxt)
                        || valor is null || string.IsNullOrWhiteSpace(vigenciaTxt) || string.IsNullOrWhiteSpace(detalles))
                        throw new InvalidOperationException("campos obligatorios vacíos");

                    if (!lineasNegocioValidas.TryGetValue(clsTexto.Normalizar(lineaTxt), out var lineaNegocioCanonica))
                        throw new InvalidOperationException($"'{lineaTxt}' no es una línea de negocio del catálogo");
                    if (!clasesViajeValidas.TryGetValue(clsTexto.Normalizar(claseTxt), out var claseViajeCanonica))
                        throw new InvalidOperationException($"'{claseTxt}' no es una clase de viaje del catálogo");
                    if (!municipiosPorNombre.TryGetValue(clsTexto.Normalizar(origenTxt), out var origen))
                        throw new InvalidOperationException($"'{origenTxt}' no es un municipio del catálogo");
                    if (!municipiosPorNombre.TryGetValue(clsTexto.Normalizar(destinoTxt), out var destino))
                        throw new InvalidOperationException($"'{destinoTxt}' no es un municipio del catálogo");

                    clsRangoPeso? rango = null;
                    if (!string.IsNullOrWhiteSpace(rangoTxt) && !rangosPorTexto.TryGetValue(rangoTxt.Trim(), out rango))
                        throw new InvalidOperationException($"'{rangoTxt}' no es un rango de peso del catálogo");

                    var tipoVehiculoLimpio = string.IsNullOrWhiteSpace(tipoVehiculo) ? null : tipoVehiculo.Trim();
                    var claseContenedorLimpia = string.IsNullOrWhiteSpace(claseContenedor) ? null : claseContenedor.Trim();

                    // Verificado contra datos reales: las filas de contenedor usan PesoId
                    // Y TipoVehiculo a la vez; las nacionales usan solo TipoVehiculo. Solo
                    // es inválido que falten las dos.
                    if (rango is null && string.IsNullOrWhiteSpace(tipoVehiculoLimpio))
                        throw new InvalidOperationException("indica al menos rango de peso o tipo de vehículo");

                    if (!DateOnly.TryParse(vigenciaTxt, out var vigencia))
                        throw new InvalidOperationException($"'{vigenciaTxt}' no es una fecha de vigencia válida");

                    var clave = $"{lineaNegocioCanonica}|{claseViajeCanonica}|{origen.Id}|{destino.Id}|{claseContenedorLimpia}|{rango?.Id}|{tipoVehiculoLimpio}";
                    if (pendientes.TryGetValue(clave, out var yaEnLote))
                    {
                        AplicarValores(yaEnLote, valor.Value, vigencia, detalles);
                        continue;
                    }

                    int? pesoIdBuscado = rango?.Id;
                    var existenteDb = await db.TarifasPlaza.FirstOrDefaultAsync(t =>
                        t.LineaNegocio == lineaNegocioCanonica && t.ClaseViaje == claseViajeCanonica &&
                        t.OrigenMunicipioId == origen.Id && t.DestinoMunicipioId == destino.Id &&
                        t.ClaseContenedor == claseContenedorLimpia && t.PesoId == pesoIdBuscado && t.TipoVehiculo == tipoVehiculoLimpio, ct);

                    if (existenteDb is null)
                    {
                        var nueva = new clsTarifaPlaza
                        {
                            LineaNegocio = lineaNegocioCanonica, ClaseViaje = claseViajeCanonica,
                            OrigenMunicipioId = origen.Id, DestinoMunicipioId = destino.Id,
                            ClaseContenedor = claseContenedorLimpia, PesoId = rango?.Id, TipoVehiculo = tipoVehiculoLimpio,
                            Valor = valor.Value, Vigencia = vigencia, DetallesTarifa = detalles.Trim(),
                            CreadoPor = usuarioId,
                        };
                        db.TarifasPlaza.Add(nueva);
                        pendientes[clave] = nueva;
                        insertadas++;
                    }
                    else
                    {
                        AplicarValores(existenteDb, valor.Value, vigencia, detalles);
                        pendientes[clave] = existenteDb;
                        actualizadas++;
                    }
                }
                catch (Exception ex)
                {
                    rechazadas++;
                    if (errores.Count < TopeErrores) errores.Add($"Fila {fila}: {ex.Message}.");
                }
            }

            var procesadas = insertadas + actualizadas + rechazadas;
            if (insertadas + actualizadas > 0)
            {
                db.AuditLogs.Add(new clsAuditLog
                {
                    UsuarioId = usuarioId,
                    Entidad = "tarifas_plaza",
                    Accion = "importar",
                    Diff = $"{{\"procesadas\":{procesadas},\"insertadas\":{insertadas},\"actualizadas\":{actualizadas},\"rechazadas\":{rechazadas}}}",
                });
                await db.SaveChangesAsync(ct);
            }

            return new clsImportarTarifasPlazaResultDto(procesadas, insertadas, actualizadas, rechazadas, errores);
        }
    }

    private static void AplicarValores(clsTarifaPlaza tarifa, decimal valor, DateOnly vigencia, string detalles)
    {
        tarifa.Valor = valor;
        tarifa.Vigencia = vigencia;
        tarifa.DetallesTarifa = detalles.Trim();
    }

    /// <summary>Mismo formato que <c>clsTarifaPlazaMapper.FormatoRango</c> en Application.</summary>
    private static string FormatoRango(clsRangoPeso p) => p.PesoMaximo is { } max ? $"{p.PesoMinimo}-{max}" : $"{p.PesoMinimo}+";

    private static string? ColOpcional(Dictionary<string, int> cols, string nombre) => cols.ContainsKey(nombre) ? nombre : null;
    private static string Str(IXLRow row, Dictionary<string, int> cols, string col) => row.Cell(cols[col]).GetString().Trim();

    /// <summary>Mismo criterio que clsEfTarifasSicetacRepository (histórico, ver git):
    /// primero el tipo numérico nativo de la celda, con fallback a clsImporteParser para
    /// texto con formato heterogéneo (coma o punto decimal).</summary>
    private static decimal? Num(IXLRow row, Dictionary<string, int> cols, string col)
    {
        var celda = row.Cell(cols[col]);
        if (celda.IsEmpty()) return null;
        if (celda.TryGetValue<decimal>(out var v)) return v;
        return clsImporteParser.Parse(celda.GetString());
    }
}
