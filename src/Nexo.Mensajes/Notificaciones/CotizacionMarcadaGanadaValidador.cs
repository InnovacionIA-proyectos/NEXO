namespace Nexo.Mensajes.Notificaciones;

public sealed class CotizacionMarcadaGanadaValidador : IValidadorDeMensaje<CotizacionMarcadaGanada>
{
    public IReadOnlyList<string> Validar(CotizacionMarcadaGanada m)
    {
        var motivos = Motivos.De(("CotizacionId", m.CotizacionId), ("SolicitudId", m.SolicitudId));
        if (m.MarcadaEn == default)
            motivos.Add("MarcadaEn es obligatorio: sin fecha no hay rastro de cuándo se marcó.");
        return motivos;
    }
}
