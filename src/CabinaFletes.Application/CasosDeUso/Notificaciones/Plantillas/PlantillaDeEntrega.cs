using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Cotizaciones.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Notificaciones.Plantillas;

/// <summary>
/// El cuerpo del correo de una cotización entregada.
///
/// Sale del caso de uso a su propia clase porque son dos cosas distintas: una es decidir
/// a quién se notifica y cuándo, la otra es cómo se ve el correo. Mezcladas, cualquier
/// ajuste de redacción obliga a tocar el archivo donde vive la lógica de la notificación.
///
/// El contenido es el que ya estaba, movido tal cual. La HU-06.2 (#22) pide una plantilla
/// revisada por el negocio, así que ahí es donde toca cambiarlo — no aquí, no a medias.
/// </summary>
public static class PlantillaDeEntrega
{
    public static string Asunto(Cotizacion c, Cliente cliente) =>
        $"Cotización Nexo — {cliente.Nombre} — {c.Total:C0}";

    public static string CuerpoHtml(Cotizacion c, Cliente cliente, string nombreDestinatario)
    {
        var subtotalFletes = c.Lineas.Sum(l =>
            l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0));
        var subtotalConexos = c.Conexos.Sum(x => x.ValorAplicado);

        return $"""
            <p>Hola {nombreDestinatario},</p>
            <p>Nexo entregó la cotización para <strong>{cliente.Nombre}</strong> (NIT {cliente.Nit}).
            El formato completo con todos los ítems va adjunto en PDF.</p>
            <table cellpadding="6" style="border-collapse:collapse;font-family:Arial,sans-serif;font-size:13px">
              <tr><td>Fletes ({c.Lineas.Count})</td><td align="right">{subtotalFletes:C0}</td></tr>
              <tr><td>Servicios Logísticos ({c.Conexos.Count})</td><td align="right">{subtotalConexos:C0}</td></tr>
              <tr style="font-weight:bold;border-top:2px solid #0b5394"><td>Total cotización</td><td align="right">{c.Total:C0}</td></tr>
            </table>
            <p style="color:#777;font-size:12px">Correo automático de Nexo · Botero Soto — Soluciones Logísticas.</p>
            """;
    }
}
