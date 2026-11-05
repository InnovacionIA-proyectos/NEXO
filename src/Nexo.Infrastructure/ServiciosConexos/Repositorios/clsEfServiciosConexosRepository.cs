using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos.Mappers;
using Nexo.Domain.Auditoria.Entidades;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Infrastructure.Common;
using Nexo.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.ServiciosConexos.Repositorios;

/// <summary>Catálogo de servicios conexos; incluye el parseo del Excel de carga masiva (ClosedXML).</summary>
public sealed class clsEfServiciosConexosRepository(clsNexoDbContext db) : IServiciosConexosRepository, IServiciosConexosPort
{
    /// <summary>Lookup TRACKED a propósito: se asigna como navegación a un clsCotizacionConexo nuevo;
    /// no-tracked, EF intentaría reinsertar la fila del servicio.</summary>
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
            // Busca también por las dimensiones del servicio, no solo por tipo/código.
            var t = q.Trim();
            query = query.Where(s =>
                EF.Functions.Like(s.TipoServicio, $"%{t}%") || EF.Functions.Like(s.CodigoServicio, $"%{t}%") ||
                EF.Functions.Like(s.Origen, $"%{t}%") || EF.Functions.Like(s.Destino, $"%{t}%") ||
                EF.Functions.Like(s.Puerto, $"%{t}%") || EF.Functions.Like(s.Ruta, $"%{t}%"));
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
            Accion = "crear", Diff = System.Text.Json.JsonSerializer.Serialize(new
            {
                codigoServicio = servicio.CodigoServicio, tipoServicio = servicio.TipoServicio, precioVenta = servicio.PrecioVenta,
            }),
        });
    }

    public void RegistrarCambioDePrecioVenta(clsServicioConexo servicio, decimal? precioVentaAntes) =>
        db.AuditLogs.Add(new clsAuditLog
        {
            UsuarioId = servicio.ActualizadoPor, Entidad = "servicios_conexos", EntidadId = servicio.Id,
            Accion = "editar", Diff = System.Text.Json.JsonSerializer.Serialize(new { precioVenta = new { antes = precioVentaAntes, despues = servicio.PrecioVenta } }),
        });

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

            // Mapeo de columnas por encabezado; dimensiones vacías se normalizan a "NO APLICA".
            // Si la primera fila no parece encabezado, se asume Codigo | Tipo | Costo en las 3 primeras columnas.
            int cCodigo = 1, cTipo = 2, cCosto = 3, cOrigen = 0, cDestino = 0, cPuerto = 0, cRuta = 0,
                cMaquina = 0, cUnidad = 0, cVigencia = 0, cCategoria = 0, cActivo = 0, cPrecioVenta = 0, primeraDataIdx = 0;
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
                    // La columna de venta se busca PRIMERO: «VALOR VENTA» o «PRECIO VENTA» no son el costo aunque contengan «VALOR».
                    if (h.Contains("VENTA")) cPrecioVenta = c;
                    else if (h.Contains("CODIGO")) cCodigo = c;
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

            // Upsert por las 6 dimensiones, tracked a propósito para modificar in-place. La llave se compara sin mayúsculas y con
            // normalización Unicode: la colación de UQ_servicios_conexos_dimensiones es CI y iguala NFC/NFD; el Dictionary ordinal no.
            var existentesPorClave = (await db.ServiciosConexos.ToListAsync(ct))
                .ToDictionary(s => ClaveNormalizada(s.CodigoServicio, s.Origen, s.Destino, s.Puerto, s.Ruta, s.TipoMaquina));

            // "Duplicada" = la misma llave más de una vez dentro del archivo (la primera gana).
            var vistosEnArchivo = new HashSet<string>();

            int leidas = 0, creadas = 0, actualizadas = 0, duplicadas = 0, invalidas = 0;
            var nuevos = new List<clsServicioConexo>();
            var errores = new List<string>();
            var fila0 = primeraDataIdx; // la fila de Excel real (1-based + encabezado) para el mensaje

            foreach (var fila in filas.Skip(primeraDataIdx))
            {
                fila0++;
                var codigo = Txt(fila, cCodigo);
                var tipo = Txt(fila, cTipo);
                var costoTxt = Txt(fila, cCosto);
                if (codigo.Length == 0 && tipo.Length == 0 && costoTxt.Length == 0) continue;
                leidas++;

                // Valida longitud/rango antes de guardar, con detalle por fila: una fila inválida se cuenta y no descarta las demás.
                var costo = LeerDecimal(fila.Cell(cCosto));
                var unidad = cUnidad > 0 ? Txt(fila, cUnidad) : "";
                // Sin columna o con la celda vacía no hay precio de venta que leer: no se toca el que ya tenga el servicio.
                var precioVentaTxt = cPrecioVenta > 0 ? Txt(fila, cPrecioVenta) : "";
                var precioVenta = precioVentaTxt.Length > 0 ? LeerDecimal(fila.Cell(cPrecioVenta)) : null;
                string? motivoInvalido =
                    codigo.Length == 0 ? "CODIGO vacío"
                    : tipo.Length == 0 ? "TIPO SERVICIO vacío"
                    : costo is null ? "COSTO/VALOR vacío o ilegible"
                    : unidad.Length == 0 ? "UNIDAD vacía"
                    : codigo.Length > 30 ? $"CODIGO de {codigo.Length} caracteres (máx. 30)"
                    : tipo.Length > 120 ? $"TIPO SERVICIO de {tipo.Length} caracteres (máx. 120)"
                    : unidad.Length > 30 ? $"UNIDAD de {unidad.Length} caracteres (máx. 30)"
                    : costo < 0 ? "COSTO/VALOR negativo"
                    : costo >= 10_000_000_000_000_000m ? "COSTO/VALOR excede el máximo permitido (16 dígitos enteros)"
                    : precioVentaTxt.Length > 0 && precioVenta is null ? "PRECIO VENTA ilegible"
                    : precioVenta < 0 ? "PRECIO VENTA negativo"
                    : precioVenta >= 10_000_000_000_000_000m ? "PRECIO VENTA excede el máximo permitido (16 dígitos enteros)"
                    : null;
                if (motivoInvalido is not null)
                {
                    invalidas++;
                    if (errores.Count < 20) errores.Add($"Fila {fila0}: {motivoInvalido}.");
                    continue;
                }

                var origen = NoVacio(cOrigen > 0 ? Txt(fila, cOrigen) : null, "ORIGEN", 150, fila0, errores, ref invalidas);
                var destino = NoVacio(cDestino > 0 ? Txt(fila, cDestino) : null, "DESTINO", 150, fila0, errores, ref invalidas);
                var puerto = NoVacio(cPuerto > 0 ? Txt(fila, cPuerto) : null, "PUERTO", 50, fila0, errores, ref invalidas);
                var ruta = NoVacio(cRuta > 0 ? Txt(fila, cRuta) : null, "RUTA", 50, fila0, errores, ref invalidas);
                var maquina = NoVacio(cMaquina > 0 ? Txt(fila, cMaquina) : null, "TIPO MAQUINA", 60, fila0, errores, ref invalidas);
                if (origen is null || destino is null || puerto is null || ruta is null || maquina is null) continue;

                var clave = ClaveNormalizada(codigo, origen, destino, puerto, ruta, maquina);
                if (!vistosEnArchivo.Add(clave)) { duplicadas++; continue; }

                var categoria = cCategoria > 0 ? Txt(fila, cCategoria) : "";
                var activoTxt = cActivo > 0 ? clsTexto.Normalizar(Txt(fila, cActivo)) : "";
                var vigencia = cVigencia > 0 ? LeerFecha(fila.Cell(cVigencia)) : null;
                var activo = !(activoTxt is "NO" or "INACTIVO" or "FALSE" or "0"); // activo por defecto

                if (existentesPorClave.TryGetValue(clave, out var existente))
                {
                    existente.TipoServicio = tipo;
                    existente.CostoUnitario = costo!.Value;
                    if (precioVenta is { } pv) existente.PrecioVenta = Math.Round(pv, 2, MidpointRounding.AwayFromZero);
                    existente.Unidad = unidad;
                    if (vigencia is { } v) existente.Vigencia = v;
                    if (categoria.Length > 0) existente.Categoria = clsServicioConexoCategoria.Normalizada(categoria);
                    // Una celda ACTIVO vacía no toca el estado existente: solo un valor explícito lo cambia.
                    if (cActivo > 0 && activoTxt.Length > 0) existente.Activo = activo;
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
                    CostoUnitario = costo!.Value,
                    PrecioVenta = precioVenta is { } nuevoPv ? Math.Round(nuevoPv, 2, MidpointRounding.AwayFromZero) : null,
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
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Red de seguridad: si la base rechaza el lote (p. ej. carga concurrente de la misma fila nueva),
                    // se responde con un mensaje de negocio, nunca con el texto crudo de EF.
                    throw new Application.Common.Exceptions.clsValidationFailedException(
                        "SERVICIO_DUPLICADO", "Una o más filas del archivo ya existen con esas mismas dimensiones (código, origen, destino, puerto, ruta, tipo de máquina).");
                }
            }
            return new clsImportarServiciosResultDto(leidas, creadas, actualizadas, duplicadas, invalidas, errores);
        }
    }

    /// <summary>Cuenta la fila como inválida y devuelve null si el valor excede el máximo de la columna.</summary>
    private static string? NoVacio(string? dimension, string columna, int maxLength, int fila, List<string> errores, ref int invalidas)
    {
        var limpio = string.IsNullOrWhiteSpace(dimension) ? clsServicioConexo.NoAplica : dimension.Trim();
        if (limpio.Length <= maxLength) return limpio;
        invalidas++;
        if (errores.Count < 20) errores.Add($"Fila {fila}: {columna} de {limpio.Length} caracteres (máx. {maxLength}).");
        return null;
    }

    /// <summary>Llave de las 6 dimensiones normalizada para coincidir con la colación de la base: insensible a
    /// mayúsculas y a NFC/NFD, pero NO quita tildes (la colación distingue acentos).</summary>
    private static string ClaveNormalizada(string codigo, string origen, string destino, string puerto, string ruta, string maquina) =>
        string.Join('|', new[] { codigo, origen, destino, puerto, ruta, maquina }
            .Select(v => v.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant()));

    /// <summary>Acepta numérica nativa o texto ("$ 780.077", "1.250.000,50").</summary>
    private static decimal? LeerDecimal(IXLCell celda) =>
        celda.DataType == XLDataType.Number ? celda.GetValue<decimal>() : clsImporteParser.Parse(celda.GetString());

    private static DateOnly? LeerFecha(IXLCell celda) => clsLectorFechasExcel.Leer(celda);
}
