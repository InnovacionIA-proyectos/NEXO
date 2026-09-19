using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;

namespace Nexo.Application.CasosDeUso.Notificaciones.Plantillas;

/// <summary>
/// El cuerpo del correo de una cotización entregada.
///
/// Sale del caso de uso a su propia clase porque son dos cosas distintas: una es decidir
/// a quién se notifica y cuándo, la otra es cómo se ve el correo. Mezcladas, cualquier
/// ajuste de redacción obliga a tocar el archivo donde vive la lógica de la notificación.
///
/// S2-HU-06.2 (#22): el destinatario ahora es quien SOLICITÓ la consulta (no un
/// "comercial responsable" externo), así que el saludo y el cuerpo se ajustan a eso —
/// confirma que Nexo entregó SU solicitud, no una notificación genérica de cliente.
/// Redacción sujeta a revisión final de negocio antes de producción; el contenido en sí
/// (qué datos muestra, qué omite) sí quedó acotado acá: nunca nombre, correo ni ningún
/// dato de contacto del cliente más allá de razón social y NIT, que ya son públicos en
/// cualquier factura.
/// </summary>
public static class clsPlantillaDeEntrega
{
    public static string Asunto(clsCotizacion c, clsCliente cliente) =>
        $"Tu cotización para {cliente.Nombre} quedó entregada — {c.Total:C0}";

    public static string CuerpoHtml(clsCotizacion c, clsCliente cliente, string nombreDestinatario)
    {
        var subtotalFletes = c.Lineas.Sum(l =>
            l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0));
        var subtotalConexos = c.Conexos.Sum(x => x.ValorAplicado);

        return $"""
            <p>Hola {nombreDestinatario},</p>
            <p>La cotización que solicitaste para <strong>{cliente.Nombre}</strong> (NIT {cliente.Nit})
            ya quedó entregada. El formato completo con todos los ítems va adjunto en PDF.</p>
            <table cellpadding="6" style="border-collapse:collapse;font-family:Arial,sans-serif;font-size:13px">
              <tr><td>Fletes ({c.Lineas.Count})</td><td align="right">{subtotalFletes:C0}</td></tr>
              <tr><td>Servicios Logísticos ({c.Conexos.Count})</td><td align="right">{subtotalConexos:C0}</td></tr>
              <tr style="font-weight:bold;border-top:2px solid #0b5394"><td>Total cotización</td><td align="right">{c.Total:C0}</td></tr>
            </table>
            <p style="color:#777;font-size:12px">Correo automático de Nexo · Botero Soto — Soluciones Logísticas.
            Este aviso es interno: nunca se envía al cliente.</p>
            """;
    }
}
