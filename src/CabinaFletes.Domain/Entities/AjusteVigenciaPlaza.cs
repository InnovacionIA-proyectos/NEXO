using System.ComponentModel.DataAnnotations;

namespace CabinaFletes.Domain.Entities;

/// <summary>
/// Ajuste LOCAL de la fecha de vigencia de una tarifa de plaza de SAP TM. La fuente
/// (dbsolucionesTI.tblSAPTarifas) es de solo lectura, así que Nexo guarda aquí el
/// override por Id de tarifa: la vigencia EFECTIVA es este valor si existe, o la de
/// SAP si no. Rige tanto en la Biblioteca como en la selección de tarifa (propuesta/costeo).
/// </summary>
public class AjusteVigenciaPlaza : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Id de la fila de tblSAPTarifas (plaza) a la que aplica el ajuste. Único.</summary>
    public int TarifaPlazaId { get; set; }

    /// <summary>Nueva vigencia efectiva de la tarifa.</summary>
    public DateTime VigenciaAjustada { get; set; }

    /// <summary>Vigencia original de SAP al momento del ajuste (referencia/auditoría).</summary>
    public DateTime? VigenciaOriginal { get; set; }

    [MaxLength(300)]
    public string? Motivo { get; set; }

    public Guid? AjustadoPor { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
