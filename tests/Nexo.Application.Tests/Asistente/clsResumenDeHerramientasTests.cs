using System.Text.Json;
using Moq;
using Nexo.Application.CasosDeUso.Asistente;
using Nexo.Application.CasosDeUso.Asistente.Herramientas;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.Tests.Asistente;

/// <summary>El resumen que viaja en el evento de herramienta lo arma NEXO a partir del resultado: nombra la configuración del piso y no trae el resultado completo.</summary>
public class clsResumenDeHerramientasTests
{
    private static JsonElement Entrada(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task El_resumen_del_piso_nombra_la_configuracion_el_valor_y_el_periodo()
    {
        var propuestas = new Mock<IPropuestaTarifaPort>();
        propuestas.Setup(p => p.PisoSicetacVigenteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TipologiaCarga?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPisoSicetacVigenteDto(202610, 3_422_777m, 40_000m, 8m, "3S3", "Carga general", "Tractocamión", "Cargado"));

        var r = await new clsConsultarPisoSicetacHerramienta(propuestas.Object).EjecutarAsync(Entrada("""{"origen":"Cali","destino":"Barranquilla"}"""));

        Assert.True(r.Encontrado);
        Assert.Equal("Piso SICE-TAC 3S3 (Carga general, Cargado): $3.422.777 · período 202610", r.Resumen);
    }

    [Fact]
    public async Task El_resumen_del_piso_sin_datos_es_el_motivo()
    {
        var propuestas = new Mock<IPropuestaTarifaPort>();
        propuestas.Setup(p => p.MotivoSicetacNoUsadoAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var r = await new clsConsultarPisoSicetacHerramienta(propuestas.Object).EjecutarAsync(Entrada("""{"origen":"Cali","destino":"Barranquilla"}"""));

        Assert.False(r.Encontrado);
        Assert.Equal("No hay piso SICE-TAC vigente para esa ruta y configuración.", r.Resumen);
    }

    [Fact]
    public void El_resumen_no_se_serializa_hacia_el_modelo()
    {
        var resultado = clsResultadoHerramienta.Con(new { valor = 1 }, "fuente", resumen: "Frase para la pantalla");

        var json = JsonSerializer.Serialize(resultado, JsonSerializerOptions.Web);

        Assert.DoesNotContain("Frase para la pantalla", json);
    }
}
