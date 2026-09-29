using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Condiciones del servicio de una clsSolicitud, persistidas como JSON en
// clsSolicitud.CondicionesServicio. Las escribe la Captura (módulo Solicitudes,
// aún no migrado) y las lee/actualiza Cotizaciones al registrar un rechazo
// (MarcarResultado). Viven en Common porque las consumen ambos módulos.
// ---------------------------------------------------------------------------

/// <summary>Rechazo registrado por el equipo comercial (motivo seleccionado + comentario).</summary>
public record clsMotivoRechazoDto(string Motivo, string? Comentario, DateTimeOffset Fecha);

/// <summary>Tiempos logísticos estimados de la solicitud (horas de cargue y descargue).</summary>
public record clsTiemposLogisticosDto(decimal HorasCargue, decimal HorasDescargue)
{
    public static readonly clsTiemposLogisticosDto PorDefecto = new(4m, 4m);
}

public record clsCondicionesServicioDto(
    string? Referencia,
    clsTiemposLogisticosDto? TiemposLogisticos,
    clsMotivoRechazoDto? Rechazo = null,
    /// <summary>Notas u observaciones comerciales de la solicitud (S4-HU-18.10/#227) —
    /// libres, del comercial que captura, no confundir con <see cref="Referencia"/>
    /// (referencia/PO del cliente).</summary>
    string? Notas = null)
{
    /// <summary>Opciones de (de)serialización del JSON de condiciones (camelCase, tolerante).</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Deserializa el JSON; condiciones con tiempos por defecto si es nulo o inválido.</summary>
    public static clsCondicionesServicioDto Desde(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var c = JsonSerializer.Deserialize<clsCondicionesServicioDto>(json, Json);
                if (c is not null) return c with { TiemposLogisticos = c.TiemposLogisticos ?? clsTiemposLogisticosDto.PorDefecto };
            }
            catch (JsonException)
            {
                // contenido legado con otro formato: se usan los valores por defecto
            }
        }
        return new clsCondicionesServicioDto(null, clsTiemposLogisticosDto.PorDefecto);
    }
}
