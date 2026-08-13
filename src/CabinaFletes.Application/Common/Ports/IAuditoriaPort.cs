namespace CabinaFletes.Application.Common.Ports;

/// <summary>
/// Registro de auditoría append-only (quién hizo qué, y cuándo). Puerto COMPARTIDO
/// por todos los módulos (no es específico de Cotizaciones): vive en Common.
/// </summary>
public interface IAuditoriaPort
{
    void Registrar(Guid? usuarioId, string entidad, Guid entidadId, string accion, string diff);
}
