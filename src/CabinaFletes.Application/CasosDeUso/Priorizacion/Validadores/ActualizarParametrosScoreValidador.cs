using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Priorizacion.Reglas;

namespace CabinaFletes.Application.CasosDeUso.Priorizacion.Validadores;

/// <summary>
/// Traduce la regla de Domain a la excepción que el API convierte en 400.
///
/// Aquí no hay ninguna regla: están todas en <see cref="ReglaDeParametrosScore"/>. Esto
/// solo decide qué error se lanza, que es lo único de esta decisión que no es del
/// negocio.
/// </summary>
public static class ActualizarParametrosScoreValidador
{
    public static void Validar(ParametrosScoreDto d)
    {
        // Se valida sobre una copia y no sobre la fila real: si algo no cuadra, la fila
        // guardada no puede quedar a medio modificar.
        var candidato = new ParametrosScore();
        Mappers.ParametrosScoreMappers.Copiar(d, candidato);

        var motivos = ReglaDeParametrosScore.Validar(candidato);
        if (motivos.Count > 0)
            throw new ValidationFailedException("PARAMETROS_SCORE_INVALIDOS", string.Join(" ", motivos));
    }
}
