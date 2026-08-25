using CabinaFletes.Application.Common.Mensajeria;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.CasosDeUso.Costeo.Interfaces;
using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Infrastructure.Costeo.Adaptadores;
using CabinaFletes.Infrastructure.Costeo.Repositorios;
using CabinaFletes.Infrastructure.Solicitudes.Adaptadores;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Infrastructure.Solicitudes.Repositorios;
using CabinaFletes.Infrastructure.Trabajos.Repositorios;
using CabinaFletes.Mensajes.Costeo;
using CabinaFletes.Mensajes.Solicitudes;
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

            x.UsingRabbitMq((contexto, cfg) =>
            {
                if (!string.IsNullOrWhiteSpace(host))
                    cfg.Host(new Uri(host));

                // Registrar el planificador NO basta: el bus tiene que usarlo en su
                // tubería. Sin esta línea la actividad Schedule de la saga falla con
                // "MessageSchedulerContext payload not found" — se descubrió en las
                // pruebas de la saga, no desplegado.
                cfg.UseDelayedMessageScheduler();

                // Reintentos escalonados: un fallo transitorio del broker o de una
                // fuente externa no debe tumbar el mensaje al primer intento. Agotados
                // los intentos, MassTransit lo lleva a la cola de error (DLQ), que es
                // donde la #13 conectará la alerta al equipo.
                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(60)));

                cfg.ConfigureEndpoints(contexto);
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

        return services;
    }
}
