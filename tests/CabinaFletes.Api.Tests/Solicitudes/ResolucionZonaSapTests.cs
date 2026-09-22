using CabinaFletes.Domain.Common;
using CabinaFletes.Infrastructure.Solicitudes.Adaptadores;

namespace CabinaFletes.Api.Tests.Solicitudes;

/// <summary>
/// HU-005 parte 3: réplica en miniatura de la regla ciudad→zona que ya usa
/// TarifarioSapAdapter. Casos tomados directo de la verificación contra datos reales
/// (dbSolucionesTI, 2026-09-21): 577 códigos de zona, 564 resuelven con la regla
/// automática; los 13 restantes están en <see cref="ResolucionZonaSap.AliasZonasAgregadas"/>.
/// </summary>
public class ResolucionZonaSapTests
{
    /// <summary>Pipeline completo, igual al que arma CiudadesDwAdapter: normaliza, quita
    /// prefijo y expande abreviaturas de CADA código de zona antes de comparar.</summary>
    private static bool Resuelve(string municipio, params string[] codigosDeZonaBrutos)
    {
        var zonas = codigosDeZonaBrutos
            .Select(c => ResolucionZonaSap.SinPrefijoZona(Texto.Normalizar(c)))
            .SelectMany(ResolucionZonaSap.ExpandirAbreviaturas)
            .ToList();
        return ResolucionZonaSap.Resuelve(Texto.Normalizar(municipio), zonas);
    }

    [Fact]
    public void Ciudad_con_codigo_de_zona_directo_resuelve()
    {
        // ZC_BOGOTA -> "BOGOTA" tras quitar prefijo.
        Assert.True(Resuelve("Bogotá", "BOGOTA"));
    }

    [Fact]
    public void Ciudad_que_solo_resuelve_via_zona_agregada_de_tblZonasTarificacionRelacion()
    {
        // Caso real: Medellín no tiene tarifa directa a su nombre en algunos productos,
        // pero tblZonasTarificacionRelacion mapea ZC_MEDELLIN -> ZTC_VABURRA. El conjunto
        // de "zonas válidas" que le pasa el adapter ya incluye ambos códigos (directo +
        // relación), sin prefijo.
        Assert.True(Resuelve("Medellín", "MEDELLIN", "VABURRA"));
    }

    [Theory]
    [InlineData("Puerto Nare", "PTO NARE")]
    [InlineData("Puerto Wilches", "PTO WILCHES")]
    [InlineData("Santa Rosa de Cabal", "S ROSA DE CABAL")]
    [InlineData("San Juan del Cesar", "S JUAN DEL CESAR")]
    [InlineData("Santa Fe de Antioquia", "STFE DE ANTIOQUIA")]
    [InlineData("Santander de Quilichao", "SANT DE QUILICHAO")]
    public void Abreviaturas_reales_de_los_codigos_de_zona_resuelven(string municipioReal, string codigoAbreviado)
    {
        Assert.True(Resuelve(municipioReal, codigoAbreviado));
    }

    [Fact]
    public void Ciudad_sin_ninguna_zona_relacionada_no_resuelve()
    {
        Assert.False(Resuelve("Leticia", "BOGOTA", "CALI", "VABURRA"));
    }

    [Fact]
    public void SinPrefijoZona_quita_ZTC_y_ZC_pero_no_toca_lo_demas()
    {
        Assert.Equal("VABURRA", ResolucionZonaSap.SinPrefijoZona("ZTC_VABURRA"));
        Assert.Equal("MEDELLIN", ResolucionZonaSap.SinPrefijoZona("ZC_MEDELLIN"));
        Assert.Equal("BOGOTA", ResolucionZonaSap.SinPrefijoZona("BOGOTA"));
    }

    [Fact]
    public void Zonas_agregadas_conocidas_tienen_alias_manual()
    {
        Assert.True(ResolucionZonaSap.AliasZonasAgregadas.ContainsKey("VABURRA"));
        Assert.Equal("Valle de Aburrá", ResolucionZonaSap.AliasZonasAgregadas["VABURRA"].Municipio);
    }

    [Fact]
    public void Diccionario_de_alias_cubre_los_13_casos_verificados()
    {
        // 577 códigos de zona totales, 564 resuelven con la regla automática (abreviaturas
        // + zonas). Los 13 restantes deben estar todos acá — si falta alguno, un código de
        // zona real quedaría sin ninguna ciudad ofrecida en el autocompletar.
        Assert.Equal(13, ResolucionZonaSap.AliasZonasAgregadas.Count);
    }

    [Theory]
    [InlineData("DON MATIAS", "Don Matías")]
    [InlineData("CARTAGENA (BOL)", "Cartagena de Indias")]
    [InlineData("CATAMBUCO (NAR)", "Catambuco (Pasto)")]
    [InlineData("S J DE LA MONTANA", "San José de la Montaña")]
    [InlineData("SAN A DE CUERQUIA", "San Andrés de Cuerquía")]
    [InlineData("DORADAL", "Doradal (Puerto Triunfo)")]
    [InlineData("SAN B DEL VIENTO", "San Bernardo del Viento")]
    [InlineData("S J DEL GUAVIARE", "San José del Guaviare")]
    [InlineData("BOLOMBOLO", "Bolombolo (Venecia)")]
    [InlineData("CHIBOLO", "Chibolo")]
    public void Casos_reales_sin_match_automatico_tienen_alias_correcto(string codigoZona, string municipioEsperado)
    {
        Assert.Equal(municipioEsperado, ResolucionZonaSap.AliasZonasAgregadas[codigoZona].Municipio);
    }
}
