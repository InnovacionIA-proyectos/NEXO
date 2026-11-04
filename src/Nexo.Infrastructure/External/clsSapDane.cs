namespace Nexo.Infrastructure.External;

/// <summary>Catálogo DANE de centros poblados (tblsapdane, solo lectura): resuelve los códigos DIVIPOLA del histórico SAP a nombres legibles.</summary>
public class clsSapDane
{
    public decimal Id { get; set; }
    public string CodigoCentroPoblado { get; set; } = null!; // llave de cruce (== ORIGEN/DESTINO)
    public string NombreDepartamento { get; set; } = null!;
    public string NombreMunicipio { get; set; } = null!;
    public string NombreCentroPoblado { get; set; } = null!;
    public string? NombreMostrar { get; set; }               // "Medellín (ANT)" — puede venir vacío
}
