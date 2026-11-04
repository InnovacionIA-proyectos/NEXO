using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Catálogo de municipios (Colombia/Venezuela/Ecuador) para el desplegable de Origen/Destino. Poblado una
/// sola vez por script, sin sincronización periódica. El costeo usa <see cref="CodigoDane"/> para SICE-TAC
/// y <see cref="NombreTarifasPlaza"/>/<see cref="ZonaTarificacion"/> para el Tarifario SAP (que usa códigos ZC_/ZTC_).
/// </summary>
public class clsMunicipio
{
    /// <summary>Autoincremental propio: el código DANE no sirve de llave primaria porque se repite entre Venezuela y Ecuador.</summary>
    public int Id { get; set; }

    /// <summary>Código DANE/DIVIPOLA con ceros a la izquierda. En Colombia termina en "000" (nivel municipio) salvo excepciones de corregimiento. Único solo junto con Pais.</summary>
    [Required, MaxLength(20)]
    public string CodigoDane { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Municipio { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Departamento { get; set; } = null!;

    /// <summary>"CO" / "VE" / "EC".</summary>
    [Required, MaxLength(2)]
    public string Pais { get; set; } = null!;

    /// <summary>Código ZC_ del Tarifario SAP (ej. "ZC_MEDELLIN"). Null si el municipio no tiene movimiento comercial.</summary>
    [MaxLength(60)]
    public string? NombreTarifasPlaza { get; set; }

    /// <summary>Código ZTC_ (zona agregada) de <see cref="NombreTarifasPlaza"/>. Null si esta es null o si el código ZC_ no está en la relación oficial.</summary>
    [MaxLength(60)]
    public string? ZonaTarificacion { get; set; }
}
