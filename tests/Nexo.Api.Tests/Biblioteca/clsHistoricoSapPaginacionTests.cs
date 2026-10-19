using System.Net;
using System.Net.Http.Json;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>
/// S6-HU-29.2/#312 contra el histórico SAP REAL (dbo.tblSAPTarifasLog, ~26 mil filas): <c>GET /api/biblioteca-sap-historico</c> aceptaba solo
/// <c>top</c> y las filas más allá de las primeras eran inalcanzables. Ahora <c>page</c> (desde 1) + <c>top</c> como tamaño de página, con
/// <c>total</c> sin paginar y orden estable — el mismo contrato que ya tenía la plaza.
/// </summary>
[Collection("api")]
public class clsHistoricoSapPaginacionTests(clsApiFixture fx)
{
    private async Task<clsSapHistoricoResultDto> Pagina(string query)
    {
        var resp = await fx.Client("Comercial").GetAsync($"/api/biblioteca-sap-historico?{query}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<clsSapHistoricoResultDto>())!;
    }

    [Fact]
    public async Task La_pagina_2_trae_filas_distintas_a_la_pagina_1_y_el_total_es_el_mismo_sin_paginar()
    {
        var p1 = await Pagina("top=10&page=1");
        var p2 = await Pagina("top=10&page=2");

        Assert.Equal(10, p1.Items.Count);
        Assert.Equal(10, p2.Items.Count);
        Assert.Empty(p1.Items.Select(i => i.Id).Intersect(p2.Items.Select(i => i.Id)));   // filas distintas
        Assert.True(p1.Total > 20, "Se esperaba un histórico con más de 20 filas para poder paginar.");
        Assert.Equal(p1.Total, p2.Total);                                                   // el total NO es el de la página
    }

    [Fact]
    public async Task El_orden_es_estable_la_misma_pagina_pedida_dos_veces_da_las_mismas_filas_en_el_mismo_orden()
    {
        var a = await Pagina("top=25&page=3");
        var b = await Pagina("top=25&page=3");

        Assert.Equal(a.Items.Select(i => i.Id), b.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Sin_page_responde_la_primera_pagina_como_siempre()
    {
        var sinPage = await Pagina("top=10");
        var pagina1 = await Pagina("top=10&page=1");

        Assert.Equal(pagina1.Items.Select(i => i.Id), sinPage.Items.Select(i => i.Id));
        Assert.Equal(pagina1.Total, sinPage.Total);
    }

    [Fact]
    public async Task Una_pagina_mas_alla_del_final_responde_lista_vacia_pero_con_el_total()
    {
        // 200 es el tamaño de página máximo efectivo del histórico (ver clsSapHistoricoAdapter.MaxTop): un top mayor se reduce a 200.
        var total = (await Pagina("top=200&page=1")).Total;
        var ultimaMasUno = (total / 200) + 2;

        var vacia = await Pagina($"top=200&page={ultimaMasUno}");

        Assert.Empty(vacia.Items);
        Assert.Equal(total, vacia.Total);
    }

    [Fact]
    public async Task Page_0_se_rechaza_con_400()
    {
        var resp = await fx.Client("Comercial").GetAsync("/api/biblioteca-sap-historico?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
