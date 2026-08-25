using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.CasosDeUso.ServiciosConexos.DTOs;

public record ServicioConexoDto(
    Guid Id, string Nombre, string Categoria, string? ReglaCobro, decimal? ValorBase, DateOnly? VigenteHasta, bool Activo);

public static class ServicioConexoDtoMappers
{
    public static ServicioConexoDto ToDto(this ServicioConexo s) =>
        new(s.Id, s.Nombre, s.Categoria, s.ReglaCobro, s.ValorBase, s.VigenteHasta, s.Activo);
}

/// <summary>Página del catálogo de servicios: los ítems de la página y el total que cumple el filtro.</summary>
public record PaginaServiciosDto(IReadOnlyList<ServicioConexoDto> Items, int Total);

/// <summary>Resultado de una carga masiva de servicios logísticos desde Excel.</summary>
public record ImportarServiciosResultDto(int Filas, int Creadas, int Duplicadas, int Invalidas);
