namespace Nexo.Domain.Costeo.Entidades;

/// <summary>
/// Snapshot del valor anterior de una <see cref="clsTarifaPlaza"/>, archivado por el trigger de BD
/// <c>TRG_tarifas_plaza_historico</c> cuando cambia Valor/Vigencia/DetallesTarifa. Nunca se escribe desde la aplicación.
/// </summary>
public class clsTarifaPlazaHistorico
{
    public Guid Id { get; set; }
    public Guid TarifaPlazaId { get; set; }

    public string LineaNegocio { get; set; } = null!;
    public string ClaseViaje { get; set; } = null!;
    public int OrigenMunicipioId { get; set; }
    public int DestinoMunicipioId { get; set; }
    public string? ClaseContenedor { get; set; }
    public int? PesoId { get; set; }
    public string? TipoVehiculo { get; set; }

    public decimal Valor { get; set; }
    public DateOnly Vigencia { get; set; }
    public string DetallesTarifa { get; set; } = null!;

    public Guid? CreadoPor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ReemplazadoAt { get; set; }
}
