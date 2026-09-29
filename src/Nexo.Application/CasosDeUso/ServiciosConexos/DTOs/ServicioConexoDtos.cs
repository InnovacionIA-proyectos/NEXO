using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;

/// <summary>Reestructurado en la Issue #223 (S4-HU-15.1): tarifas variables por
/// ciudad/ruta/puerto en vez de un valor único genérico. Las dimensiones que no
/// aplican a este servicio vienen en <see cref="clsServicioConexo.NoAplica"/>.</summary>
public record clsServicioConexoDto(
    Guid Id, string CodigoServicio, string TipoServicio,
    string Origen, string Destino, string Puerto, string Ruta, string TipoMaquina,
    decimal CostoUnitario, string Unidad, DateOnly Vigencia, string Categoria, bool Activo);

/// <summary>Página del catálogo de servicios: los ítems de la página y el total que cumple el filtro.</summary>
public record clsPaginaServiciosDto(IReadOnlyList<clsServicioConexoDto> Items, int Total);

/// <summary>Resultado de una carga masiva de servicios logísticos desde Excel.</summary>
public record clsImportarServiciosResultDto(int Filas, int Creadas, int Duplicadas, int Invalidas);
