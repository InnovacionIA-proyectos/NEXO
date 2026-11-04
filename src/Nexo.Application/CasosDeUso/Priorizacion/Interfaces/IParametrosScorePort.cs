using Nexo.Domain.Priorizacion.Entidades;

namespace Nexo.Application.CasosDeUso.Priorizacion.Interfaces;

/// <summary>
/// Administración de los parámetros: devuelve la entidad con seguimiento porque el caso de uso la modifica y guarda
/// (el puerto de lectura de Solicitudes solo calcula).
/// </summary>
public interface IParametrosScorePort
{
    Task<clsParametrosScore> ParaEditarAsync(CancellationToken ct = default);
}
