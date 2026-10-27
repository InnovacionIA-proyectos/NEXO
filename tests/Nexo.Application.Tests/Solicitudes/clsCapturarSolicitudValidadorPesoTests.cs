using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Validadores;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// El peso total de una línea incluye la tara y lo escribe quien captura; las posiciones traen el peso de su mercancía sin tara.
/// Por eso el peso total puede ser mayor que la suma de las posiciones. Solo se rechaza (PESO_INCONSISTENTE) cuando las
/// posiciones suman más que el peso total, con un margen del 1 % por redondeos.
/// </summary>
public class clsCapturarSolicitudValidadorPesoTests
{
    private static clsCapturarSolicitudCommand Comando(decimal? peso, params clsPosicionCargaDto[] posiciones) => new(
        ClienteId: Guid.NewGuid(), ClienteBp: null, Tipo: "spot", Modalidad: null, Deadline: null, ValorEstimado: null,
        Referencia: null, HorasCargue: null, HorasDescargue: null,
        Lineas:
        [
            new clsCapturaLineaInput(
                "Bogotá", "Medellín", "3S3", null, null, null, null, null, null, null, null, null, null, null, null,
                Peso: peso, ValorMercancia: null, Posiciones: posiciones.Length == 0 ? null : posiciones),
        ]);

    /// <summary>Una posición de <paramref name="unidades"/> × <paramref name="pesoKgPorUnidad"/> kg.</summary>
    private static clsPosicionCargaDto Posicion(int unidades, decimal pesoKgPorUnidad) =>
        new("Cajas", unidades, 40m, 30m, 30m, pesoKgPorUnidad, "CAJAS_CARTON");

    [Fact]
    public void Un_peso_total_mayor_que_la_suma_de_las_posiciones_es_la_tara_y_se_acepta()
    {
        clsCapturarSolicitudValidador.Validar(Comando(1000m, Posicion(1, 100m)));
    }

    [Fact]
    public void Un_peso_total_igual_a_la_suma_de_las_posiciones_se_acepta()
    {
        clsCapturarSolicitudValidador.Validar(Comando(500m, Posicion(5, 100m)));
    }

    [Fact]
    public void Las_posiciones_que_suman_mas_que_el_peso_total_se_rechazan_con_PESO_INCONSISTENTE()
    {
        var ex = Assert.Throws<clsValidationFailedException>(
            () => clsCapturarSolicitudValidador.Validar(Comando(1000m, Posicion(12, 100m))));

        Assert.Equal("PESO_INCONSISTENTE", ex.Code);
        Assert.Contains("línea 1", ex.Message);
        Assert.Contains("1200", ex.Message);
        Assert.Contains("1000", ex.Message);
    }

    [Fact]
    public void Un_exceso_de_hasta_el_1_por_ciento_de_las_posiciones_sobre_el_peso_total_se_tolera()
    {
        // 1010 kg de posiciones contra 1000 kg totales: justo el 1 % (redondeos de fuentes distintas).
        clsCapturarSolicitudValidador.Validar(Comando(1000m, Posicion(1, 1010m)));
    }

    [Fact]
    public void Un_exceso_de_mas_del_1_por_ciento_de_las_posiciones_sobre_el_peso_total_se_rechaza()
    {
        var ex = Assert.Throws<clsValidationFailedException>(
            () => clsCapturarSolicitudValidador.Validar(Comando(1000m, Posicion(1, 1011m))));

        Assert.Equal("PESO_INCONSISTENTE", ex.Code);
    }

    [Fact]
    public void Una_linea_sin_posiciones_solo_necesita_su_peso_total()
    {
        clsCapturarSolicitudValidador.Validar(Comando(1000m));
    }

    [Fact]
    public void Sin_peso_total_el_peso_es_la_suma_de_las_posiciones_y_nunca_es_inconsistente()
    {
        clsCapturarSolicitudValidador.Validar(Comando(null, Posicion(3, 100m), Posicion(2, 50m)));
    }

    [Fact]
    public void Sin_peso_total_y_sin_posiciones_falla_con_PESO_REQUERIDO()
    {
        var ex = Assert.Throws<clsValidationFailedException>(() => clsCapturarSolicitudValidador.Validar(Comando(null)));

        Assert.Equal("PESO_REQUERIDO", ex.Code);
    }

    [Fact]
    public void El_peso_total_con_tara_es_el_que_se_guarda_en_la_linea()
    {
        var comando = Comando(1000m, Posicion(1, 100m));

        Assert.Equal(1000m, clsCapturarSolicitudValidador.PesoResuelto(comando.Lineas[0]));
    }
}
