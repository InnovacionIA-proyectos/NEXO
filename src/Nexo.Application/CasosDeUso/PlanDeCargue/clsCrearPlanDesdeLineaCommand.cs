using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Crea la flota de un plan de cargue a partir de una línea de
/// solicitud ya cotizada — POST /api/planes-cargue/desde-linea/{lineaId} — reemplaza el
/// POST que <c>crearPlanDesdeLinea()</c> (cubicador-api.service.ts) hacía directo contra
/// Cubicador con la clave compartida.
///
/// Mismo patrón que <see cref="clsCrearPlanDemoCommand"/>, no el de <see cref="clsCrearPlanCommand"/>:
/// Cubicador arma la flota completa (uno o varios vehículos — reparto en cascada, ver
/// <c>cubicador/application/plan_cargue/desde_linea.py</c>) a partir de las posiciones de
/// la línea, así que NEXO no conoce de antemano ni la cantidad de planes ni sus Ids. No hay
/// fila espejo que dejar acá, solo un correlationId de seguimiento — que a propósito ES el
/// propio <see cref="clsCrearPlanDesdeLineaCommand.LineaSolicitudId"/> (no uno nuevo): es lo
/// que permite sondear con <see cref="clsObtenerPlanesPorLineaQuery"/> sin que el frontend
/// tenga que recordar un id de seguimiento aparte — mismo shape que ya espera
/// <c>SolicitudCubicacionEncolada</c> del lado del frontend (correlationId = lineaId).
///
/// Valida que la línea exista ANTES de publicar (a diferencia de clsCrearPlanDemoCommand, que
/// no tiene nada que validar): sin esto, un lineaId inexistente encolaría un comando que el
/// worker de Cubicador rechazaría igual al intentar leer
/// GET /api/lineas-solicitud/{id}/cubicacion (mismo <see cref="ILineaSolicitudCubicacionPort"/>
/// que sirve ese endpoint) — mejor devolver 404 de una vez que gastar un viaje redondo a la
/// cola para llegar al mismo resultado.
///
/// A propósito NO pasa por <see cref="IParidadCubicadorPort"/>: arma un plan a partir de
/// datos ya existentes (dato, no cálculo) — mismo criterio que <see cref="clsCrearPlanCommand"/>
/// y el resto de los comandos de escritura.
///
/// LIMITACIÓN CONOCIDA (no de este comando, del worker de Cubicador): hoy
/// <c>worker/main.py::_ejecutar_solicitud</c> solo devuelve el <b>primer</b> plan de la
/// flota (`crear_plan_desde_linea` → `plan.id` del "primero"), y sólo ESE plan se reporta
/// por "plan-de-cargue.generado". Cuando una línea reparte en 2+ vehículos, los vehículos
/// 2..N quedan optimizados en la base de Cubicador pero NUNCA llegan a proyectarse en NEXO
/// — <see cref="clsObtenerPlanesPorLineaQuery"/> (que lee solo la proyección local, sin
/// llamar a Cubicador, por el criterio no-negociable) no puede mostrarlos.
/// Detectado al implementar este comando, no estaba documentado antes; requiere que el
/// worker publique un evento por cada plan de la flota, no solo por el primero —
/// pendiente en `cubicador/worker/main.py` y
/// `cubicador/application/plan_cargue/desde_linea.py`. No bloquea el caso de un solo
/// vehículo (el más común), que ya funciona de punta a punta.
/// </summary>
public sealed record clsCrearPlanDesdeLineaCommand(Guid LineaSolicitudId) : ICommand<clsComandoAceptadoDto>;

public sealed class clsCrearPlanDesdeLineaCommandHandler(
    ILineaSolicitudCubicacionPort lineas, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsCrearPlanDesdeLineaCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsCrearPlanDesdeLineaCommand command, CancellationToken ct = default)
    {
        var linea = await lineas.ObtenerAsync(command.LineaSolicitudId, ct)
            ?? throw new clsNotFoundException("LINEA_SOLICITUD_NO_ENCONTRADA", "La línea de solicitud no existe.");

        // #304: el puente del Cubicador ya no inventa 1 kg para una posición sin peso: la rechaza con
        // PESO_NO_DECLARADO (el peso decide la capacidad del vehículo). Pero ese rechazo nace en el worker, DESPUÉS de este
        // 202, y vuelve por cola: clsCubicacionRechazadaConsumer solo lo registra en el log (no hay PlanCargue que marcar,
        // el Id es el de la línea) y el front se quedaba sondeando para siempre. Se valida acá, antes de encolar, con el
        // MISMO código y mensaje del Cubicador: 400 sincrónico y nada que publicar. El cero cuenta como no declarado.
        var sinPeso = linea.Posiciones
            .Where(p => p.PesoKg is not > 0)
            .Select(p => string.IsNullOrWhiteSpace(p.Descripcion) ? "(sin descripcion)" : p.Descripcion)
            .ToList();
        if (sinPeso.Count > 0)
            throw new clsValidationFailedException("PESO_NO_DECLARADO",
                $"Estas posiciones no declaran su peso: {string.Join(", ", sinPeso)}. " +
                "El peso decide la capacidad del vehículo; recapturar la línea con el peso de cada posición.");

        var lineaIdTexto = command.LineaSolicitudId.ToString();

        // "crear_desde_linea": el mismo tipo de cola que el worker de Cubicador YA
        // implementa (verificado en cubicador/worker/main.py y
        // cubicador/application/plan_cargue/desde_linea.py) — no "crear_plan_desde_linea".
        // El correlationId del sobre (ver clsCubicacionPublicadorAdapter) queda como el propio
        // lineaId, igual que el payload.
        await cubicacion.PublicarComandoAsync(
            "crear_desde_linea", lineaIdTexto, new clsCrearPlanDesdeLineaInput(lineaIdTexto), ct);

        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.LineaSolicitudId, "procesando");
    }
}
