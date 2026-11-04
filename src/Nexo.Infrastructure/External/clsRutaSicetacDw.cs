namespace Nexo.Infrastructure.External;

/// <summary>Fila de la matriz oficial SICE-TAC (DWSice.dbo.tblRutasSicetac). Solo lectura: la mantiene el ETL del DW.</summary>
public class clsRutaSicetacDw
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
