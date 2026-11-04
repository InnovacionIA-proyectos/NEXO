namespace Nexo.Mensajes.Notificaciones;

/// <summary>
/// Invariantes de <see cref="clsCotizacionEntregada"/>. El adjunto es opcional; la correlación
/// (cotización y quién entregó) no.
/// </summary>
public sealed class clsCotizacionEntregadaValidador : IValidadorDeMensaje<clsCotizacionEntregada>
{
    public IReadOnlyList<string> Validar(clsCotizacionEntregada m)
    {
        var motivos = clsMotivos.De(
            ("CotizacionId", m.CotizacionId),
            ("SolicitudId", m.SolicitudId),
            ("EntregadaPor", m.EntregadaPor));

        if (m.EntregadaEn == default)
            motivos.Add("EntregadaEn es obligatorio: sin fecha no hay rastro de la entrega.");

        if (m.FormatoPdf is { Length: > 0 } && string.IsNullOrWhiteSpace(m.NombrePdf))
            motivos.Add("Si viene FormatoPdf tiene que venir NombrePdf: es con lo que se adjunta.");

        return motivos;
    }
}
