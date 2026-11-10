using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// <c>POST /api/solicitudes/resolver-catalogos</c> con los municipios que el front resolvió, contra la API y la base REALES: la ruta se guarda
/// con su municipio, se busca primero por municipios, los ids vacíos se completan y los que ya tiene nunca se cambian. Cada prueba crea sus
/// municipios y rutas con una marca única y los borra al terminar.
/// </summary>
[Collection("api")]
public class clsResolverCatalogosMunicipioApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/solicitudes/resolver-catalogos";

    private static string Marca() => $"T445{Guid.NewGuid():N}"[..13].ToUpperInvariant();

    private (int Id, string Nombre) NuevoMunicipio(string marca, string sufijo)
    {
        var nombre = $"{marca} {sufijo}";
        var id = fx.WithDb(db =>
        {
            var m = new clsMunicipio { CodigoDane = (marca + sufijo).PadLeft(20, '0')[^20..], Municipio = nombre, Departamento = "PRUEBA", Pais = "CO" };
            db.Municipios.Add(m);
            db.SaveChanges();
            return m.Id;
        });
        return (id, nombre);
    }

    private void Limpiar(string marca) => fx.WithDb(db =>
    {
        db.Rutas.Where(r => r.Origen.StartsWith(marca) || r.Destino.StartsWith(marca)).ExecuteDelete();
        db.Municipios.Where(m => m.Municipio.StartsWith(marca)).ExecuteDelete();
        return 0;
    });

    private static object Linea(string origen, string destino, int? origenId = null, int? destinoId = null) =>
        new { origen, destino, origenMunicipioId = origenId, destinoMunicipioId = destinoId };

    private static async Task<Guid> RutaDe(HttpResponseMessage r, int indice = 0) =>
        (await r.Json())[indice].GetProperty("rutaId").GetGuid();

    private clsRuta Fila(Guid id) => fx.WithDb(db => db.Rutas.AsNoTracking().Single(r => r.Id == id));

    [Fact]
    public async Task Una_ruta_nueva_con_ids_toma_el_nombre_del_catalogo_y_guarda_los_municipios()
    {
        var marca = Marca();
        var (origenId, origenNombre) = NuevoMunicipio(marca, "ORIGEN");
        var (destinoId, destinoNombre) = NuevoMunicipio(marca, "DESTINO");
        using var c = fx.Client("Comercial");
        try
        {
            var resp = await c.PostAsJsonAsync(Ruta, new[] { Linea("texto libre origen", "texto libre destino", origenId, destinoId) });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = (await resp.Json())[0];
            Assert.True(dto.GetProperty("rutaCreada").GetBoolean());
            var ruta = Fila(dto.GetProperty("rutaId").GetGuid());
            Assert.Equal((origenNombre, destinoNombre), (ruta.Origen, ruta.Destino));
            Assert.Equal((origenId, destinoId), (ruta.OrigenMunicipioId, ruta.DestinoMunicipioId));
        }
        finally { Limpiar(marca); }
    }

    [Fact]
    public async Task Con_ids_se_encuentra_la_ruta_aunque_el_texto_sea_otro_y_la_respuesta_conserva_el_orden()
    {
        var marca = Marca();
        var (origenId, origenNombre) = NuevoMunicipio(marca, "A");
        var (destinoId, destinoNombre) = NuevoMunicipio(marca, "B");
        var (otroId, otroNombre) = NuevoMunicipio(marca, "C");
        using var c = fx.Client("Comercial");
        try
        {
            var primera = await RutaDe(await c.PostAsJsonAsync(Ruta, new[] { Linea(origenNombre, destinoNombre, origenId, destinoId) }));

            var resp = await c.PostAsJsonAsync(Ruta, new[]
            {
                Linea("otro texto", "otro texto más", origenId, destinoId),
                Linea(origenNombre, otroNombre, origenId, otroId),
                Linea(origenNombre, destinoNombre),
            });

            var lista = await resp.Json();
            Assert.Equal([0, 1, 2], lista.EnumerateArray().Select(x => x.GetProperty("indice").GetInt32()));
            Assert.Equal(primera, lista[0].GetProperty("rutaId").GetGuid());
            Assert.False(lista[0].GetProperty("rutaCreada").GetBoolean());
            Assert.True(lista[1].GetProperty("rutaCreada").GetBoolean());
            Assert.Equal(primera, lista[2].GetProperty("rutaId").GetGuid());
        }
        finally { Limpiar(marca); }
    }

    [Fact]
    public async Task Una_ruta_existente_sin_municipios_los_recibe_y_con_otros_municipios_no_se_pisa()
    {
        var marca = Marca();
        var (origenId, origenNombre) = NuevoMunicipio(marca, "A");
        var (destinoId, destinoNombre) = NuevoMunicipio(marca, "B");
        var (ajenoId, _) = NuevoMunicipio(marca, "C");
        using var c = fx.Client("Comercial");
        try
        {
            var existente = await RutaDe(await c.PostAsJsonAsync(Ruta, new[] { Linea(origenNombre, destinoNombre) }));
            Assert.Null(Fila(existente).OrigenMunicipioId);

            await c.PostAsJsonAsync(Ruta, new[] { Linea(origenNombre, destinoNombre, origenId, destinoId) });
            Assert.Equal((origenId, destinoId), (Fila(existente).OrigenMunicipioId, Fila(existente).DestinoMunicipioId));

            // Otro municipio para el mismo texto: se reutiliza la ruta y no se cambia el que ya tiene.
            var otra = await RutaDe(await c.PostAsJsonAsync(Ruta, new[] { Linea(origenNombre, destinoNombre, ajenoId, destinoId) }));
            Assert.Equal(existente, otra);
            Assert.Equal(origenId, Fila(existente).OrigenMunicipioId);
        }
        finally { Limpiar(marca); }
    }

    [Fact]
    public async Task Sin_ids_el_endpoint_responde_como_siempre()
    {
        var marca = Marca();
        using var c = fx.Client("Comercial");
        try
        {
            var resp = await c.PostAsJsonAsync(Ruta, new[] { Linea($"{marca} Cali", $"{marca} Bogotá") });

            var ruta = Fila(await RutaDe(resp));
            Assert.Equal(($"{marca} Cali", $"{marca} Bogotá"), (ruta.Origen, ruta.Destino));
            Assert.Null(ruta.OrigenMunicipioId);
        }
        finally { Limpiar(marca); }
    }

    [Fact]
    public async Task Un_municipio_que_no_existe_es_400_y_no_crea_nada()
    {
        var marca = Marca();
        using var c = fx.Client("Comercial");
        try
        {
            var resp = await c.PostAsJsonAsync(Ruta, new[] { Linea($"{marca} Cali", $"{marca} Bogotá", 2_000_000_000, null) });

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            Assert.Equal("MUNICIPIO_NO_ENCONTRADO", await resp.ErrorCode());
            Assert.Equal(0, fx.WithDb(db => db.Rutas.Count(r => r.Origen.StartsWith(marca))));
        }
        finally { Limpiar(marca); }
    }
}
