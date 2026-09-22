using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Mappers;

public static class SolicitudDtoMappers
{
    public static LineaSolicitudDto ToDto(this LineaSolicitud l) =>
        new(l.Id, l.RutaId, l.ConfigVehiculoId, l.Tipologia, l.Peso, l.ValorMercancia, l.EstadoLinea, l.MotivoPendiente);

    public static SolicitudDto ToDto(this Solicitud s) =>
        new(s.Id, s.ClienteId, s.Cliente?.Nombre, s.Tipo, s.Modalidad, s.Deadline, s.ProbabilidadCierre,
            s.ValorEstimado, s.ScorePrioridad, s.Estado, s.CreatedAt, s.Lineas.Select(l => l.ToDto()).ToList());
}
