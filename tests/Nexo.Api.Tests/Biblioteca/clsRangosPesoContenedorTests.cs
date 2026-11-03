using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.Common.Dtos;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Biblioteca.Adaptadores;
using Nexo.Infrastructure.External;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>
/// El vehículo de una línea de contenedor según <c>tbl_rangos_peso_contenedor</c> y la carrocería PORTACONTENEDORES por defecto, contra la base REAL y
/// el motor real con un DWSice de mentira. La prueba asegura las filas de rangos que necesita (y borra solo las que ella creó), así que corre igual si la
/// tabla ya tiene su carga inicial o está vacía.
/// </summary>
[Collection("api")]
public class clsRangosPesoContenedorTests(clsApiFixture fx) : IDisposable
{
    private readonly List<Guid> _rangos = [];
    private readonly List<Guid> _mapeos = [];
    private readonly List<Guid> _solicitudes = [];
    private clsBaseDeSicetacDePrueba? _dw;
    private string? _sap;
    private string? _nombreSap;
    private WebApplicationFactory<clsProgram>? _fabrica;

    public void Dispose()
    {
        _fabrica?.Dispose();
        _dw?.Dispose();
        if (_nombreSap is not null)
            try
            {
                Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
                using var c = new Microsoft.Data.SqlClient.SqlConnection(clsBaseDeSicetacDePrueba.ConBase(Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")!, "master"));
                c.Open();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand($"ALTER DATABASE [{_nombreSap}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_nombreSap}]", c);
                cmd.ExecuteNonQuery();
            }
            catch (Microsoft.Data.SqlClient.SqlException) { }
        fx.WithDb(db =>
        {
            foreach (var s in _solicitudes)
            {
                db.LineasSolicitud.Where(l => l.SolicitudId == s).ExecuteDelete();
                db.Solicitudes.Where(x => x.Id == s).ExecuteDelete();
            }
            db.Rutas.Where(r => r.Origen.StartsWith("CiudadT381")).ExecuteDelete();
            db.RangosPesoContenedor.Where(r => _rangos.Contains(r.Id)).ExecuteDelete();
            db.MapeosVehiculoTarifario.Where(m => _mapeos.Contains(m.Id)).ExecuteDelete();
            return 0;
        });
    }

    /// <summary>Deja el mapeo del vehículo y el rango (si no existen ya) para la prueba; lo que crea lo borra al terminar.</summary>
    private void Asegurar(int pies, string vehiculo, int pesoMaximo) => fx.WithDb(db =>
    {
        if (!db.MapeosVehiculoTarifario.Any(m => m.TipoVehiculo == vehiculo))
        {
            var m = new clsMapeoVehiculoTarifario { TipoVehiculo = vehiculo, CodigoSicetac = vehiculo, CodigoPlazaSap = vehiculo, Verificacion = "fila de prueba" };
            db.MapeosVehiculoTarifario.Add(m);
            _mapeos.Add(m.Id);
        }
        db.SaveChanges();
        if (!db.RangosPesoContenedor.Any(r => r.Contenedor == pies && r.TipoVehiculo == vehiculo))
        {
            var r = new clsRangoPesoContenedor { Contenedor = pies, TipoVehiculo = vehiculo, PesoMaximo = pesoMaximo };
            db.RangosPesoContenedor.Add(r);
            _rangos.Add(r.Id);
        }
        return db.SaveChanges();
    });

    private clsRangosPesoContenedorAdapter Adaptador(IMemoryCache? cache = null) => new(
        fx.Factory.Services.GetRequiredService<IServiceScopeFactory>(), cache ?? new MemoryCache(new MemoryCacheOptions()),
        NullLogger<clsRangosPesoContenedorAdapter>.Instance);

    // ------------------------------------------------------------------ la lectura de la tabla

    [Fact]
    public async Task El_adaptador_escoge_el_vehiculo_de_la_tabla_y_dice_que_no_hay_cuando_el_peso_supera_el_tope()
    {
        Asegurar(20, "3S3", 30_000);
        Asegurar(20, "3S2", 25_000);
        var a = Adaptador();

        var medio = await a.VehiculoParaAsync(20, 28_000);
        var tope = await a.VehiculoParaAsync(20, 30_000);
        var encima = await a.VehiculoParaAsync(20, 1_000_000);

        Assert.Equal(new clsVehiculoDeContenedor(true, "3S3"), medio);
        Assert.Equal("3S3", tope.TipoVehiculo);
        Assert.Equal(new clsVehiculoDeContenedor(true, null), encima); // tabla disponible, ningún vehículo aplica: no se vuelve a la regla vieja
    }

    [Fact]
    public async Task Un_rango_desactivado_no_se_usa()
    {
        Asegurar(20, "3S3", 30_000);
        fx.WithDb(db => db.RangosPesoContenedor.Where(r => _rangos.Contains(r.Id)).ExecuteUpdate(s => s.SetProperty(r => r.Activo, false)));
        // si la fila era de la carga inicial (no se creó aquí) sigue activa: solo se comprueba lo que la prueba controla
        if (_rangos.Count == 0) return;

        var r = await Adaptador().VehiculoParaAsync(20, 29_000);

        Assert.NotEqual("3S3", r.TipoVehiculo);
    }

    private sealed class clsAlcancesQueFallan : IServiceScopeFactory
    {
        public int Intentos { get; private set; }

        public IServiceScope CreateScope()
        {
            Intentos++;
            throw new InvalidOperationException("Invalid object name 'db_NEXO.tbl_rangos_peso_contenedor'.");
        }
    }

    [Fact]
    public async Task Si_la_tabla_no_se_puede_leer_informa_que_no_esta_disponible_para_usar_la_regla_de_antes_y_no_lanza()
    {
        var scopes = new clsAlcancesQueFallan();
        var a = new clsRangosPesoContenedorAdapter(scopes, new MemoryCache(new MemoryCacheOptions()), NullLogger<clsRangosPesoContenedorAdapter>.Instance);

        var r1 = await a.VehiculoParaAsync(20, 10_000);
        var r2 = await a.VehiculoParaAsync(40, 10_000);

        Assert.Equal(new clsVehiculoDeContenedor(false, null), r1);
        Assert.Equal(new clsVehiculoDeContenedor(false, null), r2);
        Assert.Equal(1, scopes.Intentos);
    }

    [Fact]
    public void El_recomendador_se_registra_como_servicio_unico_porque_lo_consultan_costeos_en_paralelo()
    {
        Assert.Same(fx.Factory.Services.GetRequiredService<IRangosPesoContenedorPort>(), fx.Factory.Services.GetRequiredService<IRangosPesoContenedorPort>());
    }

    // ------------------------------------------------------------------ el piso SICE-TAC de una línea de contenedor

    private const string Ruta = "CIUDADT381A - CIUDADT381B";

    private (Guid SolicitudId, Guid LineaId) Linea(decimal peso, string clase, string? vehiculo = null)
    {
        var (solicitudId, lineaId) = fx.WithDb(db =>
        {
            var s = new clsSolicitud { ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente, CreadoPor = db.Usuarios.First().Id };
            var ruta = new clsRuta { Origen = "CiudadT381A", Destino = "CiudadT381B" };
            db.Rutas.Add(ruta);
            clsConfiguracionVehiculo? v = vehiculo is null ? null : new clsConfiguracionVehiculo { TipoVehiculo = vehiculo };
            if (v is not null) db.ConfiguracionesVehiculo.Add(v);
            var linea = new clsLineaSolicitud
            {
                SolicitudId = s.Id, clsRuta = ruta, RutaId = ruta.Id, Peso = peso, NumeroLinea = 1,
                ClaseViaje = ClaseViaje.Contenedor, ClaseContenedor = clase, clsConfiguracionVehiculo = v, ConfigVehiculoId = v?.Id,
            };
            s.Lineas.Add(linea);
            db.Solicitudes.Add(s);
            db.SaveChanges();
            return (s.Id, linea.Id);
        });
        _solicitudes.Add(solicitudId);
        return (solicitudId, lineaId);
    }

    private async Task<clsPropuestaLineaDtoResumen> Proponer(Guid solicitudId, Guid lineaId)
    {
        _fabrica = fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddDbContext<clsDwSiceDbContext>(o => o.UseSqlServer(_dw!.Cadena))));
        using var scope = _fabrica.Services.CreateScope();
        var propuesta = await scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>().ProponerAsync(solicitudId);
        var linea = propuesta.Lineas.Single(l => l.LineaId == lineaId);
        return new clsPropuestaLineaDtoResumen(linea.Sicetac?.Configuracion, linea.Sicetac?.Valor, linea.Sicetac?.Detalle, linea.Sicetac?.VehiculoRelajado);
    }

    private sealed record clsPropuestaLineaDtoResumen(string? Configuracion, decimal? Valor, string? Detalle, bool? Relajado);

    private void DwDeContenedores() => _dw = new clsBaseDeSicetacDePrueba(
    [
        // La fila que corresponde a un contenedor de 20 pies de 28.000 kg (3S3 en portacontenedores) y las más baratas con las que antes se confundía.
        new("3S3", 8_320_000m, Ruta, "CARGADO", "PORTACONTENEDORES"), new("3S3", 6_000_000m, Ruta, "CARGADO", "ESTACAS"),
        new("2S2", 5_000_000m, Ruta, "CARGADO", "PORTACONTENEDORES"), new("3S2", 7_000_000m, Ruta, "CARGADO", "PORTACONTENEDORES"),
        new("CA_", 500_000m, Ruta, "CARGADO", "ESTACAS"),
    ]);

    [clsHechoConBaseDePruebaPropia]
    public async Task Una_linea_de_contenedor_sin_vehiculo_toma_el_de_la_tabla_segun_el_peso_y_la_unidad_portacontenedores()
    {
        Asegurar(20, "3S3", 30_000);
        Asegurar(20, "3S2", 25_000);
        Asegurar(20, "2S2", 14_000);
        DwDeContenedores();
        var (solicitud, linea) = Linea(28_000m, "CNT20.HC");

        var piso = await Proponer(solicitud, linea);

        Assert.Equal("3S3", piso.Configuracion);
        Assert.Equal(8_320_000m, piso.Valor);
        Assert.Contains("PORTACONTENEDORES", piso.Detalle);
        Assert.False(piso.Relajado);
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task El_peso_decide_el_vehiculo_y_un_contenedor_mas_liviano_da_otro_piso()
    {
        Asegurar(20, "3S3", 30_000);
        Asegurar(20, "3S2", 25_000);
        Asegurar(20, "2S2", 14_000);
        DwDeContenedores();
        var (solicitud, linea) = Linea(24_000m, "CNT20.HC");

        var piso = await Proponer(solicitud, linea);

        Assert.Equal("3S2", piso.Configuracion);
        Assert.Equal(7_000_000m, piso.Valor);
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task Una_linea_de_contenedor_con_vehiculo_pero_sin_carroceria_usa_portacontenedores_y_no_la_unidad_mas_barata()
    {
        Asegurar(20, "3S3", 30_000);
        DwDeContenedores();
        var (solicitud, linea) = Linea(28_000m, "CNT20.HC", vehiculo: "3S3");

        var piso = await Proponer(solicitud, linea);

        Assert.Equal("3S3", piso.Configuracion);
        Assert.Equal(8_320_000m, piso.Valor); // antes salía 6.000.000: la unidad ESTACAS, más barata
        Assert.Contains("PORTACONTENEDORES", piso.Detalle);
    }

    // ------------------------------------------------------------------ el camino oficial (tarifario SAP): contenedor y Sencillo

    /// <summary>Un dbSolucionesTI de mentira, vacío: sin tarifas de plaza ni catálogos, solo para que el camino oficial llegue al piso SICE-TAC.</summary>
    private void SapVacio()
    {
        _nombreSap = "NexoSapFalso_" + Guid.NewGuid().ToString("N")[..10];
        _sap = clsBaseDeSicetacDePrueba.ConBase(Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")!, _nombreSap);
        using var contexto = new clsDbSolucionesTiDbContext(new DbContextOptionsBuilder<clsDbSolucionesTiDbContext>().UseSqlServer(_sap).Options);
        contexto.Database.EnsureCreated();
    }

    private async Task<clsResultadoTarifarioSap?> Consultar(clsConsultaTarifarioSap consulta)
    {
        if (_sap is null) SapVacio();
        _fabrica = fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            if (_dw is not null) s.AddDbContext<clsDwSiceDbContext>(o => o.UseSqlServer(_dw.Cadena));
            s.AddDbContext<clsDbSolucionesTiDbContext>(o => o.UseSqlServer(_sap));
        }));
        using var scope = _fabrica.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITarifarioSapPort>().ConsultarAsync(consulta);
    }

    private async Task<clsCandidatoTarifaDto?> PisoOficial(clsConsultaTarifarioSap consulta) => (await Consultar(consulta))?.SicetacOficial;

    /// <summary>
    /// Inserta una fila en el dbSolucionesTI de mentira (el contexto real es de solo lectura). Las columnas obligatorias que la prueba no menciona
    /// reciben un valor neutro según su tipo.
    /// </summary>
    private void InsertarSap(string tabla, Dictionary<string, object> valores)
    {
        using var c = new Microsoft.Data.SqlClient.SqlConnection(_sap);
        c.Open();
        var obligatorias = new List<(string Nombre, string Tipo)>();
        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
            $"SELECT c.name, t.name FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID('dbo.{tabla}') AND c.is_nullable = 0 AND c.is_identity = 0", c))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) obligatorias.Add((r.GetString(0), r.GetString(1)));

        var todas = new Dictionary<string, object>(valores, StringComparer.OrdinalIgnoreCase);
        foreach (var (nombre, tipo) in obligatorias.Where(o => !todas.ContainsKey(o.Nombre)))
            todas[nombre] = tipo switch
            {
                "datetime" or "datetime2" or "date" => DateTime.UtcNow,
                "bit" => false,
                "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric" => 0,
                _ => string.Empty,
            };

        using var identidad = new Microsoft.Data.SqlClient.SqlCommand($"SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID('dbo.{tabla}')", c);
        var conIdentidad = todas.ContainsKey("ID") && Convert.ToInt32(identidad.ExecuteScalar()) > 0;
        var insercion = $"INSERT INTO dbo.{tabla} ({string.Join(", ", todas.Keys)}) VALUES ({string.Join(", ", todas.Keys.Select((_, i) => "@p" + i))})";
        using var insertar = new Microsoft.Data.SqlClient.SqlCommand(
            conIdentidad ? $"SET IDENTITY_INSERT dbo.{tabla} ON; {insercion}; SET IDENTITY_INSERT dbo.{tabla} OFF" : insercion, c);
        var n = 0;
        foreach (var v in todas.Values) insertar.Parameters.AddWithValue("@p" + n++, v);
        insertar.ExecuteNonQuery();
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task El_camino_oficial_escoge_el_vehiculo_de_la_tabla_para_un_contenedor_y_su_unidad_es_portacontenedores()
    {
        Asegurar(20, "3S3", 30_000);
        Asegurar(20, "3S2", 25_000);
        DwDeContenedores();

        var piso = await PisoOficial(new clsConsultaTarifarioSap(
            "CiudadT381A", "CiudadT381B", LineaNegocio.PuertoAInterior, ClaseViaje.Contenedor, "CNT20.HC", null, null, 28_000m, false, false));

        Assert.NotNull(piso);
        Assert.Equal("3S3", piso!.Configuracion);
        Assert.Equal(8_320_000m, piso.Valor);
        Assert.Contains("unidad 'PORTACONTENEDORES'", piso.Criterio);
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task El_camino_oficial_vuelve_a_la_unidad_de_antes_si_SICE_TAC_no_trae_portacontenedores_para_esa_ruta()
    {
        Asegurar(20, "3S3", 30_000);
        _dw = new clsBaseDeSicetacDePrueba([new("3S3", 6_000_000m, Ruta, "CARGADO", "ESTACAS")]);

        var piso = await PisoOficial(new clsConsultaTarifarioSap(
            "CiudadT381A", "CiudadT381B", LineaNegocio.PuertoAInterior, ClaseViaje.Contenedor, "CNT20.HC", null, null, 28_000m, false, false));

        Assert.NotNull(piso);
        Assert.Equal(6_000_000m, piso!.Valor);
        Assert.Contains("unidad 'ESTACAS'", piso.Criterio);
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task El_camino_oficial_sin_la_clase_de_contenedor_o_por_encima_del_tope_no_inventa_un_vehiculo()
    {
        Asegurar(20, "3S3", 30_000);
        DwDeContenedores();

        var encima = await PisoOficial(new clsConsultaTarifarioSap(
            "CiudadT381A", "CiudadT381B", LineaNegocio.PuertoAInterior, ClaseViaje.Contenedor, "CNT20.HC", null, null, 1_000_000m, false, false));

        Assert.Null(encima); // ningún vehículo lleva ese peso: sin piso, no el de otro vehículo
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task El_camino_oficial_busca_el_Sencillo_C2_como_2_en_SICE_TAC()
    {
        _dw = new clsBaseDeSicetacDePrueba([new("2", 2_000_000m, Ruta, "CARGADO"), new("CA_", 500_000m, Ruta, "CARGADO")]);

        var piso = await PisoOficial(new clsConsultaTarifarioSap(
            "CiudadT381A", "CiudadT381B", LineaNegocio.Nacional, ClaseViaje.Cupo, null, "C2", null, 8_000m, false, false));

        Assert.NotNull(piso);
        Assert.Equal("2", piso!.Configuracion);
        Assert.Equal(2_000_000m, piso.Valor);
    }

    // ------------------------------------------------------------------ el camino oficial: tarifas de plaza de SAP

    private void PlazaDeCupo()
    {
        SapVacio();
        // Catálogo de vehículos de SAP: el Sencillo se llama 2 y el Turbo de plaza 2L2. El 3S2 no está: no tiene tarifa de plaza.
        InsertarSap("tblSAPTiposVehiculoTarifario", new() { ["ID"] = 9, ["CODIGO"] = "2", ["DESCRIPCION"] = "Sencillo", ["ESTADO"] = true });
        InsertarSap("tblSAPTiposVehiculoTarifario", new() { ["ID"] = 10, ["CODIGO"] = "2L2", ["DESCRIPCION"] = "Turbo", ["ESTADO"] = true });
        InsertarSap("tblSAPTiposVehiculoTarifario", new() { ["ID"] = 12, ["CODIGO"] = "3S3", ["DESCRIPCION"] = "Tractomula", ["ESTADO"] = true });

        // Vigentes, de cupo, en la misma ruta. La del vehículo más caro es la que antes se ofrecía "en silencio" cuando el vehículo no se encontraba.
        foreach (var (vehiculo, valor) in new[] { ("9", "2500000"), ("10", "1800000"), ("12", "5000000") })
            InsertarSap("tblSAPTarifas", new()
            {
                ["LINEANEGOCIO"] = 4, ["CLASEVIAJE"] = 2, ["ORIGEN"] = "CIUDADT381A", ["DESTINO"] = "CIUDADT381B", ["VALOR"] = valor,
                ["VIGENCIA"] = DateTime.UtcNow.AddYears(1), ["TIPOVEHICULO"] = vehiculo, ["TIPOCARROSERIA"] = "1", ["PESOLIMITE"] = "10000",
            });
    }

    private Task<clsResultadoTarifarioSap?> PlazaDeCupo(string vehiculo) => Consultar(new clsConsultaTarifarioSap(
        "CiudadT381A", "CiudadT381B", LineaNegocio.Nacional, ClaseViaje.Cupo, null, vehiculo, null, 8_000m, false, false));

    [InlineData("C2", 2_500_000)]    // Sencillo: 2 en Plaza/SAP (antes no lo encontraba y proponía la tarifa más cara de la ruta)
    [InlineData("2L1", 1_800_000)]   // Turbo: se tarifa como 2L2
    [InlineData("2L3", 1_800_000)]
    [InlineData("3S3", 5_000_000)]   // mismo código en las dos fuentes
    [clsTeoriaConBaseDePruebaPropia]
    public async Task El_camino_oficial_busca_cada_vehiculo_en_plaza_con_su_codigo_de_Plaza_SAP(string vehiculo, int valorEsperado)
    {
        PlazaDeCupo();

        var resultado = await PlazaDeCupo(vehiculo);

        Assert.NotNull(resultado?.Tarifa);
        Assert.Equal(valorEsperado, resultado!.Tarifa!.Valor);
        Assert.Null(resultado.Tarifa.Advertencia); // un match del vehículo, no el "mayor valor vigente" de reserva
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task Un_vehiculo_que_no_tiene_tarifa_de_plaza_no_recibe_la_de_otro_en_una_linea_de_cupo()
    {
        PlazaDeCupo();

        var resultado = await PlazaDeCupo("3S2"); // 3S2 solo existe en SICE-TAC: el mapeo no le da código de plaza

        Assert.Null(resultado?.Tarifa);
    }
}
