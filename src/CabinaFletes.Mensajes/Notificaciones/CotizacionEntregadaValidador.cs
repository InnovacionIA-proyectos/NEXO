namespace CabinaFletes.Mensajes.Notificaciones;

/// <summary>
/// Invariantes de <see cref="CotizacionEntregada"/>.
///
/// El adjunto es opcional a propósito: una entrega sin formato es válida y el correo sale
/// sin él. Lo que no puede faltar es la correlación — sin cotización no hay nada que
/// notificar, y sin quién entregó no hay rastro de la notificación.
/// </summary>
public sealed class CotizacionEntregadaValidador : IValidadorDeMensaje<CotizacionEntregada>
{
    public IReadOnlyList<string> Validar(CotizacionEntregada m)
    {
        var motivos = Motivos.De(
            ("CotizacionId", m.CotizacionId),
            ("SolicitudId", m.SolicitudId),
            ("EntregadaPor", m.EntregadaPor));

        if (m.EntregadaEn == default)
            motivos.Add("EntregadaEn es obligatorio: sin fecha no hay rastro de la entrega.");

        // Un nombre sin contenido, o contenido sin nombre, deja al adaptador adivinando.
        if (m.FormatoPdf is { Length: > 0 } && string.IsNullOrWhiteSpace(m.NombrePdf))
            motivos.Add("Si viene FormatoPdf tiene que venir NombrePdf: es con lo que se adjunta.");

        return motivos;
    }
}
