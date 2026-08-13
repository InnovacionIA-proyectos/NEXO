namespace CabinaFletes.Application.Cotizaciones.Ports;

/// <summary>Comercial responsable de un cliente, para notificarle la cotización entregada.</summary>
public record ResponsableComercialDto(string Nombre, string Correo);

/// <summary>
/// Puerto hacia el Data Warehouse de cubos SAP (DWCubosEBSSAP.DimCliente, solo
/// lectura). Null si el DW no está configurado o el cliente no tiene responsable.
/// </summary>
public interface IComercialResponsablePort
{
    bool Configurado { get; }
    Task<ResponsableComercialDto?> BuscarPorNitAsync(string nit, CancellationToken ct);
}
