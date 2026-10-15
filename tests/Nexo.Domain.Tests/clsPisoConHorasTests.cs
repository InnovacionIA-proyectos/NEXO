using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Domain.Tests;

public class clsPisoConHorasTests
{
    // S4-HU-14.1/#221: solicitud con horas por defecto (4h+4h=8h, igual a HorasPactadas)
    // no cambia de valor frente al ValorSiceConHora que ya trae DWSice — regresión.
    [Fact]
    public void Horas_iguales_a_las_pactadas_devuelve_el_fallback_sin_recalcular()
    {
        var valor = clsPisoConHoras.Calcular(valorBase: 1_000_000m, valorHora: 50_000m, horasPactadas: 8m, valorSiceConHora: 1_400_000m, horasSolicitud: 8m);

        Assert.Equal(1_400_000m, valor); // el fallback, no 1_000_000 + 50_000*8 = 1_400_000 (aunque coincide, no pasa por la cuenta)
    }

    [Fact]
    public void Horas_distintas_a_las_pactadas_recalcula_valor_mas_valorHora_por_horas()
    {
        // 6h+6h=12h en vez de las 8h pactadas.
        var valor = clsPisoConHoras.Calcular(valorBase: 1_000_000m, valorHora: 50_000m, horasPactadas: 8m, valorSiceConHora: 1_400_000m, horasSolicitud: 12m);

        Assert.Equal(1_600_000m, valor); // 1_000_000 + 50_000*12
    }

    [Fact]
    public void Sin_horas_de_solicitud_usa_el_fallback()
    {
        var valor = clsPisoConHoras.Calcular(valorBase: 1_000_000m, valorHora: 50_000m, horasPactadas: 8m, valorSiceConHora: 1_400_000m, horasSolicitud: null);

        Assert.Equal(1_400_000m, valor);
    }

    [Fact]
    public void Sin_valorHora_usa_el_fallback_aunque_las_horas_difieran()
    {
        var valor = clsPisoConHoras.Calcular(valorBase: 1_000_000m, valorHora: null, horasPactadas: 8m, valorSiceConHora: 1_400_000m, horasSolicitud: 12m);

        Assert.Equal(1_400_000m, valor);
    }

    [Fact]
    public void Sin_horasPactadas_usa_el_fallback_aunque_haya_valorHora()
    {
        var valor = clsPisoConHoras.Calcular(valorBase: 1_000_000m, valorHora: 50_000m, horasPactadas: null, valorSiceConHora: 1_400_000m, horasSolicitud: 12m);

        Assert.Equal(1_400_000m, valor);
    }

    [Fact]
    public void Sin_valorSiceConHora_el_fallback_es_el_valor_base()
    {
        var valor = clsPisoConHoras.Calcular(valorBase: 1_000_000m, valorHora: null, horasPactadas: 8m, valorSiceConHora: null, horasSolicitud: null);

        Assert.Equal(1_000_000m, valor);
    }

    // #300: tramo vacío (levante / retorno) = 1+1 = 2 h. Con horas pactadas de 8 h, el piso se recalcula con 2 h:
    // base + valorHora × 2, no el ValorSiceConHora de las 8 h (que sobrestimaba el tramo vacío).
    [Fact]
    public void Tramo_vacio_con_2_horas_recalcula_el_piso_con_2_horas_y_no_con_las_8_pactadas()
    {
        var valor = clsPisoConHoras.Calcular(
            valorBase: 1_000_000m, valorHora: 50_000m, horasPactadas: 8m, valorSiceConHora: 1_400_000m,
            horasSolicitud: clsResolucionHorasLinea.HorasTotalesTramoVacio);

        Assert.Equal(1_100_000m, valor); // 1_000_000 + 50_000 × 2
    }

    // Filas VACIO de DWSice (Sin carga) traen ValorHora = 0: las horas no mueven el valor.
    [Fact]
    public void Tramo_vacio_con_valorHora_cero_deja_el_valor_base()
    {
        var valor = clsPisoConHoras.Calcular(
            valorBase: 5_877_577m, valorHora: 0m, horasPactadas: 8m, valorSiceConHora: 5_877_577m,
            horasSolicitud: clsResolucionHorasLinea.HorasTotalesTramoVacio);

        Assert.Equal(5_877_577m, valor);
    }
}
