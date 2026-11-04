namespace Nexo.Mensajes.Bandeja;

/// <summary>Invariantes de <see cref="clsBandejaCambio"/>: el motivo debe ser uno de los conocidos, no texto libre.</summary>
public sealed class clsBandejaCambioValidador : IValidadorDeMensaje<clsBandejaCambio>
{
    private static readonly string[] Conocidos =
    [
        clsMotivoDeCambio.SolicitudNueva,
        clsMotivoDeCambio.SolicitudRechazada,
        clsMotivoDeCambio.TrabajoTerminado,
        clsMotivoDeCambio.ParametrosRecalibrados,
        clsMotivoDeCambio.RefrescoPeriodico,
    ];

    public IReadOnlyList<string> Validar(clsBandejaCambio m)
    {
        var motivos = new List<string>();

        if (string.IsNullOrWhiteSpace(m.Motivo))
            motivos.Add("Motivo es obligatorio: es lo que explica el refresco en el log y en el cliente.");
        else if (!Conocidos.Contains(m.Motivo))
            motivos.Add(
                $"Motivo '{m.Motivo}' no es uno de los conocidos ({string.Join(", ", Conocidos)}). " +
                "Un motivo libre volvería el contrato una convención de pasillo.");

        if (m.En == default)
            motivos.Add("En es obligatorio: sin fecha no se puede ordenar ni descartar un aviso viejo.");

        return motivos;
    }
}
