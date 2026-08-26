using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Application.Common.Ports;

namespace CabinaFletes.Application.CasosDeUso.Biblioteca;

/// <summary>Propuesta de tarifa (plaza vs SICE-TAC vs histórico) para cada línea de la solicitud.</summary>
public sealed record ProponerPropuestaTarifaQuery(Guid SolicitudId) : IQuery<PropuestaSolicitudDto>;

public sealed class ProponerPropuestaTarifaQueryHandler(IPropuestaTarifaPort propuestas)
    : IQueryHandler<ProponerPropuestaTarifaQuery, PropuestaSolicitudDto>
{
    public Task<PropuestaSolicitudDto> Handle(ProponerPropuestaTarifaQuery query, CancellationToken ct = default) =>
        propuestas.ProponerAsync(query.SolicitudId, ct);
}
