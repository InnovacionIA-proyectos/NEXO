using System.Globalization;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos.Mappers;
using Nexo.Domain.Auditoria.Entidades;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.ServiciosConexos.Repositorios;

/// <summary>
/// Implementa el catálogo de servicios conexos, INCLUYENDO el parseo del Excel de carga
/// masiva (ClosedXML). El mapeo de columnas tolerante y las conversiones de celda son
/// detalle de acceso a datos entrelazado con el formato de archivo: no hay una regla de
/// negocio separable con provecho real de testabilidad, así que vive aquí completo
/// (mismo criterio pragmático usado en Biblioteca/ReglasSoloSicetac).
/// </summary>
public sealed class clsEfServiciosConexosRepository(clsNexoDbContext db) : IServiciosConexosRepository, IServiciosConexosPort
{
    /// <summary>
    /// Para Cotizaciones (clsAgregarConexosCotizacionCommand): lookup TRACKED a propósito
    /// (sin AsNoTracking). El resultado se asigna como navegación clsServicioConexo de un
    /// clsCotizacionConexo nuevo antes de SaveChanges; si viniera no-tracked, EF podría
    /// intentar reinsertar la fila del servicio en vez de solo usar su Id como FK
    /// (mismo comportamiento que el CotizacionService.AgregarConexosAsync original).
    /// </summary>
    public Task<clsServicioConexo?> ObtenerPorIdAsync(Guid id, CancellationToken ct) =>
        db.ServiciosConexos.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<clsPaginaServiciosDto> ListarAsync(
        bool soloActivos, string? q, string? categoria, int page, int pageSize, CancellationToken ct)
    {
        var query = db.ServiciosConexos.AsNoTracking();
        if (soloActivos) query = query.Where(s => s.Activo);
        if (!string.IsNullOrWhiteSpace(categoria))
        {
            var cat = clsServicioConexoCategoria.Normalizada(categoria);
            query = query.Where(s => s.Categoria == cat);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(s => EF.Functions.Like(s.TipoServicio, $"%{t}%") || EF.Functions.Like(s.CodigoServicio, $"%{t}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.TipoServicio).ThenBy(s => s.Origen)
            .Skip(Math.Max(0, (page - 1) * pageSize)).Take(pageSize)
            .ToListAsync(ct);
        return new clsPaginaServiciosDto(items.Select(s => s.ToDto()).ToList(), total);
    }

    public Task<bool> ExisteDimensionesAsync(
        string codigoServicio, string origen, string destino, string puerto, string ruta, string tipoMaquina,
        Guid? excluirId, CancellationToken ct) =>
        db.ServiciosConexos.AnyAsync(s =>
            s.CodigoServicio == codigoServicio && s.Origen == origen && s.Destino == destino &&
            s.Puerto == puerto && s.Ruta == ruta && s.TipoMaquina == tipoMaquina &&
            (excluirId == null || s.Id != excluirId), ct);

    public void Agregar(clsServicioConexo servicio)
    {
        db.ServiciosConexos.Add(servicio);
        db.AuditLogs.Add(new clsAuditLog
        {
            UsuarioId = servicio.ActualizadoPor, Entidad = "servicios_conexos", EntidadId = servicio.Id,
            Accion = "crear", Diff = $"{{\"codigoServicio\":\"{servicio.CodigoServicio}\",\"tipoServicio\":\"{servicio.TipoServicio}\"}}",
        });
    }

    public Task<clsServicioConexo?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.ServiciosConexos.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<clsImportarServiciosResultDto> ImportarExcelAsync(Stream archivo, Guid usuarioId, CancellationToken ct)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(archivo); }
        catch (Exception)
        {
            throw new Application.Common.Exceptions.clsValidationFailedException(
                "EXCEL_INVALIDO", "No se pudo leer el archivo. Debe ser un Excel .xlsx válido.");
        }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault(w => w.RangeUsed() is not null)
                ?? throw new Application.Common.Exceptions.clsValidationFailedException("EXCEL_SIN_DATOS", "El archivo no contiene datos.");
            var usado = ws.RangeUsed()!;
            var filas = usado.RowsUsed().ToList();
            static string Txt(IXLRangeRow r, int c) => c > 0 ? r.Cell(c).GetString().Trim() : "";

            // Mapeo de columnas por encabezado (S4-HU-15.1/#223: plantilla única con las 13
            // columnas de negocio; las dimensiones que no apliquen al servicio de una fila se
            // dejan vacías y se normalizan a "NO APLICA"). Si la primera fila no parece
            // encabezado, se asume Codigo | Tipo | Costo en las 3 primeras columnas.
            int cCodigo = 1, cTipo = 2, cCosto = 3, cOrigen = 0, cDestino = 0, cPuerto = 0, cRuta = 0,
                cMaquina = 0, cUnidad = 0, cVigencia = 0, cCategoria = 0, cActivo = 0, primeraDataIdx = 0;
            var cols = usado.ColumnCount();
            var enc = filas.First();
            string H(int c) => clsTexto.Normalizar(Txt(enc, c));
            bool pareceEncabezado = Enumerable.Range(1, cols).Select(H)
                .Any(h => h.Contains("CODIGO") || h.Contains("TIPO SERVICIO") || h.Contains("COSTO") || h.Contains("VIGEN"));
            if (pareceEncabezado)
            {
                for (var c = 1; c <= cols; c++)
                {
                    var h = H(c);
                    if (h.Contains("CODIGO")) cCodigo = c;
                    else if (h.Contains("TIPO SERVICIO") || h.Contains("SERVICIO")) cTipo = c;
                    else if (h.Contains("ORIGEN")) cOrigen = c;
                    else if (h.Contains("DESTINO")) cDestino = c;
                    else if (h.Contains("PUERTO")) cPuerto = c;
                    else if (h.Contains("RUTA")) cRuta = c;
                    else if (h.Contains("MAQUINA") || h.Contains("MÁQUINA")) cMaquina = c;
                    else if (h.Contains("COSTO") || h.Contains("VALOR")) cCosto = c;
                    else if (h.Contains("UNIDAD")) cUnidad = c;
                    else if (h.Contains("VIGEN")) cVigencia = c;
                    else if (h.Contains("CATEGOR")) cCategoria = c;
                    else if (h.Contains("ACTIVO") || h.Contains("ESTADO")) cActivo = c;
                }
                primeraDataIdx = 1;
            }

            // S4-HU-15.3/#249: upsert por las 6 dimensiones — permite exportar el catálogo y
            // reimportarlo para actualizar (en vez de que cada fila ya existente cuente como
            // "duplicada" y se descarte). Tracked a propósito (sin AsNoTracking) para poder
            // modificar las filas existentes in-place.
            var existentesPorClave = (await db.ServiciosConexos.ToListAsync(ct))
                .ToDictionary(s => $"{s.CodigoServicio}|{s.Origen}|{s.Destino}|{s.Puerto}|{s.Ruta}|{s.TipoMaquina}");

            // "Duplicada" ahora solo significa que la MISMA llave aparece más de una vez
            // DENTRO del archivo (la primera gana, el resto se cuenta y se descarta).
            var vistosEnArchivo = new HashSet<string>();

            int leidas = 0, creadas = 0, actualizadas = 0, duplicadas = 0, invalidas = 0;
            var nuevos = new List<clsServicioConexo>();

            foreach (var fila in filas.Skip(primeraDataIdx))
            {
                var codigo = Txt(fila, cCodigo);
                var tipo = Txt(fila, cTipo);
                var costoTxt = Txt(fila, cCosto);
                if (codigo.Length == 0 && tipo.Length == 0 && costoTxt.Length == 0) continue; // fila vacía
                leidas++;

                var costo = LeerDecimal(fila.Cell(cCosto));
                var unidad = cUnidad > 0 ? Txt(fila, cUnidad) : "";
                if (codigo.Length == 0 || tipo.Length == 0 || costo is null || unidad.Length == 0)
                { invalidas++; continue; }

                var origen = NoVacio(cOrigen > 0 ? Txt(fila, cOrigen) : null);
                var destino = NoVacio(cDestino > 0 ? Txt(fila, cDestino) : null);
                var puerto = NoVacio(cPuerto > 0 ? Txt(fila, cPuerto) : null);
                var ruta = NoVacio(cRuta > 0 ? Txt(fila, cRuta) : null);
                var maquina = NoVacio(cMaquina > 0 ? Txt(fila, cMaquina) : null);

                var clave = $"{codigo}|{origen}|{destino}|{puerto}|{ruta}|{maquina}";
                if (!vistosEnArchivo.Add(clave)) { duplicadas++; continue; }

                var categoria = cCategoria > 0 ? Txt(fila, cCategoria) : "";
                var activoTxt = cActivo > 0 ? clsTexto.Normalizar(Txt(fila, cActivo)) : "";
                var vigencia = cVigencia > 0 ? LeerFecha(fila.Cell(cVigencia)) : null;
                var activo = !(activoTxt is "NO" or "INACTIVO" or "FALSE" or "0"); // activo por defecto

                if (existentesPorClave.TryGetValue(clave, out var existente))
                {
                    existente.TipoServicio = tipo;
                    existente.CostoUnitario = costo.Value;
                    existente.Unidad = unidad;
                    if (vigencia is { } v) existente.Vigencia = v;
                    if (categoria.Length > 0) existente.Categoria = clsServicioConexoCategoria.Normalizada(categoria);
                    if (cActivo > 0) existente.Activo = activo;
                    existente.ActualizadoPor = usuarioId;
                    actualizadas++;
                    continue;
                }

                nuevos.Add(new clsServicioConexo
                {
                    CodigoServicio = codigo,
                    TipoServicio = tipo,
                    Origen = origen,
                    Destino = destino,
                    Puerto = puerto,
                    Ruta = ruta,
                    TipoMaquina = maquina,
                    CostoUnitario = costo.Value,
                    Unidad = unidad,
                    Vigencia = vigencia ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    Categoria = clsServicioConexoCategoria.Normalizada(categoria),
                    Activo = activo,
                    ActualizadoPor = usuarioId,
                });
                creadas++;
            }

            if (nuevos.Count > 0 || actualizadas > 0)
            {
                db.ServiciosConexos.AddRange(nuevos);
                db.AuditLogs.Add(new clsAuditLog
                {
                    UsuarioId = usuarioId, Entidad = "servicios_conexos", EntidadId = Guid.Empty,
                    Accion = "importar_excel", Diff = $"{{\"creadas\":{creadas},\"actualizadas\":{actualizadas}}}",
                });
                await db.SaveChangesAsync(ct);
            }
            return new clsImportarServiciosResultDto(leidas, creadas, actualizadas, duplicadas, invalidas);
        }
    }

    private static string NoVacio(string? dimension) =>
        string.IsNullOrWhiteSpace(dimension) ? clsServicioConexo.NoAplica : dimension.Trim();

    /// <summary>Lee un valor decimal de una celda: numérica nativa (valor exacto) o texto
    /// ("$ 780.077", "1.250.000,50", "1500.50"), vía <see cref="clsImporteParser"/>.</summary>
    private static decimal? LeerDecimal(IXLCell celda) =>
        celda.DataType == XLDataType.Number ? celda.GetValue<decimal>() : clsImporteParser.Parse(celda.GetString());

    /// <summary>Lee una fecha de una celda: fecha nativa de Excel o texto (dd/MM/yyyy, ISO).</summary>
    private static DateOnly? LeerFecha(IXLCell celda)
    {
        if (celda.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(celda.GetDateTime());
        var t = celda.GetString()?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        foreach (var fmt in new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" })
            if (DateTime.TryParseExact(t, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                return DateOnly.FromDateTime(f);
        return DateTime.TryParse(t, CultureInfo.GetCultureInfo("es-CO"), DateTimeStyles.None, out var g) ? DateOnly.FromDateTime(g) : null;
    }
}
