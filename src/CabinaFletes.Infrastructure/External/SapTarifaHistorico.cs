namespace CabinaFletes.Infrastructure.External;

/// <summary>
/// Estructura común de las tarifas SAP TM del datacenter corporativo: la tabla
/// vigente (dbo.tblSAPTarifas, "tarifas de plaza") y su log de cambios
/// (dbo.tblSAPTarifasLog) comparten estas columnas. Modelos de SOLO LECTURA:
/// las tablas las mantiene la integración SAP.
/// </summary>
public abstract class SapTarifaBase
{
    public int Id { get; set; }
    public int LineaNegocio { get; set; }               // código de línea (1, 2, 4, 6, …)
    public string Origen { get; set; } = null!;         // código DIVIPOLA o zona SAP (ZC_/ZTC_)
    public string Destino { get; set; } = null!;        // código DIVIPOLA o zona SAP (ZC_/ZTC_)
    public string Valor { get; set; } = null!;          // importe como texto (origen SAP)
    public DateTime Vigencia { get; set; }
    public int? ClaseContenedor { get; set; }
    public string? TamanoContenedor { get; set; }
    public int? RangoPesoInf { get; set; }              // kg
    public int? RangoPesoSup { get; set; }              // kg
    public string TipoVehiculo { get; set; } = null!;   // código o vacío
    public string TipoCarroseria { get; set; } = null!; // código o vacío
    public string? PesoLimite { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string UsuarioCreacion { get; set; } = null!;
    public int ClaseViaje { get; set; }
    public bool? Devolucion { get; set; }
}

/// <summary>
/// Tarifa de plaza (vigente) de SAP TM (dbo.tblSAPTarifas). Trae columnas
/// adicionales que el log no tiene (detalles, flags, fecha de modificación).
/// </summary>
public class SapTarifaPlaza : SapTarifaBase
{
    public DateTime FechaModificacion { get; set; }
    public string UsuarioModificacion { get; set; } = null!;
    public string? DetallesTarifa { get; set; }
    public bool? Consolidado { get; set; }
    public bool? Exclusivo { get; set; }
}

/// <summary>
/// Log de cambios de tarifas SAP TM (dbo.tblSAPTarifasLog): reemplaza al antiguo
/// tblSAPTarifasHistorico como fuente del histórico. LINEANEGOCIO y CLASEVIAJE
/// llegan como varchar con el código numérico (conversión en el mapeo EF).
/// </summary>
public class SapTarifaLog : SapTarifaBase
{
    public int IdTarifa { get; set; }                   // fila de tblSAPTarifas que originó el registro
}
