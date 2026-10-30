using Nexo.Mensajes.Priorizacion;

namespace Nexo.Api.Tests.Mensajeria;

/// <summary>
/// El reloj en sí: que suene, que pida el recálculo y que se vuelva a poner.
///
/// Es la única clase con intervalo corto, y por eso la única que tarda: el arnés de
/// MassTransit espera a que el bus se quede quieto para detenerse, y un tic recurrente
/// nunca lo deja. Se paga una vez, aquí, en vez de en cada prueba de la cadena.
///
/// Lo que se afirma es que se REPROGRAMA. Sin eso sería una alarma que suena una sola vez,
/// y el síntoma en producción sería un único recálculo tras cada reinicio del API — algo
/// que en desarrollo no se distingue de que funcione.
/// </summary>
[Trait("Categoria", "Saga")]
public class clsPriorizacionRelojTests() : clsPriorizacionSagaBase(TimeSpan.FromMilliseconds(300))
{
    [Fact]
    public async Task El_tic_pide_el_recalculo_y_se_vuelve_a_programar()
    {
        await Arrancar();

        // Dos vueltas: con una sola no se distingue un reloj de una alarma.
        var estado = await EsperarSaga(s => s.RecalculosPorReloj >= 2);

        Assert.True(PedidosDeRecalculo(clsCadenaDePriorizacion.PorElReloj) >= 2,
            $"Se esperaban al menos 2 pedidos por reloj y hubo {PedidosDeRecalculo(clsCadenaDePriorizacion.PorElReloj)}.");
        Assert.NotNull(estado.TokenTic);
        Assert.Equal("Programado", estado.CurrentState);
        Assert.NotNull(estado.UltimoRecalculoEn);
    }

    [Fact]
    public async Task El_reloj_sigue_andando_despues_de_un_cambio_de_parametros()
    {
        // Recalibrar no debe detener la cadena. Si el disparador manual consumiera el
        // temporizador, el reloj se pararía tras la primera recalibración y nadie lo
        // notaría hasta que los reportes se quedaran viejos.
        await Arrancar();
        await EsperarSaga(s => s.RecalculosPorReloj >= 1);

        await Bus.Bus.Publish(new clsParametrosDePriorizacionCambiaron(
            Cadena, Guid.NewGuid(), DateTimeOffset.UtcNow));
        var trasElCambio = await EsperarSaga(s => s.RecalculosPorParametros >= 1);
        var relojEnEseMomento = trasElCambio.RecalculosPorReloj;

        var despues = await EsperarSaga(s => s.RecalculosPorReloj > relojEnEseMomento);

        Assert.True(despues.RecalculosPorReloj > relojEnEseMomento);
    }
}
