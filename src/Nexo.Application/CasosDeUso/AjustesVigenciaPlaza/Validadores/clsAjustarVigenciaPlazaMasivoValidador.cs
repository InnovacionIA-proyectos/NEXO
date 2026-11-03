using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Validadores;

/// <summary>
/// Forma de <see cref="clsAjustarVigenciaPlazaMasivoCommand"/>: qué campos exige cada modo.
/// Solo mira el comando — no consulta repositorios ni SAP, que es trabajo del caso de uso.
/// </summary>
public static class clsAjustarVigenciaPlazaMasivoValidador
{
    public static void Validar(clsAjustarVigenciaPlazaMasivoCommand command)
    {
        var modo = command.Modo.Trim().ToLowerInvariant();

        if (modo == "fecha" && command.NuevaVigencia is null)
            throw new clsValidationFailedException("FECHA_REQUERIDA", "En modo 'fecha' debe indicar la nueva vigencia.");

        if (modo != "extender") return;

        if (command.Cantidad is not > 0)
            throw new clsValidationFailedException("CANTIDAD_INVALIDA", "En modo 'extender' la cantidad debe ser mayor a 0.");
        if (string.IsNullOrWhiteSpace(command.Unidad))
            throw new clsValidationFailedException("UNIDAD_REQUERIDA", "En modo 'extender' debe indicar la unidad (dias|meses).");
    }
}
