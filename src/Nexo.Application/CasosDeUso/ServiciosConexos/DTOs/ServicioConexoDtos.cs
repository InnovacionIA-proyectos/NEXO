using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;

/// <summary>Reestructurado en la Issue #223 (S4-HU-15.1): tarifas variables por
/// ciudad/ruta/puerto en vez de un valor único genérico. Las dimensiones que no
/// aplican a este servicio vienen en <see cref="clsServicioConexo.NoAplica"/>.</summary>
public record clsServicioConexoDto(
    Guid Id, string CodigoServicio, string TipoServicio,
    string Origen, string Destino, string Puerto, string Ruta, string TipoMaquina,
    decimal CostoUnitario, string Unidad, DateOnly Vigencia, string Categoria, bool Activo, decimal? PrecioVenta = null);

/// <summary>Página del catálogo de servicios: los ítems de la página y el total que cumple el filtro.</summary>
public record clsPaginaServiciosDto(IReadOnlyList<clsServicioConexoDto> Items, int Total);

/// <summary>Resultado de una carga masiva de servicios logísticos desde Excel. S4-HU-15.3/#249:
/// <see cref="Actualizadas"/> distingue el upsert (reimportar un export previo) de una creación
/// nueva; <see cref="Duplicadas"/> ahora solo cuenta las 6-dimensiones repetidas DENTRO del
/// mismo archivo (contra la BD ya no es "duplicada", es "actualizada"). S5-HU-19/#261:
/// <see cref="Errores"/> trae el detalle fila a fila de las filas inválidas (máx. 20, mismo
/// criterio que ImportarTarifasPlazaResultDto) — una fila inválida NUNCA descarta el archivo
/// completo, solo esa fila se cuenta como inválida y el resto sigue procesándose.</summary>
public record clsImportarServiciosResultDto(
    int Filas, int Creadas, int Actualizadas, int Duplicadas, int Invalidas, IReadOnlyList<string> Errores);

/// <summary>Archivo generado (export de catálogo o plantilla por tipo) listo para descargar.</summary>
public record clsArchivoDto(byte[] Contenido, string NombreArchivo, string ContentType);
