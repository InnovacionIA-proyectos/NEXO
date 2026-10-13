using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Costeo;

/// <summary>
/// S5-HU-19.1/#281: contra la base real de pruebas, no un servidor de mentira — los
/// datos de <c>tbl_tarifas_plaza</c> son los reales sembrados en S4-HU-10.1
/// (Buenaventura → Pereira/Cali/Manizales/Medellín, "Oferta de Expedición Puerto a
/// interior", Contenedor, CNT40.HC). Confirma el nivel exacto (las 5 dimensiones) y el
/// nivel tolerante (se relaja lo que deja 0 filas) contra filas que de verdad existen.
/// </summary>
[Collection("api")]
public class clsTarifaPlazaInternaE2ETests(clsApiFixture fixture)
{
    [Fact]
    public async Task Nivel_exacto_encuentra_Buenaventura_Pereira_con_las_5_dimensiones_correctas()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var puerto = scope.ServiceProvider.GetRequiredService<ITarifaPlazaInternaPort>();

        // Peso=10000 cae en la banda PesoId=1: SIN peso, "Buenaventura→Pereira 2S2" tiene
        // dos filas reales (PesoId=1, Valor=3.300.000 y PesoId=2, Valor=3.400.000) — el
        // peso es lo único que las distingue, hace falta para un match realmente exacto.
        var resultado = await puerto.BuscarAsync(new clsConsultaTarifaPlazaInterna(
            "Buenaventura", "Pereira", "Oferta de Expedición Puerto a interior", "Contenedor", "CNT40.HC", "2S2", 10_000m));

        Assert.NotNull(resultado.Vigente);
        Assert.Equal(3_300_000m, resultado.Vigente!.Valor);
        Assert.Equal("NEXO", resultado.Vigente.TablaOrigen);
        Assert.Contains("nivel exacto", resultado.Vigente.Criterio);
        Assert.Null(resultado.Vencida); // tbl_tarifas_plaza nunca marca vencida — ver el puerto.
    }

    [Fact]
    public async Task Nivel_tolerante_encuentra_la_fila_aunque_la_linea_de_negocio_no_coincida()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var puerto = scope.ServiceProvider.GetRequiredService<ITarifaPlazaInternaPort>();

        // La fila real tiene LineaNegocio "Oferta de Expedición Puerto a interior" — con
        // una línea de negocio que NO calza con ninguna fila, el nivel exacto da 0 y debe
        // relajarse hasta encontrar la fila por municipios + el resto de los filtros.
        var resultado = await puerto.BuscarAsync(new clsConsultaTarifaPlazaInterna(
            "Buenaventura", "Pereira", "Línea de negocio que no existe", "Contenedor", "CNT40.HC", "2S2", 10_000m));

        Assert.NotNull(resultado.Vigente);
        Assert.Equal(3_300_000m, resultado.Vigente!.Valor);
        Assert.Contains("nivel tolerante", resultado.Vigente.Criterio);
    }

    [Fact]
    public async Task Sin_peso_en_la_consulta_no_filtra_por_banda_y_toma_el_mayor_entre_las_bandas()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var puerto = scope.ServiceProvider.GetRequiredService<ITarifaPlazaInternaPort>();

        // Sin Peso, PesoId no se resuelve — "coincide con cualquier banda" (no se puede
        // distinguir cuál banda aplica), y entre las que matchean el resto de los filtros
        // (PesoId=1 Valor=3.300.000, PesoId=2 Valor=3.400.000) gana la de mayor valor.
        var resultado = await puerto.BuscarAsync(new clsConsultaTarifaPlazaInterna(
            "Buenaventura", "Pereira", "Oferta de Expedición Puerto a interior", "Contenedor", "CNT40.HC", "2S2", null));

        Assert.NotNull(resultado.Vigente);
        Assert.Equal(3_400_000m, resultado.Vigente!.Valor);
    }

    [Fact]
    public async Task Sin_ninguna_fila_para_el_par_de_municipios_no_hay_candidato()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var puerto = scope.ServiceProvider.GetRequiredService<ITarifaPlazaInternaPort>();

        var resultado = await puerto.BuscarAsync(new clsConsultaTarifaPlazaInterna(
            "Ciudad que no existe en ningún municipio", "Otra ciudad inexistente", null, null, null, null, null));

        Assert.Null(resultado.Vigente);
        Assert.Null(resultado.Vencida);
    }

    [Fact]
    public async Task Historico_interno_no_revienta_contra_una_ruta_sin_historico()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var puertoHistorico = scope.ServiceProvider.GetRequiredService<IHistoricoPlazaInternoPort>();

        // tbl_tarifas_plaza_historico solo se llena por trigger al editar — en la base de
        // pruebas puede no tener filas para esta ruta; lo que importa es que no falle.
        var historico = await puertoHistorico.BuscarAsync(new clsConsultaTarifaPlazaInterna(
            "Buenaventura", "Pereira", null, null, null, null, null));

        // Sin asserts sobre el valor (depende de si alguien editó esa tarifa en los
        // últimos 3 meses en la base compartida) — solo que la consulta no lanza.
        _ = historico;
    }
}
