using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Ports;

namespace Nexo.Application.CasosDeUso.Biblioteca;

/// <summary>Propuesta de tarifa (plaza vs SICE-TAC vs histórico) para cada línea de la solicitud.</summary>
public sealed record clsProponerPropuestaTarifaQuery(Guid SolicitudId) : IQuery<clsPropuestaSolicitudDto>;

public sealed class clsProponerPropuestaTarifaQueryHandler(IPropuestaTarifaPort propuestas)
    : IQueryHandler<clsProponerPropuestaTarifaQuery, clsPropuestaSolicitudDto>
{
    public Task<clsPropuestaSolicitudDto> Handle(clsProponerPropuestaTarifaQuery query, CancellationToken ct = default) =>
        propuestas.ProponerAsync(query.SolicitudId, ct);
}
