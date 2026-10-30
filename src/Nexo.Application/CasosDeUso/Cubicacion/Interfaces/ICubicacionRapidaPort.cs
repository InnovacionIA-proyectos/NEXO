using Nexo.Application.CasosDeUso.Cubicacion.DTOs;

namespace Nexo.Application.CasosDeUso.Cubicacion.Interfaces;

/// <summary>
/// Cierra el único de los 20 métodos de
/// <c>CubicadorApiService</c>/<c>CapturaApiService</c> sin equivalente en NEXO:
/// <c>cubicar()</c> (<c>captura-api.service.ts</c>), usado por <c>captura-page</c> y
/// <c>mesa-costeo-page</c>.
///
/// POR QUÉ ES UNA EXCEPCIÓN DECLARADA, NO EL PATRÓN NUEVO. El resto de Cubicación sigue al
/// pie de la letra "NEXO habla con Cubicador solo por colas, nunca HTTP"
/// (<c>cubicador/CONTRATO.md</c> sección 5). <see cref="CrearAsync"/> es síncrono por
/// naturaleza: la pantalla necesita el resultado para renderizar YA, no un aviso más
/// tarde. S5-HU-18.1/#273: el contrato nuevo (<c>POST /recomendaciones</c>) SÍ persiste
/// (append-only, cada corrida queda guardada en el Cubicador) — ya no es "cálculo puro sin
/// persistencia" como lo era el viejo <c>POST /api/cubicacion</c>, pero la persistencia es
/// enteramente responsabilidad del Cubicador: NEXO sigue sin tener nada propio que guardar
/// ni proyectar de esta llamada, así que la excepción declarada se mantiene.
///
/// El secreto (<c>Cubicador3D:RecomendacionesApiKey</c>, equivalente a
/// <c>CUBICADOR_API_KEY_NEXO</c> del lado del Cubicador — distinto del
/// <c>Cubicador3D:OutboundApiKey</c> genérico que usaba el contrato viejo) vive SOLO en la
/// configuración del backend — nunca en el bundle del portal.
/// </summary>
public interface ICubicacionRapidaPort
{
    /// <summary>POST /recomendaciones — crea una corrida nueva (append-only).</summary>
    Task<clsRecomendacionDto> CrearAsync(clsCrearRecomendacionInput solicitud, CancellationToken ct = default);

    /// <summary>
    /// GET /recomendaciones?idLineaSolicitud=&amp;origenSistema= — la última corrida de esa
    /// línea. <paramref name="origenSistema"/> es obligatorio y NO se infiere de la llave:
    /// un mismo código de línea puede existir tanto en NEXO como en Cotifly, como rutas
    /// distintas, y un sistema puede necesitar leer lo que recomendó el otro.
    /// </summary>
    Task<clsRecomendacionDto> ConsultarAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default);
}
