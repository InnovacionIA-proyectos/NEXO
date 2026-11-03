using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// Importar un Excel identificando al cliente por NIT (<c>clienteNit</c>) contra la API y la base REALES: las lecturas buscan el cliente local y
/// nunca lo crean; guardar una plantilla lo crea desde los clientes o prospectos del DW sin duplicar por NIT.
/// </summary>
[Collection("api")]
public class clsClienteNitImportacionApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/captura-solicitudes/plantillas-mapeo";
    private const string RutaSugerir = "/api/captura-solicitudes/sugerir-mapeo";
    private const string RutaAplicar = "/api/captura-solicitudes/aplicar-mapeo";
    private const string RutaNormalizar = "/api/captura-solicitudes/normalizar-valores";

    private sealed class ClientesDwFalso(params clsClienteDwDto[] filas) : IClientesDwPort
    {
        public bool Configurado => true;
        public Task<IReadOnlyList<clsClienteDwDto>> ListarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<clsClienteDwDto>>(filas);
    }

    private sealed class ProspectosDwFalso(params clsProspectoDwDto[] filas) : IProspectosDwPort
    {
        public bool Configurado => true;
        public Task<IReadOnlyList<clsProspectoDwDto>> ListarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<clsProspectoDwDto>>(filas);
    }

    private static readonly object[] Columnas =
    [
        new { indice = 0, campo = "origen" }, new { indice = 1, campo = "destino" }, new { indice = 2, campo = "peso", transformacion = "peso_toneladas_a_kg" },
    ];

    private static string[] Encabezados(string marca) => [$"{marca} Origen", $"{marca} Destino", $"{marca} Peso (TON)"];

    private static string NitUnico() => (800_000_000 + Random.Shared.Next(0, 99_999_999)).ToString();

    private static string Marca() => $"T410{Guid.NewGuid():N}"[..14];

    private static string ConFormato(string nit) => $"{nit[..3]}.{nit[3..6]}.{nit[6..]}-7";

    private WebApplicationFactory<clsProgram> Fabrica(IClientesDwPort clientesDw, IProspectosDwPort prospectosDw, IAsistenteIaPort? ia = null) =>
        fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IClientesDwPort>();
            s.AddSingleton(clientesDw);
            s.RemoveAll<IProspectosDwPort>();
            s.AddSingleton(prospectosDw);
            s.RemoveAll<IAsistenteIaPort>();
            s.AddSingleton(ia ?? clsAsistenteIaFalso.SinConfigurar());
        }));

    private HttpClient Comercial(WebApplicationFactory<clsProgram> fabrica)
    {
        var c = fabrica.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, "Comercial");
        return c;
    }

    private void Db(Action<Nexo.Infrastructure.Persistence.clsNexoDbContext> accion) => fx.WithDb(db => { accion(db); return 0; });

    private int ClientesConNit(string nit) => fx.WithDb(db => db.Clientes.Count(c => c.Nit == nit || c.Nit.StartsWith(nit + "-")));

    private void Limpiar(string nit, string marca) => Db(db =>
    {
        var ids = db.Clientes.Where(c => c.Nit == nit || c.Nit.StartsWith(nit + "-")).Select(c => c.Id).ToList();
        db.PlantillasMapeo.Where(p => p.Encabezados.Contains(marca) || ids.Contains(p.ClienteId)).ExecuteDelete();
        db.Clientes.Where(c => ids.Contains(c.Id)).ExecuteDelete();
    });

    private static object Cuerpo(string? nit, string marca, Guid? clienteId = null) =>
        new { clienteId, clienteNit = nit, nombre = "Formato", encabezados = Encabezados(marca), columnas = Columnas };

    // ---------------------------------------------------------------- guardar

    [Fact]
    public async Task Guardar_con_el_NIT_de_un_cliente_local_usa_ese_cliente_sin_importar_el_formato_del_NIT()
    {
        var nit = NitUnico();
        var marca = Marca();
        var local = new clsCliente { Nombre = $"{marca} Local", Nit = nit, RazonSocial = $"{marca} Local" };
        Db(db => { db.Clientes.Add(local); db.SaveChanges(); });
        using var fabrica = Fabrica(new ClientesDwFalso(), new ProspectosDwFalso());
        using var c = Comercial(fabrica);
        try
        {
            var resp = await c.PutAsJsonAsync(Ruta, Cuerpo(ConFormato(nit), marca));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            Assert.Equal(local.Id, (await resp.Json()).GetProperty("clienteId").GetGuid());

            // Con el NIT sin formato es la misma plantilla del mismo cliente: se reemplaza.
            var otra = await c.PutAsJsonAsync(Ruta, Cuerpo(nit, marca));
            Assert.Equal(HttpStatusCode.OK, otra.StatusCode);
            Assert.Equal(1, ClientesConNit(nit));
        }
        finally { Limpiar(nit, marca); }
    }

    [Fact]
    public async Task Guardar_con_el_NIT_de_un_cliente_del_DW_sin_cliente_local_lo_crea_una_sola_vez()
    {
        var nit = NitUnico();
        var marca = Marca();
        using var fabrica = Fabrica(new ClientesDwFalso(new clsClienteDwDto("BP-410", nit, $"{marca} Real SAS")), new ProspectosDwFalso());
        using var c = Comercial(fabrica);
        try
        {
            Assert.Equal(0, ClientesConNit(nit));
            var primera = await c.PutAsJsonAsync(Ruta, Cuerpo(ConFormato(nit), marca));
            Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
            var clienteId = (await primera.Json()).GetProperty("clienteId").GetGuid();

            var creado = fx.WithDb(db => db.Clientes.Single(x => x.Id == clienteId));
            Assert.Equal(($"{marca} Real SAS", nit), (creado.Nombre, creado.Nit));

            var segunda = await c.PutAsJsonAsync(Ruta, Cuerpo(nit, marca));
            Assert.Equal(clienteId, (await segunda.Json()).GetProperty("clienteId").GetGuid());
            Assert.Equal(1, ClientesConNit(nit));
        }
        finally { Limpiar(nit, marca); }
    }

    [Fact]
    public async Task Guardar_con_el_NIT_de_un_prospecto_crea_el_cliente_local_y_si_luego_es_cliente_real_sigue_siendo_el_mismo()
    {
        var nit = NitUnico();
        var marca = Marca();
        Guid clienteId;
        try
        {
            using (var fabrica = Fabrica(new ClientesDwFalso(), new ProspectosDwFalso(new clsProspectoDwDto(nit, $"{marca} Prospecto"))))
            using (var c = Comercial(fabrica))
            {
                var resp = await c.PutAsJsonAsync(Ruta, Cuerpo(nit, marca));
                Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
                clienteId = (await resp.Json()).GetProperty("clienteId").GetGuid();
                Assert.Equal($"{marca} Prospecto", fx.WithDb(db => db.Clientes.Single(x => x.Id == clienteId).Nombre));
            }

            // El prospecto ya es cliente real en el DW: el NIT sigue apuntando al mismo cliente local.
            using (var fabrica = Fabrica(new ClientesDwFalso(new clsClienteDwDto("BP-411", nit, $"{marca} Ya Real")), new ProspectosDwFalso()))
            using (var c = Comercial(fabrica))
            {
                var resp = await c.PutAsJsonAsync(Ruta, Cuerpo(ConFormato(nit), marca));
                Assert.Equal(clienteId, (await resp.Json()).GetProperty("clienteId").GetGuid());
                Assert.Equal(1, ClientesConNit(nit));
            }
        }
        finally { Limpiar(nit, marca); }
    }

    [Fact]
    public async Task Guardar_con_un_NIT_que_no_es_de_ningun_cliente_ni_prospecto_es_400_y_no_crea_nada()
    {
        var nit = NitUnico();
        var marca = Marca();
        using var fabrica = Fabrica(new ClientesDwFalso(), new ProspectosDwFalso());
        using var c = Comercial(fabrica);
        try
        {
            foreach (var invalido in new[] { nit, "---", "  ", "sin-numeros-.-" })
            {
                var resp = await c.PutAsJsonAsync(Ruta, Cuerpo(invalido, marca));
                Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
                // Un NIT en blanco no es un NIT: lo que falta es el cliente.
                Assert.Equal(string.IsNullOrWhiteSpace(invalido) ? "CLIENTE_REQUERIDO" : "CLIENTE_NIT_INVALIDO", await resp.ErrorCode());
            }
            Assert.Equal(0, ClientesConNit(nit));
            Assert.Equal(0, fx.WithDb(db => db.PlantillasMapeo.Count(p => p.Encabezados.Contains(marca))));
        }
        finally { Limpiar(nit, marca); }
    }

    [Fact]
    public async Task Con_clienteId_y_NIT_a_la_vez_manda_el_clienteId()
    {
        var marca = Marca();
        var nit = NitUnico();
        var local = new clsCliente { Nombre = $"{marca} Local", Nit = NitUnico(), RazonSocial = $"{marca} Local" };
        Db(db => { db.Clientes.Add(local); db.SaveChanges(); });
        using var fabrica = Fabrica(new ClientesDwFalso(new clsClienteDwDto("BP-412", nit, $"{marca} Otro")), new ProspectosDwFalso());
        using var c = Comercial(fabrica);
        try
        {
            var resp = await c.PutAsJsonAsync(Ruta, Cuerpo(nit, marca, local.Id));
            Assert.Equal(local.Id, (await resp.Json()).GetProperty("clienteId").GetGuid());
            Assert.Equal(0, ClientesConNit(nit));
        }
        finally { Limpiar(local.Nit, marca); }
    }

    // ---------------------------------------------------------------- leer

    [Fact]
    public async Task Las_lecturas_con_el_NIT_de_un_cliente_local_usan_su_plantilla()
    {
        var nit = NitUnico();
        var marca = Marca();
        var local = new clsCliente { Nombre = $"{marca} Local", Nit = nit, RazonSocial = $"{marca} Local" };
        Db(db => { db.Clientes.Add(local); db.SaveChanges(); });
        using var fabrica = Fabrica(new ClientesDwFalso(), new ProspectosDwFalso());
        using var c = Comercial(fabrica);
        try
        {
            var plantilla = (await (await c.PutAsJsonAsync(Ruta, Cuerpo(nit, marca))).Json()).GetProperty("id").GetGuid();

            var sugerida = await (await c.PostAsJsonAsync(RutaSugerir, new { clienteNit = ConFormato(nit), encabezados = Encabezados(marca) })).Json();
            Assert.Equal(plantilla, sugerida.GetProperty("plantillaId").GetGuid());

            var lista = await (await c.GetAsync($"{Ruta}?clienteNit={Uri.EscapeDataString(ConFormato(nit))}")).Json();
            Assert.Equal([plantilla], lista.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()));
        }
        finally { Limpiar(nit, marca); }
    }

    [Fact]
    public async Task Las_lecturas_con_un_NIT_sin_cliente_local_responden_como_sin_cliente_y_no_crean_nada()
    {
        var nit = NitUnico();
        var marca = Marca();
        using var fabrica = Fabrica(new ClientesDwFalso(new clsClienteDwDto("BP-413", nit, $"{marca} Real")), new ProspectosDwFalso(), clsAsistenteIaFalso.SinConfigurar());
        using var c = Comercial(fabrica);
        try
        {
            var sugerida = await c.PostAsJsonAsync(RutaSugerir, new { clienteNit = nit, encabezados = Encabezados(marca) });
            Assert.Equal(HttpStatusCode.OK, sugerida.StatusCode);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, (await sugerida.Json()).GetProperty("plantillaId").ValueKind);

            var aplicada = await c.PostAsJsonAsync(RutaAplicar, new
            {
                clienteNit = nit,
                columnas = new object[] { new { indice = 0, campo = "origen" }, new { indice = 1, campo = "destino" } },
                filas = new[] { new[] { "BOGOTA", "CALI" } },
            });
            Assert.Equal(HttpStatusCode.OK, aplicada.StatusCode);

            var normalizada = await c.PostAsJsonAsync(RutaNormalizar, new { clienteNit = nit, valores = new[] { new { campo = "tipoVehiculo", valor = "Tractomula" } } });
            Assert.Equal(HttpStatusCode.OK, normalizada.StatusCode);

            var lista = await c.GetAsync($"{Ruta}?clienteNit={nit}");
            Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
            Assert.Equal(0, (await lista.Json()).GetArrayLength());

            Assert.Equal(0, ClientesConNit(nit));
        }
        finally { Limpiar(nit, marca); }
    }
}
