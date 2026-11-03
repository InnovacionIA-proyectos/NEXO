using Nexo.Application.CasosDeUso.Metricas;
using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Exceptions;
using Moq;

namespace Nexo.Application.Tests.Metricas;

/// <summary>S5-HU-21.1/#287: corte del tiempo de costeo por tamaño de pliego y paso de
/// parámetros del Tablero hacia el puerto.</summary>
public class clsTamanoPliegoTests
{
    [Theory]
    [InlineData("1-10", TamanoPliego.Hasta10)]
    [InlineData("11-100", TamanoPliego.De11A100)]
    [InlineData("+100", TamanoPliego.Mas100)]
    [InlineData(" 100", TamanoPliego.Mas100)]    // un "+" sin codificar en la URL llega como espacio
    [InlineData("100+", TamanoPliego.Mas100)]
    [InlineData("mas100", TamanoPliego.Mas100)]
    [InlineData(">100", TamanoPliego.Mas100)]
    public void Parsea_los_tres_tramos_y_sus_variantes(string texto, TamanoPliego esperado) =>
        Assert.Equal(esperado, clsTamanoPliego.Parsear(texto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_valor_significa_todos(string? texto) => Assert.Null(clsTamanoPliego.Parsear(texto));

    [Theory]
    [InlineData("grande")]
    [InlineData("0-10")]
    [InlineData("5-50")]
    public void Un_valor_desconocido_es_error_del_cliente_no_se_cae_a_todos(string texto)
    {
        var ex = Assert.Throws<clsValidationFailedException>(() => clsTamanoPliego.Parsear(texto));
        Assert.Equal("TAMANO_INVALIDO", ex.Code);
    }

    [Theory]
    [InlineData(TamanoPliego.Hasta10, 0, false)]
    [InlineData(TamanoPliego.Hasta10, 1, true)]
    [InlineData(TamanoPliego.Hasta10, 10, true)]
    [InlineData(TamanoPliego.Hasta10, 11, false)]
    [InlineData(TamanoPliego.De11A100, 10, false)]
    [InlineData(TamanoPliego.De11A100, 11, true)]
    [InlineData(TamanoPliego.De11A100, 100, true)]
    [InlineData(TamanoPliego.De11A100, 101, false)]
    [InlineData(TamanoPliego.Mas100, 100, false)]
    [InlineData(TamanoPliego.Mas100, 101, true)]
    public void Los_limites_de_cada_tramo_no_se_solapan(TamanoPliego tramo, int lineas, bool dentro) =>
        Assert.Equal(dentro, clsTamanoPliego.Contiene(tramo, lineas));

    [Fact]
    public async Task El_query_de_cumplimiento_pasa_desde_y_hasta_al_puerto()
    {
        var puerto = new Mock<IMetricasPort>();
        var desde = new DateTimeOffset(2031, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var hasta = new DateTimeOffset(2031, 3, 31, 0, 0, 0, TimeSpan.Zero);
        puerto.Setup(p => p.CumplimientoAsync(desde, hasta, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsCumplimientoDto(desde, hasta, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0));

        var dto = await new clsObtenerCumplimientoQueryHandler(puerto.Object).Handle(new clsObtenerCumplimientoQuery(desde, hasta));

        Assert.Equal((desde, hasta), (dto.Desde, dto.Hasta));
    }

    [Fact]
    public async Task El_query_de_kpis_traduce_el_texto_de_tamano_al_tramo()
    {
        var puerto = new Mock<IMetricasPort>();
        puerto.Setup(p => p.KpisPilotoAsync(null, null, TamanoPliego.De11A100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsKpisPilotoDto(null, null, 0, null, null, 0, null, 0, 0, null, 0, 0, "11-100"));

        var dto = await new clsObtenerKpisPilotoQueryHandler(puerto.Object).Handle(new clsObtenerKpisPilotoQuery(null, null, "11-100"));

        Assert.Equal("11-100", dto.Tamano);
    }

    [Fact]
    public async Task Un_tamano_invalido_falla_antes_de_llegar_al_puerto()
    {
        var puerto = new Mock<IMetricasPort>();

        await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            new clsObtenerKpisPilotoQueryHandler(puerto.Object).Handle(new clsObtenerKpisPilotoQuery(null, null, "xx")));

        puerto.VerifyNoOtherCalls();
    }
}
