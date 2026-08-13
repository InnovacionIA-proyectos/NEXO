namespace CabinaFletes.Infrastructure.External;

/// <summary>
/// Fila de la matriz oficial de tarifas SICE-TAC en el Data Warehouse corporativo
/// (DWSice.dbo.tblRutasSicetac). Modelo de SOLO LECTURA: la tabla la mantiene el
/// proceso ETL del DW, esta aplicación nunca la escribe.
/// </summary>
public class RutaSicetacDw
{
    public int Id { get; set; }
    public int Periodo { get; set; }
    public string Configuracion { get; set; } = null!;
    public decimal Valor { get; set; }
    public string NombreRuta { get; set; } = null!;
    public string NombreTipoCarga { get; set; } = null!;
    public string NombreUnidadTransporte { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaModificacion { get; set; }
    public string? Origen { get; set; }
    public string? Destino { get; set; }
    public decimal? ValorHora { get; set; }
    public decimal? ValorSiceConHora { get; set; }
    public decimal? HorasRecorrido { get; set; }
    public int? HorasPactadas { get; set; }
    public string? CondicionCarga { get; set; }
    public string? ViaEstandar { get; set; }
}
