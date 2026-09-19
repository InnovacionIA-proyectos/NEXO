// Publicador MassTransit desechable para verificar QA el puente NEXO<->Cubicador de la
// HU-075 (issue #28, PR #73) SIN pasar por ICubicacionPort (que hoy nadie dispara desde
// un flujo de usuario real) ni por un harness de xUnit contra ApiFixture. Habla
// exactamente la misma topología manual que registra
// src/Nexo.Infrastructure/Common/Mensajeria/MensajeriaDependencyInjection.cs
// para los 3 tipos de Cubicacion (exchange topic `cubicador.eventos`, routing keys
// semánticas) — sin esto el mensaje "se publica" pero no llega a nadie (topic exchange
// sin bind que matchee, sin error).
//
// Requiere el stack de NEXO (db+rabbit+api) y el de Cubicador (db+api+worker, unido al
// mismo `rabbit` por la red externa `nexo_internal`) corriendo a la vez. Ver el README
// de esta carpeta para el paso a paso completo (crear el plan+ítem en Cubicador por su
// API REST, el espejo en NEXO por SQL directo, etc).
//
// Uso, corrido desde la raíz del repo (necesita estar en la red del stack):
//   docker run --rm --network nexo_internal \
//     -e RABBIT_HOST=rabbit -e RABBIT_USER=<user> -e RABBIT_PASS=<pass> \
//     -v "$PWD:/src" -w /src/tests/QA/28-s2-hu-075-consumers-del-motor-por-masstransit-trabajos-y-resultados-por-cola \
//     mcr.microsoft.com/dotnet/sdk:10.0 dotnet run -- solicitar <planId> [<planId> ...]
//
//   ... dotnet run -- fault <planId-que-YA-EXISTA-en-PlanesCargue-de-NEXO>
//
// "solicitar" publica CubicacionSolicitada (feliz, admite varios ids para carga
// concurrente). "fault" publica un PlanDeCargueGenerado con Resultado=null sobre un
// PlanCargue existente para forzar una NullReferenceException real en
// PlanDeCargueGeneradoConsumer y ver los reintentos + DLQ + alerta. Con un plan
// INEXISTENTE no pasa nada -- el propio consumer hace `if (plan is null) return`, a
// propósito (ver su comentario: "el trabajo puede haberse creado y purgado en otro
// proceso").
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

var modo = args.Length > 0 ? args[0] : "solicitar";
var host = Environment.GetEnvironmentVariable("RABBIT_HOST") ?? "rabbit";
var user = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "nexo";
var pass = Environment.GetEnvironmentVariable("RABBIT_PASS") ?? "";

var services = new ServiceCollection();
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(host, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        // Misma topología manual que MensajeriaDependencyInjection.cs: Cubicador
        // declara su propio exchange topic, no el exchange-por-tipo por defecto.
        cfg.Message<CubicacionSolicitada>(m => m.SetEntityName("cubicador.eventos"));
        cfg.Publish<CubicacionSolicitada>(p => p.ExchangeType = "topic");
        cfg.Message<PlanDeCargueGenerado>(m => m.SetEntityName("cubicador.eventos"));
        cfg.Publish<PlanDeCargueGenerado>(p => p.ExchangeType = "topic");
    });
});
await using var provider = services.BuildServiceProvider(true);
var bus = provider.GetRequiredService<IBusControl>();
await bus.StartAsync();

if (modo == "solicitar")
{
    var planIds = args.Skip(1).ToArray();
    await Task.WhenAll(planIds.Select(async planId =>
    {
        var mensaje = new CubicacionSolicitada(
            "optimizar", planId, planId,
            new SolicitudDeCubicacion("Combinada", "bfd", null, null, false));
        await bus.Publish(mensaje, ctx => ctx.SetRoutingKey("cubicacion.solicitada"));
        Console.WriteLine($"CubicacionSolicitada publicada para planId={planId}");
    }));
}
else if (modo == "fault")
{
    var planId = args[1];
    var mensaje = new PlanDeCargueGenerado(planId, planId, null!);
    await bus.Publish(mensaje, ctx => ctx.SetRoutingKey("plan-de-cargue.generado"));
    Console.WriteLine($"PlanDeCargueGenerado (Resultado=null, forzando fallo) publicado para planId={planId}");
}
else
{
    Console.WriteLine($"Modo desconocido: {modo}");
}

await bus.StopAsync();
Console.WriteLine("Listo.");
