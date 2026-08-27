namespace CabinaFletes.Application.Common.Ports;

/// <summary>
/// Solicita una cubicación (plan de cargue) al microservicio Cubicador — issue #28,
/// S2-HU-075. NO usa <see cref="IPublicadorDeMensajes"/>: ese puerto publica vía
/// MassTransit/Outbox con la topología propia de NEXO (un exchange por tipo de
/// mensaje), y Cubicador espera un routing key y un shape de JSON específicos y ya
/// documentados (cubicador/docs/CONTRATOS.md sección B) — ver
/// CabinaFletes.Mensajes.Cubicacion.MensajesDeCubicacion para el porqué completo.
///
/// Limitación conocida y aceptada: a diferencia de IPublicadorDeMensajes, esta
/// solicitud NO pasa por el Outbox transaccional — se publica directo al broker. Si
/// el proceso muere entre guardar `PlanCargue` y publicar, el trabajo queda en
/// "procesando" sin que nadie lo procese nunca (no hay reintento automático de la
/// solicitud en sí, solo de su procesamiento del lado de Cubicador). Aceptable hoy
/// porque nada dispara esto todavía en producción (ver DECISIONES_TECNICAS.md); si
/// esto se vuelve una ruta crítica real, vale la pena revisar.
/// </summary>
public interface ICubicacionPort
{
    Task SolicitarCubicacionAsync(
        Guid planCargueId, string estrategia, string algoritmo, int? semilla, bool respetarBloqueadas,
        CancellationToken ct);
}
