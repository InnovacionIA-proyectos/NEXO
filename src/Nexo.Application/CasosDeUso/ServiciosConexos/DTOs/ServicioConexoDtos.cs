using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;

/// <summary>Las dimensiones que no aplican a este servicio vienen en <see cref="clsServicioConexo.NoAplica"/>.</summary>
public record clsServicioConexoDto(
    Guid Id, string CodigoServicio, string TipoServicio,
    string Origen, string Destino, string Puerto, string Ruta, string TipoMaquina,
    decimal CostoUnitario, string Unidad, DateOnly Vigencia, string Categoria, bool Activo, decimal? PrecioVenta = null);

/// <summary>Página del catálogo de servicios: los ítems de la página y el total que cumple el filtro.</summary>
public record clsPaginaServiciosDto(IReadOnlyList<clsServicioConexoDto> Items, int Total);

/// <summary>
/// Resultado de una carga masiva desde Excel. <see cref="Actualizadas"/> distingue el upsert de una creación nueva;
/// <see cref="Duplicadas"/> cuenta solo las 6-dimensiones repetidas DENTRO del archivo; <see cref="Errores"/> trae el detalle
/// por fila inválida (máx. 20) y una fila inválida nunca descarta el archivo.
/// </summary>
public record clsImportarServiciosResultDto(
    int Filas, int Creadas, int Actualizadas, int Duplicadas, int Invalidas, IReadOnlyList<string> Errores);

/// <summary>Archivo generado (export de catálogo o plantilla por tipo) listo para descargar.</summary>
public record clsArchivoDto(byte[] Contenido, string NombreArchivo, string ContentType);
