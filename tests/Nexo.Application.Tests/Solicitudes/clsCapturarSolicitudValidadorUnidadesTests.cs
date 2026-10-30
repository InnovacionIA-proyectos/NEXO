using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Validadores;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// #304: el Cubicador corta en 10.000 unidades por petición, pero la captura admitía hasta 100.000 por posición: una
/// línea así se guardaba y después el Cubicador rechazaba la recomendación entera, lejos de donde nació el error. Ahora el
/// tope vale al capturar, para la SUMA de las posiciones de la línea.
/// </summary>
public class clsCapturarSolicitudValidadorUnidadesTests
{
    private static clsCapturarSolicitudCommand Comando(params clsPosicionCargaDto[] posiciones) => new(
        ClienteId: Guid.NewGuid(), ClienteBp: null, Tipo: "spot", Modalidad: null, Deadline: null, ValorEstimado: null,
        Referencia: null, HorasCargue: null, HorasDescargue: null,
        Lineas:
        [
            new clsCapturaLineaInput(
                "Bogotá", "Medellín", "3S3", null, null, null, null, null, null, null, null, null, null, null, null,
                Peso: null, ValorMercancia: null, Posiciones: posiciones),
        ]);

    private static clsPosicionCargaDto Posicion(int cantidad, decimal pesoKg = 20m) =>
        new("Cajas", cantidad, 40m, 30m, 30m, pesoKg, "CAJAS_CARTON");

    [Fact]
    public void Una_linea_en_el_tope_de_10000_unidades_se_acepta()
    {
        clsCapturarSolicitudValidador.Validar(Comando(Posicion(6000), Posicion(4000)));
    }

    [Fact]
    public void Una_linea_con_una_unidad_de_mas_se_rechaza_con_el_mismo_codigo_del_Cubicador()
    {
        var ex = Assert.Throws<clsValidationFailedException>(
            () => clsCapturarSolicitudValidador.Validar(Comando(Posicion(6000), Posicion(4001))));

        Assert.Equal("CUBICACION_DEMASIADO_GRANDE", ex.Code);
        Assert.Contains("10.000", ex.Message);
        Assert.Contains("línea 1", ex.Message);
    }

    // Una posición sola de 100.000 unidades (lo que antes pasaba el [Range]) también cae.
    [Fact]
    public void Una_sola_posicion_enorme_se_rechaza()
    {
        var ex = Assert.Throws<clsValidationFailedException>(
            () => clsCapturarSolicitudValidador.Validar(Comando(Posicion(100_000))));

        Assert.Equal("CUBICACION_DEMASIADO_GRANDE", ex.Code);
    }

    // Sin posiciones no hay unidades que contar: la línea se captura solo con su peso total, como siempre.
    [Fact]
    public void Una_linea_sin_posiciones_no_se_ve_afectada()
    {
        var comando = Comando() with
        {
            Lineas = [Comando().Lineas[0] with { Posiciones = null, Peso = 18_000m }],
        };

        clsCapturarSolicitudValidador.Validar(comando);
    }
}
