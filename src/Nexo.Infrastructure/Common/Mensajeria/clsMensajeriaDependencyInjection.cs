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

/// <summary>Registro único de la mensajería, para que cualquier host (Api o workers) la configure igual.</summary>
public static class clsMensajeriaDependencyInjection
{
    public static IServiceCollection AddMensajeria(this IServiceCollection services, IConfiguration config)
    {
        var host = config.GetConnectionString("RabbitMq") ?? config["RabbitMq:Host"];

        // Se registra el intervalo, no la máquina de estados: MassTransit la construye con el contenedor,
        // y así la saga es instanciable en una prueba con 300 ms.
        services.AddSingleton(new clsIntervaloDeRecalculo(
            clsSagaDePriorizacion.IntervaloDe(
                clsConfiguracionDeMensajeria.EnteroDeConfig(config, clsSagaDePriorizacion.ClaveIntervalo, clsSagaDePriorizacion.MinutosPorDefecto))));

        services.AddMassTransit(x =>
        {
            // Los timeouts de saga los programa el broker (no un temporizador en memoria) para sobrevivir a un reinicio; requiere el plugin de retardo.
            x.AddDelayedMessageScheduler();

            // Nombres de cola/exchange estables pese al prefijo "cls" (ver clsFormateadorNombresSinPrefijoCls).
            x.SetEndpointNameFormatter(new clsFormateadorNombresSinPrefijoCls());

            // Reparte el pliego en un mensaje por línea para que la saga no lea la base y siga siendo pura.
            x.AddConsumer<clsRepartidorDeLineasConsumer>();

            // Costea una línea; si una fuente falla la excepción sube y la política escalonada del bus reintenta.
            x.AddConsumer<clsCostearLineaConsumer>();

            // Convierte el fallo agotado en un desenlace que la saga cuenta; sin él el pliego esperaría esa línea hasta el timeout.
            x.AddConsumer<clsFalloDeCosteoConsumer>();

            // Recosteo: las líneas en espera vuelven a la cola cuando se cargan o extienden tarifas. No pasa por la saga: ese trabajo ya cerró en Parcial.
            x.AddConsumer<clsRecosteoPorTarifasConsumer>();
            x.AddConsumer<clsRecostearLineaConsumer>();

            // Lee el estado en vez de confiar en el mensaje, para que el aviso y la consulta de respaldo no digan cosas distintas.
            x.AddConsumer<clsAvisarTrabajoConsumer>();

            // Alerta al equipo cuando un mensaje cae a la cola de error (ver clsAlertaDeFalloConsumer<T>). AddConsumer(Type) solo acepta tipos
            // CERRADOS, así que va una línea por mensaje; si se olvida uno, ese mensaje no genera alerta.
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

            // Puente hacia Cubicador (microservicio Python). La topología manual de abajo existe porque Cubicador usa un exchange y routing keys
            // propios, no el exchange-por-tipo de MassTransit (ver docs/mensajeria/CUBICACION.md).
            x.AddConsumer<clsPlanDeCargueGeneradoConsumer>();
            x.AddConsumer<clsCubicacionRechazadaConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<PlanDeCargueGenerado>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<CubicacionRechazada>>();

            // Catálogo de vehículos: un solo consumer atiende los 2 tipos, pero MassTransit necesita un AddConsumer<T> y un Fault<T> por mensaje.
            x.AddConsumer<clsVehiculoCatalogoConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<VehiculoCreado>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<VehiculoActualizado>>();

            // Catálogos de tipo de mercancía y de carrocería: mismo puente que el catálogo de vehículos.
            x.AddConsumer<clsMercanciaCatalogoConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<MercanciaCreada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<MercanciaActualizada>>();
            x.AddConsumer<clsCarroceriaCatalogoConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<CarroceriaCreada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<CarroceriaActualizada>>();

            // Refresca el score persistido de las activas. La excepción sube: si deja de correr, los reportes mienten y alguien debe enterarse.
            x.AddConsumer<clsRecalcularScoresConsumer>();

            // Empuja a la bandeja abierta a volver a consultar. El fallo se registra y se sigue: cerrar una pestaña no es una avería.
            x.AddConsumer<clsAvisarBandejaConsumer>();

            // Correo de una cotización entregada. La excepción sube: una notificación perdida no se recupera y nadie se enteraría.
            x.AddConsumer<clsNotificarEntregaConsumer>();

            // Avisa a quien creó la solicitud que cambiaron los servicios conexos de su cotización entregada. La excepción sube.
            x.AddConsumer<clsNotificarConexosCambiadosConsumer>();

            // Alertas de negocio: tarifa vencida, línea sin costear por falla técnica y cotización ganada. La excepción sube.
            x.AddConsumer<clsNotificarTarifaVencidaConsumer>();
            x.AddConsumer<clsNotificarLineaFalloTecnicoConsumer>();
            x.AddConsumer<clsNotificarCotizacionGanadaConsumer>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsTarifaVencidaDetectada>>();
            x.AddConsumer<clsAlertaDeFalloConsumer<clsCotizacionMarcadaGanada>>();

            // La saga se persiste en la base del negocio: su estado es la respuesta al cliente, así que un reinicio no puede borrarlo.
            x.AddSagaStateMachine<clsAutoCosteoSaga, clsEstadoAutoCosteo>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<clsNexoDbContext>();
                    // Bloqueo pesimista: dos resultados de línea del mismo pliego pueden
                    // llegar a la vez, y con optimista se perderían contadores.
                    r.ConcurrencyMode = MassTransit.ConcurrencyMode.Pessimistic;
                });

            // Reloj del recálculo de scores. La fila de esta saga es el candado que impide tener dos cadenas,
            // algo imposible de garantizar con un temporizador por proceso y varias réplicas.
            x.AddSagaStateMachine<clsSagaDePriorizacion, clsEstadoPriorizacion>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<clsNexoDbContext>();
                    // Pesimista por el mismo motivo que la otra: un tic del reloj y un
                    // cambio de parámetros pueden caer sobre la misma fila a la vez.
                    r.ConcurrencyMode = MassTransit.ConcurrencyMode.Pessimistic;
                });

            // El Outbox escribe el mensaje en la misma transacción que los datos del caso de uso, lo que hace confiable la publicación.
            x.AddEntityFrameworkOutbox<clsNexoDbContext>(o =>
            {
                o.UseSqlServer();

                // Sin esto Publish() enviaría directo al broker y se perdería la atomicidad.
                o.UseBusOutbox();

                // Cada cuánto revisa la tabla de salida por mensajes sin entregar.
                o.QueryDelay = TimeSpan.FromSeconds(5);
            });

            // La tubería es la misma con broker y sin él, para que una prueba no corra sobre una distinta de la de producción.
            // `excluirDeAutoConfigurar`: consumers cuyo receive endpoint ya se configuró a mano (exchange propio de Cubicador);
            // sin excluirlos, ConfigureEndpoints crea otro endpoint con el mismo nombre y revienta.
            void Tuberia<T>(IBusRegistrationContext contexto, IBusFactoryConfigurator<T> cfg, params Type[] excluirDeAutoConfigurar)
                where T : IReceiveEndpointConfigurator
            {
                // Registrar el planificador no basta: el bus debe usarlo en su tubería, o Schedule falla con "MessageSchedulerContext payload not found".
                cfg.UseDelayedMessageScheduler();

                // Reintentos escalonados para fallos transitorios; agotados, el mensaje va a la cola de error (donde clsAlertaDeFalloConsumer alerta al equipo).
                cfg.UseMessageRetry(r =>
                {
                    // No se reintenta lo que no tiene arreglo (adjunto demasiado grande, correo rechazado por su forma): va directo a la cola de error
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

            // Sin broker configurado el transporte va en memoria: de lo contrario el bus no arranca, el health check queda Unhealthy y /health responde 503.
            // Arrancar sin broker en Producción lo impide la guarda de clsProgram.cs.
            if (string.IsNullOrWhiteSpace(host))
                x.UsingInMemory((contexto, cfg) => Tuberia(contexto, cfg));
            else
                x.UsingRabbitMq((contexto, cfg) =>
                {
                    cfg.Host(new Uri(host));

                    // Cubicador declara su propio exchange topic (`cubicador.eventos`) con routing keys semánticas, no el exchange-por-tipo de MassTransit.
                    // Se anula esa convención solo para estos 3 tipos; el tipo de exchange es un string porque así lo expone la topología de MassTransit.
                    cfg.Message<clsCubicacionSolicitada>(m => m.SetEntityName("cubicador.eventos"));
                    cfg.Publish<clsCubicacionSolicitada>(p => p.ExchangeType = "topic");

                    // El prefijo "Cubicacion." evita que el endpoint por defecto de clsAlertaDeFalloConsumer<T> (nombre del tipo de mensaje) colisione
                    // con estos endpoints manuales ("A receive endpoint with the same key was already added").
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

                    // Un endpoint por routing key: MassTransit no permite bindear 2 routing keys a un mismo endpoint con colas separadas,
                    // y se quieren colas separadas para que un fallo en vehiculo.creado no bloquee vehiculo.actualizado.
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
                    // Sin este binding, `eliminar_vehiculo` se descartaría en silencio (topic exchange sin bind que calce) y la proyección conservaría vehículos inexistentes.
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

                    // Mismo patrón de 3 endpoints por catálogo (mercancía/carrocería): cada routing key necesita el suyo.
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

        // El decorador de validación envuelve al adaptador de MassTransit; IPublicadorDeMensajes siempre resuelve el validado, nunca el crudo.
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

        // Validadores explícitos por mensaje (en vez de escanear el ensamblado) para ver qué viaja al broker; si se olvida uno, el decorador falla cerrado.
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
