using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// [S3-HU-04.3, issue #78] Crea la flota de un plan de cargue a partir de una línea de
/// solicitud ya cotizada — POST /api/planes-cargue/desde-linea/{lineaId} — reemplaza el
/// POST que <c>crearPlanDesdeLinea()</c> (cubicador-api.service.ts) hacía directo contra
/// Cubicador con la clave compartida.
///
/// Mismo patrón que <see cref="CrearPlanDemoCommand"/>, no el de <see cref="CrearPlanCommand"/>:
/// Cubicador arma la flota completa (uno o varios vehículos — reparto en cascada, ver
/// <c>cubicador/application/plan_cargue/desde_linea.py</c>) a partir de las posiciones de
/// la línea, así que NEXO no conoce de antemano ni la cantidad de planes ni sus Ids. No hay
/// fila espejo que dejar acá, solo un correlationId de seguimiento — que a propósito ES el
/// propio <see cref="CrearPlanDesdeLineaCommand.LineaSolicitudId"/> (no uno nuevo): es lo
/// que permite sondear con <see cref="ObtenerPlanesPorLineaQuery"/> sin que el frontend
/// tenga que recordar un id de seguimiento aparte — mismo shape que ya espera
/// <c>SolicitudCubicacionEncolada</c> del lado del frontend (correlationId = lineaId).
///
/// Valida que la línea exista ANTES de publicar (a diferencia de CrearPlanDemoCommand, que
/// no tiene nada que validar): sin esto, un lineaId inexistente encolaría un comando que el
/// worker de Cubicador rechazaría igual al intentar leer
/// GET /api/lineas-solicitud/{id}/cubicacion (mismo <see cref="ILineaSolicitudCubicacionPort"/>
/// que sirve ese endpoint) — mejor devolver 404 de una vez que gastar un viaje redondo a la
/// cola para llegar al mismo resultado.
///
/// A propósito NO pasa por <see cref="IParidadCubicadorPort"/>: arma un plan a partir de
/// datos ya existentes (dato, no cálculo) — mismo criterio que <see cref="CrearPlanCommand"/>
/// y el resto de los comandos de escritura de S3-HU-02.
///
/// LIMITACIÓN CONOCIDA (no de este comando, del worker de Cubicador): hoy
/// <c>worker/main.py::_ejecutar_solicitud</c> solo devuelve el <b>primer</b> plan de la
/// flota (`crear_plan_desde_linea` → `plan.id` del "primero"), y sólo ESE plan se reporta
/// por "plan-de-cargue.generado". Cuando una línea reparte en 2+ vehículos, los vehículos
/// 2..N quedan optimizados en la base de Cubicador pero NUNCA llegan a proyectarse en NEXO
/// — <see cref="ObtenerPlanesPorLineaQuery"/> (que lee solo la proyección local, sin
/// llamar a Cubicador, por el criterio no-negociable de S3-HU-03) no puede mostrarlos.
/// Detectado al implementar este comando, no estaba documentado antes; requiere que el
/// worker publique un evento por cada plan de la flota, no solo por el primero — issue
/// nueva pendiente de abrir contra `cubicador/worker/main.py` y
/// `cubicador/application/plan_cargue/desde_linea.py`. No bloquea el caso de un solo
/// vehículo (el más común), que ya funciona de punta a punta.
/// </summary>
public sealed record CrearPlanDesdeLineaCommand(Guid LineaSolicitudId) : ICommand<ComandoAceptadoDto>;

public sealed class CrearPlanDesdeLineaCommandHandler(
    ILineaSolicitudCubicacionPort lineas, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<CrearPlanDesdeLineaCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(CrearPlanDesdeLineaCommand command, CancellationToken ct = default)
    {
        _ = await lineas.ObtenerAsync(command.LineaSolicitudId, ct)
            ?? throw new NotFoundException("LINEA_SOLICITUD_NO_ENCONTRADA", "La línea de solicitud no existe.");

        var lineaIdTexto = command.LineaSolicitudId.ToString();

        // "crear_desde_linea": el mismo tipo de cola que el worker de Cubicador YA
        // implementa (verificado en cubicador/worker/main.py y
        // cubicador/application/plan_cargue/desde_linea.py) — no "crear_plan_desde_linea".
        // El correlationId del sobre (ver CubicacionPublicadorAdapter) queda como el propio
        // lineaId, igual que el payload.
        await cubicacion.PublicarComandoAsync(
            "crear_desde_linea", lineaIdTexto, new CrearPlanDesdeLineaInput(lineaIdTexto), ct);

        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.LineaSolicitudId, "procesando");
    }
}
