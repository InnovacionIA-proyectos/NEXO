namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Código con el que se busca un vehículo de NEXO en cada fuente de tarifas. SICE-TAC y Plaza/SAP lo nombran distinto
/// (Sencillo: <c>C2</c> en NEXO, <c>2</c> en las fuentes; Turbo <c>2L1</c>/<c>2L3</c> se tarifa en plaza como <c>2L2</c>);
/// con el código de NEXO no hay filas y se elegiría sin aviso el vehículo más barato de la ruta.
/// </summary>
public class clsMapeoVehiculoTarifario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>El vehículo de NEXO, ya resuelto por el diccionario de alias. Único.</summary>
    public string TipoVehiculo { get; set; } = null!;

    /// <summary>Valor de <c>CONFIGURACION</c> en <c>tblRutasSicetac</c>.</summary>
    public string CodigoSicetac { get; set; } = null!;

    public string? NombreSicetac { get; set; }

    /// <summary>Código en Plaza/SAP. Null = ese vehículo no tiene tarifa de plaza (solo existe en SICE-TAC).</summary>
    public string? CodigoPlazaSap { get; set; }

    public string? NombreTarifasPlaza { get; set; }

    /// <summary>De dónde salió cada código y cómo se confirmó.</summary>
    public string? Verificacion { get; set; }

    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
