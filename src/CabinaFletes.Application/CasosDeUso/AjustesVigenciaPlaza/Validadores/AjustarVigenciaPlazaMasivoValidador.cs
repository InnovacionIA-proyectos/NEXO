using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza.Validadores;

/// <summary>
/// Forma de <see cref="AjustarVigenciaPlazaMasivoCommand"/>: qué campos exige cada modo.
/// Solo mira el comando — no consulta repositorios ni SAP, que es trabajo del caso de uso.
/// </summary>
public static class AjustarVigenciaPlazaMasivoValidador
{
    public static void Validar(AjustarVigenciaPlazaMasivoCommand command)
    {
        var modo = command.Modo.Trim().ToLowerInvariant();

        if (modo == "fecha" && command.NuevaVigencia is null)
            throw new ValidationFailedException("FECHA_REQUERIDA", "En modo 'fecha' debe indicar la nueva vigencia.");

        if (modo != "extender") return;

        if (command.Cantidad is not > 0)
            throw new ValidationFailedException("CANTIDAD_INVALIDA", "En modo 'extender' la cantidad debe ser mayor a 0.");
        if (string.IsNullOrWhiteSpace(command.Unidad))
            throw new ValidationFailedException("UNIDAD_REQUERIDA", "En modo 'extender' debe indicar la unidad (dias|meses).");
    }
}
