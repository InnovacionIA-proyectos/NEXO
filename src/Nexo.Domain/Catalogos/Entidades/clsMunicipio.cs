using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Catálogo propio de municipios (Colombia/Venezuela/Ecuador) para el desplegable de
/// Origen/Destino en Captura de Solicitudes — evita que el comercial digite el nombre
/// de la ciudad a mano. Sembrado UNA SOLA VEZ (S4-HU-09.1) desde tblSAPDANE +
/// tblZonasTarificacionRelacion + tblSAPTarifas/tblSAPTarifasLog del datacenter
/// corporativo; no hay sincronización periódica (ver
/// <c>clsSembrarMunicipiosCommandHandler</c>). El motor de costeo usa internamente
/// <see cref="CodigoDane"/> para SICE-TAC (join directo contra
/// DWSice.tblRutasSicetac.ORIGEN/DESTINO, que ya son códigos DANE) y
/// <see cref="NombreTarifasPlaza"/>/<see cref="ZonaTarificacion"/> para el Tarifario SAP
/// (que identifica ciudades por código ZC_/ZTC_, no por código DANE).
/// </summary>
public class clsMunicipio
{
    /// <summary>Autoincremental propio de Nexo — el código DANE NO sirve como llave
    /// primaria (choca entre Venezuela y Ecuador, verificado contra datos reales:
    /// ~90 códigos numéricos se repiten entre los dos países).</summary>
    public int Id { get; set; }

    /// <summary>Código DANE/DIVIPOLA de origen (tblSAPDANE.CODIGOCENTROPOBLADO tal
    /// cual, con ceros a la izquierda). Para Colombia siempre termina en "000"
    /// (nivel de municipio, no centro poblado) salvo las 5 excepciones de
    /// corregimiento documentadas en <c>clsResolucionMunicipios</c>. No es único
    /// por sí solo entre países — la combinación real es (CodigoDane, Pais).</summary>
    [Required, MaxLength(20)]
    public string CodigoDane { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Municipio { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Departamento { get; set; } = null!;

    /// <summary>"CO" / "VE" / "EC" — únicos 3 países presentes en tblSAPDANE
    /// (verificado contra datos reales: 8.019 + 1.424 + 1.252 filas, sin residuo).</summary>
    [Required, MaxLength(2)]
    public string Pais { get; set; } = null!;

    /// <summary>Código ZC_ del Tarifario SAP que matcheó con este municipio
    /// (ej. "ZC_MEDELLIN"). Null si no se encontró ningún código de zona para
    /// este municipio en tblSAPTarifas/tblSAPTarifasLog — no todos los 1.122
    /// municipios de Colombia tienen movimiento comercial.</summary>
    [MaxLength(60)]
    public string? NombreTarifasPlaza { get; set; }

    /// <summary>Código ZTC_ (zona agregada) de tblZonasTarificacionRelacion para
    /// <see cref="NombreTarifasPlaza"/>. Null cuando <see cref="NombreTarifasPlaza"/>
    /// también es null, O cuando el código ZC_ sí aparece en el tarifario pero NO
    /// está en la relación oficial — este segundo caso queda señalado así (no se
    /// omite la fila), tal como pide la HU.</summary>
    [MaxLength(60)]
    public string? ZonaTarificacion { get; set; }
}
