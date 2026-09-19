namespace Nexo.Mensajes.Notificaciones;

public sealed class TarifaVencidaDetectadaValidador : IValidadorDeMensaje<TarifaVencidaDetectada>
{
    public IReadOnlyList<string> Validar(TarifaVencidaDetectada m)
    {
        var motivos = Motivos.De(("LineaId", m.LineaId));
        if (string.IsNullOrWhiteSpace(m.Justificacion))
            motivos.Add("Justificacion es obligatoria: sin ella el correo no dice a qué tarifa se refiere.");
        if (m.DetectadaEn == default)
            motivos.Add("DetectadaEn es obligatorio: sin fecha no hay rastro de cuándo se detectó.");
        return motivos;
    }
}
