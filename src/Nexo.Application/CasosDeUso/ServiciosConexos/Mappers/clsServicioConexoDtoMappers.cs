using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.Mappers;

public static class clsServicioConexoDtoMappers
{
    public static clsServicioConexoDto ToDto(this clsServicioConexo s) => new(
        s.Id, s.CodigoServicio, s.TipoServicio,
        s.Origen, s.Destino, s.Puerto, s.Ruta, s.TipoMaquina,
        s.CostoUnitario, s.Unidad, s.Vigencia, s.Categoria, s.Activo);
}
