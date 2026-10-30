using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Catalogos;

/// <summary>Catálogo de línea de negocio para el desplegable de Tarifas de Plaza (S4-HU-10.1).</summary>
public sealed record clsListarLineasNegocioQuery : IQuery<IReadOnlyList<clsLineaNegocioDto>>;

public sealed class clsListarLineasNegocioQueryHandler(ILineasNegocioPort lineasNegocio)
    : IQueryHandler<clsListarLineasNegocioQuery, IReadOnlyList<clsLineaNegocioDto>>
{
    public Task<IReadOnlyList<clsLineaNegocioDto>> Handle(clsListarLineasNegocioQuery query, CancellationToken ct = default) =>
        lineasNegocio.ListarAsync(ct);
}

/// <summary>Catálogo de clase de viaje para el desplegable de Tarifas de Plaza (S4-HU-10.1).</summary>
public sealed record clsListarClasesViajeQuery : IQuery<IReadOnlyList<clsClaseViajeDto>>;

public sealed class clsListarClasesViajeQueryHandler(IClasesViajePort clasesViaje)
    : IQueryHandler<clsListarClasesViajeQuery, IReadOnlyList<clsClaseViajeDto>>
{
    public Task<IReadOnlyList<clsClaseViajeDto>> Handle(clsListarClasesViajeQuery query, CancellationToken ct = default) =>
        clasesViaje.ListarAsync(ct);
}

/// <summary>Catálogo de clase de contenedor para el desplegable de Tarifas de Plaza (S4-HU-10.1).</summary>
public sealed record clsListarClasesContenedorQuery : IQuery<IReadOnlyList<clsClaseContenedorDto>>;

public sealed class clsListarClasesContenedorQueryHandler(IClasesContenedorPort clasesContenedor)
    : IQueryHandler<clsListarClasesContenedorQuery, IReadOnlyList<clsClaseContenedorDto>>
{
    public Task<IReadOnlyList<clsClaseContenedorDto>> Handle(clsListarClasesContenedorQuery query, CancellationToken ct = default) =>
        clasesContenedor.ListarAsync(ct);
}
