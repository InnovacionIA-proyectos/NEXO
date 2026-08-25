using CabinaFletes.Application.CasosDeUso.ServiciosConexos.DTOs;
using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.CasosDeUso.ServiciosConexos.Mappers;

public static class ServicioConexoDtoMappers
{
    public static ServicioConexoDto ToDto(this ServicioConexo s) =>
        new(s.Id, s.Nombre, s.Categoria, s.ReglaCobro, s.ValorBase, s.VigenteHasta, s.Activo);
}
