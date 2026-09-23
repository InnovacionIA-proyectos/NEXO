namespace Nexo.Mensajes.Notificaciones;

public sealed class clsCotizacionMarcadaGanadaValidador : IValidadorDeMensaje<clsCotizacionMarcadaGanada>
{
    public IReadOnlyList<string> Validar(clsCotizacionMarcadaGanada m)
    {
        var motivos = clsMotivos.De(("CotizacionId", m.CotizacionId), ("SolicitudId", m.SolicitudId));
        if (m.MarcadaEn == default)
            motivos.Add("MarcadaEn es obligatorio: sin fecha no hay rastro de cuándo se marcó.");
        return motivos;
    }
}
