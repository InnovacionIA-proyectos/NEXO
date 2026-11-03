using System.Globalization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Notificaciones.Plantillas;

/// <summary>
/// Correo de "Cotización actualizada" (S5-HU-20.1/#286): se agregó o se quitó un servicio
/// conexo de una cotización que ya se había entregado. Misma identidad visual y misma
/// moneda explícita (es-CO, sufijo COP) que <see cref="clsPlantillaDeEntrega"/>; los nombres
/// de servicio vienen del catálogo y se escapan igual por si traen caracteres de marcado.
/// </summary>
public static class clsPlantillaConexosCambiados
{
    private static readonly CultureInfo Cop = CultureInfo.GetCultureInfo("es-CO");

    private static string Moneda(decimal valor) => $"{valor.ToString("N0", Cop)} COP";

    private static string Cantidad(decimal cantidad) => cantidad.ToString("0.##", Cop);

    // Escape mínimo de marcado (& < > "): WebUtility.HtmlEncode además vuelve entidad cada tilde
    // ("ó" -> &#243;), que renderiza igual pero deja el HTML ilegible sin necesidad.
    private static string Esc(string texto) =>
        texto.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>"se agregó Servicio x 2" / "se quitó A x 1 y B x 3": una frase por petición.</summary>
    public static string Resumen(clsConexosCotizacionCambiados m)
    {
        var verbo = m.Accion == "agregado" ? "se agregó" : "se quitó";
        var servicios = m.Items.Select(i => $"{i.Servicio} x {Cantidad(i.Cantidad)}").ToList();
        var lista = servicios.Count <= 1
            ? string.Join("", servicios)
            : string.Join(", ", servicios.Take(servicios.Count - 1)) + " y " + servicios[^1];
        return $"{verbo} {lista}";
    }

    public static string Asunto(clsConexosCotizacionCambiados m, clsCliente cliente) =>
        $"Cotización actualizada para {cliente.Nombre} — nuevo total {Moneda(m.NuevoTotal)}";

    public static string CuerpoHtml(clsConexosCotizacionCambiados m, clsCliente cliente, string nombreDestinatario)
    {
        var resumen = Esc(Resumen(m));
        return $"""
            <div style="max-width:560px;margin:0 auto;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
                <tr>
                  <td style="height:6px;line-height:6px;font-size:0;border-radius:10px 10px 0 0;background-color:#0b5394;background-image:linear-gradient(90deg,#0b5394,#34a853);">&nbsp;</td>
                </tr>
              </table>
              <div style="border:1px solid #e3e6ea;border-top:none;border-radius:0 0 10px 10px;padding:28px 32px;background:#ffffff;">
                <div style="text-align:center;margin-bottom:22px;">
                  <div style="font-size:18px;font-weight:bold;color:#0b5394;letter-spacing:.5px;">BOTERO SOTO</div>
                  <div style="font-size:11px;font-weight:600;color:#34a853;letter-spacing:1.5px;margin-top:2px;">SOLUCIONES LOGÍSTICAS</div>
                </div>
                <div style="background:#eaf1fb;border-radius:6px;padding:14px;text-align:center;margin-bottom:22px;">
                  <span style="font-size:16px;font-weight:bold;color:#0b5394;">Cotización actualizada</span>
                </div>
                <p style="font-size:14px;color:#333;margin:0 0 12px;">Hola {Esc(nombreDestinatario)},</p>
                <p style="font-size:14px;color:#333;line-height:1.5;margin:0 0 20px;">
                  La cotización que solicitaste para <strong>{Esc(cliente.Nombre)}</strong> (NIT {Esc(cliente.Nit)})
                  ya estaba entregada y se modificó: {resumen}.
                </p>
                <table role="presentation" width="100%" cellpadding="10" cellspacing="0" style="border-collapse:collapse;font-size:14px;color:#333;">
                  <tr style="font-weight:bold;border-top:2px solid #0b5394;">
                    <td>Nuevo total de la cotización</td>
                    <td align="right">{Moneda(m.NuevoTotal)}</td>
                  </tr>
                </table>
                <hr style="border:none;border-top:1px solid #e3e6ea;margin:24px 0 16px;">
                <p style="color:#888;font-size:12px;text-align:center;margin:0;">
                  Correo automático de Nexo — Botero Soto Soluciones Logísticas.<br>
                  Este aviso es interno: nunca se envía al cliente.
                </p>
              </div>
            </div>
            """;
    }
}
