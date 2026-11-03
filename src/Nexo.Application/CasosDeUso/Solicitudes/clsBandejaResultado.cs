using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Filtro <c>resultado</c> de la Bandeja (S5-HU-23.1/#289, HU-B25): al marcar Ganada o Perdida la
/// solicitud pasa a Entregada y sale de la vista por defecto "Pendientes", y la Bandeja no mostraba
/// el resultado. Decisión (opción A): columna Resultado + este filtro; NO se mezclan en
/// "Pendientes", que es la cola de trabajo del Administrador de Tarifas.
///
/// - <c>esperando</c>: cotización Entregada (o auto-costeada en Borrador, que nunca se entrega) con
///   Resultado SinDefinir y solicitud no Descartada — "ya salió, falta que el comercial diga si se ganó".
/// - <c>ganada</c> / <c>perdida</c>: solo esas.
/// </summary>
public static class clsBandejaResultado
{
    public const string Esperando = "esperando";
    public const string Ganada = "ganada";
    public const string Perdida = "perdida";

    /// <summary>null/vacío = sin filtro. Un valor desconocido es un error del cliente: ignorarlo
    /// devolvería TODA la bandeja como si el filtro no existiera.</summary>
    public static string? Parsear(string? texto)
    {
        var t = texto?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t)) return null;
        if (t is Esperando or Ganada or Perdida) return t;
        throw new clsValidationFailedException("RESULTADO_INVALIDO", "resultado debe ser 'esperando', 'ganada' o 'perdida'.");
    }
}
