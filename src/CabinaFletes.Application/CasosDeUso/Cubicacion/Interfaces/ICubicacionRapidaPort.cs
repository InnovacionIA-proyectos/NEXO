using CabinaFletes.Application.CasosDeUso.Cubicacion.DTOs;

namespace CabinaFletes.Application.CasosDeUso.Cubicacion.Interfaces;

/// <summary>
/// [S3-HU-04.3, issue #78, sección 8.2] Cierra el único de los 20 métodos de
/// <c>CubicadorApiService</c>/<c>CapturaApiService</c> sin equivalente en NEXO:
/// <c>cubicar()</c> (<c>captura-api.service.ts</c>), usado por <c>captura-page</c> y
/// <c>mesa-costeo-page</c> para una cubicación rápida SIN persistencia — no hay plan, no
/// hay Id, no hay nada que guardar ni proyectar.
///
/// POR QUÉ ES UNA EXCEPCIÓN DECLARADA, NO EL PATRÓN NUEVO. El resto de Cubicación (S3-HU-02/
/// 03/04.3) sigue al pie de la letra "NEXO habla con Cubicador solo por colas, nunca HTTP"
/// (<c>cubicador/CONTRATO.md</c> sección 5) porque cada operación tiene un dueño async claro
/// (una cola que la procesa, una proyección que la lee). Este cálculo no tiene ninguno de
/// los dos: es síncrono por naturaleza (la pantalla necesita el resultado para renderizar
/// YA, no un aviso más tarde) y no persiste nada que una cola pudiera procesar ni una
/// proyección pudiera servir después. Inventar una cola+proyección para esto sería
/// simular una llamada síncrona con más piezas, no evitarla.
///
/// La alternativa (ver <c>docs/mensajeria/CUBICACION.md</c> §8.2) era resolverlo con un
/// patrón de solicitud/respuesta sobre RabbitMQ (reply-to). Se descartó por
/// desproporcionado: un patrón nuevo, usado en ningún otro flujo, para un cálculo que ya
/// responde en el orden de cientos de milisegundos por HTTP.
///
/// El secreto (mismo Bearer que Cubicador ya valida en <c>verificar_api_key</c>) vive SOLO
/// en la configuración del backend (<c>Cubicador3D:OutboundApiKey</c>) — nunca en el bundle
/// del portal. Eso es, en una frase, todo lo que esta HU necesitaba lograr.
/// </summary>
public interface ICubicacionRapidaPort
{
    Task<CubicacionRapidaResultado> EvaluarAsync(CubicacionRapidaSolicitud solicitud, CancellationToken ct = default);
}
