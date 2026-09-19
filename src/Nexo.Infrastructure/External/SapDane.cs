namespace Nexo.Infrastructure.External;

/// <summary>
/// Catálogo DANE de centros poblados en el datacenter corporativo
/// (dbsolucionesTI.dbo.tblsapdane). SOLO LECTURA. Resuelve los códigos
/// DIVIPOLA de origen/destino del histórico SAP a nombres legibles.
/// </summary>
public class SapDane
{
    public decimal Id { get; set; }
    public string CodigoCentroPoblado { get; set; } = null!; // llave de cruce (== ORIGEN/DESTINO)
    public string NombreDepartamento { get; set; } = null!;
    public string NombreMunicipio { get; set; } = null!;
    public string NombreCentroPoblado { get; set; } = null!;
    public string? NombreMostrar { get; set; }               // "Medellín (ANT)" — puede venir vacío
}
