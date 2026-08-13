namespace CabinaFletes.Domain.Entities;

/// <summary>
/// Entidad con timestamps de auditoría. El DbContext estampa CreatedAt al insertar
/// y UpdatedAt al insertar/actualizar, de forma automática.
/// (audit_log queda fuera a propósito: es append-only y no se actualiza.)
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
