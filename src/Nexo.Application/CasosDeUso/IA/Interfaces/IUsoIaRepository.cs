using Nexo.Application.CasosDeUso.IA.DTOs;

namespace Nexo.Application.CasosDeUso.IA.Interfaces;

/// <summary>Registro y consulta del uso de los modelos de lenguaje (<c>tbl_ia_uso</c>).</summary>
public interface IUsoIaRepository
{
    Task RegistrarAsync(clsRegistroUsoIa registro, CancellationToken ct = default);

    /// <summary>Tokens (entrada, salida y caché) que una persona ha consumido desde <paramref name="desde"/> hasta ahora.</summary>
    Task<long> TokensConsumidosDesdeAsync(Guid usuarioId, DateTimeOffset desde, CancellationToken ct = default);

    /// <summary>Gasto agrupado por mes (hora de Colombia) y función, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<clsGastoIaDto>> GastoPorFuncionYMesAsync(
        DateTimeOffset desde, DateTimeOffset hasta, string? funcion, CancellationToken ct = default);
}
