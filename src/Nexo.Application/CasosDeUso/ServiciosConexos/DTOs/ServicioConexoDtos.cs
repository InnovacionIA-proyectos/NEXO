using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;

public record clsServicioConexoDto(
    Guid Id, string Nombre, string Categoria, string? ReglaCobro, decimal? ValorBase, DateOnly? VigenteHasta, bool Activo);

/// <summary>Página del catálogo de servicios: los ítems de la página y el total que cumple el filtro.</summary>
public record clsPaginaServiciosDto(IReadOnlyList<clsServicioConexoDto> Items, int Total);

/// <summary>Resultado de una carga masiva de servicios logísticos desde Excel.</summary>
public record clsImportarServiciosResultDto(int Filas, int Creadas, int Duplicadas, int Invalidas);
