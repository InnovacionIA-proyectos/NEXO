using CabinaFletes.Application.Common.Mensajeria;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.CasosDeUso.Costeo.Interfaces;
using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Infrastructure.Bandeja.Adaptadores;
using CabinaFletes.Infrastructure.Notificaciones.Adaptadores;
using CabinaFletes.Infrastructure.Costeo.Adaptadores;
using CabinaFletes.Infrastructure.Costeo.Repositorios;
using CabinaFletes.Infrastructure.Solicitudes.Adaptadores;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Infrastructure.Solicitudes.Repositorios;
using CabinaFletes.Infrastructure.Trabajos.Adaptadores;
using CabinaFletes.Infrastructure.Priorizacion.Adaptadores;
using CabinaFletes.Infrastructure.Trabajos.Repositorios;
using CabinaFletes.Mensajes.Bandeja;
using CabinaFletes.Mensajes.Notificaciones;
using CabinaFletes.Mensajes.Costeo;
using CabinaFletes.Mensajes.Cubicacion;
using CabinaFletes.Mensajes.Priorizacion;
using CabinaFletes.Mensajes.Solicitudes;
using CabinaFletes.Infrastructure.Cubicacion;
using CabinaFletes.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure.Common.Mensajeria;

/// <summary>
/// Registro de la mensajería. Vive en Infrastructure y se expone como un solo método
/// para que cualquier host lo llame igual: hoy el proceso de la Api, mañana un
/// proyecto de workers. Ninguno de los dos configura MassTransit por su cuenta.
/// </summary>
public static class MensajeriaDependencyInjection
{
    public static IServiceCollection AddMensajeria(this IServiceCollection services, IConfiguration config)
    {
        var host = config.GetConnectionString("RabbitMq") ?? config["RabbitMq:Host"];

        // Se registra el INTERVALO, no la máquina de estados: MassTransit construye la
        // máquina con el contenedor y su registro gana sobre cualquiera que pongamos
        // nosotros. Pasarle el intervalo resuelto deja la saga instanciable en una prueba
        // con 300 ms, sin armarle un IConfiguration.
        services.AddSingleton(new IntervaloDeRecalculo(
            SagaDePriorizacion.IntervaloDe(
                ConfiguracionDeMensajeria.EnteroDeConfig(config, SagaDePriorizacion.ClaveIntervalo, SagaDePriorizacion.MinutosPorDefecto))));

        services.AddMassTransit(x =>
        {
            // Los timeouts de saga se programan por el broker, no con un temporizador
            // en memoria: así el reloj sobrevive a un reinicio del proceso. Necesita el
            // plugin de retardo, de ahí la imagen propia de RabbitMQ.
            x.AddDelayedMessageScheduler();

            // Reparte el pliego en un mensaje por línea. Existe para que la saga no
            // tenga que leer la base y siga siendo pura.
            x.AddConsumer<RepartidorDeLineasConsumer>();

            // Costea una línea. Los reintentos son suyos: si una fuente falla, la
            // excepción sube y la política escalonada del bus se encarga.
            x.AddConsumer<CostearLineaConsumer>();

            // Y cuando esos reintentos se agotan, esto convierte el fallo en un
            // desenlace que la saga puede contar. Sin él, el pliego esperaría esa línea
            // hasta el timeout sin que nadie supiera que había fallado.
            x.AddConsumer<FalloDeCosteoConsumer>();

            // Recosteo: cuando el Administrador de Tarifas carga o extiende tarifas, las
            // líneas que estaban esperando vuelven a la cola. No van por la saga: ese
            // trabajo ya cerró, y cerró en Parcial con razón.
            x.AddConsumer<RecosteoPorTarifasConsumer>();
            x.AddConsumer<RecostearLineaConsumer>();

            // Empuja el avance a quien esté mirando el trabajo. Lee el estado en vez de
            // confiar en el mensaje: así el aviso y la consulta de respaldo salen de la
            // misma fuente y no pueden decir cosas distintas.
            x.AddConsumer<AvisarTrabajoConsumer>();

            // Issue #13: alerta al equipo cuando un mensaje agota reintentos y cae a la
            // cola de error (ver AlertaDeFalloConsumer<T>). MassTransit 8.5 no tiene forma
            // de registrar un Fault<T> genérico abierto contra IRegistrationConfigurator
            // (AddConsumer(Type) solo acepta tipos CERRADOS, se verificó por reflexión y
            // se confirmó en runtime: revienta con "Type.ContainsGenericParameters is
            // true"), así que toca una línea por tipo de mensaje - igual que los
            // validadores de abajo. Si se agrega un mensaje nuevo y se olvida esta lista,
            // ese mensaje simplemente no genera alerta (sin excepción, sin romper nada) -
            // por eso el criterio de cierre de la #13 debe verificar esto explícitamente,
            // no asumir que es automático.
            x.AddConsumer<AlertaDeFalloConsumer<AutoCosteoSolicitado>>();
            x.AddConsumer<AlertaDeFalloConsumer<RepartirLineasDeCosteo>>();
            x.AddConsumer<AlertaDeFalloConsumer<PliegoRepartido>>();
            x.AddConsumer<AlertaDeFalloConsumer<CostearLineaSolicitada>>();
            x.AddConsumer<AlertaDeFalloConsumer<LineaCosteada>>();
            x.AddConsumer<AlertaDeFalloConsumer<LineaFalloTecnico>>();
            x.AddConsumer<AlertaDeFalloConsumer<LineaRequiereTarifaManual>>();
            x.AddConsumer<AlertaDeFalloConsumer<TarifasActualizadas>>();
            x.AddConsumer<AlertaDeFalloConsumer<RecostearLineaSolicitada>>();
            x.AddConsumer<AlertaDeFalloConsumer<TrabajoActualizado>>();
            x.AddConsumer<AlertaDeFalloConsumer<ExpiracionAutoCosteo>>();

            // Issue #28, S2-HU-075: puente hacia Cubicador (microservicio Python,
            // cubicador/, mismo repo). MassTransit real (decisión del equipo: no la
            // aproximación previa con RabbitMQ.Client directo) - ver
            // docs/mensajeria/CUBICACION.md para el porqué de la topología manual de
            // abajo (Cubicador usa un exchange/routing-keys propios, no el
            // exchange-por-tipo que MassTransit crearía por defecto).
            x.AddConsumer<PlanDeCargueGeneradoConsumer>();
            x.AddConsumer<CubicacionRechazadaConsumer>();
            x.AddConsumer<AlertaDeFalloConsumer<PlanDeCargueGenerado>>();
            x.AddConsumer<AlertaDeFalloConsumer<CubicacionRechazada>>();

            // Refresca el score persistido de las activas cuando la cadena de
            // priorización lo pide. La excepción sube: si esto deja de correr, los
            // reportes empiezan a mentir y hace falta que alguien se entere.
            x.AddConsumer<RecalcularScoresConsumer>();

            // Empuja a quien tenga la bandeja abierta que vuelva a consultarla. El fallo
            // se registra y se sigue: que alguien cierre una pestaña no es una avería.
            x.AddConsumer<AvisarBandejaConsumer>();

            // Manda el correo de una cotización entregada. La excepción sube: una
            // notificación perdida no se recupera consultando nada y nadie se enteraría.
            x.AddConsumer<NotificarEntregaConsumer>();

            // La saga se persiste en la misma base que los datos del negocio. Su estado
            // ES la respuesta al cliente que preguntó por su trabajo, así que un
            // reinicio no puede borrarlo.
            x.AddSagaStateMachine<AutoCosteoSaga, EstadoAutoCosteo>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<CabinaFletesDbContext>();
                    // Bloqueo pesimista: dos resultados de línea del mismo pliego pueden
                    // llegar a la vez, y con optimista se perderían contadores.
                    r.ConcurrencyMode = MassTransit.ConcurrencyMode.Pessimistic;
                });

            // El reloj del recálculo de scores. Reemplaza al PeriodicTimer que vivía en
            // el proceso del API: la fila de esta saga es el candado que impide que haya
            // dos cadenas, cosa que con un temporizador por proceso era imposible de
            // garantizar en cuanto hubiera más de una réplica.
            x.AddSagaStateMachine<SagaDePriorizacion, EstadoPriorizacion>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<CabinaFletesDbContext>();
                    // Pesimista por el mismo motivo que la otra: un tic del reloj y un
                    // cambio de parámetros pueden caer sobre la misma fila a la vez.
                    r.ConcurrencyMode = MassTransit.ConcurrencyMode.Pessimistic;
                });

            // El Outbox es lo que hace que esto sea confiable y no solo asíncrono.
            // Escribe el mensaje en la MISMA transacción que los datos del caso de uso.
            x.AddEntityFrameworkOutbox<CabinaFletesDbContext>(o =>
            {
                o.UseSqlServer();

                // Sin esto, Publish() enviaría directo al broker y perderíamos la
                // atomicidad: es la línea que convierte "publicar" en "encolar dentro
                // de la transacción".
                o.UseBusOutbox();

                // Cada cuánto revisa la tabla de salida por mensajes sin entregar.
                o.QueryDelay = TimeSpan.FromSeconds(5);
            });

            // La tubería es la misma con broker y sin él. Se escribe una sola vez para
            // que no puedan divergir: una prueba que corre sobre una tubería distinta
            // de la de producción afirma menos de lo que parece.
            //
            // `excluirDeAutoConfigurar`: consumers cuyo receive endpoint YA se configuró
            // a mano (issue #28 - PlanDeCargueGeneradoConsumer/CubicacionRechazadaConsumer,
            // enganchados al exchange propio de Cubicador, no al de MassTransit por tipo).
            // Sin excluirlos, ConfigureEndpoints intenta crearles OTRO endpoint con el
            // mismo nombre por defecto y revienta con "A receive endpoint with the same
            // key was already added" - se confirmó en runtime, no es una suposición.
            void Tuberia<T>(IBusRegistrationContext contexto, IBusFactoryConfigurator<T> cfg, params Type[] excluirDeAutoConfigurar)
                where T : IReceiveEndpointConfigurator
            {
                // Registrar el planificador NO basta: el bus tiene que usarlo en su
                // tubería. Sin esta línea la actividad Schedule de la saga falla con
                // "MessageSchedulerContext payload not found" — se descubrió en las
                // pruebas de la saga, no desplegado.
                cfg.UseDelayedMessageScheduler();

                // Reintentos escalonados: un fallo transitorio del broker o de una
                // fuente externa no debe tumbar el mensaje al primer intento. Agotados
                // los intentos, MassTransit lo lleva a la cola de error (DLQ), que es
                // donde la #13 conectará la alerta al equipo.
                cfg.UseMessageRetry(r =>
                {
                    // NO SE REINTENTA LO QUE NO TIENE ARREGLO. Un adjunto que pesa más de
                    // lo permitido, o un correo que el relay rechaza por su forma, va a
                    // fallar igual las cuatro veces: reintentarlo solo retrasa la alerta
                    // hasta un minuto y medio. Marcado así, va derecho a la cola de error
                    // y el equipo se entera al primer intento.
                    r.Ignore<CorreoRechazadoException>();

                    r.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15),
                        TimeSpan.FromSeconds(60));
                });

                if (excluirDeAutoConfigurar.Length == 0)
                    cfg.ConfigureEndpoints(contexto);
                else
                    cfg.ConfigureEndpoints(contexto, f => f.Exclude(excluirDeAutoConfigurar));
            }

            // SIN BROKER CONFIGURADO, EL TRANSPORTE VA EN MEMORIA.
            //
            // No es una comodidad: sin esto el bus intenta conectarse a RabbitMQ, no
            // lo consigue y se queda en "not started". El chequeo de salud del bus
            // pasa a Unhealthy y /health responde 503 aunque el API atienda bien —
            // que es exactamente lo que llevaba pasando en las pruebas desde la #14
            // sin que nadie lo notara, porque la suite de Api.Tests no se corría
            // completa.
            //
            // Arrancar sin broker en Producción sería perder mensajes en silencio, y
            // eso lo impide la guarda de Program.cs, con el mismo criterio fail-closed
            // que la de la identidad del portal (S2-HU-03.3).
            if (string.IsNullOrWhiteSpace(host))
                x.UsingInMemory((contexto, cfg) => Tuberia(contexto, cfg));
            else
                x.UsingRabbitMq((contexto, cfg) =>
                {
                    cfg.Host(new Uri(host));

                    // Cubicador declara su propio exchange topic (`cubicador.eventos`) con
                    // routing keys semánticas - no el exchange-por-tipo que MassTransit usa
                    // por defecto. Se anula esa convención SOLO para estos 3 tipos, tanto
                    // para publicar (CubicacionSolicitada) como para consumir (los otros 2):
                    // el resto de la mensajería de NEXO sigue con la convención por defecto.
                    // "topic" es un string plano (no un enum) a propósito: MassTransit
                    // expone el tipo de exchange como string en su API de topología, sin
                    // depender de RabbitMQ.Client directo - todo el puente usa solamente
                    // las abstracciones de MassTransit, ninguna API de bajo nivel.
                    cfg.Message<CubicacionSolicitada>(m => m.SetEntityName("cubicador.eventos"));
                    cfg.Publish<CubicacionSolicitada>(p => p.ExchangeType = "topic");

                    // Nombres con prefijo "Cubicacion." a propósito: el nombre de endpoint
                    // por defecto de AlertaDeFalloConsumer<T> es el nombre del TIPO de
                    // mensaje ("PlanDeCargueGenerado"/"CubicacionRechazada", no el de la
                    // clase consumer) - sin el prefijo, colisiona con estos dos endpoints
                    // manuales (mismo nombre, "A receive endpoint with the same key was
                    // already added"), confirmado en runtime.
                    cfg.ReceiveEndpoint("Cubicacion.PlanDeCargueGenerado", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "plan-de-cargue.generado";
                        });
                        e.ConfigureConsumer<PlanDeCargueGeneradoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.CubicacionRechazada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "cubicacion.rechazada";
                        });
                        e.ConfigureConsumer<CubicacionRechazadaConsumer>(contexto);
                    });

                    Tuberia(contexto, cfg, typeof(PlanDeCargueGeneradoConsumer), typeof(CubicacionRechazadaConsumer));
                });
        });

        // Cadena de publicación: el decorador de validación envuelve al adaptador de
        // MassTransit. Quien pida IPublicadorDeMensajes recibe SIEMPRE el validado —
        // el adaptador crudo no se expone bajo el puerto, justamente para que no haya
        // forma de saltarse la validación pidiendo la otra implementación.
        services.AddScoped<ILineasDePliegoPort, EfLineasDePliegoRepository>();
        services.AddScoped<IEstadoDeTrabajoPort, EfEstadoDeTrabajoRepository>();
        services.AddScoped<IRegistroDeTrabajos, EfRegistroDeTrabajos>();
        services.AddScoped<IPendientesDeCosteoPort, EfPendientesDeCosteoRepository>();
        services.AddScoped<ILineasCosteadasPort, EfLineasCosteadasRepository>();
        services.AddScoped<IPropuestaDeLineaPort>(sp => sp.GetRequiredService<IPropuestaTarifaPort>() as IPropuestaDeLineaPort
            ?? throw new InvalidOperationException("El adaptador de propuesta debe implementar IPropuestaDeLineaPort."));
        services.AddScoped<MassTransitPublicador>();
        services.AddScoped<IPublicadorDeMensajes>(sp =>
            new PublicadorValidado(sp.GetRequiredService<MassTransitPublicador>(), sp));
        services.AddScoped<ICubicacionPort, CubicacionPublicadorAdapter>();

        // Los validadores de cada mensaje. Se registran explícitamente en vez de
        // escanear el ensamblado: es una línea por mensaje y se lee de un vistazo qué
        // viaja al broker. Si alguien agrega un mensaje y olvida esta línea, el
        // decorador falla cerrado y la prueba lo detecta.
        services.AddScoped<IValidadorDeMensaje<AutoCosteoSolicitado>, AutoCosteoSolicitadoValidador>();
        services.AddScoped<IValidadorDeMensaje<RepartirLineasDeCosteo>, RepartirLineasDeCosteoValidador>();
        services.AddScoped<IValidadorDeMensaje<PliegoRepartido>, PliegoRepartidoValidador>();
        services.AddScoped<IValidadorDeMensaje<CostearLineaSolicitada>, CostearLineaSolicitadaValidador>();
        services.AddScoped<IValidadorDeMensaje<LineaCosteada>, LineaCosteadaValidador>();
        services.AddScoped<IValidadorDeMensaje<LineaFalloTecnico>, LineaFalloTecnicoValidador>();
        services.AddScoped<IValidadorDeMensaje<LineaRequiereTarifaManual>, LineaRequiereTarifaManualValidador>();
        services.AddScoped<IValidadorDeMensaje<TarifasActualizadas>, TarifasActualizadasValidador>();
        services.AddScoped<IValidadorDeMensaje<RecostearLineaSolicitada>, RecostearLineaSolicitadaValidador>();
        services.AddScoped<IValidadorDeMensaje<TrabajoActualizado>, TrabajoActualizadoValidador>();
        services.AddScoped<IValidadorDeMensaje<CubicacionSolicitada>, CubicacionSolicitadaValidador>();
        services.AddScoped<IValidadorDeMensaje<ArrancarPriorizacion>, ArrancarPriorizacionValidador>();
        services.AddScoped<IValidadorDeMensaje<TicDePriorizacion>, TicDePriorizacionValidador>();
        services.AddScoped<IValidadorDeMensaje<ParametrosDePriorizacionCambiaron>, ParametrosDePriorizacionCambiaronValidador>();
        services.AddScoped<IValidadorDeMensaje<RecalcularScoresSolicitado>, RecalcularScoresSolicitadoValidador>();
        services.AddScoped<IValidadorDeMensaje<BandejaCambio>, BandejaCambioValidador>();
        services.AddScoped<IValidadorDeMensaje<CotizacionEntregada>, CotizacionEntregadaValidador>();

        return services;
    }
}
