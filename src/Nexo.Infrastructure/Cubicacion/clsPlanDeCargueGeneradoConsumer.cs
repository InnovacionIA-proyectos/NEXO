using System.Text.Json;
using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.PlanDeCargue.Entidades;
using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>
/// Consume el resultado de una escritura/optimización exitosa (issue #28, S2-HU-075;
/// enriquecido en S3-HU-03, issue #111) y proyecta la base propia de NEXO —
/// reactivando tablas que ya existían en el <c>DbContext</c> sin ningún código que las
/// usara (<c>VehiculosCubicacion</c>, <c>ItemsPlanCargue</c>, <c>ColocacionesCargue</c>).
///
/// IConsumer&lt;T&gt; real de MassTransit: hereda la política de reintentos (1s/5s/
/// 15s/60s) y el DLQ (`PlanDeCargueGenerado_error`) de la tubería compartida de
/// NEXO (ver clsMensajeriaDependencyInjection.cs::Tuberia) — no hay código de
/// reintento propio aquí. La topología (bind al exchange `cubicador.eventos` de
/// Cubicador) se configura en el registro del bus, no aquí.
///
/// NO hace ninguna llamada saliente a Cubicador (criterio de cierre de S3-HU-03,
/// issue #111): todo lo que escribe viene del propio evento — nunca reconsulta.
///
/// UPSERT, no solo UPDATE: a diferencia de la versión anterior a S3-HU-03, este
/// consumer inserta el plan si no existe (crear_plan_demo no deja fila espejo al
/// publicar — ver docs/mensajeria/CUBICACION.md §6.5 y ADR-planid-generado-por-nexo.md)
/// y siempre upsertea el vehículo con datos REALES (ya no el stub de solo Id+Nombre que
/// deja <c>clsCrearPlanCommand</c> para no romper la FK).
/// </summary>
public sealed class clsPlanDeCargueGeneradoConsumer(
    clsNexoDbContext db, INotificadorDePlanCargue avisos, ILogger<clsPlanDeCargueGeneradoConsumer> log)
    : IConsumer<clsPlanDeCargueGenerado>
{
    private static readonly JsonSerializerOptions JsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Consume(ConsumeContext<clsPlanDeCargueGenerado> context)
    {
        var mensaje = context.Message;
        if (!Guid.TryParse(mensaje.PlanId, out var planId)) return;

        var resultado = mensaje.Resultado;
        var ct = context.CancellationToken;

        await clsVehiculoProyeccion.UpsertAsync(db, resultado.Vehiculo, ct);

        var plan = await db.PlanesCargue
            .Include(p => p.Items)
            .Include(p => p.Colocaciones)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);

        if (plan is null)
        {
            plan = new clsPlanCargue { Id = planId };
            db.PlanesCargue.Add(plan);
        }

        AplicarPlan(plan, resultado.Plan);

        // Colocaciones: se REEMPLAZAN completas en cada evento (mismo criterio que
        // Cubicador usa en su propia base, `guardar_resultado_optimizacion` — el evento
        // trae el snapshot COMPLETO, no un delta). Se marcan para borrar ANTES de tocar
        // items: la FK clsColocacionCargue.ItemPlanCargueId es Restrict, así que un item que
        // ya no viene en el evento no se puede borrar mientras una colocación vieja lo
        // referencie (EF Core igual ordena el DELETE real por dependencia de FK al margen
        // del orden de estas llamadas, pero dejarlo en este orden es más fácil de leer).
        if (plan.Colocaciones.Count > 0)
            db.ColocacionesCargue.RemoveRange(plan.Colocaciones);

        AplicarItems(plan, resultado.Plan.Items);

        foreach (var c in resultado.Colocaciones)
            db.ColocacionesCargue.Add(AColocacion(c));

        plan.MetricasJson = resultado.Metricas is null ? null : JsonSerializer.Serialize(resultado.Metricas, JsonCamelCase);
        plan.IncidenciasJson = JsonSerializer.Serialize(resultado.Incidencias, JsonCamelCase);
        plan.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        // Un fallo al avisar NO es un fallo del trabajo (S2-HU-08.1): la verdad sigue en
        // GET /api/planes-cargue/{id}. Dejar que la excepción suba mandaría este mensaje
        // — que ya cumplió su parte, el plan quedó guardado — a reintentos y DLQ por algo
        // tan ajeno como un navegador desconectado. Mismo criterio que clsAvisarTrabajoConsumer.
        try
        {
            await avisos.AvisarAsync(new clsAvisoPlanCargueDto(plan.Id, plan.Estado, plan.Puntaje), ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el desenlace del plan de cargue {PlanCargueId}. El plan ya quedó " +
                "guardado; se puede consultar en /api/planes-cargue/{PlanCargueId}.", plan.Id, plan.Id);
        }
    }

    private static void AplicarPlan(clsPlanCargue plan, clsPlanCargueEvento e)
    {
        plan.Nombre = e.Nombre;
        plan.VehiculoId = Guid.Parse(e.VehiculoId);
        plan.LineaSolicitudId = e.LineaSolicitudId is null ? null : Guid.Parse(e.LineaSolicitudId);
        plan.NumeroVehiculo = e.NumeroVehiculo;
        plan.Estado = e.Estado;
        plan.Estrategia = e.Estrategia;
        plan.Algoritmo = e.Algoritmo;
        plan.Semilla = e.Semilla;
        plan.PesoTotalKg = e.PesoTotalKg;
        plan.VolumenUtilizadoM3 = e.VolumenUtilizadoM3;
        plan.UtilizacionVolumen = e.UtilizacionVolumen;
        plan.UtilizacionPiso = e.UtilizacionPiso;
        plan.Puntaje = e.Puntaje;
    }

    private void AplicarItems(clsPlanCargue plan, IReadOnlyList<clsItemPlanCargueEvento> itemsDelEvento)
    {
        var existentesPorId = plan.Items.ToDictionary(i => i.Id);
        var idsDelEvento = new HashSet<Guid>();

        foreach (var e in itemsDelEvento)
        {
            var id = Guid.Parse(e.Id);
            idsDelEvento.Add(id);

            if (existentesPorId.TryGetValue(id, out var item))
                AplicarItem(item, e);
            else
                db.ItemsPlanCargue.Add(AplicarItem(new clsItemPlanCargue { Id = id, PlanCargueId = plan.Id }, e));
        }

        // Items que ya no vienen en el evento (borrados via eliminar_item desde el
        // último snapshot) - sus colocaciones ya se removieron arriba, así que esto
        // es seguro contra la FK Restrict.
        foreach (var item in plan.Items.Where(i => !idsDelEvento.Contains(i.Id)).ToList())
            db.ItemsPlanCargue.Remove(item);
    }

    private static clsItemPlanCargue AplicarItem(clsItemPlanCargue item, clsItemPlanCargueEvento e)
    {
        item.Sku = e.Sku;
        item.Descripcion = e.Descripcion;
        item.Pedido = e.Pedido;
        item.clsCliente = e.clsCliente;
        item.Destino = e.Destino;
        item.SecuenciaEntrega = e.SecuenciaEntrega;
        item.LargoMm = e.LargoMm;
        item.AnchoMm = e.AnchoMm;
        item.AltoMm = e.AltoMm;
        item.PesoKg = e.PesoKg;
        item.Cantidad = e.Cantidad;
        item.Color = e.Color;
        item.Prioridad = e.Prioridad;
        item.TipoEmbalaje = e.TipoEmbalaje;
        item.Apilable = e.Apilable;
        item.MaxNiveles = e.MaxNiveles;
        item.MaxPesoEncimaKg = e.MaxPesoEncimaKg;
        item.Rotable = e.Rotable;
        item.MantenerVertical = e.MantenerVertical;
        item.Fragil = e.Fragil;
        item.Peligroso = e.Peligroso;
        item.Refrigerado = e.Refrigerado;
        item.GrupoCompatibilidad = e.GrupoCompatibilidad;
        item.Observaciones = e.Observaciones;
        return item;
    }

    private static clsColocacionCargue AColocacion(clsColocacionCargueEvento e) => new()
    {
        Id = Guid.Parse(e.Id),
        PlanCargueId = Guid.Parse(e.PlanCargueId),
        ItemPlanCargueId = Guid.Parse(e.ItemPlanCargueId),
        IndiceUnidad = e.IndiceUnidad,
        Cargada = e.Cargada,
        X = e.X,
        Y = e.Y,
        Z = e.Z,
        LargoColocadoMm = e.LargoColocadoMm,
        AnchoColocadoMm = e.AnchoColocadoMm,
        AltoColocadoMm = e.AltoColocadoMm,
        OrientacionIndice = e.OrientacionIndice,
        SecuenciaCargue = e.SecuenciaCargue,
        SoporteColocacionId = e.SoporteColocacionId is null ? null : Guid.Parse(e.SoporteColocacionId),
        PorcentajeSoporte = e.PorcentajeSoporte,
        PesoEncimaKg = e.PesoEncimaKg,
        EstadoEstabilidad = e.EstadoEstabilidad,
        Accesibilidad = e.Accesibilidad,
        Bloqueada = e.Bloqueada,
        AjusteManual = e.AjusteManual,
        CodigoRechazo = e.CodigoRechazo,
        RazonRechazo = e.RazonRechazo,
    };

}

/// <summary>
/// Upsert de <see cref="clsVehiculoCubicacion"/> desde el evento de Cubicador — compartido
/// entre <see cref="clsPlanDeCargueGeneradoConsumer"/> (que trae el vehículo del plan
/// embebido) y <see cref="clsVehiculoCatalogoConsumer"/> (vehiculo.creado/actualizado
/// dedicados, S3-HU-03 issue #110/#111), para no duplicar el mapeo evento-a-entidad.
/// </summary>
internal static class clsVehiculoProyeccion
{
    private static readonly JsonSerializerOptions JsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task UpsertAsync(clsNexoDbContext db, clsVehiculoCubicacionEvento e, CancellationToken ct)
    {
        var id = Guid.Parse(e.Id);
        var vehiculo = await db.VehiculosCubicacion.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehiculo is null)
        {
            vehiculo = new clsVehiculoCubicacion { Id = id };
            db.VehiculosCubicacion.Add(vehiculo);
        }

        vehiculo.Nombre = e.Nombre;
        vehiculo.Descripcion = e.Descripcion;
        vehiculo.LargoInternoMm = e.LargoInternoMm;
        vehiculo.AnchoInternoMm = e.AnchoInternoMm;
        vehiculo.AltoInternoMm = e.AltoInternoMm;
        vehiculo.CapacidadKg = e.CapacidadKg;
        vehiculo.TaraKg = e.TaraKg;
        vehiculo.PuertaAnchoMm = e.PuertaAnchoMm;
        vehiculo.PuertaAltoMm = e.PuertaAltoMm;
        vehiculo.PuertaPosicion = e.PuertaPosicion;
        vehiculo.EjesJson = JsonSerializer.Serialize(e.Ejes, JsonCamelCase);
        vehiculo.ToleranciaParedesMm = e.ToleranciaParedesMm;
        vehiculo.ToleranciaEntreUnidadesMm = e.ToleranciaEntreUnidadesMm;
        vehiculo.EsPreset = e.EsPreset;
        vehiculo.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
