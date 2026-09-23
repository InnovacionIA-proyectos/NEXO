using Nexo.Domain.Priorizacion.Entidades;

namespace Nexo.Application.CasosDeUso.Priorizacion.Interfaces;

/// <summary>
/// Los parámetros del score, para leerlos y para recalibrarlos.
///
/// Existe un puerto de lectura en Solicitudes (<c>ParametrosScoreAsync</c>) que sirve
/// para calcular. Este es el de la ADMINISTRACIÓN de esos parámetros: devuelve la
/// entidad con seguimiento porque el caso de uso la va a modificar y guardar, mientras
/// que el otro solo necesita leerla.
///
/// Son dos intenciones distintas sobre la misma fila, y separarlas evita que un puerto
/// de consulta empiece a devolver entidades editables "por si acaso".
/// </summary>
public interface IParametrosScorePort
{
    /// <summary>La fila única, con seguimiento: el llamador la va a modificar.</summary>
    Task<clsParametrosScore> ParaEditarAsync(CancellationToken ct = default);
}
