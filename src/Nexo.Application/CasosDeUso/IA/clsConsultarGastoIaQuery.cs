using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.IA;

/// <summary>
/// Gasto en modelos de lenguaje por función y por mes. Sin <paramref name="Desde"/> mira desde el primer día del mes de hace
/// cinco meses (seis meses con el actual); sin <paramref name="Hasta"/>, hasta ahora.
/// </summary>
public sealed record clsConsultarGastoIaQuery(DateTimeOffset? Desde, DateTimeOffset? Hasta, string? Funcion)
    : IQuery<IReadOnlyList<clsGastoIaDto>>;

public sealed class clsConsultarGastoIaQueryHandler(IUsoIaRepository repositorio, TimeProvider reloj)
    : IQueryHandler<clsConsultarGastoIaQuery, IReadOnlyList<clsGastoIaDto>>
{
    /// <summary>Colombia no tiene horario de verano: el desfase es fijo.</summary>
    public static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(-5);

    public async Task<IReadOnlyList<clsGastoIaDto>> Handle(clsConsultarGastoIaQuery query, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var hasta = query.Hasta ?? ahora;
        var desde = query.Desde ?? PrimerDiaDelMes(ahora.ToOffset(DesfaseColombia)).AddMonths(-5);

        if (desde >= hasta)
            throw new clsValidationFailedException("RANGO_INVALIDO", "La fecha inicial debe ser anterior a la final.");

        string? funcion = null;
        if (!string.IsNullOrWhiteSpace(query.Funcion))
        {
            if (!Enum.TryParse<clsFuncionIa>(query.Funcion.Trim(), ignoreCase: true, out var f) || !Enum.IsDefined(f))
                throw new clsValidationFailedException("FUNCION_IA_INVALIDA",
                    $"La función no existe. Valores válidos: {string.Join(", ", Enum.GetNames<clsFuncionIa>())}.");
            funcion = f.ToString();
        }

        return await repositorio.GastoPorFuncionYMesAsync(desde, hasta, funcion, ct);
    }

    private static DateTimeOffset PrimerDiaDelMes(DateTimeOffset localColombia) =>
        new(localColombia.Year, localColombia.Month, 1, 0, 0, 0, DesfaseColombia);
}

public static class clsDependencyInjection
{
    public static IServiceCollection AddIaUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<clsConsultarGastoIaQuery, IReadOnlyList<clsGastoIaDto>>, clsConsultarGastoIaQueryHandler>();
}
