using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Infrastructure.Biblioteca.Adaptadores;
using Xunit;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>
/// S2-HU-09.3 (#33): el adaptador mock de plaza en vivo, sin broker ni base de datos —
/// pura lógica de dataset provisional. Vive en Api.Tests (no en Application.Tests)
/// porque es una clase de Infrastructure, mismo criterio que ContratoCubicacionTests.
/// </summary>
public class SapTarifaFleteEnVivoMockAdapterTests
{
    private readonly SapTarifaFleteEnVivoMockAdapter _adapter = new();

    private static ConsultaTarifarioSap Consulta(string origen, string destino, decimal? peso = 8000) =>
        new(origen, destino, null, null, null, null, null, peso, false, false);

    [Fact]
    public void Esta_siempre_configurado()
    {
        Assert.True(_adapter.Configurado);
    }

    [Fact]
    public async Task Una_ruta_del_dataset_devuelve_un_candidato_marcado_como_mock()
    {
        var candidato = await _adapter.ConsultarPlazaAsync(Consulta("Bogota", "Medellin"));

        Assert.NotNull(candidato);
        Assert.Equal("sap_en_vivo_mock", candidato!.Fuente);
        Assert.True(candidato.Valor > 0);
    }

    [Fact]
    public async Task La_ruta_es_simetrica()
    {
        var ida = await _adapter.ConsultarPlazaAsync(Consulta("Bogota", "Cali"));
        var vuelta = await _adapter.ConsultarPlazaAsync(Consulta("Cali", "Bogota"));

        Assert.NotNull(ida);
        Assert.NotNull(vuelta);
        Assert.Equal(ida!.Valor, vuelta!.Valor);
    }

    [Fact]
    public async Task Una_ruta_fuera_del_dataset_no_tiene_candidato()
    {
        // Null, no una excepción: es "no hay dato", igual semántica que ITarifarioSapPort.
        var candidato = await _adapter.ConsultarPlazaAsync(Consulta("Leticia", "Mitú"));

        Assert.Null(candidato);
    }

    [Fact]
    public async Task Un_peso_mayor_al_de_referencia_sube_el_valor_pero_no_lo_dispara()
    {
        var base8Ton = await _adapter.ConsultarPlazaAsync(Consulta("Bogota", "Medellin", 8000));
        var doble = await _adapter.ConsultarPlazaAsync(Consulta("Bogota", "Medellin", 16000));

        Assert.True(doble!.Valor > base8Ton!.Valor);
        // El factor está acotado (0.6x–1.6x): no escala linealmente sin límite.
        Assert.True(doble.Valor <= base8Ton.Valor * 1.6m);
    }

    [Fact]
    public async Task Sin_peso_usa_la_tarifa_base_sin_ajustar()
    {
        var base8Ton = await _adapter.ConsultarPlazaAsync(Consulta("Bogota", "Medellin", 8000));
        var sinPeso = await _adapter.ConsultarPlazaAsync(Consulta("Bogota", "Medellin", null));

        Assert.Equal(base8Ton!.Valor, sinPeso!.Valor);
    }
}
