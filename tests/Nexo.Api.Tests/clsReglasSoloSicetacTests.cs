using System.Net.Http.Json;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;

namespace Nexo.Api.Tests;

// Front pedía el nombre del municipio, no el código DANE crudo, en el listado
// (administracion_embarqa_nexo#195/NEXO#242-adjacent). Usa una regla real ya cargada
// (origen "05001000" = Medellín, Antioquia) en vez de crear una nueva: el valor crudo
// que guardan las reglas reales es código DANE, exactamente el caso que hay que resolver.
[Collection("api")]
public class clsReglasSoloSicetacTests(clsApiFixture fixture)
{
    [Fact]
    public async Task El_listado_resuelve_el_codigo_DANE_a_nombre_de_municipio()
    {
        var reglas = await fixture.Client("AdminTarifas")
            .GetFromJsonAsync<List<clsReglaSoloSicetacDto>>("/api/reglas-solo-sicetac");

        var conOrigenMedellin = reglas!.Where(r => r.Origen == "05001000").ToList();
        Assert.NotEmpty(conOrigenMedellin); // hay datos reales cargados con ese código
        Assert.All(conOrigenMedellin, r => Assert.Equal("MEDELLIN, ANTIOQUIA", r.OrigenMunicipio));
    }
}
