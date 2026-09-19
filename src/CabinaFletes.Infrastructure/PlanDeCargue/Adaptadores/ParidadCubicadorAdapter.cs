using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Domain.PlanDeCargue.Reglas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.PlanDeCargue.Adaptadores;

/// <summary>
/// S2-HU-08.2 (#30): lee la evidencia de paridad de configuración y aplica la regla de
/// negocio (Domain.PlanDeCargue.Reglas.CompuertaDeParidad). Tres claves, no una:
///
/// - <c>Cubicacion:ParidadVerificada</c> (bool) — el interruptor.
/// - <c>Cubicacion:ParidadEvidencia</c> (string) — A QUÉ corrida de paridad se refiere
///   (ej. la URL de la corrida verde de golden tests en cubicador-ci.yml, o el commit).
/// - <c>Cubicacion:ParidadFecha</c> (date) — CUÁNDO se confirmó.
///
/// Encender la función es, a propósito, más que voltear un booleano: exige dejar dicho
/// contra qué evidencia se prendió. Ver README (sección "Encendido de la función") para
/// el procedimiento — quién lo hace y cuándo.
/// </summary>
public sealed class ParidadCubicadorAdapter(IConfiguration config, ILogger<ParidadCubicadorAdapter> logger)
    : IParidadCubicadorPort
{
    public bool Habilitada
    {
        get
        {
            // Issue #183: config.GetValue<bool>(...) usa Convert.ChangeType, que lanza
            // FormatException sin controlar con el valor por defecto real de esta clave
            // (cadena vacía, no ausente): un despliegue nuevo sin encender la compuerta
            // crasheaba con 500 en vez de responder el 503 documentado.
            var verificada = bool.TryParse(config["Cubicacion:ParidadVerificada"], out var v) && v;
            var evidencia = config["Cubicacion:ParidadEvidencia"];
            var fechaTexto = config["Cubicacion:ParidadFecha"];
            var fecha = DateOnly.TryParse(fechaTexto, out var f) ? f : (DateOnly?)null;

            var habilitada = CompuertaDeParidad.EstaHabilitada(verificada, evidencia, fecha);

            if (verificada && !habilitada)
                logger.LogWarning(
                    "Cubicacion:ParidadVerificada está en true pero falta Evidencia y/o Fecha — " +
                    "la compuerta queda CERRADA. Configure las tres claves juntas.");

            return habilitada;
        }
    }
}
