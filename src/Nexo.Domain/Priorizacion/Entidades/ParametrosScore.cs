using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Common;

namespace Nexo.Domain.Priorizacion.Entidades;

/// <summary>
/// Parámetros configurables del score de priorización (fila única, editable por el Admin).
/// Permite recalibrar pesos y referencias sin tocar código.
/// </summary>
public class ParametrosScore : IAuditable
{
    /// <summary>Fila única (singleton). Siempre Id = 1.</summary>
    public int Id { get; set; } = 1;

    // Pesos de cada componente (deberían sumar 1.0).
    [Range(0, 1)] public double PesoUrgencia { get; set; } = 0.40;
    [Range(0, 1)] public double PesoValor { get; set; } = 0.30;
    [Range(0, 1)] public double PesoProbabilidad { get; set; } = 0.20;
    [Range(0, 1)] public double PesoTipo { get; set; } = 0.10;

    /// <summary>Horizonte en horas para normalizar la urgencia (default 240h ≈ 10 días).</summary>
    [Range(1, 8760)]
    public double HorizonteHoras { get; set; } = 240;

    /// <summary>Valor de referencia (COP) para normalizar el componente económico (escala log).</summary>
    [Range(typeof(decimal), "1", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorReferencia { get; set; } = 100_000_000m;

    [Range(0, 100)] public double PuntajeLicitacion { get; set; } = 100;
    [Range(0, 100)] public double PuntajeSpot { get; set; } = 50;

    /// <summary>Puntaje del componente valor cuando la solicitud no trae valor_estimado.</summary>
    [Range(0, 100)] public double ValorPorDefectoSinValor { get; set; } = 30;

    /// <summary>Puntaje de probabilidad cuando la solicitud no trae probabilidad_cierre.</summary>
    [Range(0, 100)] public double ProbabilidadPorDefecto { get; set; } = 50;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Token de concurrencia optimista (rowversion) al recalibrar el score.</summary>
    public byte[]? RowVersion { get; set; }
}
