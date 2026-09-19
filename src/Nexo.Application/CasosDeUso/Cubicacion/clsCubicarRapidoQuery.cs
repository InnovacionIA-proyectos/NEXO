using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Cubicacion;

/// <summary>
/// [S3-HU-04.3, issue #78] POST /api/cubicacion — reemplaza el POST que <c>cubicar()</c>
/// (<c>captura-api.service.ts</c>) hacía directo contra Cubicador con la clave compartida.
/// Es Query (no Command) a propósito: "lectura, sin efectos secundarios" describe
/// exactamente este cálculo — ver <see cref="ICubicacionRapidaPort"/> para el porqué de la
/// excepción HTTP.
/// </summary>
public sealed record clsCubicarRapidoQuery(clsCubicacionRapidaSolicitud clsSolicitud) : IQuery<clsCubicacionRapidaResultado>;

public sealed class clsCubicarRapidoQueryHandler(ICubicacionRapidaPort cubicacion)
    : IQueryHandler<clsCubicarRapidoQuery, clsCubicacionRapidaResultado>
{
    public Task<clsCubicacionRapidaResultado> Handle(clsCubicarRapidoQuery query, CancellationToken ct = default)
        => cubicacion.EvaluarAsync(query.clsSolicitud, ct);
}
