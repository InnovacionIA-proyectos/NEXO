using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.PlanDeCargue.Reglas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.PlanDeCargue.Adaptadores;

/// <summary>Lee la evidencia de paridad (ParidadVerificada, ParidadEvidencia, ParidadFecha) y aplica clsCompuertaDeParidad.
/// Encender la función exige dejar dicho contra qué evidencia se prendió, no solo voltear un booleano.</summary>
public sealed class clsParidadCubicadorAdapter(IConfiguration config, ILogger<clsParidadCubicadorAdapter> logger)
    : IParidadCubicadorPort
{
    public bool Habilitada
    {
        get
        {
            // GetValue<bool> lanza FormatException con la cadena vacía (valor por defecto real de la clave):
            // se parsea a mano para responder el 503 documentado en vez de 500.
            var verificada = bool.TryParse(config["Cubicacion:ParidadVerificada"], out var v) && v;
            var evidencia = config["Cubicacion:ParidadEvidencia"];
            var fechaTexto = config["Cubicacion:ParidadFecha"];
            var fecha = DateOnly.TryParse(fechaTexto, out var f) ? f : (DateOnly?)null;

            var habilitada = clsCompuertaDeParidad.EstaHabilitada(verificada, evidencia, fecha);

            if (verificada && !habilitada)
                logger.LogWarning(
                    "Cubicacion:ParidadVerificada está en true pero falta Evidencia y/o Fecha — " +
                    "la compuerta queda CERRADA. Configure las tres claves juntas.");

            return habilitada;
        }
    }
}
