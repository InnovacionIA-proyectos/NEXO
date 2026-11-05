namespace Nexo.Mensajes.Priorizacion;

/// <summary>
/// Identidad de la única cadena de recálculo. Es una constante: la saga se correlaciona por este valor,
/// así que por construcción no puede haber dos cadenas (ni dos relojes con varias réplicas del API).
/// </summary>
public static class clsCadenaDePriorizacion
{
    public static readonly Guid Unica = new("b7c1a5d2-0000-4000-8000-5c07e0000001");

    /// <summary>Por qué se pidió el recálculo. Viaja al log; no cambia el trabajo.</summary>
    public const string PorElReloj = "reloj";

    /// <summary>El Admin recalibró los pesos del score.</summary>
    public const string PorCambioDeParametros = "cambio-de-parametros";
}

/// <summary>
/// Arma la cadena de recálculo; el API lo publica en cada arranque. Es idempotente: si la cadena existe
/// se le rearma el reloj, así el arranque repara la cadena si el reloj se perdió.
/// </summary>
public sealed record clsArrancarPriorizacion(Guid CadenaId);

/// <summary>Vence el plazo y toca recalcular. El reloj vive en el broker para sobrevivir a reinicios del proceso.</summary>
public sealed record clsTicDePriorizacion(Guid CadenaId);

/// <summary>
/// El Admin recalibró los parámetros del score. Es un disparador y no un recálculo directo: quien recalibra
/// recibe su respuesta de inmediato y el recálculo ocurre aparte, con reintentos.
/// </summary>
public sealed record clsParametrosDePriorizacionCambiaron(
    Guid CadenaId, Guid CambiadoPor, DateTimeOffset CambiadoEn);

/// <summary>
/// Hay que refrescar el score persistido de las solicitudes activas. Lo publica la saga y lo atiende un
/// consumidor con reintentos escalonados; si los agota va a la cola de error y alerta al equipo.
/// </summary>
/// <param name="CadenaId">La cadena que lo pidió. Para trazar, no para correlacionar.</param>
/// <param name="Motivo">Reloj o cambio de parámetros. Va al log.</param>
public sealed record clsRecalcularScoresSolicitado(Guid CadenaId, string Motivo);
