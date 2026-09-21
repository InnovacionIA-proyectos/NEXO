namespace CabinaFletes.Infrastructure.External;

/// <summary>
/// Prospecto comercial (tblProspectos, dbSolucionesTI) — todavía no es un cliente real
/// con BP en el DW (DimBPSolicitante). Solo se proyectan NIT y CLIENTE (razón social);
/// el resto de columnas (contacto, seguimiento comercial, etc.) no hace falta para la
/// búsqueda del formulario de captura.
/// </summary>
public class Prospecto
{
    public string Nit { get; set; } = null!;
    public string Cliente { get; set; } = null!;
}
