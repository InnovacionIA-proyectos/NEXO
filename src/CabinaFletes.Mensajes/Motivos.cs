namespace CabinaFletes.Mensajes;

/// <summary>
/// Atajo para el chequeo que repiten todos los validadores: un identificador vacío
/// nunca es válido en un mensaje.
///
/// Vive en el namespace raíz porque lo usan los validadores de todos los contextos.
/// Estaba dentro de los de costeo, que fue donde nació.
/// </summary>
internal static class Motivos
{
    public static List<string> De(params (string Nombre, Guid Valor)[] campos)
    {
        var motivos = new List<string>();
        foreach (var (nombre, valor) in campos)
            if (valor == Guid.Empty)
                motivos.Add($"{nombre} no puede ser vacío.");
        return motivos;
    }
}
