using System.Globalization;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;

namespace Nexo.Application.CasosDeUso.Notificaciones.Plantillas;

/// <summary>
/// Cuerpo del correo de una cotización entregada. Del cliente solo muestra razón social y NIT: nunca nombre, correo ni
/// datos de contacto.
/// </summary>
public static class clsPlantillaDeEntrega
{
    // es-CO explícita: el runtime corre con globalización invariante en el contenedor y la cultura ambiente no trae símbolo
    // de moneda real. Sufijo "COP" en vez de "$" para que quede explícito que son pesos colombianos.
    private static readonly CultureInfo Cop = CultureInfo.GetCultureInfo("es-CO");

    private static string Moneda(decimal valor) => $"{valor.ToString("N0", Cop)} COP";

    public static string Asunto(clsCotizacion c, clsCliente cliente) =>
        $"Tu cotización para {cliente.Nombre} quedó entregada — {Moneda(c.Total)}";

    /// <param name="lineasRechazadas">Líneas rechazadas por el Administrador de Tarifas, con su motivo; solo el correo lleva los motivos (no el Excel ni el PDF).</param>
    public static string CuerpoHtml(
        clsCotizacion c, clsCliente cliente, string nombreDestinatario, IReadOnlyList<clsLineaRechazadaInfo>? lineasRechazadas = null)
    {
        var rechazadas = lineasRechazadas ?? [];
        var bloqueRechazadas = rechazadas.Count == 0 ? string.Empty : $"""
                <div style="margin:20px 0 0;padding:14px;background:#fff6e5;border-radius:6px;font-size:13px;color:#5a4300;">
                  <strong>Líneas sin costeo ({rechazadas.Count})</strong>
                  <p style="margin:6px 0 10px;">Estas líneas quedaron fuera de la cotización (sin precio) por el motivo indicado:</p>
                  <table role="presentation" width="100%" cellpadding="6" cellspacing="0" style="border-collapse:collapse;font-size:13px;color:#333;">
                    {string.Concat(rechazadas.Select(r => $"<tr style=\"border-top:1px solid #f0dca8;\"><td>{System.Security.SecurityElement.Escape(r.Ruta)}</td><td>{System.Security.SecurityElement.Escape(r.Motivo)}</td></tr>"))}
                  </table>
                </div>
            """;

        var subtotalFletes = c.Lineas.Sum(l =>
            l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0));
        var subtotalConexos = c.Conexos.Sum(x => x.ValorVenta);

        // Estilos inline y tablas (sin <style> ni flex/grid) para que se vea igual en clientes de correo sin CSS moderno.
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
                  <span style="font-size:16px;font-weight:bold;color:#0b5394;">Cotización entregada</span>
                </div>
                <p style="font-size:14px;color:#333;margin:0 0 12px;">Hola {nombreDestinatario},</p>
                <p style="font-size:14px;color:#333;line-height:1.5;margin:0 0 20px;">
                  La cotización que solicitaste para <strong>{cliente.Nombre}</strong> (NIT {cliente.Nit})
                  ya quedó entregada. El formato completo con todos los ítems va adjunto en PDF.
                </p>
                <table role="presentation" width="100%" cellpadding="10" cellspacing="0" style="border-collapse:collapse;font-size:14px;color:#333;">
                  <tr style="background:#f7f9fb;">
                    <td>Fletes ({c.Lineas.Count})</td>
                    <td align="right">{Moneda(subtotalFletes)}</td>
                  </tr>
                  <tr>
                    <td>Servicios Logísticos ({c.Conexos.Count})</td>
                    <td align="right">{Moneda(subtotalConexos)}</td>
                  </tr>
                  <tr style="font-weight:bold;border-top:2px solid #0b5394;">
                    <td>Total cotización</td>
                    <td align="right">{Moneda(c.Total)}</td>
                  </tr>
                </table>
                {bloqueRechazadas}
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
