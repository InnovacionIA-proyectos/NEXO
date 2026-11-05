using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Filtro <c>resultado</c> de la Bandeja: <c>esperando</c> (cotización Entregada, o auto-costeada en Borrador, sin resultado y solicitud no
/// descartada), <c>ganada</c> o <c>perdida</c>. No se mezcla con "Pendientes", que es la cola de trabajo.
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
