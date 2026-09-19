namespace CabinaFletes.Infrastructure.External;

/// <summary>
/// Catálogos SAP del datacenter (dbsolucionesTI, SOLO LECTURA) que resuelven los
/// códigos del histórico de tarifas a descripciones legibles.
/// </summary>

/// <summary>Línea de negocio (tblSAPLineaNegocio). El histórico referencia el ID.</summary>
public class SapLineaNegocio
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = null!;
}

/// <summary>Tipo de vehículo tarifario (tblSAPTiposVehiculoTarifario). El histórico referencia el ID.</summary>
public class SapTipoVehiculo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;      // 3S3, 2S2, C2, …
    public string Descripcion { get; set; } = null!; // Tractomula, Minimula, …
    public bool Estado { get; set; }
}

/// <summary>Tipo de carrocería (tblSAPTiposCarroseria). El histórico referencia el CODIGO.</summary>
public class SapTipoCarroseria
{
    public decimal Id { get; set; }
    public string Codigo { get; set; } = null!;      // 1, 2, 4, 11, …
    public string Descripcion { get; set; } = null!; // CAMABAJA, ESTACAS, FURGON, …
}

/// <summary>Clase de viaje (tblSAPClaseViaje). El histórico referencia el ID.</summary>
public class SapClaseViaje
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = null!; // Contenedor, Cupo, Carga Suelta, …
}
