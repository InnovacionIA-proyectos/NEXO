namespace Nexo.Infrastructure.External;

/// <summary>Prospecto comercial (tblProspectos): aún no es cliente real con BP en el DW. Solo se proyectan NIT y CLIENTE, lo necesario para la búsqueda de captura.</summary>
public class clsProspecto
{
    public string Nit { get; set; } = null!;
    public string Cliente { get; set; } = null!;
}
