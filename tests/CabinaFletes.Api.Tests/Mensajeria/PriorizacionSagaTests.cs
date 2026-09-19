using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Mensajes.Priorizacion;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// La cadena de priorización: armado, rearmado y el disparador por cambio de parámetros.
///
/// LO QUE ESTAS PRUEBAS PROTEGEN es lo que el PeriodicTimer no podía garantizar: que
/// exista UNA sola cadena. Con un temporizador por proceso, dos réplicas del API eran dos
/// relojes haciendo el mismo UPDATE sobre las mismas filas, y con concurrencia optimista
/// uno de los dos perdía. Aquí es imposible por construcción, y esto lo fija para que
/// nadie lo deshaga sin darse cuenta.
///
/// Intervalo largo a propósito: ninguna de estas pruebas necesita que el reloj suene, y
/// dejar que suene hacía que cada una tardara 29 segundos en detener el arnés. El reloj en
/// sí se prueba en <see cref="PriorizacionRelojTests"/>.
/// </summary>
[Trait("Categoria", "Saga")]
public class PriorizacionSagaTests() : PriorizacionSagaBase(TimeSpan.FromHours(1))
{
    [Fact]
    public async Task Arrancar_arma_el_reloj_y_deja_la_cadena_programada()
    {
        var estado = await Arrancar();

        Assert.Equal("Programado", estado.CurrentState);
        Assert.NotNull(estado.TokenTic);
        Assert.NotEqual(default, estado.ArmadaEn);
    }

    [Fact]
    public async Task Arrancar_dos_veces_no_crea_dos_cadenas()
    {
        // ESTA ES LA PRUEBA QUE JUSTIFICA LA SAGA. El CorrelationId es una constante, así
        // que el segundo arranque cae sobre la cadena que ya existe en vez de abrir otra.
        await Arrancar();
        await Bus.Bus.Publish(new ArrancarPriorizacion(Cadena));
        await Bus.Bus.Publish(new ArrancarPriorizacion(Cadena));
        await EsperarSaga(s => s.RearmadaEn > s.ArmadaEn);

        Assert.Single(Saga.Created.Select(s => s.CorrelationId == Cadena));
    }

    [Fact]
    public async Task Rearmar_reemplaza_el_temporizador_en_vez_de_sumar_otro()
    {
        // Si el rearmado sumara temporizadores, la frecuencia se duplicaría en cada
        // arranque del API y nadie lo notaría hasta ver la base trabajando sola. El token
        // nuevo es lo que descarta el anterior cuando llegue.
        var estado = await Arrancar();
        var tokenInicial = estado.TokenTic;

        await Bus.Bus.Publish(new ArrancarPriorizacion(Cadena));
        var rearmada = await EsperarSaga(s => s.TokenTic != tokenInicial);

        Assert.NotNull(rearmada.TokenTic);
        Assert.NotEqual(tokenInicial, rearmada.TokenTic);
        Assert.Equal("Programado", rearmada.CurrentState);
    }

    [Fact]
    public async Task Un_cambio_de_parametros_dispara_el_recalculo_sin_esperar_el_tic()
    {
        // Es el segundo disparador que pide la HU, y el intervalo largo de esta clase lo
        // demuestra sin ambigüedad: si hiciera falta esperar el tic, esta prueba tardaría
        // una hora.
        await Arrancar();

        await Bus.Bus.Publish(new ParametrosDePriorizacionCambiaron(
            Cadena, Guid.NewGuid(), DateTimeOffset.UtcNow));

        var estado = await EsperarSaga(s => s.RecalculosPorParametros > 0);

        Assert.Equal(1, estado.RecalculosPorParametros);
        Assert.Equal(0, estado.RecalculosPorReloj);
        Assert.Equal(1, PedidosDeRecalculo(CadenaDePriorizacion.PorCambioDeParametros));
        Assert.NotNull(estado.UltimoRecalculoEn);
    }

    [Fact]
    public async Task Recalibrar_antes_de_que_exista_la_cadena_la_crea_en_vez_de_fallar()
    {
        // El mensaje puede adelantarse al arranque del API. Sin la rama inicial,
        // MassTransit lo daría por no aceptado, agotaría reintentos y lo mandaría a la
        // cola de error: una alerta al equipo por algo que no está roto.
        await Bus.Bus.Publish(new ParametrosDePriorizacionCambiaron(
            Cadena, Guid.NewGuid(), DateTimeOffset.UtcNow));

        var estado = await EsperarSaga(s => s.CurrentState == "Programado");

        Assert.Equal(1, estado.RecalculosPorParametros);
        Assert.NotNull(estado.TokenTic);
        Assert.Equal(1, PedidosDeRecalculo(CadenaDePriorizacion.PorCambioDeParametros));
    }

    [Fact]
    public async Task Recalibrar_dos_veces_pide_dos_recalculos()
    {
        // No se agrupan. Cada recalibración tiene que reflejarse: si se descartara la
        // segunda por "ya hay uno en curso", el Admin vería la bandeja con los pesos
        // viejos y no sabría por qué.
        await Arrancar();

        for (var i = 0; i < 2; i++)
            await Bus.Bus.Publish(new ParametrosDePriorizacionCambiaron(
                Cadena, Guid.NewGuid(), DateTimeOffset.UtcNow));

        var estado = await EsperarSaga(s => s.RecalculosPorParametros >= 2);

        Assert.Equal(2, estado.RecalculosPorParametros);
        Assert.Equal(2, PedidosDeRecalculo(CadenaDePriorizacion.PorCambioDeParametros));
    }

    [Fact]
    public void El_intervalo_se_acota_a_algo_razonable()
    {
        // Un minuto abajo: por debajo deja de ser un refresco y es carga constante sobre
        // la base. Un día arriba: por encima no refresca nada.
        Assert.Equal(TimeSpan.FromMinutes(1), SagaDePriorizacion.IntervaloDe(0));
        Assert.Equal(TimeSpan.FromMinutes(1), SagaDePriorizacion.IntervaloDe(-30));
        Assert.Equal(TimeSpan.FromMinutes(1440), SagaDePriorizacion.IntervaloDe(99_999));
        Assert.Equal(TimeSpan.FromMinutes(15),
            SagaDePriorizacion.IntervaloDe(SagaDePriorizacion.MinutosPorDefecto));
    }

    [Fact]
    public void La_cadena_no_usa_un_identificador_por_arranque()
    {
        // Si algún día alguien lo cambia por Guid.NewGuid(), la garantía de "una sola
        // cadena" desaparece en silencio y esta prueba es lo único que lo frena.
        Assert.NotEqual(Guid.Empty, CadenaDePriorizacion.Unica);
        Assert.Equal(CadenaDePriorizacion.Unica, CadenaDePriorizacion.Unica);
    }
}
