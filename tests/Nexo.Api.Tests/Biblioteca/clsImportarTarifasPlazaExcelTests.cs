using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Catalogos.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>
/// S5-HU-22.1/#288 de extremo a extremo (BD real): la carga masiva de Tarifas de Plaza lee la
/// fecha de Vigencia sin depender de la cultura del servidor (serial de Excel incluido) y, con las
/// columnas opcionales DEPARTAMENTO ORIGEN / DEPARTAMENTO DESTINO, ubica la tarifa en el municipio
/// del departamento indicado cuando el nombre se repite. Cada prueba usa un TIPO VEHICULO único
/// (clave de la fila) y borra lo que creó.
/// </summary>
[Collection("api")]
public class clsImportarTarifasPlazaExcelTests
{
    private static readonly DateOnly Veinte = new(2026, 10, 20);

    private readonly clsApiFixture _fx;
    public clsImportarTarifasPlazaExcelTests(clsApiFixture fx) => _fx = fx;

    private sealed record clsFila(string Origen, string Destino, string TipoVehiculo, object Vigencia,
        string? DepOrigen = null, string? DepDestino = null);

    private async Task<(string linea, string clase)> Catalogos()
    {
        var cli = _fx.Client("AdminTarifas");
        var lineas = await cli.GetFromJsonAsync<JsonElement>("/api/catalogos/lineas-negocio");
        var clases = await cli.GetFromJsonAsync<JsonElement>("/api/catalogos/clases-viaje");
        return (lineas[0].GetProperty("descripcion").GetString()!, clases[0].GetProperty("descripcion").GetString()!);
    }

    /// <summary>Un nombre de municipio colombiano repetido y sus candidatos (ordenados por Id).</summary>
    private List<clsMunicipio> NombreRepetido() => _fx.WithDb(db =>
    {
        var nombre = db.Municipios.AsNoTracking().Where(m => m.Pais == "CO")
            .GroupBy(m => m.Municipio).Where(g => g.Count() >= 2).Select(g => g.Key).OrderBy(n => n).First();
        return db.Municipios.AsNoTracking().Where(m => m.Pais == "CO" && m.Municipio == nombre).OrderBy(m => m.Id).ToList();
    });

    private string UnMunicipioUnico() => _fx.WithDb(db => db.Municipios.AsNoTracking()
        .Where(m => m.Pais == "CO" && m.Municipio == "MEDELLIN").Select(m => m.Municipio).First());

    private static HttpContent Excel(string linea, string clase, IEnumerable<clsFila> filas, bool conDepartamentos)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Datos");
        var cab = new List<string> { "LINEA DE NEGOCIO", "CLASE DE VIAJE", "ORIGEN", "DESTINO", "TIPO VEHICULO", "VALOR", "VIGENCIA", "DETALLES" };
        if (conDepartamentos) cab.AddRange(["DEPARTAMENTO ORIGEN", "DEPARTAMENTO DESTINO"]);
        for (var i = 0; i < cab.Count; i++) ws.Cell(1, i + 1).Value = cab[i];

        var r = 2;
        foreach (var f in filas)
        {
            ws.Cell(r, 1).Value = linea; ws.Cell(r, 2).Value = clase;
            ws.Cell(r, 3).Value = f.Origen; ws.Cell(r, 4).Value = f.Destino; ws.Cell(r, 5).Value = f.TipoVehiculo;
            ws.Cell(r, 6).Value = 1_500_000; ws.Cell(r, 8).Value = "prueba #288";
            var celdaFecha = ws.Cell(r, 7);
            switch (f.Vigencia)
            {
                case DateTime d: celdaFecha.Value = d; break;
                case int serial: celdaFecha.Value = serial; celdaFecha.Style.NumberFormat.Format = "General"; break;
                default: celdaFecha.Value = (string)f.Vigencia; break;
            }
            if (conDepartamentos) { ws.Cell(r, 9).Value = f.DepOrigen ?? ""; ws.Cell(r, 10).Value = f.DepDestino ?? ""; }
            r++;
        }
        return Multipart(wb);
    }

    private static HttpContent Multipart(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(ms.ToArray());
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(parte, "archivo", "tarifas.xlsx");
        return contenido;
    }

    private async Task<JsonElement> Importar(HttpContent contenido)
    {
        var resp = await _fx.Client("AdminTarifas").PostAsync("/api/tarifas-plaza/importar-excel", contenido);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Json();
    }

    private static string Prefijo() => $"T288{Guid.NewGuid():N}"[..14];

    private void Limpiar(string prefijo) => _fx.WithDb(db =>
    {
        db.TarifasPlazaHistorico.Where(h => h.TipoVehiculo!.StartsWith(prefijo)).ExecuteDelete();
        return db.TarifasPlaza.Where(t => t.TipoVehiculo!.StartsWith(prefijo)).ExecuteDelete();
    });

    private List<(string Tipo, int Origen, DateOnly Vigencia)> Guardadas(string prefijo) => _fx.WithDb(db =>
        db.TarifasPlaza.AsNoTracking().Where(t => t.TipoVehiculo!.StartsWith(prefijo))
            .Select(t => new { t.TipoVehiculo, t.OrigenMunicipioId, t.Vigencia }).ToList()
            .Select(t => (t.TipoVehiculo!, t.OrigenMunicipioId, t.Vigencia)).ToList());

    // ------------------------------------------------------------------ fecha de Vigencia

    [Fact]
    public async Task Serial_de_Excel_fecha_nativa_y_texto_en_cuatro_formatos_dan_la_misma_vigencia()
    {
        var p = Prefijo();
        var (linea, clase) = await Catalogos();
        var med = UnMunicipioUnico();
        try
        {
            var filas = new object[]
            {
                46315,                    // el caso real: serial en celda General -> 2026-10-20
                new DateTime(2026, 10, 20),
                "20/10/2026",             // dd/MM/yyyy
                "2026-10-20",             // yyyy-MM-dd
                "10/20/2026",             // MM/dd/yyyy
                "46315",                  // serial guardado como texto
            };
            var datos = filas.Select((v, i) => new clsFila(med, "BOGOTA, D.C.", $"{p}-{i}", v)).ToList();

            var r = await Importar(Excel(linea, clase, datos, conDepartamentos: false));

            Assert.Equal(0, r.GetProperty("rechazadas").GetInt32());
            Assert.Equal(datos.Count, r.GetProperty("insertadas").GetInt32());
            Assert.All(Guardadas(p), g => Assert.Equal(Veinte, g.Vigencia));
        }
        finally { Limpiar(p); }
    }

    [Fact]
    public async Task El_error_de_fecha_solo_sale_cuando_de_verdad_no_es_una_fecha_reconocible()
    {
        var p = Prefijo();
        var (linea, clase) = await Catalogos();
        var med = UnMunicipioUnico();
        try
        {
            var datos = new[]
            {
                new clsFila(med, "BOGOTA, D.C.", $"{p}-ok", 46315),
                new clsFila(med, "BOGOTA, D.C.", $"{p}-txt", "no-es-fecha"),
                new clsFila(med, "BOGOTA, D.C.", $"{p}-num", 5),
            };

            var r = await Importar(Excel(linea, clase, datos, conDepartamentos: false));

            Assert.Equal(1, r.GetProperty("insertadas").GetInt32());
            Assert.Equal(2, r.GetProperty("rechazadas").GetInt32());
            var errores = r.GetProperty("errores").EnumerateArray().Select(e => e.GetString()!).ToList();
            Assert.Contains(errores, e => e.Contains("Fila 3") && e.Contains("'no-es-fecha' no es una fecha de vigencia válida"));
            Assert.Contains(errores, e => e.Contains("Fila 4") && e.Contains("no es una fecha de vigencia válida"));
        }
        finally { Limpiar(p); }
    }

    // ------------------------------------------------------------------ departamento

    [Fact]
    public async Task Con_departamento_la_tarifa_queda_en_el_municipio_de_ese_departamento()
    {
        var p = Prefijo();
        var (linea, clase) = await Catalogos();
        var candidatos = NombreRepetido();
        var primero = candidatos[0]; var segundo = candidatos[1];
        try
        {
            var datos = new[]
            {
                // departamento en minúsculas y sin tildes: se normaliza igual que el municipio
                new clsFila(segundo.Municipio.ToLowerInvariant(), "BOGOTA, D.C.", $"{p}-2", Veinte.ToString("yyyy-MM-dd"), DepOrigen: segundo.Departamento.ToLowerInvariant()),
                new clsFila(primero.Municipio, "BOGOTA, D.C.", $"{p}-1", Veinte.ToString("yyyy-MM-dd"), DepOrigen: primero.Departamento),
            };

            var r = await Importar(Excel(linea, clase, datos, conDepartamentos: true));

            Assert.Equal(0, r.GetProperty("rechazadas").GetInt32());
            var guardadas = Guardadas(p).ToDictionary(g => g.Tipo, g => g.Origen);
            Assert.Equal(segundo.Id, guardadas[$"{p}-2"]);
            Assert.Equal(primero.Id, guardadas[$"{p}-1"]);
        }
        finally { Limpiar(p); }
    }

    [Fact]
    public async Task Un_departamento_que_no_corresponde_da_error_en_esa_fila_con_el_mensaje_pedido()
    {
        var p = Prefijo();
        var (linea, clase) = await Catalogos();
        var candidatos = NombreRepetido();
        var departamentosConEseNombre = candidatos.Select(c => c.Departamento).ToHashSet();
        var otroDepartamento = _fx.WithDb(db => db.Municipios.AsNoTracking().Where(m => m.Pais == "CO")
            .Select(m => m.Departamento).Distinct().ToList()).First(d => !departamentosConEseNombre.Contains(d));
        try
        {
            var datos = new[] { new clsFila(candidatos[0].Municipio, "BOGOTA, D.C.", $"{p}-x", Veinte.ToString("yyyy-MM-dd"), DepOrigen: otroDepartamento) };

            var r = await Importar(Excel(linea, clase, datos, conDepartamentos: true));

            Assert.Equal(1, r.GetProperty("rechazadas").GetInt32());
            Assert.Contains(r.GetProperty("errores").EnumerateArray().Select(e => e.GetString()!),
                e => e.Contains("Fila 2") && e.Contains($"'{candidatos[0].Municipio}' no existe en el departamento '{otroDepartamento}'"));
            Assert.Empty(Guardadas(p));
        }
        finally { Limpiar(p); }
    }

    [Fact]
    public async Task Sin_las_columnas_de_departamento_o_con_ellas_vacias_se_comporta_como_hoy()
    {
        var p = Prefijo();
        var (linea, clase) = await Catalogos();
        var candidatos = NombreRepetido();
        try
        {
            // Como hoy: el primero de la lista (Colombia primero, y a igualdad el de menor Id).
            var sinColumnas = new[] { new clsFila(candidatos[0].Municipio, "BOGOTA, D.C.", $"{p}-a", "2026-10-20") };
            var conVacias = new[] { new clsFila(candidatos[0].Municipio, "BOGOTA, D.C.", $"{p}-b", "2026-10-20", DepOrigen: "") };

            await Importar(Excel(linea, clase, sinColumnas, conDepartamentos: false));
            await Importar(Excel(linea, clase, conVacias, conDepartamentos: true));

            Assert.All(Guardadas(p), g => Assert.Equal(candidatos[0].Id, g.Origen));
            Assert.Equal(2, Guardadas(p).Count);
        }
        finally { Limpiar(p); }
    }

    [Fact]
    public async Task Un_municipio_inexistente_sigue_dando_su_error_de_siempre()
    {
        var p = Prefijo();
        var (linea, clase) = await Catalogos();
        try
        {
            var datos = new[] { new clsFila("CIUDAD-QUE-NO-EXISTE", "BOGOTA, D.C.", $"{p}-x", "2026-10-20") };

            var r = await Importar(Excel(linea, clase, datos, conDepartamentos: false));

            Assert.Contains(r.GetProperty("errores").EnumerateArray().Select(e => e.GetString()!),
                e => e.Contains("'CIUDAD-QUE-NO-EXISTE' no es un municipio del catálogo"));
        }
        finally { Limpiar(p); }
    }

    // ------------------------------------------------------------------ export -> reimport

    [Fact]
    public async Task El_export_trae_las_columnas_de_departamento_y_reimportarlo_no_mueve_una_ciudad_repetida_al_primero()
    {
        var p = Prefijo();
        var (linea, clase) = await Catalogos();
        var candidatos = NombreRepetido();
        var segundo = candidatos[1];
        try
        {
            var datos = new[] { new clsFila(segundo.Municipio, "BOGOTA, D.C.", $"{p}-rt", "2026-10-20", DepOrigen: segundo.Departamento) };
            await Importar(Excel(linea, clase, datos, conDepartamentos: true));

            var export = await _fx.Client("AdminTarifas").GetAsync("/api/tarifas-plaza/exportar-excel");
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            using var wb = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
            var ws = wb.Worksheets.First();
            var encabezados = ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
            Assert.Equal(["LINEA DE NEGOCIO", "CLASE DE VIAJE", "ORIGEN", "DESTINO", "CLASE CONTENEDOR", "RANGO PESO",
                "TIPO VEHICULO", "VALOR", "VIGENCIA", "DETALLES", "DEPARTAMENTO ORIGEN", "DEPARTAMENTO DESTINO"], encabezados);

            // Se queda solo con la fila de esta prueba y se reimporta tal cual salió.
            foreach (var fila in ws.RowsUsed().Skip(1).Where(r => !r.Cell(7).GetString().StartsWith(p)).Reverse().ToList())
                fila.Delete();
            var r2 = await Importar(Multipart(wb));

            Assert.Equal(0, r2.GetProperty("rechazadas").GetInt32());
            Assert.Equal(0, r2.GetProperty("insertadas").GetInt32());
            Assert.Equal(1, r2.GetProperty("actualizadas").GetInt32());
            var guardadas = Guardadas(p);
            Assert.Single(guardadas);                                  // no apareció una tarifa duplicada en el primero
            Assert.Equal(segundo.Id, guardadas[0].Origen);
        }
        finally { Limpiar(p); }
    }
}
