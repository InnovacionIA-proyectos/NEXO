namespace Nexo.Domain.Common;

/// <summary>Entidad con timestamps de auditoría; el DbContext estampa CreatedAt al insertar y UpdatedAt al insertar/actualizar.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
