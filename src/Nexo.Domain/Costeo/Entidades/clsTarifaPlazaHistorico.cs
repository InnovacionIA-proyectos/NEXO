namespace Nexo.Domain.Costeo.Entidades;

/// <summary>
/// Snapshot del valor ANTERIOR de una <see cref="clsTarifaPlaza"/>, archivado solo por el
/// trigger de BD <c>TRG_tarifas_plaza_historico</c> cuando cambia Valor/Vigencia/
/// DetallesTarifa — nunca se escribe desde la aplicación (sin método Agregar/puerto de
/// escritura). Mapeada por primera vez en S5-HU-16.3/#256: antes de esta issue
/// <c>tbl_tarifas_plaza_historico</c> existía como tabla SQL pero sin ningún mapeo EF —
/// el motor de costeo no podía consultar el histórico propio y caía directo al log
/// externo (tblSAPTarifasLog).
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
