using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Dtos;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Mappers;

public static class SolicitudDtoMappers
{
    public static LineaSolicitudDto ToDto(this LineaSolicitud l) =>
        new(l.Id, l.RutaId, l.ConfigVehiculoId, l.Tipologia, l.Peso, l.ValorMercancia, l.EstadoLinea, l.MotivoPendiente);

    public static SolicitudDto ToDto(this Solicitud s) =>
        new(s.Id, s.ClienteId, s.Cliente?.Nombre, s.Tipo, s.Modalidad, s.Deadline, s.ProbabilidadCierre,
            s.ValorEstimado, s.ScorePrioridad, s.Estado, s.CreatedAt, s.Lineas.Select(l => l.ToDto()).ToList());
}
