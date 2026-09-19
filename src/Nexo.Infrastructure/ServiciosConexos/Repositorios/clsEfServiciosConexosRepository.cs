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
            query = query.Where(s => EF.Functions.Like(s.Nombre, $"%{t}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.Nombre)
            .Skip(Math.Max(0, (page - 1) * pageSize)).Take(pageSize)
            .ToListAsync(ct);
        return new clsPaginaServiciosDto(items.Select(s => s.ToDto()).ToList(), total);
    }

    public Task<bool> ExisteNombreAsync(string nombre, CancellationToken ct) =>
        db.ServiciosConexos.AnyAsync(s => s.Nombre == nombre, ct);

    public void Agregar(clsServicioConexo servicio)
    {
        db.ServiciosConexos.Add(servicio);
        db.AuditLogs.Add(new clsAuditLog
        {
            UsuarioId = servicio.ActualizadoPor, Entidad = "servicios_conexos", EntidadId = servicio.Id,
            Accion = "crear", Diff = $"{{\"nombre\":\"{servicio.Nombre}\"}}",
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

            // Mapeo de columnas por encabezado. Si la primera fila no parece encabezado, se
            // asume Nombre | Valor base | Vigente hasta en las 3 primeras columnas.
            int cNombre = 1, cValor = 2, cVigente = 3, cCategoria = 0, cActivo = 0, primeraDataIdx = 0;
            var cols = usado.ColumnCount();
            var enc = filas.First();
            string H(int c) => clsTexto.Normalizar(Txt(enc, c));
            bool pareceEncabezado = Enumerable.Range(1, cols).Select(H)
                .Any(h => h.Contains("NOMBRE") || h.Contains("SERVICIO") || h.Contains("VALOR") || h.Contains("VIGEN") || h.Contains("CATEGOR") || h.Contains("ESTADO") || h.Contains("ACTIVO"));
            if (pareceEncabezado)
            {
                for (var c = 1; c <= cols; c++)
                {
                    var h = H(c);
                    if (h.Contains("NOMBRE") || h.Contains("SERVICIO")) cNombre = c;
                    else if (h.Contains("VALOR")) cValor = c;
                    else if (h.Contains("VIGEN")) cVigente = c;
                    else if (h.Contains("CATEGOR")) cCategoria = c;
                    else if (h.Contains("ACTIVO") || h.Contains("ESTADO")) cActivo = c;
                }
                primeraDataIdx = 1;
            }

            // Nombres ya existentes (para no duplicar; el HashSet también detecta repetidos en el archivo).
            var nombres = (await db.ServiciosConexos.AsNoTracking().Select(s => s.Nombre).ToListAsync(ct))
                .Select(n => n.Trim().ToUpperInvariant()).ToHashSet();

            int leidas = 0, creadas = 0, duplicadas = 0, invalidas = 0;
            var nuevos = new List<clsServicioConexo>();

            foreach (var fila in filas.Skip(primeraDataIdx))
            {
                var nombre = Txt(fila, cNombre);
                var valorTxt = Txt(fila, cValor);
                if (nombre.Length == 0 && valorTxt.Length == 0) continue; // fila vacía
                leidas++;
                if (nombre.Length == 0) { invalidas++; continue; }
                if (!nombres.Add(nombre.ToUpperInvariant())) { duplicadas++; continue; }

                var categoria = cCategoria > 0 ? Txt(fila, cCategoria) : "";
                var activoTxt = cActivo > 0 ? clsTexto.Normalizar(Txt(fila, cActivo)) : "";
                nuevos.Add(new clsServicioConexo
                {
                    Nombre = nombre,
                    Categoria = clsServicioConexoCategoria.Normalizada(categoria),
                    ValorBase = LeerDecimal(fila.Cell(cValor)),
                    VigenteHasta = cVigente > 0 ? LeerFecha(fila.Cell(cVigente)) : null,
                    // Activo por defecto; solo "no"/"inactivo"/"false"/"0" lo desactiva.
                    Activo = !(activoTxt is "NO" or "INACTIVO" or "FALSE" or "0"),
                    ActualizadoPor = usuarioId,
                });
                creadas++;
            }

            if (nuevos.Count > 0)
            {
                db.ServiciosConexos.AddRange(nuevos);
                db.AuditLogs.Add(new clsAuditLog
                {
                    UsuarioId = usuarioId, Entidad = "servicios_conexos", EntidadId = Guid.Empty,
                    Accion = "importar_excel", Diff = $"{{\"creadas\":{creadas}}}",
                });
                await db.SaveChangesAsync(ct);
            }
            return new clsImportarServiciosResultDto(leidas, creadas, duplicadas, invalidas);
        }
    }

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
