using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones.Excepciones;

/// <summary>
/// Una cotización sin líneas de flete no se puede entregar ni marcar como ganada o
/// perdida: no hay nada que cotizar.
///
/// Tiene tipo propio porque es una regla del negocio que aparece en dos casos de uso
/// distintos, no un error de forma de un comando concreto. Hereda de
/// ValidationFailedException para conservar el 400 y el código que el frontend ya usa.
///
/// Su sitio natural sería Domain, junto al agregado Cotización. Queda anotado para
/// cuando se toque ese agregado; mover una regla de capa es un cambio con criterio
/// propio, no un efecto colateral de una reorganización de carpetas.
/// </summary>
public sealed class CotizacionSinFleteException(string accion)
    : ValidationFailedException("COTIZACION_SIN_FLETE", $"No se puede {accion} una cotización sin líneas de flete.");
