using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Priorizacion.Reglas;

namespace Nexo.Application.CasosDeUso.Priorizacion.Validadores;

/// <summary>Traduce la regla de <see cref="clsReglaDeParametrosScore"/> a la excepción que el API convierte en 400.</summary>
public static class clsActualizarParametrosScoreValidador
{
    public static void Validar(clsParametrosScoreDto d)
    {
        // Se valida sobre una copia para que la fila guardada no quede a medio modificar.
        var candidato = new clsParametrosScore();
        Mappers.clsParametrosScoreMappers.Copiar(d, candidato);

        var motivos = clsReglaDeParametrosScore.Validar(candidato);
        if (motivos.Count > 0)
            throw new clsValidationFailedException("PARAMETROS_SCORE_INVALIDOS", string.Join(" ", motivos));
    }
}
