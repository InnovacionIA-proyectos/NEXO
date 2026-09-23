using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Mappers;
using Nexo.Domain.Auditoria.Entidades;
using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;
using Nexo.Domain.Usuarios.Entidades;
using Nexo.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.TarifasSicetacAdmin.Repositorios;

/// <summary>
/// Caché local del piso SICE-TAC, incluyendo el import completo desde Excel (ClosedXML):
/// mapeo de columnas por encabezado, enlace a rutas locales por NOMBRERUTA (usando
/// Domain.Common.clsTexto), deduplicación dentro del mismo archivo y upsert contra la BD.
/// </summary>
public sealed class clsEfTarifasSicetacRepository(clsNexoDbContext db) : ITarifasSicetacRepository
{
    public async Task<IReadOnlyList<clsTarifaSicetacDto>> ConsultarAsync(clsSicetacFiltro f, CancellationToken ct)
    {
        var q = db.TarifasSicetac.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Origen)) q = q.Where(t => t.OrigenDane == f.Origen);
        if (!string.IsNullOrWhiteSpace(f.Destino)) q = q.Where(t => t.DestinoDane == f.Destino);
        if (!string.IsNullOrWhiteSpace(f.Config)) q = q.Where(t => t.Configuracion == f.Config);
        if (!string.IsNullOrWhiteSpace(f.TipoCarga)) q = q.Where(t => t.TipoCarga == f.TipoCarga);
        if (!string.IsNullOrWhiteSpace(f.Unidad)) q = q.Where(t => t.UnidadTransporte == f.Unidad);
        if (!string.IsNullOrWhiteSpace(f.Condicion) && Enum.TryParse<CondicionCarga>(f.Condicion, true, out var cond))
            q = q.Where(t => t.CondicionCarga == cond);
        if (f.Periodo is int p) q = q.Where(t => t.Periodo == p);

        var items = await q.OrderByDescending(t => t.Periodo).ThenBy(t => t.Configuracion).Take(200).ToListAsync(ct);
        return items.Select(t => t.ToDto()).ToList();
    }

    public async Task<clsImportResumenDto> ImportarExcelAsync(Stream excel, Guid usuarioId, CancellationToken ct)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(excel); }
        catch { throw new Application.Common.Exceptions.clsValidationFailedException("EXCEL_INVALIDO", "El archivo no es un Excel (.xlsx) válido."); }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault()
                ?? throw new Application.Common.Exceptions.clsValidationFailedException("EXCEL_VACIO", "El Excel no tiene hojas.");
            var header = ws.FirstRowUsed()
                ?? throw new Application.Common.Exceptions.clsValidationFailedException("EXCEL_VACIO", "El Excel no tiene encabezados.");

            var cols = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in header.CellsUsed()) cols[cell.GetString().Trim()] = cell.Address.ColumnNumber;

            foreach (var requerida in new[] { "PERIODO", "ORIGEN", "DESTINO", "CONFIGURACION", "NOMBRETIPOCARGA", "NOMBREUNIDADTRANSPORTE", "CONDICIONCARGA", "VALOR" })
                if (!cols.ContainsKey(requerida))
                    throw new Application.Common.Exceptions.clsValidationFailedException("EXCEL_COLUMNAS", $"Falta la columna obligatoria '{requerida}'.");

            // Catálogo de rutas locales en memoria: el piso se enlaza a la ruta (RutaId)
            // para que la Mesa de Costeo lo encuentre. Sin este enlace el piso importado
            // quedaba invisible (se buscaba por RutaId que nunca se asignaba).
            var rutasPorClave = (await db.Rutas.ToListAsync(ct))
                .GroupBy(r => clsTexto.ClaveRuta(r.Origen, r.Destino))
                .ToDictionary(g => g.Key, g => g.First());

            int insertadas = 0, actualizadas = 0, rechazadas = 0, sinRuta = 0, fila = 1;
            var errores = new List<string>();
            // Deduplicación dentro del MISMO Excel: la llave natural ya vista en esta
            // corrida (el ChangeTracker no la detecta con FirstOrDefaultAsync y el índice
            // único haría fallar todo el SaveChanges final por un solo duplicado).
            var pendientes = new Dictionary<string, clsTarifaSicetac>();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                fila++;
                try
                {
                    var periodo = (int)Num(row, cols, "PERIODO")!.Value;
                    var origen = Str(row, cols, "ORIGEN");
                    var destino = Str(row, cols, "DESTINO");
                    var config = Str(row, cols, "CONFIGURACION");
                    var tipo = Str(row, cols, "NOMBRETIPOCARGA");
                    var unidad = Str(row, cols, "NOMBREUNIDADTRANSPORTE");
                    var condStr = Str(row, cols, "CONDICIONCARGA");
                    var valor = Num(row, cols, "VALOR");

                    if (string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(destino) ||
                        string.IsNullOrWhiteSpace(config) || string.IsNullOrWhiteSpace(tipo) ||
                        string.IsNullOrWhiteSpace(unidad) || valor is null)
                        throw new InvalidOperationException("campos obligatorios vacíos");

                    var cond = condStr.StartsWith("vac", StringComparison.OrdinalIgnoreCase)
                        ? CondicionCarga.Vacio : CondicionCarga.Cargado;

                    var valorHora = Num(row, cols, "VALORHORA");
                    var horasPact = Num(row, cols, "HORASPACTADAS");
                    var horasRec = Num(row, cols, "HORASRECORRIDO");
                    var valorSiceHora = Num(row, cols, "VALORSICEHORA");
                    var nombreRuta = Str(row, cols, "NOMBRERUTA");
                    var viaEstandar = Str(row, cols, "VIAESTANDAR");

                    // Enlace a la ruta local por NOMBRERUTA ("ORIGEN - DESTINO"); solo se
                    // enlaza a rutas existentes (no se crean rutas huérfanas desde el import).
                    var rutaId = ResolverRutaId(nombreRuta, rutasPorClave);

                    var clave = $"{periodo}|{origen}|{destino}|{config}|{tipo}|{unidad}|{cond}";
                    if (pendientes.TryGetValue(clave, out var yaEnLote))
                    {
                        // Duplicado en el mismo archivo: la última fila gana, no cuenta doble.
                        AplicarValores(yaEnLote, valor.Value, valorHora, horasPact, horasRec, valorSiceHora, nombreRuta, rutaId, usuarioId);
                        continue;
                    }

                    var existente = await db.TarifasSicetac.FirstOrDefaultAsync(t =>
                        t.Periodo == periodo && t.OrigenDane == origen && t.DestinoDane == destino &&
                        t.Configuracion == config && t.TipoCarga == tipo &&
                        t.UnidadTransporte == unidad && t.CondicionCarga == cond, ct);

                    if (existente is null)
                    {
                        var nueva = new clsTarifaSicetac
                        {
                            Periodo = periodo, OrigenDane = origen, DestinoDane = destino,
                            RutaId = rutaId,
                            NombreRuta = string.IsNullOrWhiteSpace(nombreRuta) ? null : nombreRuta,
                            Configuracion = config, TipoCarga = tipo, UnidadTransporte = unidad,
                            CondicionCarga = cond, ValorPiso = valor.Value, ValorHora = valorHora,
                            HorasPactadas = horasPact, HorasRecorrido = horasRec, ValorSiceHora = valorSiceHora,
                            ViaEstandar = string.IsNullOrWhiteSpace(viaEstandar) ? null : viaEstandar.StartsWith("s", StringComparison.OrdinalIgnoreCase),
                            ImportadoPor = usuarioId,
                        };
                        db.TarifasSicetac.Add(nueva);
                        pendientes[clave] = nueva;
                        insertadas++;
                    }
                    else
                    {
                        AplicarValores(existente, valor.Value, valorHora, horasPact, horasRec, valorSiceHora, nombreRuta, rutaId, usuarioId);
                        pendientes[clave] = existente;
                        actualizadas++;
                    }

                    if (rutaId is null) sinRuta++;
                }
                catch (Exception ex)
                {
                    rechazadas++;
                    if (errores.Count < 20) errores.Add($"Fila {fila}: {ex.Message}");
                }
            }

            if (sinRuta > 0)
                errores.Add($"{sinRuta} piso(s) sin ruta local: no aparecerán en la Mesa de Costeo hasta que exista la ruta (capturada o creada).");

            var procesadas = insertadas + actualizadas + rechazadas;
            db.AuditLogs.Add(new clsAuditLog
            {
                UsuarioId = usuarioId,
                Entidad = "tarifas_sicetac",
                Accion = "importar",
                Diff = $"{{\"procesadas\":{procesadas},\"insertadas\":{insertadas},\"actualizadas\":{actualizadas},\"rechazadas\":{rechazadas},\"sinRuta\":{sinRuta}}}",
            });
            await db.SaveChangesAsync(ct);

            return new clsImportResumenDto(procesadas, insertadas, actualizadas, rechazadas, errores, sinRuta);
        }
    }

    public async Task<IReadOnlyList<clsImportacionDto>> ListarImportacionesAsync(CancellationToken ct)
    {
        return await db.AuditLogs.AsNoTracking()
            .Where(a => a.Entidad == "tarifas_sicetac" && a.Accion == "importar")
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new clsImportacionDto(a.Id, a.clsUsuario != null ? a.clsUsuario.Nombre : null, a.Diff, a.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Enlaza el piso a una ruta local EXISTENTE partiendo NOMBRERUTA ("ORIGEN - DESTINO").
    /// No crea rutas nuevas desde el import (evita rutas huérfanas que no cruzan con
    /// las líneas capturadas); devuelve null si no hay NOMBRERUTA partible o no existe.
    /// </summary>
    private static Guid? ResolverRutaId(string? nombreRuta, Dictionary<string, clsRuta> rutasPorClave)
    {
        if (clsTexto.PartirRuta(nombreRuta) is not { } partes) return null;
        return rutasPorClave.TryGetValue(clsTexto.ClaveRuta(partes.Origen, partes.Destino), out var r) ? r.Id : null;
    }

    /// <summary>Aplica los valores de una fila del Excel sobre la tarifa (insert o update).</summary>
    private static void AplicarValores(clsTarifaSicetac t, decimal valor, decimal? valorHora, decimal? horasPact,
        decimal? horasRec, decimal? valorSiceHora, string nombreRuta, Guid? rutaId, Guid usuarioId)
    {
        t.ValorPiso = valor;
        t.ValorHora = valorHora;
        t.HorasPactadas = horasPact;
        t.HorasRecorrido = horasRec;
        t.ValorSiceHora = valorSiceHora;
        if (!string.IsNullOrWhiteSpace(nombreRuta)) t.NombreRuta = nombreRuta;
        t.RutaId ??= rutaId; // completa el enlace si faltaba, sin pisar uno ya resuelto
        t.ImportadoPor = usuarioId;
    }

    // ----------------------------------------------------------------- helpers de lectura de celdas
    private static string Str(IXLRow row, Dictionary<string, int> cols, string name)
    {
        if (!cols.TryGetValue(name, out var c)) return "";
        var cell = row.Cell(c);
        return cell.DataType == XLDataType.Number ? ((long)cell.GetDouble()).ToString() : cell.GetString().Trim();
    }

    private static decimal? Num(IXLRow row, Dictionary<string, int> cols, string name)
    {
        if (!cols.TryGetValue(name, out var c)) return null;
        var cell = row.Cell(c);
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<decimal>(out var d)) return d;
        // clsTexto con formato heterogéneo (coma o punto decimal): parseo robusto y culture-safe.
        return clsImporteParser.Parse(cell.GetString());
    }
}
