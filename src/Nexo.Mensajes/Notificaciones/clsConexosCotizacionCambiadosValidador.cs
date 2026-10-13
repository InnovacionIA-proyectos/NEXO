namespace Nexo.Mensajes.Notificaciones;

/// <summary>Invariantes de <see cref="clsConexosCotizacionCambiados"/>: sin correlación, sin
/// acción reconocible o sin ningún ítem no hay nada que decirle a quien la creó.</summary>
public sealed class clsConexosCotizacionCambiadosValidador : IValidadorDeMensaje<clsConexosCotizacionCambiados>
{
    public IReadOnlyList<string> Validar(clsConexosCotizacionCambiados m)
    {
        var motivos = clsMotivos.De(
            ("CotizacionId", m.CotizacionId),
            ("SolicitudId", m.SolicitudId),
            ("ModificadoPor", m.ModificadoPor));

        if (m.Accion is not ("agregado" or "quitado"))
            motivos.Add("Accion debe ser 'agregado' o 'quitado'.");
        if (m.Items is null || m.Items.Count == 0)
            motivos.Add("Items no puede estar vacío: sin servicios no hay qué avisar.");
        if (m.ModificadoEn == default)
            motivos.Add("ModificadoEn es obligatorio: sin fecha no hay rastro del cambio.");

        return motivos;
    }
}
