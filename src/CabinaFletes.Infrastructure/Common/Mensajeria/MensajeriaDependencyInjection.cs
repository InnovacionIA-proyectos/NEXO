using CabinaFletes.Application.Common.Mensajeria;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes;
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
        services.AddScoped<MassTransitPublicador>();
        services.AddScoped<IPublicadorDeMensajes>(sp =>
            new PublicadorValidado(sp.GetRequiredService<MassTransitPublicador>(), sp));

        // Los validadores de cada mensaje. Se registran explícitamente en vez de
        // escanear el ensamblado: es una línea por mensaje y se lee de un vistazo qué
        // viaja al broker. Si alguien agrega un mensaje y olvida esta línea, el
        // decorador falla cerrado y la prueba lo detecta.
        services.AddScoped<IValidadorDeMensaje<AutoCosteoSolicitado>, AutoCosteoSolicitadoValidador>();

        return services;
    }
}
