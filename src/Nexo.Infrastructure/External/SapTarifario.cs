namespace Nexo.Infrastructure.External;

/// <summary>Tablas del datacenter (solo lectura) del tarifario SAP: zonas de tarificación, rangos de peso, parámetros y clases de contenedor.</summary>

/// <summary>Zona de tarificación (ZTC_*) → zonas de transporte (ZC_*); fuente del fallback por zonas:
/// una ciudad pertenece a la zona cuyo IDZONATRANSPORTE contiene su nombre.</summary>
public class clsSapZonaTarificacionRelacion
{
    public int Id { get; set; }
    public string IdZonaTarificacion { get; set; } = null!; // ZTC_CALI
    public string IdZonaTransporte { get; set; } = null!;   // ZC_YUMBO
}

/// <summary>Rango de peso → configuración vehicular SICE-TAC; con tblContenedorRangoPeso deriva la config de viajes en contenedor.</summary>
public class clsSapRangoPeso
{
    public int Id { get; set; }
    public int PesoMinimo { get; set; }   // kg
    public int PesoMaximo { get; set; }   // kg
    public string TipoVehiculo { get; set; } = null!; // config SICE-TAC: 2, 2S2, 2S3, 3S3
}

/// <summary>Rangos de peso aplicables a cada clase de contenedor (tblContenedorRangoPeso).</summary>
public class clsSapContenedorRangoPeso
{
    public int Id { get; set; }
    public int IdClaseContenedor { get; set; }
    public int IdRangoPeso { get; set; }
}

/// <summary>Parámetros del tarifario (tblParametros): 457 = nombre tipo carga cupo ("General"), 458 = nombre unidad transporte cupo ("ESTACAS").</summary>
public class clsSapParametro
{
    public string DataAreaId { get; set; } = null!;
    public int IdParametro { get; set; }
    public string ValorParametro { get; set; } = null!;
    public string? NombreParametro { get; set; }
}

/// <summary>Resuelve el texto local ('CNT20.IT') al ID que referencian las tarifas SAP, con la TARA del equipo.</summary>
public class clsSapClaseContenedorCatalogo
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = null!;  // CNT40.HC, CNT20.IT, …
    public int? TamanoContenedor { get; set; }        // 20 / 40
    public int? PesoInfCnt { get; set; }              // TARA inferior (kg)
    public int? PesoSupCnt { get; set; }              // TARA superior (kg)
}
