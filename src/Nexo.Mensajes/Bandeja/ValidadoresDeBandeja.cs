namespace Nexo.Mensajes.Bandeja;

/// <summary>
/// Invariantes de <see cref="BandejaCambio"/>.
///
/// El motivo tiene que ser uno de los conocidos, no cualquier texto. Un motivo libre
/// convertiría el contrato en una convención de pasillo: el cliente empezaría a comparar
/// contra cadenas que alguien escribió de memoria, y un error de tipeo pasaría al
/// navegador sin que nada lo frene.
/// </summary>
public sealed class BandejaCambioValidador : IValidadorDeMensaje<BandejaCambio>
{
    private static readonly string[] Conocidos =
    [
        MotivoDeCambio.SolicitudNueva,
        MotivoDeCambio.SolicitudRechazada,
        MotivoDeCambio.TrabajoTerminado,
        MotivoDeCambio.ParametrosRecalibrados,
        MotivoDeCambio.RefrescoPeriodico,
    ];

    public IReadOnlyList<string> Validar(BandejaCambio m)
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
