using System.Net;
using CabinaFletes.Api.Tests.Infra;

namespace CabinaFletes.Api.Tests;

// Va en la colección "api": una segunda instancia de ApiFixture borraría la base de
// pruebas en pleno uso de las otras clases.
[Collection("api")]
public class HealthTests(ApiFixture fx)
{
    [Fact] // anónimo permitido + verifica la BD y el bus
    public async Task Health_llega_a_200_y_healthy()
    {
        // /health incluye el bus de mensajería, y el bus tarda en arrancar. Mientras
        // tanto responde 503, que es lo CORRECTO para una sonda de readiness: hasta que
        // el bus no está listo, este proceso no debería recibir tráfico.
        //
        // La prueba pedía 200 en el primer intento, o sea daba por hecho que no existe
        // ventana de arranque. Fallaba desde que entró MassTransit (#14) y nadie lo vio
        // porque Api.Tests no se corría completa. El compose ya contempla esa ventana
        // con start_period; aquí se contempla esperando.
        HttpResponseMessage? resp = null;
        for (var intento = 0; intento < 40; intento++)
        {
            resp = await fx.Anon().GetAsync("/health");
            if (resp.StatusCode == HttpStatusCode.OK) break;
            await Task.Delay(100);
        }

        Assert.Equal(HttpStatusCode.OK, resp!.StatusCode);

        // El cuerpo trae el detalle por chequeo. La prueba lo afirma porque es lo que
        // hace útil un 503: sin esto la respuesta es la palabra "Unhealthy" y toca
        // adivinar cuál de los chequeos está en rojo.
        var cuerpo = await resp.Json();
        Assert.Equal("Healthy", cuerpo.GetProperty("status").GetString());

        var chequeos = cuerpo.GetProperty("chequeos");
        Assert.Equal("Healthy", chequeos.GetProperty("database").GetProperty("status").GetString());
        Assert.Equal("Healthy", chequeos.GetProperty("masstransit-bus").GetProperty("status").GetString());
    }
}
