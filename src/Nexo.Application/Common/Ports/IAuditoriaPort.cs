namespace Nexo.Application.Common.Ports;

/// <summary>Registro de auditoría append-only (quién hizo qué y cuándo), compartido por todos los módulos.</summary>
public interface IAuditoriaPort
{
    void Registrar(Guid? usuarioId, string entidad, Guid entidadId, string accion, string diff);
}
