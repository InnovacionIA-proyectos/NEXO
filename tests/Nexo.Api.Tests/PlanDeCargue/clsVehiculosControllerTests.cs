using System.Net;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests.PlanDeCargue;

/// <summary>
/// clsGuardarVehiculoRequest es un record con constructor primario. Poner los atributos
/// de validación con el target "property:" (en vez de sobre el parámetro) hace que
/// ASP.NET Core detecte la inconsistencia y responda 500 a CUALQUIER POST — válido o
/// no — en vez de aplicar la validación. Se probó manualmente contra el contenedor antes
/// de desplegar (ver conversación) y se agrega acá para que no se vuelva a colar.
/// </summary>
[Collection("api")]
public class clsVehiculosControllerTests(clsApiFixture fixture)
{
    private static object CuerpoValido(string puertaPosicion) => new
    {
        nombre = "Sencillo 3 ejes",
        largoInternoMm = 12500,
        anchoInternoMm = 2450,
        altoInternoMm = 2700,
        capacidadKg = 34000,
        taraKg = 8000,
        puertaAnchoMm = 2400,
        puertaAltoMm = 2600,
        puertaPosicion,
        ejes = new[] { new { posicionMm = 0, capacidadKg = 6000 } },
    };

    [Fact]
    public async Task Puerta_posicion_valida_devuelve_202_no_500()
    {
        var resp = await fixture.Client().PostJson("/api/vehiculos", CuerpoValido("trasera"));
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Puerta_posicion_en_mayuscula_se_normaliza_y_acepta()
    {
        // Case-insensitive en el límite HTTP (igual criterio que "tipo" en Solicitudes/
        // Cotizaciones), normalizado a minúscula antes de llegar al validador del mensaje
        // a publicar (que sí compara con StringComparer.Ordinal).
        var resp = await fixture.Client().PostJson("/api/vehiculos", CuerpoValido("Trasera"));
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Puerta_posicion_invalida_devuelve_400_no_500()
    {
        var resp = await fixture.Client().PostJson("/api/vehiculos", CuerpoValido("Arriba"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await resp.ErrorCode());
    }

    [Fact]
    public async Task Sin_puerta_posicion_devuelve_400()
    {
        var resp = await fixture.Client().PostJson("/api/vehiculos", new
        {
            nombre = "Sencillo 3 ejes",
            largoInternoMm = 12500,
            anchoInternoMm = 2450,
            altoInternoMm = 2700,
            capacidadKg = 34000,
            taraKg = 8000,
            puertaAnchoMm = 2400,
            puertaAltoMm = 2600,
            ejes = new[] { new { posicionMm = 0, capacidadKg = 6000 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await resp.ErrorCode());
    }
}
