using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Common;

namespace Nexo.Domain.Costeo.Entidades;

/// <summary>
/// Ajuste local de la vigencia de una tarifa de plaza de SAP TM (la fuente es de solo lectura). La vigencia
/// efectiva es este valor si existe, o la de SAP si no; rige en la Biblioteca y en el costeo.
/// </summary>
public class clsAjusteVigenciaPlaza : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Id de la fila de tblSAPTarifas a la que aplica el ajuste. Único.</summary>
    public int TarifaPlazaId { get; set; }

    public DateTime VigenciaAjustada { get; set; }

    /// <summary>Vigencia original de SAP al momento del ajuste (referencia/auditoría).</summary>
    public DateTime? VigenciaOriginal { get; set; }

    [MaxLength(300)]
    public string? Motivo { get; set; }

    public Guid? AjustadoPor { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
