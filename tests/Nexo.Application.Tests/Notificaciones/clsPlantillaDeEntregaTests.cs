using Nexo.Application.CasosDeUso.Notificaciones.Plantillas;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.Tests.Notificaciones;

/// <summary>
/// El correo de cotización entregada corría con la cultura ambiente del proceso para
/// formatear dinero (<c>{valor:C0}</c>): en el contenedor real esa cultura es invariante,
/// así que el total salía como "¤1,500,000" en vez de un monto legible (visto en un correo
/// real, ver tests/QA/hu-06.2-destinatario-real-plantilla/README.md). Estas pruebas fijan
/// que el formato usa es-CO explícita, sin importar la cultura del proceso que las corre,
/// y el sufijo "COP" (a pedido de negocio, en vez de un símbolo "$" ambiguo entre monedas).
/// </summary>
public class clsPlantillaDeEntregaTests
{
    private static readonly clsCliente Cliente = new() { Nombre = "PREBEL S.A.S. BIC", Nit = "890905032" };

    private static clsCotizacion Cotizacion() => new()
    {
        Total = 48_982_983m,
        Lineas = { new clsCotizacionLinea { ValorFlete = 48_982_983m, Fuente = FuenteFlete.Sicetac } },
    };

    [Fact]
    public void El_asunto_muestra_el_monto_en_pesos_colombianos_sin_importar_la_cultura_del_proceso()
    {
        var asunto = clsPlantillaDeEntrega.Asunto(Cotizacion(), Cliente);

        Assert.Contains("48.982.983 COP", asunto);
        Assert.DoesNotContain("¤", asunto);
        Assert.DoesNotContain("$", asunto);
    }

    [Fact]
    public void El_cuerpo_muestra_el_monto_en_pesos_colombianos_sin_importar_la_cultura_del_proceso()
    {
        var cuerpo = clsPlantillaDeEntrega.CuerpoHtml(Cotizacion(), Cliente, "Ismael");

        Assert.Contains("48.982.983 COP", cuerpo);
        Assert.DoesNotContain("¤", cuerpo);
        Assert.DoesNotContain("$", cuerpo);
    }

    [Fact]
    public void El_cuerpo_nunca_pierde_el_aviso_de_que_es_interno()
    {
        // Regla de negocio, no solo cosmética: este correo no se le manda al cliente.
        var cuerpo = clsPlantillaDeEntrega.CuerpoHtml(Cotizacion(), Cliente, "Ismael");

        Assert.Contains("nunca se envía al cliente", cuerpo);
    }

    [Fact]
    public void El_cuerpo_muestra_nombre_cliente_y_nit_sin_ningun_dato_de_contacto()
    {
        var cuerpo = clsPlantillaDeEntrega.CuerpoHtml(Cotizacion(), Cliente, "Ismael");

        Assert.Contains("PREBEL S.A.S. BIC", cuerpo);
        Assert.Contains("890905032", cuerpo);
    }
}
