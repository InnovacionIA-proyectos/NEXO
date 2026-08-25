using System.Text.Json;
using System.Text.Json.Serialization;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Condiciones del servicio de una Solicitud, persistidas como JSON en
// Solicitud.CondicionesServicio. Las escribe la Captura (módulo Solicitudes,
// aún no migrado) y las lee/actualiza Cotizaciones al registrar un rechazo
// (MarcarResultado). Viven en Common porque las consumen ambos módulos.
// ---------------------------------------------------------------------------

/// <summary>Rechazo registrado por el equipo comercial (motivo seleccionado + comentario).</summary>
public record MotivoRechazoDto(string Motivo, string? Comentario, DateTimeOffset Fecha);

/// <summary>Tiempos logísticos estimados de la solicitud (horas de cargue y descargue).</summary>
public record TiemposLogisticosDto(decimal HorasCargue, decimal HorasDescargue)
{
    public static readonly TiemposLogisticosDto PorDefecto = new(4m, 4m);
}

public record CondicionesServicioDto(
    string? Referencia,
    TiemposLogisticosDto? TiemposLogisticos,
    MotivoRechazoDto? Rechazo = null)
{
    /// <summary>Opciones de (de)serialización del JSON de condiciones (camelCase, tolerante).</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Deserializa el JSON; condiciones con tiempos por defecto si es nulo o inválido.</summary>
    public static CondicionesServicioDto Desde(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var c = JsonSerializer.Deserialize<CondicionesServicioDto>(json, Json);
                if (c is not null) return c with { TiemposLogisticos = c.TiemposLogisticos ?? TiemposLogisticosDto.PorDefecto };
            }
            catch (JsonException)
            {
                // contenido legado con otro formato: se usan los valores por defecto
            }
        }
        return new CondicionesServicioDto(null, TiemposLogisticosDto.PorDefecto);
    }
}
