using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Dtos;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Mappers;

public static class clsSolicitudDtoMappers
{
    public static clsLineaSolicitudDto ToDto(this clsLineaSolicitud l) =>
        new(l.Id, l.RutaId, l.ConfigVehiculoId, l.Tipologia, l.Peso, l.ValorMercancia, l.EstadoLinea, l.MotivoPendiente);

    public static clsSolicitudDto ToDto(this clsSolicitud s) =>
        new(s.Id, s.ClienteId, s.clsCliente?.Nombre, s.Tipo, s.Modalidad, s.Deadline, s.ProbabilidadCierre,
            s.ValorEstimado, s.ScorePrioridad, s.Estado, s.CreatedAt, s.Lineas.Select(l => l.ToDto()).ToList(),
            clsCondicionesServicioDto.Desde(s.CondicionesServicio).Notas);
}
