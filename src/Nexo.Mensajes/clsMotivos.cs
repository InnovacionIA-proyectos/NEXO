namespace Nexo.Mensajes;

/// <summary>Atajo para el chequeo que repiten los validadores: un identificador vacío nunca es válido.</summary>
internal static class clsMotivos
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
