using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S6-HU-27.2/#321 y S6-HU-27.3/#322 contra la API y la base reales: el diccionario de alias (tbl_alias_elementos, cargado en #315 con 71
/// filas), la resolución de catálogos con alias, la bandeja de normalización de vehículos en cuarentena (mapear / descartar) y el cambio de
/// vehículo de una línea. Requiere la columna <c>Descartada</c> de tbl_configuraciones_vehiculo (ver ESQUEMA_NEXO.sql).
/// </summary>
[Collection("api")]
public class clsAliasYBandejaVehiculosApiTests
{
    private readonly clsApiFixture _fx;
    public clsAliasYBandejaVehiculosApiTests(clsApiFixture fx) => _fx = fx;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private static string Enc(string s) => Uri.EscapeDataString(s);

    // ------------------------------------------------------------------ diccionario: leer y resolver

    [Theory]
    [InlineData("TB")]
    [InlineData("T.B")]
    [InlineData("Turbo")]
    [InlineData("2L2")]
    [InlineData("  turbo ")]
    public async Task Las_variantes_de_Turbo_resuelven_al_mismo_codigo(string texto)
    {
        var resp = await _fx.Client("Comercial").GetAsync($"/api/catalogos/alias/resolver?tipo=vehiculo&texto={Enc(texto)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var j = await resp.Json();
        Assert.True(j.GetProperty("reconocido").GetBoolean());
        Assert.Equal("2L2", j.GetProperty("codigo").GetString());
        Assert.Equal("Vehiculo", j.GetProperty("tipoElemento").GetString());
    }

    [Fact]
    public async Task Un_texto_inventado_no_se_reconoce()
    {
        var j = await (await _fx.Client().GetAsync($"/api/catalogos/alias/resolver?texto={Enc("Vehiculo inventado " + Sufijo())}")).Json();

        Assert.False(j.GetProperty("reconocido").GetBoolean());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("codigo").ValueKind);
    }

    [Theory]
    [InlineData("40 HC", "CNT40.HC")]
    [InlineData("40HC", "CNT40.HC")]
    [InlineData("20 STD", "CNT20.ST")]
    public async Task Las_clases_de_contenedor_resuelven_por_el_diccionario(string texto, string codigo)
    {
        var j = await (await _fx.Client().GetAsync($"/api/catalogos/alias/resolver?tipo=contenedor&texto={Enc(texto)}")).Json();

        Assert.True(j.GetProperty("reconocido").GetBoolean());
        Assert.Equal(codigo, j.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Un_alias_de_vehiculo_no_resuelve_como_contenedor()
    {
        var j = await (await _fx.Client().GetAsync($"/api/catalogos/alias/resolver?tipo=contenedor&texto=TB")).Json();

        Assert.False(j.GetProperty("reconocido").GetBoolean());
    }

    [Fact]
    public async Task Resolver_sin_texto_o_con_tipo_invalido_responde_400()
    {
        var cli = _fx.Client();

        var sinTexto = await cli.GetAsync("/api/catalogos/alias/resolver?tipo=vehiculo");
        var tipoMalo = await cli.GetAsync("/api/catalogos/alias/resolver?tipo=camion&texto=TB");

        Assert.Equal(HttpStatusCode.BadRequest, sinTexto.StatusCode);
        Assert.Equal("TEXTO_REQUERIDO", await sinTexto.ErrorCode());
        Assert.Equal(HttpStatusCode.BadRequest, tipoMalo.StatusCode);
        Assert.Equal("TIPO_ALIAS_INVALIDO", await tipoMalo.ErrorCode());
    }

    [Fact]
    public async Task La_lista_del_diccionario_se_filtra_por_tipo()
    {
        var cli = _fx.Client("LiderProceso");

        var vehiculos = await cli.GetFromJsonAsync<JsonElement>("/api/catalogos/alias?tipo=vehiculo");
        var contenedores = await cli.GetFromJsonAsync<JsonElement>("/api/catalogos/alias?tipo=contenedor");
        var todos = await cli.GetFromJsonAsync<JsonElement>("/api/catalogos/alias");

        Assert.All(vehiculos.EnumerateArray(), a => Assert.Equal("Vehiculo", a.GetProperty("tipoElemento").GetString()));
        Assert.All(contenedores.EnumerateArray(), a => Assert.Equal("Contenedor", a.GetProperty("tipoElemento").GetString()));
        Assert.Contains(vehiculos.EnumerateArray(), a => a.GetProperty("aliasNormalizado").GetString() == "TB");
        Assert.True(todos.GetArrayLength() >= vehiculos.GetArrayLength() + contenedores.GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await cli.GetAsync("/api/catalogos/alias?tipo=otro")).StatusCode);
    }

    // ------------------------------------------------------------------ diccionario: agregar

    private void BorrarAlias(string normalizado) =>
        _fx.WithDb(db => db.AliasElementos.Where(a => a.AliasNormalizado == normalizado).ExecuteDelete());

    [Theory]
    [InlineData("Comercial")]
    [InlineData("AdminBiblioteca")]
    [InlineData("ServiciosLogisticos")]
    [InlineData("LiderProceso")]
    public async Task Solo_AdminTarifas_y_Admin_agregan_alias(string rol)
    {
        var resp = await _fx.Client(rol).PostJson("/api/catalogos/alias", new { tipo = "vehiculo", alias = "Zzz " + Sufijo(), codigo = "2L2" });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Theory]
    [InlineData("AdminTarifas")]
    [InlineData("Admin")]
    public async Task Agregar_un_alias_lo_deja_resoluble_y_uno_repetido_da_409(string rol)
    {
        var texto = $"Test321 {Sufijo()}";
        var normalizado = texto.ToUpperInvariant();
        try
        {
            var cli = _fx.Client(rol);

            var creado = await cli.PostJson("/api/catalogos/alias", new { tipo = "Vehiculo", alias = texto, codigo = "2l2" });
            Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
            var j = await creado.Json();
            Assert.Equal("2L2", j.GetProperty("codigo").GetString());          // código canónico, aunque llegó en minúscula
            Assert.Equal(normalizado, j.GetProperty("aliasNormalizado").GetString());

            var resuelto = await (await cli.GetAsync($"/api/catalogos/alias/resolver?tipo=vehiculo&texto={Enc(texto.ToLowerInvariant())}")).Json();
            Assert.True(resuelto.GetProperty("reconocido").GetBoolean());

            var repetido = await cli.PostJson("/api/catalogos/alias", new { tipo = "contenedor", alias = texto, codigo = "CNT40.HC" });
            Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
            Assert.Equal("ALIAS_DUPLICADO", await repetido.ErrorCode());
        }
        finally { BorrarAlias(normalizado); }
    }

    [Fact]
    public async Task Agregar_con_un_codigo_que_no_es_oficial_o_datos_incompletos_da_400_y_no_guarda()
    {
        var texto = $"Test321 malo {Sufijo()}";
        var cli = _fx.Client("AdminTarifas");

        var codigoMalo = await cli.PostJson("/api/catalogos/alias", new { tipo = "vehiculo", alias = texto, codigo = "NOEXISTE" });
        var tipoMalo = await cli.PostJson("/api/catalogos/alias", new { tipo = "camion", alias = texto, codigo = "2L2" });
        var sinAlias = await cli.PostJson("/api/catalogos/alias", new { tipo = "vehiculo", alias = "", codigo = "2L2" });

        Assert.Equal(HttpStatusCode.BadRequest, codigoMalo.StatusCode);
        Assert.Equal("CODIGO_NO_RECONOCIDO", await codigoMalo.ErrorCode());
        Assert.Equal("TIPO_ALIAS_INVALIDO", await tipoMalo.ErrorCode());
        Assert.Equal(HttpStatusCode.BadRequest, sinAlias.StatusCode);
        Assert.False(_fx.WithDb(db => db.AliasElementos.Any(a => a.AliasNormalizado == texto.ToUpperInvariant())));
    }

    // ------------------------------------------------------------------ captura: resolver-catalogos usa el diccionario

    [Fact]
    public async Task Resolver_catalogos_manda_TB_T_B_Turbo_y_2L2_a_la_misma_configuracion_y_un_desconocido_a_cuarentena()
    {
        var sufijo = Sufijo();
        var carroceria = $"Test321-{sufijo}";
        var desconocido = $"Test321 raro {sufijo}";
        try
        {
            var lineas = new[] { "TB", "T.B", "Turbo", "2L2", desconocido }
                .Select(t => new { origen = $"Cali (t321-{sufijo})", destino = $"Bogotá (t321-{sufijo})", tipoVehiculo = t, carroceria })
                .ToArray();

            var resp = await _fx.Client("Comercial").PostJson("/api/solicitudes/resolver-catalogos", lineas);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var r = (await resp.Json()).EnumerateArray().ToArray();
            var ids = r.Take(4).Select(x => x.GetProperty("configVehiculoId").GetGuid()).Distinct().ToArray();
            Assert.Single(ids);                                                                   // las cuatro, la MISMA fila
            Assert.True(r[0].GetProperty("clsVehiculoCreado").GetBoolean());                      // la primera crea la fila…
            Assert.All(r.Skip(1).Take(3), x => Assert.False(x.GetProperty("clsVehiculoCreado").GetBoolean()));   // …las otras tres la reutilizan
            var (tipo, oficial) = _fx.WithDb(db => db.ConfiguracionesVehiculo.Where(v => v.Id == ids[0]).Select(v => new { v.TipoVehiculo, v.EsCatalogoOficial }).AsEnumerable().Select(v => (v.TipoVehiculo, v.EsCatalogoOficial)).Single());
            Assert.Equal("2L2", tipo);                                                            // se guardó el código, no el texto
            Assert.True(oficial);

            var rara = r[4].GetProperty("configVehiculoId").GetGuid();
            var (tipoRaro, oficialRaro) = _fx.WithDb(db => db.ConfiguracionesVehiculo.Where(v => v.Id == rara).Select(v => new { v.TipoVehiculo, v.EsCatalogoOficial }).AsEnumerable().Select(v => (v.TipoVehiculo, v.EsCatalogoOficial)).Single());
            Assert.Equal(desconocido, tipoRaro);                                                  // sin alias: cuarentena, como siempre
            Assert.False(oficialRaro);
        }
        finally { LimpiarCatalogos(sufijo); }
    }

    private void LimpiarCatalogos(string sufijo) => _fx.WithDb(db =>
    {
        db.ConfiguracionesVehiculo.Where(v => v.Carroceria != null && v.Carroceria.Contains(sufijo) || v.TipoVehiculo.Contains(sufijo)).ExecuteDelete();
        db.Rutas.Where(r => r.Origen.Contains(sufijo)).ExecuteDelete();
        return 0;
    });

    // ------------------------------------------------------------------ bandeja de normalización

    private Guid NuevaEnCuarentena(string tipo, string? carroceria = null) => _fx.WithDb(db =>
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = tipo, Carroceria = carroceria, EsCatalogoOficial = false };
        db.ConfiguracionesVehiculo.Add(v);
        db.SaveChanges();
        return v.Id;
    });

    private void BorrarConfiguraciones(params Guid[] ids) =>
        _fx.WithDb(db => db.ConfiguracionesVehiculo.Where(v => ids.Contains(v.Id)).ExecuteDelete());

    private static async Task<bool> EstaEnLaBandeja(HttpClient cli, Guid id)
    {
        var lista = await cli.GetFromJsonAsync<JsonElement>("/api/catalogos/vehiculos-cuarentena");
        return lista.EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == id);
    }

    [Theory]
    [InlineData("Comercial")]
    [InlineData("AdminBiblioteca")]
    [InlineData("ServiciosLogisticos")]
    [InlineData("LiderProceso")]
    public async Task La_bandeja_es_solo_de_AdminTarifas_y_Admin(string rol)
    {
        var cli = _fx.Client(rol);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await cli.GetAsync("/api/catalogos/vehiculos-cuarentena")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cli.PostJson($"/api/catalogos/vehiculos-cuarentena/{id}/mapear", new { codigo = "2L2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cli.PostAsync($"/api/catalogos/vehiculos-cuarentena/{id}/descartar", null)).StatusCode);
    }

    [Fact]
    public async Task Mapear_una_configuracion_en_cuarentena_crea_el_alias_y_la_saca_de_la_bandeja()
    {
        var tipo = $"Test322 Mulita {Sufijo()}";
        var id = NuevaEnCuarentena(tipo, "Furgon");
        try
        {
            var cli = _fx.Client("AdminTarifas");
            Assert.True(await EstaEnLaBandeja(cli, id));

            var resp = await cli.PostJson($"/api/catalogos/vehiculos-cuarentena/{id}/mapear", new { codigo = "2s2" });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var j = await resp.Json();
            Assert.True(j.GetProperty("aliasCreado").GetBoolean());
            Assert.Equal("2S2", j.GetProperty("codigo").GetString());
            Assert.False(await EstaEnLaBandeja(cli, id));                                         // salió de la lista

            var resuelto = await (await cli.GetAsync($"/api/catalogos/alias/resolver?tipo=vehiculo&texto={Enc(tipo)}")).Json();
            Assert.Equal("2S2", resuelto.GetProperty("codigo").GetString());                      // y el mismo texto ya no cae en cuarentena

            Assert.Equal(HttpStatusCode.OK, (await cli.PostJson($"/api/catalogos/vehiculos-cuarentena/{id}/mapear", new { codigo = "2S2" })).StatusCode);   // repetir con el mismo código: idempotente
            var otro = await cli.PostJson($"/api/catalogos/vehiculos-cuarentena/{id}/mapear", new { codigo = "C2" });
            Assert.Equal(HttpStatusCode.Conflict, otro.StatusCode);                               // a otro código: el alias ya apunta a 2S2
            Assert.Equal("ALIAS_DUPLICADO", await otro.ErrorCode());
        }
        finally
        {
            BorrarAlias(tipo.ToUpperInvariant());
            BorrarConfiguraciones(id);
        }
    }

    [Fact]
    public async Task Mapear_con_un_codigo_no_oficial_da_400_y_la_fila_sigue_en_la_bandeja()
    {
        var id = NuevaEnCuarentena($"Test322 Codigo malo {Sufijo()}");
        try
        {
            var cli = _fx.Client("Admin");

            var resp = await cli.PostJson($"/api/catalogos/vehiculos-cuarentena/{id}/mapear", new { codigo = "NOEXISTE" });

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            Assert.Equal("CODIGO_NO_RECONOCIDO", await resp.ErrorCode());
            Assert.True(await EstaEnLaBandeja(cli, id));
        }
        finally { BorrarConfiguraciones(id); }
    }

    [Fact]
    public async Task Descartar_saca_la_fila_de_la_bandeja_sin_borrarla()
    {
        var id = NuevaEnCuarentena($"Test322 Basura {Sufijo()}");
        try
        {
            var cli = _fx.Client("AdminTarifas");
            Assert.True(await EstaEnLaBandeja(cli, id));

            var resp = await cli.PostAsync($"/api/catalogos/vehiculos-cuarentena/{id}/descartar", null);

            Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
            Assert.False(await EstaEnLaBandeja(cli, id));
            var (existe, descartada) = _fx.WithDb(db => (db.ConfiguracionesVehiculo.Any(v => v.Id == id), db.ConfiguracionesVehiculo.Single(v => v.Id == id).Descartada));
            Assert.True(existe);
            Assert.True(descartada);

            var otraVez = await cli.PostAsync($"/api/catalogos/vehiculos-cuarentena/{id}/descartar", null);
            Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
            Assert.Equal("VEHICULO_YA_DESCARTADO", await otraVez.ErrorCode());
        }
        finally { BorrarConfiguraciones(id); }
    }

    [Fact]
    public async Task Mapear_o_descartar_una_inexistente_da_404_y_una_oficial_da_409()
    {
        var cli = _fx.Client("Admin");
        var oficial = _fx.WithDb(db =>
        {
            var v = new clsConfiguracionVehiculo { TipoVehiculo = $"Test322 Oficial {Sufijo()}", EsCatalogoOficial = true };
            db.ConfiguracionesVehiculo.Add(v);
            db.SaveChanges();
            return v.Id;
        });
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await cli.PostJson($"/api/catalogos/vehiculos-cuarentena/{Guid.NewGuid()}/mapear", new { codigo = "2L2" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await cli.PostAsync($"/api/catalogos/vehiculos-cuarentena/{Guid.NewGuid()}/descartar", null)).StatusCode);
            var m = await cli.PostJson($"/api/catalogos/vehiculos-cuarentena/{oficial}/mapear", new { codigo = "2L2" });
            var d = await cli.PostAsync($"/api/catalogos/vehiculos-cuarentena/{oficial}/descartar", null);
            Assert.Equal("VEHICULO_NO_ESTA_EN_CUARENTENA", await m.ErrorCode());
            Assert.Equal(HttpStatusCode.Conflict, d.StatusCode);
        }
        finally { BorrarConfiguraciones(oficial); }
    }

    // ------------------------------------------------------------------ cambiar el vehículo de una línea

    private (Guid SolicitudId, Guid LineaId, Guid VehiculoViejo, Guid VehiculoNuevo, string Sufijo) NuevaLineaConVehiculo()
    {
        var sufijo = Sufijo();
        return _fx.WithDb(db =>
        {
            var viejo = new clsConfiguracionVehiculo { TipoVehiculo = $"Test322 Viejo {sufijo}", EsCatalogoOficial = true };
            var nuevo = new clsConfiguracionVehiculo { TipoVehiculo = $"Test322 Nuevo {sufijo}", EsCatalogoOficial = true };
            var ruta = new clsRuta { Origen = $"Cali (t322-{sufijo})", Destino = $"Bogotá (t322-{sufijo})" };
            db.ConfiguracionesVehiculo.AddRange(viejo, nuevo);
            db.Rutas.Add(ruta);
            var s = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
            };
            var l = new clsLineaSolicitud { SolicitudId = s.Id, clsRuta = ruta, RutaId = ruta.Id, ConfigVehiculoId = viejo.Id, Peso = 1000, NumeroLinea = 1 };
            s.Lineas.Add(l);
            db.Solicitudes.Add(s);
            db.SaveChanges();
            return (s.Id, l.Id, viejo.Id, nuevo.Id, sufijo);
        });
    }

    private void LimpiarLinea(Guid solicitudId, string sufijo)
    {
        try
        {
            _fx.WithDb(db =>
            {
                db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
                db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
                db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
                db.ConfiguracionesVehiculo.Where(v => v.TipoVehiculo.Contains(sufijo)).ExecuteDelete();
                db.Rutas.Where(r => r.Origen.Contains(sufijo)).ExecuteDelete();
                return 0;
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException) { }
    }

    [Theory]
    [InlineData("Comercial")]
    [InlineData("AdminBiblioteca")]
    [InlineData("ServiciosLogisticos")]
    [InlineData("LiderProceso")]
    public async Task Cambiar_el_vehiculo_es_solo_de_AdminTarifas_y_Admin(string rol)
    {
        var id = Guid.NewGuid();

        var resp = await _fx.Client(rol).PutJson($"/api/cotizaciones/{id}/lineas/{id}/vehiculo", new { configVehiculoId = id });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Theory]
    [InlineData("AdminTarifas")]
    [InlineData("Admin")]
    public async Task Cambiar_el_vehiculo_descarta_el_flete_y_deja_la_linea_por_costear(string rol)
    {
        var (solicitudId, lineaId, viejo, nuevo, sufijo) = NuevaLineaConVehiculo();
        try
        {
            var cot = (await (await _fx.Client().PostJson("/api/cotizaciones", new { solicitudId })).Json()).GetProperty("id").GetGuid();
            var flete = await _fx.Client("AdminTarifas").PostJson($"/api/cotizaciones/{cot}/lineas", new { lineaSolicitudId = lineaId, valorFlete = 2_000_000m, fuente = "manual" });
            Assert.Equal(HttpStatusCode.OK, flete.StatusCode);

            var resp = await _fx.Client(rol).PutJson($"/api/cotizaciones/{cot}/lineas/{lineaId}/vehiculo", new { configVehiculoId = nuevo });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var j = await resp.Json();
            Assert.True(j.GetProperty("cambioVehiculo").GetBoolean());
            Assert.True(j.GetProperty("fleteEliminado").GetBoolean());
            Assert.Equal("porCostear", j.GetProperty("estadoLinea").GetString());
            Assert.Equal(nuevo, j.GetProperty("configVehiculoId").GetGuid());

            var (vehiculoEnBd, filas, total) = _fx.WithDb(db => (
                db.LineasSolicitud.Single(l => l.Id == lineaId).ConfigVehiculoId,
                db.CotizacionLineas.Count(cl => cl.LineaSolicitudId == lineaId),
                db.Cotizaciones.Single(c => c.Id == cot).Total));
            Assert.Equal(nuevo, vehiculoEnBd);
            Assert.Equal(0, filas);
            Assert.Equal(0m, total);
            Assert.NotEqual(viejo, vehiculoEnBd);
        }
        finally { LimpiarLinea(solicitudId, sufijo); }
    }

    [Fact]
    public async Task Cambiar_el_vehiculo_con_la_cotizacion_entregada_da_409_y_con_un_vehiculo_en_cuarentena_da_409()
    {
        var (solicitudId, lineaId, viejo, nuevo, sufijo) = NuevaLineaConVehiculo();
        var cuarentena = NuevaEnCuarentena($"Test322 Cuarentena {sufijo}");
        try
        {
            var cli = _fx.Client("AdminTarifas");
            var cot = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId })).Json()).GetProperty("id").GetGuid();

            var enCuarentena = await cli.PutJson($"/api/cotizaciones/{cot}/lineas/{lineaId}/vehiculo", new { configVehiculoId = cuarentena });
            Assert.Equal(HttpStatusCode.Conflict, enCuarentena.StatusCode);
            Assert.Equal("VEHICULO_NO_OFICIAL", await enCuarentena.ErrorCode());

            _fx.WithDb(db => db.Cotizaciones.Where(c => c.Id == cot).ExecuteUpdate(c => c.SetProperty(x => x.Estado, EstadoCotizacion.Entregada)));
            var entregada = await cli.PutJson($"/api/cotizaciones/{cot}/lineas/{lineaId}/vehiculo", new { configVehiculoId = nuevo });
            Assert.Equal(HttpStatusCode.Conflict, entregada.StatusCode);
            Assert.Equal("COTIZACION_NO_EDITABLE", await entregada.ErrorCode());
            Assert.Equal(viejo, _fx.WithDb(db => db.LineasSolicitud.Single(l => l.Id == lineaId).ConfigVehiculoId));
        }
        finally
        {
            LimpiarLinea(solicitudId, sufijo);
        }
    }

    [Fact]
    public async Task Cambiar_el_vehiculo_sin_indicarlo_o_con_un_texto_desconocido_da_400_y_no_crea_cuarentena()
    {
        var (solicitudId, lineaId, _, _, sufijo) = NuevaLineaConVehiculo();
        try
        {
            var cli = _fx.Client("Admin");
            var cot = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId })).Json()).GetProperty("id").GetGuid();
            var url = $"/api/cotizaciones/{cot}/lineas/{lineaId}/vehiculo";

            var ninguno = await cli.PutJson(url, new { });
            var desconocido = await cli.PutJson(url, new { tipoVehiculo = $"Test322 Inventado {sufijo}" });

            Assert.Equal("VEHICULO_REQUERIDO", await ninguno.ErrorCode());
            Assert.Equal("VEHICULO_NO_RECONOCIDO", await desconocido.ErrorCode());
            Assert.False(_fx.WithDb(db => db.ConfiguracionesVehiculo.Any(v => v.TipoVehiculo == $"Test322 Inventado {sufijo}")));
        }
        finally { LimpiarLinea(solicitudId, sufijo); }
    }

    [Fact]
    public async Task Cambiar_el_vehiculo_por_texto_con_alias_usa_el_codigo()
    {
        var (solicitudId, lineaId, _, _, sufijo) = NuevaLineaConVehiculo();
        var carroceria = $"Test322-{sufijo}";
        try
        {
            var cli = _fx.Client("AdminTarifas");
            var cot = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId })).Json()).GetProperty("id").GetGuid();

            var resp = await cli.PutJson($"/api/cotizaciones/{cot}/lineas/{lineaId}/vehiculo", new { tipoVehiculo = "Turbo", carroceria });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var j = await resp.Json();
            Assert.Equal("2L2", j.GetProperty("tipoVehiculo").GetString());
            Assert.True(j.GetProperty("esCatalogoOficial").GetBoolean());
        }
        finally
        {
            LimpiarLinea(solicitudId, sufijo);
            _fx.WithDb(db => db.ConfiguracionesVehiculo.Where(v => v.Carroceria != null && v.Carroceria.Contains(sufijo)).ExecuteDelete());
        }
    }
}
