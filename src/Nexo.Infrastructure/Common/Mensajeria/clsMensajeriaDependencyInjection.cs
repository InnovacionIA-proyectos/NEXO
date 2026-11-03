using Nexo.Application.Common.Mensajeria;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Infrastructure.Bandeja.Adaptadores;
using Nexo.Infrastructure.Notificaciones.Adaptadores;
using Nexo.Infrastructure.Costeo.Adaptadores;
using Nexo.Infrastructure.Costeo.Repositorios;
using Nexo.Infrastructure.Solicitudes.Adaptadores;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Infrastructure.Solicitudes.Repositorios;
using Nexo.Infrastructure.Trabajos.Adaptadores;
using Nexo.Infrastructure.Priorizacion.Adaptadores;
using Nexo.Infrastructure.Trabajos.Repositorios;
using Nexo.Mensajes.Bandeja;
using Nexo.Mensajes.Notificaciones;
using Nexo.Mensajes.Costeo;
using Nexo.Mensajes.Cubicacion;
using Nexo.Mensajes.Priorizacion;
using Nexo.Mensajes.Solicitudes;
using Nexo.Infrastructure.Cubicacion;
using Nexo.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Common.Mensajeria;

/// <summary>
/// Registro de la mensajería. Vive en Infrastructure y se expone como un solo método
/// para que cualquier host lo llame igual: hoy el proceso de la Api, mañana un
/// proyecto de workers. Ninguno de los dos configura MassTransit por su cuenta.
/// </summary>
public static class clsMensajeriaDependencyInjection
{
    public static IServiceCollection AddMensajeria(this IServiceCollection services, IConfiguration config)
    {
        var host = config.GetConnectionString("RabbitMq") ?? config["RabbitMq:Host"];

        // Se registra el INTERVALO, no la máquina de estados: MassTransit construye la
        // máquina con el contenedor y su registro gana sobre cualquiera que pongamos
        // nosotros. Pasarle el intervalo resuelto deja la saga instanciable en una prueba
        // con 300 ms, sin armarle un IConfiguration.
        services.AddSingleton(new clsIntervaloDeRecalculo(
            clsSagaDePriorizacion.IntervaloDe(
                clsConfiguracionDeMensajeria.EnteroDeConfig(config, clsSagaDePriorizacion.ClaveIntervalo, clsSagaDePriorizacion.MinutosPorDefecto))));

        services.AddMassTransit(x =>
        {
            // Los timeouts de saga se programan por el broker, no con un temporizador
            // en memoria: así el reloj sobrevive a un reinicio del proceso. Necesita el
            // plugin de retardo, de ahí la imagen propia de RabbitMQ.
            x.AddDelayedMessageScheduler();

            // Nombres de cola/exchange estables pese al prefijo "cls" (ver clsFormateadorNombresSinPrefijoCls).
            x.SetEndpointNameFormatter(new clsFormateadorNombresSinPrefijoCls());

            // Reparte el pliego en un mensaje por línea. Existe para que la saga no
            // tenga que leer la base y siga siendo pura.
            x.AddConsumer<clsRepartidorDeLineasConsumer>();

            // Costea una línea. Los reintentos son suyos: si una fuente falla, la
            // excepción sube y la política escalonada del bus se encarga.
            x.AddConsumer<clsCostearLineaConsumer>();

            // Y cuando esos reintentos se agotan, esto convierte el fallo en un
            // desenlace que la saga puede contar. Sin él, el pliego esperaría esa línea
            // hasta el timeout sin que nadie supiera que había fallado.
            x.AddConsumer<clsFalloDeCosteoConsumer>();

            // Recosteo: cuando el Administrador de Tarifas carga o extiende tarifas, las
            // líneas que estaban esperando vuelven a la cola. No van por la saga: ese
            // trabajo ya cerró, y cerró en Parcial con razón.
            x.AddConsumer<clsRecosteoPorTarifasConsumer>();
            x.AddConsumer<clsRecostearLineaConsumer>();

            // Empuja el avance a quien esté mirando el trabajo. Lee el estado en vez de
            // confiar en el mensaje: así el aviso y la consulta de respaldo salen de la
            // misma fuente y no pueden decir cosas distintas.
            x.AddConsumer<clsAvisarTrabajoConsumer>();

            // Alerta al equipo cuando un mensaje agota reintentos y cae a la cola de
            // error (ver clsAlertaDeFalloConsumer<T>). MassTransit 8.5 no permite registrar
            // un Fault<T> genérico abierto contra IRegistrationConfigurator
            // (AddConsumer(Type) solo acepta tipos CERRADOS: revienta en runtime con
            // "Type.ContainsGenericParameters is true"), así que toca una línea por tipo
            // de mensaje - igual que los validadores de abajo. Si se agrega un mensaje
            // nuevo y se olvida esta lista, ese mensaje simplemente no genera alerta
            // (sin excepción, sin romper nada).
            x.AddConsumer<clsAlertaDeFalloConsumer<clsAutoCosteoSolicitado>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsRepartirLineasDeCosteo>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsPliegoRepartido>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsCostearLineaSolicitada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsLineaCosteada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsLineaFalloTecnico>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsLineaRequiereTarifaManual>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsTarifasActualizadas>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsRecostearLineaSolicitada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsTrabajoActualizado>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsExpiracionAutoCosteo>>();

            // Puente hacia Cubicador (microservicio Python, cubicador/, mismo repo).
            // MassTransit real (decisión del equipo: no la aproximación previa con
            // RabbitMQ.Client directo) - ver docs/mensajeria/CUBICACION.md para el
            // porqué de la topología manual de abajo (Cubicador usa un exchange/
            // routing-keys propios, no el exchange-por-tipo que MassTransit crearía
            // por defecto).
            x.AddConsumer<clsPlanDeCargueGeneradoConsumer>();
            x.AddConsumer<clsCubicacionRechazadaConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<PlanDeCargueGenerado>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<CubicacionRechazada>>();

            // Catálogo de vehículos - mismo puente, misma tubería. Un solo consumer
            // atiende los 2 tipos (ver clsVehiculoCatalogoConsumer), pero MassTransit
            // igual necesita un AddConsumer<T> y un Fault<T> por MENSAJE.
            x.AddConsumer<clsVehiculoCatalogoConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<VehiculoCreado>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<VehiculoActualizado>>();

            // S5-HU-18.5/#277: catálogos de tipo de mercancía y tipo de carrocería, mismo
            // puente, misma tubería que el catálogo de vehículos de arriba.
            x.AddConsumer<clsMercanciaCatalogoConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<MercanciaCreada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<MercanciaActualizada>>();
            x.AddConsumer<clsCarroceriaCatalogoConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<CarroceriaCreada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<CarroceriaActualizada>>();

            // Refresca el score persistido de las activas cuando la cadena de
            // priorización lo pide. La excepción sube: si esto deja de correr, los
            // reportes empiezan a mentir y hace falta que alguien se entere.
            x.AddConsumer<clsRecalcularScoresConsumer>();

            // Empuja a quien tenga la bandeja abierta que vuelva a consultarla. El fallo
            // se registra y se sigue: que alguien cierre una pestaña no es una avería.
            x.AddConsumer<clsAvisarBandejaConsumer>();

            // Manda el correo de una cotización entregada. La excepción sube: una
            // notificación perdida no se recupera consultando nada y nadie se enteraría.
            x.AddConsumer<clsNotificarEntregaConsumer>();

            // Avisa a quien creó la solicitud que otra persona cambió los servicios conexos
            // de su cotización ya entregada (S5-HU-20.1/#286). Misma regla: la excepción sube.
            x.AddConsumer<clsNotificarConexosCambiadosConsumer>();

            // Las 3 alertas de negocio del documento de entendimiento: tarifa vencida,
            // línea sin costear por falla técnica, y cotización ganada. Mismo criterio
            // que arriba: la excepción sube.
            x.AddConsumer<clsNotificarTarifaVencidaConsumer>();
            x.AddConsumer<clsNotificarLineaFalloTecnicoConsumer>();
            x.AddConsumer<clsNotificarCotizacionGanadaConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsTarifaVencidaDetectada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsCotizacionMarcadaGanada>>();

            // La saga se persiste en la misma base que los datos del negocio. Su estado
            // ES la respuesta al cliente que preguntó por su trabajo, así que un
            // reinicio no puede borrarlo.
            x.AddSagaStateMachine<clsAutoCosteoSaga, clsEstadoAutoCosteo>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<clsNexoDbContext>();
                    // Bloqueo pesimista: dos resultados de línea del mismo pliego pueden
                    // llegar a la vez, y con optimista se perderían contadores.
                    r.ConcurrencyMode = MassTransit.ConcurrencyMode.Pessimistic;
                });

            // El reloj del recálculo de scores. Reemplaza al PeriodicTimer que vivía en
            // el proceso del API: la fila de esta saga es el candado que impide que haya
            // dos cadenas, cosa que con un temporizador por proceso era imposible de
            // garantizar en cuanto hubiera más de una réplica.
            x.AddSagaStateMachine<clsSagaDePriorizacion, clsEstadoPriorizacion>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<clsNexoDbContext>();
                    // Pesimista por el mismo motivo que la otra: un tic del reloj y un
                    // cambio de parámetros pueden caer sobre la misma fila a la vez.
                    r.ConcurrencyMode = MassTransit.ConcurrencyMode.Pessimistic;
                });

            // El Outbox es lo que hace que esto sea confiable y no solo asíncrono.
            // Escribe el mensaje en la MISMA transacción que los datos del caso de uso.
            x.AddEntityFrameworkOutbox<clsNexoDbContext>(o =>
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
            // a mano (clsPlanDeCargueGeneradoConsumer/clsCubicacionRechazadaConsumer,
            // enganchados al exchange propio de Cubicador, no al de MassTransit por tipo).
            // Sin excluirlos, ConfigureEndpoints intenta crearles OTRO endpoint con el
            // mismo nombre por defecto y revienta con "A receive endpoint with the same
            // key was already added".
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
                // donde clsAlertaDeFalloConsumer conecta la alerta al equipo.
                cfg.UseMessageRetry(r =>
                {
                    // NO SE REINTENTA LO QUE NO TIENE ARREGLO. Un adjunto que pesa más de
                    // lo permitido, o un correo que el relay rechaza por su forma, va a
                    // fallar igual las cuatro veces: reintentarlo solo retrasa la alerta
                    // hasta un minuto y medio. Marcado así, va derecho a la cola de error
                    // y el equipo se entera al primer intento.
                    r.Ignore<clsCorreoRechazadoException>();

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
            // pasó desapercibido en pruebas porque la suite de Api.Tests no se corría
            // completa.
            //
            // Arrancar sin broker en Producción sería perder mensajes en silencio, y
            // eso lo impide la guarda de clsProgram.cs, con el mismo criterio fail-closed
            // que la de la identidad del portal.
            if (string.IsNullOrWhiteSpace(host))
                x.UsingInMemory((contexto, cfg) => Tuberia(contexto, cfg));
            else
                x.UsingRabbitMq((contexto, cfg) =>
                {
                    cfg.Host(new Uri(host));

                    // Cubicador declara su propio exchange topic (`cubicador.eventos`) con
                    // routing keys semánticas - no el exchange-por-tipo que MassTransit usa
                    // por defecto. Se anula esa convención SOLO para estos 3 tipos, tanto
                    // para publicar (clsCubicacionSolicitada) como para consumir (los otros 2):
                    // el resto de la mensajería de NEXO sigue con la convención por defecto.
                    // "topic" es un string plano (no un enum) a propósito: MassTransit
                    // expone el tipo de exchange como string en su API de topología, sin
                    // depender de RabbitMQ.Client directo - todo el puente usa solamente
                    // las abstracciones de MassTransit, ninguna API de bajo nivel.
                    cfg.Message<clsCubicacionSolicitada>(m => m.SetEntityName("cubicador.eventos"));
                    cfg.Publish<clsCubicacionSolicitada>(p => p.ExchangeType = "topic");

                    // Nombres con prefijo "Cubicacion." a propósito: el nombre de endpoint
                    // por defecto de clsAlertaDeFalloConsumer<T> es el nombre del TIPO de
                    // mensaje ("clsPlanDeCargueGenerado"/"clsCubicacionRechazada", no el de la
                    // clase consumer) - sin el prefijo, colisiona con estos dos endpoints
                    // manuales (mismo nombre, "A receive endpoint with the same key was
                    // already added"), confirmado en runtime.
                    cfg.ReceiveEndpoint("Cubicacion.clsPlanDeCargueGenerado", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "plan-de-cargue.generado";
                        });
                        e.ConfigureConsumer<clsPlanDeCargueGeneradoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.clsCubicacionRechazada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "cubicacion.rechazada";
                        });
                        e.ConfigureConsumer<clsCubicacionRechazadaConsumer>(contexto);
                    });

                    // Mismo patrón, 2 endpoints más porque cada routing key necesita el suyo
                    // propio - MassTransit no permite bindear 2 routing keys distintas a un
                    // mismo ReceiveEndpoint con nombres de cola separados (y sí queremos
                    // colas separadas: un fallo en vehiculo.creado no debe bloquear
                    // vehiculo.actualizado ni viceversa).
                    cfg.ReceiveEndpoint("Cubicacion.clsVehiculoCreado", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "vehiculo.creado";
                        });
                        e.ConfigureConsumer<clsVehiculoCatalogoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.clsVehiculoActualizado", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "vehiculo.actualizado";
                        });
                        e.ConfigureConsumer<clsVehiculoCatalogoConsumer>(contexto);
                    });
                    // Tercer endpoint del catálogo: sin este binding, `eliminar_vehiculo`
                    // publicaría al exchange y el mensaje se descartaría EN SILENCIO (topic
                    // exchange sin bind que calce = ni error ni entrega), dejando la
                    // proyección con vehículos que ya no existen.
                    cfg.ReceiveEndpoint("Cubicacion.clsVehiculoEliminado", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "vehiculo.eliminado";
                        });
                        e.ConfigureConsumer<clsVehiculoCatalogoConsumer>(contexto);
                    });

                    // S5-HU-18.5/#277: mismo patrón de 3 endpoints por catálogo, x2
                    // (mercancía/carrocería) — cada routing key necesita el suyo propio,
                    // mismo motivo que el trío de vehículo de arriba.
                    cfg.ReceiveEndpoint("Cubicacion.clsMercanciaCreada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "mercancia.creada";
                        });
                        e.ConfigureConsumer<clsMercanciaCatalogoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.clsMercanciaActualizada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "mercancia.actualizada";
                        });
                        e.ConfigureConsumer<clsMercanciaCatalogoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.clsMercanciaEliminada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "mercancia.eliminada";
                        });
                        e.ConfigureConsumer<clsMercanciaCatalogoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.clsCarroceriaCreada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "carroceria.creada";
                        });
                        e.ConfigureConsumer<clsCarroceriaCatalogoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.clsCarroceriaActualizada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "carroceria.actualizada";
                        });
                        e.ConfigureConsumer<clsCarroceriaCatalogoConsumer>(contexto);
                    });
                    cfg.ReceiveEndpoint("Cubicacion.clsCarroceriaEliminada", (IRabbitMqReceiveEndpointConfigurator e) =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind("cubicador.eventos", b =>
                        {
                            b.ExchangeType = "topic";
                            b.RoutingKey = "carroceria.eliminada";
                        });
                        e.ConfigureConsumer<clsCarroceriaCatalogoConsumer>(contexto);
                    });

                    Tuberia(
                        contexto, cfg,
                        typeof(clsPlanDeCargueGeneradoConsumer), typeof(clsCubicacionRechazadaConsumer),
                        typeof(clsVehiculoCatalogoConsumer), typeof(clsMercanciaCatalogoConsumer), typeof(clsCarroceriaCatalogoConsumer));
                });
        });

        // Cadena de publicación: el decorador de validación envuelve al adaptador de
        // MassTransit. Quien pida IPublicadorDeMensajes recibe SIEMPRE el validado —
        // el adaptador crudo no se expone bajo el puerto, justamente para que no haya
        // forma de saltarse la validación pidiendo la otra implementación.
        services.AddScoped<ILineasDePliegoPort, clsEfLineasDePliegoRepository>();
        services.AddScoped<IEstadoDeTrabajoPort, clsEfEstadoDeTrabajoRepository>();
        services.AddScoped<IRegistroDeTrabajos, clsEfRegistroDeTrabajos>();
        services.AddScoped<IPendientesDeCosteoPort, clsEfPendientesDeCosteoRepository>();
        services.AddScoped<ILineasCosteadasPort, clsEfLineasCosteadasRepository>();
        services.AddScoped<IPropuestaDeLineaPort>(sp => sp.GetRequiredService<IPropuestaTarifaPort>() as IPropuestaDeLineaPort
            ?? throw new InvalidOperationException("El adaptador de propuesta debe implementar IPropuestaDeLineaPort."));
        services.AddScoped<clsMassTransitPublicador>();
        services.AddScoped<IPublicadorDeMensajes>(sp =>
            new clsPublicadorValidado(sp.GetRequiredService<clsMassTransitPublicador>(), sp));
        services.AddScoped<ICubicacionPort, clsCubicacionPublicadorAdapter>();

        // Los validadores de cada mensaje. Se registran explícitamente en vez de
        // escanear el ensamblado: es una línea por mensaje y se lee de un vistazo qué
        // viaja al broker. Si alguien agrega un mensaje y olvida esta línea, el
        // decorador falla cerrado y la prueba lo detecta.
        services.AddScoped<IValidadorDeMensaje<clsAutoCosteoSolicitado>, clsAutoCosteoSolicitadoValidador>();
        services.AddScoped<IValidadorDeMensaje<clsRepartirLineasDeCosteo>, clsRepartirLineasDeCosteoValidador>();
        services.AddScoped<IValidadorDeMensaje<clsPliegoRepartido>, clsPliegoRepartidoValidador>();
        services.AddScoped<IValidadorDeMensaje<clsCostearLineaSolicitada>, clsCostearLineaSolicitadaValidador>();
        services.AddScoped<IValidadorDeMensaje<clsLineaCosteada>, clsLineaCosteadaValidador>();
        services.AddScoped<IValidadorDeMensaje<clsLineaFalloTecnico>, clsLineaFalloTecnicoValidador>();
        services.AddScoped<IValidadorDeMensaje<clsLineaRequiereTarifaManual>, clsLineaRequiereTarifaManualValidador>();
        services.AddScoped<IValidadorDeMensaje<clsTarifasActualizadas>, clsTarifasActualizadasValidador>();
        services.AddScoped<IValidadorDeMensaje<clsRecostearLineaSolicitada>, clsRecostearLineaSolicitadaValidador>();
        services.AddScoped<IValidadorDeMensaje<clsTrabajoActualizado>, clsTrabajoActualizadoValidador>();
        services.AddScoped<IValidadorDeMensaje<clsCubicacionSolicitada>, clsCubicacionSolicitadaValidador>();
        services.AddScoped<IValidadorDeMensaje<clsArrancarPriorizacion>, clsArrancarPriorizacionValidador>();
        services.AddScoped<IValidadorDeMensaje<clsTicDePriorizacion>, clsTicDePriorizacionValidador>();
        services.AddScoped<IValidadorDeMensaje<clsParametrosDePriorizacionCambiaron>, clsParametrosDePriorizacionCambiaronValidador>();
        services.AddScoped<IValidadorDeMensaje<clsRecalcularScoresSolicitado>, clsRecalcularScoresSolicitadoValidador>();
        services.AddScoped<IValidadorDeMensaje<clsBandejaCambio>, clsBandejaCambioValidador>();
        services.AddScoped<IValidadorDeMensaje<clsCotizacionEntregada>, clsCotizacionEntregadaValidador>();
        services.AddScoped<IValidadorDeMensaje<clsConexosCotizacionCambiados>, clsConexosCotizacionCambiadosValidador>();
        services.AddScoped<IValidadorDeMensaje<clsTarifaVencidaDetectada>, clsTarifaVencidaDetectadaValidador>();
        services.AddScoped<IValidadorDeMensaje<clsCotizacionMarcadaGanada>, clsCotizacionMarcadaGanadaValidador>();

        return services;
    }
}
