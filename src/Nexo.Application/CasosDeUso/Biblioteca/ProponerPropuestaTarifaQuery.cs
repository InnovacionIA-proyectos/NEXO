using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Ports;

namespace Nexo.Application.CasosDeUso.Biblioteca;

/// <summary>Propuesta de tarifa (plaza vs SICE-TAC vs histórico) para cada línea de la solicitud.</summary>
public sealed record ProponerPropuestaTarifaQuery(Guid SolicitudId) : IQuery<PropuestaSolicitudDto>;

public sealed class ProponerPropuestaTarifaQueryHandler(IPropuestaTarifaPort propuestas)
    : IQueryHandler<ProponerPropuestaTarifaQuery, PropuestaSolicitudDto>
{
    public Task<PropuestaSolicitudDto> Handle(ProponerPropuestaTarifaQuery query, CancellationToken ct = default) =>
        propuestas.ProponerAsync(query.SolicitudId, ct);
}
