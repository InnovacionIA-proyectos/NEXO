using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Excepciones;

/// <summary>
/// Una cotización sin líneas de flete no se puede entregar ni marcar como ganada o perdida. Hereda de clsValidationFailedException para
/// conservar el 400 y el código que usa el front.
/// </summary>
public sealed class clsCotizacionSinFleteException(string accion)
    : clsValidationFailedException("COTIZACION_SIN_FLETE", $"No se puede {accion} una cotización sin líneas de flete.");
