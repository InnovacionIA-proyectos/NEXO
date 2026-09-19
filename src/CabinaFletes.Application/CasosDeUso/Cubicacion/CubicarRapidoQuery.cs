using CabinaFletes.Application.CasosDeUso.Cubicacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Cubicacion.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Cubicacion;

/// <summary>
/// [S3-HU-04.3, issue #78] POST /api/cubicacion — reemplaza el POST que <c>cubicar()</c>
/// (<c>captura-api.service.ts</c>) hacía directo contra Cubicador con la clave compartida.
/// Es Query (no Command) a propósito: "lectura, sin efectos secundarios" describe
/// exactamente este cálculo — ver <see cref="ICubicacionRapidaPort"/> para el porqué de la
/// excepción HTTP.
/// </summary>
public sealed record CubicarRapidoQuery(CubicacionRapidaSolicitud Solicitud) : IQuery<CubicacionRapidaResultado>;

public sealed class CubicarRapidoQueryHandler(ICubicacionRapidaPort cubicacion)
    : IQueryHandler<CubicarRapidoQuery, CubicacionRapidaResultado>
{
    public Task<CubicacionRapidaResultado> Handle(CubicarRapidoQuery query, CancellationToken ct = default)
        => cubicacion.EvaluarAsync(query.Solicitud, ct);
}
