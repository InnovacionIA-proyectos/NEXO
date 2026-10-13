using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>
/// S5-HU-22.1/#288: tipos de vehículo sin los 5 casos especiales, ciudades visibles para todos los
/// roles válidos (no solo Comercial/Admin) y export de Reglas Solo-SICETAC que se edita y reimporta.
/// </summary>
[Collection("api")]
public class clsCatalogosYExportReglasTests
{
    private readonly clsApiFixture _fx;
    public clsCatalogosYExportReglasTests(clsApiFixture fx) => _fx = fx;

    private static readonly string[] CasosEspeciales =
        ["CNT20/Consolidado", "CNT20/Exclusivo", "CNT40/Consolidado", "CNT40/Exclusivo", "PBV 7500-8000"];

    private static readonly string[] RolesValidos =
        ["CoordinadorFletes", "AdminTarifas", "ServiciosLogisticos", "LiderProceso", "Comercial", "Admin"];

    // ------------------------------------------------------------------ tipos de vehículo

    [Fact]
    public async Task El_desplegable_de_tipos_de_vehiculo_ya_no_trae_los_5_casos_especiales()
    {
        var tipos = await _fx.Client("AdminTarifas").GetFromJsonAsync<JsonElement>("/api/catalogos/tipos-vehiculo");

        var codigos = tipos.EnumerateArray().Select(t => t.GetProperty("codigo").GetString()!).ToList();
        Assert.All(CasosEspeciales, c => Assert.DoesNotContain(c, codigos));
        Assert.All(tipos.EnumerateArray(), t => Assert.True(t.GetProperty("esCatalogoOficial").GetBoolean()));
    }

    // ------------------------------------------------------------------ ciudades por rol

    [Theory]
    [InlineData("/api/captura-solicitudes/ciudades?q=med")]
    [InlineData("/api/captura-solicitudes/departamentos")]
    [InlineData("/api/captura-solicitudes/municipios")]
    public async Task Las_ciudades_las_ve_cualquier_rol_valido_incluido_CoordinadorFletes(string url)
    {
        foreach (var rol in RolesValidos)
            Assert.Equal(HttpStatusCode.OK, (await _fx.Client(rol).GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("/api/captura-solicitudes/ciudades?q=med")]
    [InlineData("/api/captura-solicitudes/departamentos")]
    [InlineData("/api/captura-solicitudes/municipios")]
    public async Task Las_ciudades_siguen_exigiendo_estar_autenticado(string url) =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fx.Anon().GetAsync(url)).StatusCode);

    [Fact]
    public async Task Capturar_y_AnalizarExcel_siguen_siendo_solo_de_Comercial_y_Admin()
    {
        foreach (var rol in new[] { "CoordinadorFletes", "AdminTarifas", "ServiciosLogisticos", "LiderProceso" })
        {
            var cli = _fx.Client(rol);
            var capturar = await cli.PostJson("/api/captura-solicitudes", new { });
            var analizar = await cli.PostAsync("/api/captura-solicitudes/analizar-excel", new MultipartFormDataContent());
            Assert.Equal(HttpStatusCode.Forbidden, capturar.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, analizar.StatusCode);
        }
        // Comercial y Admin pasan la autorización (un cuerpo vacío da 400 de validación, no 403).
        foreach (var rol in new[] { "Comercial", "Admin" })
        {
            var cli = _fx.Client(rol);
            Assert.NotEqual(HttpStatusCode.Forbidden, (await cli.PostJson("/api/captura-solicitudes", new { })).StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, (await cli.PostAsync("/api/captura-solicitudes/analizar-excel", new MultipartFormDataContent())).StatusCode);
        }
    }

    [Fact] // revisado caso por caso: aprovisionamiento y clientes se quedan para Comercial/Admin.
    public async Task Aprovisionamiento_y_Clientes_no_cambian_de_roles()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _fx.Client("CoordinadorFletes").GetAsync("/api/captura-solicitudes/clientes?q=a")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _fx.Client("CoordinadorFletes").GetAsync("/api/captura-solicitudes/aprovisionamiento?levante=a&origen=b")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _fx.Client("Comercial").GetAsync("/api/captura-solicitudes/clientes?q=a")).StatusCode);
    }

    // ------------------------------------------------------------------ export de reglas

    private static HttpContent Multipart(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(ms.ToArray());
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(parte, "archivo", "reglas.xlsx");
        return contenido;
    }

    [Fact]
    public async Task El_export_de_reglas_trae_las_mismas_columnas_que_acepta_el_import_con_los_valores_guardados()
    {
        var resp = await _fx.Client("AdminTarifas").GetAsync("/api/reglas-solo-sicetac/exportar-excel");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.StartsWith("reglas-solo-sicetac-", resp.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        using var wb = new XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
        var ws = wb.Worksheets.First();
        Assert.Equal(["ORIGEN", "DESTINO", "TIPO VEHICULO"], ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList());
        Assert.True(ws.RowsUsed().Count() > 1, "hay reglas reales cargadas");

        // El código DANE conserva su cero inicial (se escribe como texto, no número): "05001000" = Medellín.
        Assert.Contains(ws.RowsUsed().Skip(1), r => r.Cell(1).GetString() == "05001000");
        Assert.All(ws.RowsUsed().Skip(1).Take(20), r => Assert.Equal(XLDataType.Text, r.Cell(1).DataType));
    }

    [Fact]
    public async Task El_export_se_edita_se_reimporta_y_solo_entra_lo_nuevo_sin_filas_invalidas()
    {
        var admin = _fx.Client("AdminTarifas");
        var tipoNuevo = _fx.WithDb(db => db.ReglasSoloSicetac.Select(r => r.TipoVehiculo).First());
        var origenNuevo = $"ORIGEN-T288-{Guid.NewGuid():N}"[..24];
        try
        {
            var export = await admin.GetAsync("/api/reglas-solo-sicetac/exportar-excel");
            using var wb = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
            var ws = wb.Worksheets.First();
            var existentes = ws.RowsUsed().Count() - 1;

            // "Se edita": se agrega una regla nueva al final del mismo archivo.
            var siguiente = ws.LastRowUsed()!.RowNumber() + 1;
            ws.Cell(siguiente, 1).Value = origenNuevo;
            ws.Cell(siguiente, 2).Value = "DESTINO-T288";
            ws.Cell(siguiente, 3).Value = tipoNuevo;

            var resp = await admin.PostAsync("/api/reglas-solo-sicetac/importar-excel", Multipart(wb));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var r = await resp.Json();
            Assert.Equal(1, r.GetProperty("creadas").GetInt32());
            Assert.Equal(existentes, r.GetProperty("duplicadas").GetInt32());   // las ya guardadas no se pisan
            Assert.Equal(0, r.GetProperty("invalidas").GetInt32());             // ni una inválida, tampoco las de 2L3/3S2
        }
        finally
        {
            _fx.WithDb(db => db.ReglasSoloSicetac.Where(x => x.Destino == "DESTINO-T288").ExecuteDelete());
        }
    }

    [Theory]
    [InlineData("Comercial")]
    [InlineData("CoordinadorFletes")]
    public async Task El_export_de_reglas_con_un_rol_no_autorizado_devuelve_403(string rol) =>
        Assert.Equal(HttpStatusCode.Forbidden, (await _fx.Client(rol).GetAsync("/api/reglas-solo-sicetac/exportar-excel")).StatusCode);
}
