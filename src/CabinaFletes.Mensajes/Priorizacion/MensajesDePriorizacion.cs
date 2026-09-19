namespace CabinaFletes.Mensajes.Priorizacion;

/// <summary>
/// Identidad de la única cadena de recálculo que existe.
///
/// Es una constante, no un identificador nuevo por arranque, y de eso depende todo lo
/// demás: la saga se correlaciona por este valor, así que por construcción NO puede
/// haber dos cadenas. Un segundo arranque cae sobre la instancia que ya existe.
///
/// Es el reemplazo del PeriodicTimer que vivía en el proceso del API. Aquel se
/// duplicaba con cada réplica —dos procesos, dos relojes, el mismo UPDATE sobre las
/// mismas filas— y ese sí es un problema que no se ve hasta que hay dos instancias.
/// </summary>
public static class CadenaDePriorizacion
{
    public static readonly Guid Unica = new("b7c1a5d2-0000-4000-8000-5c07e0000001");

    /// <summary>Por qué se pidió el recálculo. Viaja al log; no cambia el trabajo.</summary>
    public const string PorElReloj = "reloj";

    /// <summary>El Admin recalibró los pesos del score.</summary>
    public const string PorCambioDeParametros = "cambio-de-parametros";
}

/// <summary>
/// Arma la cadena de recálculo. Lo publica el API al arrancar.
///
/// Se publica en CADA arranque a propósito, y es idempotente por diseño: si la cadena
/// ya existe se le vuelve a armar el reloj, y el temporizador anterior queda
/// descartado por su token. Eso hace que el arranque REPARE la cadena si el reloj se
/// perdió —por ejemplo si alguien borró el volumen del broker—, en vez de dejarla
/// muerta en silencio sin que nadie se entere hasta revisar los reportes.
/// </summary>
public sealed record ArrancarPriorizacion(Guid CadenaId);

/// <summary>
/// Vence el plazo y toca recalcular. Es el reloj, y vive en el broker.
///
/// Que viva en el broker es lo que lo hace sobrevivir a un reinicio del proceso: con
/// un temporizador en memoria, reiniciar el API dejaba el siguiente recálculo
/// perdido hasta que a alguien se le ocurriera mirar.
/// </summary>
public sealed record TicDePriorizacion(Guid CadenaId);

/// <summary>
/// El Admin recalibró los parámetros del score. Es el segundo disparador que pide la HU.
///
/// Importa que sea un disparador y no un recálculo directo: el que cambia los
/// parámetros recibe su 200 de inmediato y el recálculo de todas las activas ocurre
/// aparte, con sus reintentos. Recalibrar no debe quedarse esperando a que termine.
/// </summary>
public sealed record ParametrosDePriorizacionCambiaron(
    Guid CadenaId, Guid CambiadoPor, DateTimeOffset CambiadoEn);

/// <summary>
/// Hay que refrescar el score persistido de las solicitudes activas.
///
/// El trabajo de verdad. Lo publica la saga —por reloj o por cambio de parámetros— y
/// lo atiende un consumidor con reintentos escalonados. Si los agota, va a la cola de
/// error y de ahí sale la alerta al equipo (#13): un recálculo caído no puede tumbar
/// el API ni pasar inadvertido.
/// </summary>
/// <param name="CadenaId">La cadena que lo pidió. Para trazar, no para correlacionar.</param>
/// <param name="Motivo">Reloj o cambio de parámetros. Va al log.</param>
public sealed record RecalcularScoresSolicitado(Guid CadenaId, string Motivo);
