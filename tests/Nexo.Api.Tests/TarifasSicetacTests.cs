using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;
using ClosedXML.Excel;

namespace Nexo.Api.Tests;

[Collection("api")]
public class TarifasSicetacTests
{
    private readonly ApiFixture _fx;
    public TarifasSicetacTests(ApiFixture fx) => _fx = fx;

    // 1) Happy path — consulta filtrada
    [Fact]
    public async Task Consultar_por_origen_devuelve_resultados()
    {
        var items = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/tarifas-sicetac?origen=66170000");
        Assert.True(items.GetArrayLength() > 0);
        foreach (var it in items.EnumerateArray())
            Assert.Equal("66170000", it.GetProperty("origenDane").GetString());
    }

    [Fact] // Happy path — importar Excel (upsert)
    public async Task Importar_excel_devuelve_resumen()
    {
        var resp = await _fx.Client("Admin").PostAsync("/api/tarifas-sicetac/importar", ExcelContent());
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var r = await resp.Json();
        Assert.Equal(2, r.GetProperty("procesadas").GetInt32());
    }

    // 2) Validación — sin archivo
    [Fact]
    public async Task Importar_sin_archivo_devuelve_400()
    {
        var resp = await _fx.Client("Admin").PostAsync("/api/tarifas-sicetac/importar", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("ARCHIVO_REQUERIDO", await resp.ErrorCode());
    }

    // 3) Autenticación
    [Fact]
    public async Task Consultar_sin_token_devuelve_401()
    {
        var resp = await _fx.Anon().GetAsync("/api/tarifas-sicetac");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 5) Edge cases
    [Fact] // idempotencia: reimportar el mismo periodo/combo ACTUALIZA, no duplica
    public async Task Reimportar_actualiza_en_vez_de_duplicar()
    {
        await _fx.Client("Admin").PostAsync("/api/tarifas-sicetac/importar", ExcelContent());
        var resp = await _fx.Client("Admin").PostAsync("/api/tarifas-sicetac/importar", ExcelContent());
        var r = await resp.Json();
        Assert.True(r.GetProperty("actualizadas").GetInt32() >= 1);
        Assert.Equal(0, r.GetProperty("rechazadas").GetInt32());
    }

    [Fact] // historial de cargas
    public async Task Importaciones_devuelve_historial()
    {
        await _fx.Client("Admin").PostAsync("/api/tarifas-sicetac/importar", ExcelContent());
        var items = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/tarifas-sicetac/importaciones");
        Assert.True(items.GetArrayLength() > 0);
    }

    private static MultipartFormDataContent ExcelContent()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        string[] headers = { "PERIODO", "ORIGEN", "DESTINO", "CONFIGURACION", "NOMBRETIPOCARGA", "NOMBREUNIDADTRANSPORTE", "CONDICIONCARGA", "VALOR" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        object[,] rows =
        {
            { 202606, "66170000", "05615000", "2L2", "General", "Estibas", "Cargado", 1000000 }, // existe en seed -> actualiza
            { 202608, "66170000", "05615000", "2L2", "General", "Estibas", "Cargado", 1020000 }, // periodo nuevo -> inserta
        };
        for (var r = 0; r < rows.GetLength(0); r++)
            for (var c = 0; c < rows.GetLength(1); c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r, c]);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(ms.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "archivo", "test.xlsx");
        return content;
    }
}
