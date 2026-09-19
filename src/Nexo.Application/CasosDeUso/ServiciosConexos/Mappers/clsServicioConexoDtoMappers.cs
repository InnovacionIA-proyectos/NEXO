using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.Mappers;

public static class clsServicioConexoDtoMappers
{
    public static clsServicioConexoDto ToDto(this clsServicioConexo s) =>
        new(s.Id, s.Nombre, s.Categoria, s.ReglaCobro, s.ValorBase, s.VigenteHasta, s.Activo);
}
