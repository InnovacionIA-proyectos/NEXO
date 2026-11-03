using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Tests.Infra;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Infrastructure.External;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>Decide si se puede crear una base de datos propia en el SQL Server de pruebas (el de CI y el local sí; la base corporativa compartida no).</summary>
public static class clsBaseDePruebaPropia
{
    public static string? MotivoParaOmitir()
    {
        var cadena = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cadena)) return "Necesita TEST_DB_CONNECTION.";
        try
        {
            using var c = new SqlConnection(clsBaseDeSicetacDePrueba.ConBase(cadena, "master"));
            c.Open();
            using var cmd = new SqlCommand("SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE')", c);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1 ? null : "El usuario de pruebas no puede crear bases de datos (base compartida).";
        }
        catch (SqlException ex)
        {
            return $"No se pudo comprobar el permiso para crear una base de pruebas: {ex.Message}";
        }
    }
}

/// <summary>Hecho que solo corre si se puede crear una base de datos propia: se prueba el motor contra un SICE-TAC de mentira que la prueba construye y borra.</summary>
public sealed class clsHechoConBaseDePruebaPropiaAttribute : FactAttribute
{
    public clsHechoConBaseDePruebaPropiaAttribute() => Skip = clsBaseDePruebaPropia.MotivoParaOmitir();
}

/// <summary>Igual que <see cref="clsHechoConBaseDePruebaPropiaAttribute"/>, para pruebas con varios casos.</summary>
public sealed class clsTeoriaConBaseDePruebaPropiaAttribute : TheoryAttribute
{
    public clsTeoriaConBaseDePruebaPropiaAttribute() => Skip = clsBaseDePruebaPropia.MotivoParaOmitir();
}

/// <summary>Una fila del DWSice de mentira. La unidad de transporte es ESTACAS salvo que la prueba diga otra.</summary>
public sealed record clsFilaSicetacDePrueba(string Configuracion, decimal Valor, string Ruta, string Condicion, string Unidad = "ESTACAS")
{
    public static implicit operator clsFilaSicetacDePrueba((string Configuracion, decimal Valor, string Ruta, string Condicion) t) =>
        new(t.Configuracion, t.Valor, t.Ruta, t.Condicion);
}

/// <summary>Un DWSice de mentira: una base temporal con la tabla <c>tblRutasSicetac</c> y unas filas, que se borra al terminar.</summary>
public sealed class clsBaseDeSicetacDePrueba : IDisposable
{
    private readonly string _administracion;
    private readonly string _nombre = "NexoDwSiceFalso_" + Guid.NewGuid().ToString("N")[..10];

    public string Cadena { get; }

    public clsBaseDeSicetacDePrueba(IEnumerable<clsFilaSicetacDePrueba> filas)
    {
        var origen = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")!;
        _administracion = ConBase(origen, "master");
        Cadena = ConBase(origen, _nombre);

        Ejecutar(_administracion, $"CREATE DATABASE [{_nombre}]");
        Ejecutar(Cadena, """
            CREATE TABLE dbo.tblRutasSicetac (
                ID int IDENTITY(1,1) PRIMARY KEY, PERIODO int NOT NULL, CONFIGURACION nvarchar(10) NOT NULL, VALOR numeric(18,5) NOT NULL,
                NOMBRERUTA nvarchar(500) NOT NULL, NOMBRETIPOCARGA nvarchar(50) NOT NULL, NOMBREUNIDADTRANSPORTE nvarchar(50) NOT NULL,
                FECHACREACION datetime2 NOT NULL, FECHAMODIFICACION datetime2 NOT NULL, ORIGEN nvarchar(50) NULL, DESTINO nvarchar(50) NULL,
                VALORHORA numeric(18,5) NULL, VALORSICECONHORA numeric(18,5) NULL, HORASRECORRIDO numeric(18,2) NULL, HORASPACTADAS int NULL,
                CONDICIONCARGA nvarchar(50) NULL, VIAESTANDAR nvarchar(3) NULL)
            """);

        var periodo = clsPeriodoSicetac.Actual(DateTimeOffset.UtcNow);
        foreach (var (configuracion, valor, ruta, condicion, unidad) in filas)
            Ejecutar(Cadena, "INSERT INTO dbo.tblRutasSicetac (PERIODO, CONFIGURACION, VALOR, NOMBRERUTA, NOMBRETIPOCARGA, NOMBREUNIDADTRANSPORTE, " +
                "FECHACREACION, FECHAMODIFICACION, HORASRECORRIDO, CONDICIONCARGA, VIAESTANDAR) " +
                $"VALUES ({periodo}, N'{configuracion}', {valor}, N'{ruta}', N'General', N'{unidad}', SYSDATETIME(), SYSDATETIME(), 10, N'{condicion}', N'SI')");
    }

    public static string ConBase(string cadena, string baseDeDatos) =>
        new SqlConnectionStringBuilder(cadena) { InitialCatalog = baseDeDatos }.ConnectionString;

    private static void Ejecutar(string cadena, string sql)
    {
        using var c = new SqlConnection(cadena);
        c.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        try
        {
            SqlConnection.ClearAllPools();
            Ejecutar(_administracion, $"ALTER DATABASE [{_nombre}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_nombre}]");
        }
        catch (SqlException)
        {
            // Mejor esfuerzo: una base temporal huérfana no afecta a nadie.
        }
    }
}

/// <summary>
/// El mapeo de vehículos aplicado al piso SICE-TAC, con el motor real y un DWSice de mentira. El caso que lo motivó: el Sencillo es <c>C2</c> en
/// NEXO y <c>2</c> en SICE-TAC; al buscarlo como <c>C2</c> no había filas y el motor, sin avisar, tomaba el vehículo más barato de la ruta
/// (la camioneta <c>CA_</c>). La prueba usa la tabla de mapeo real con su carga inicial (<c>docs/Database/377_mapeo_vehiculo_tarifario.sql</c>).
/// </summary>
[Collection("api")]
public class clsMapeoVehiculoSicetacTests(clsApiFixture fx) : IDisposable
{
    private const string Ruta = "MEDELLIN - BOGOTA";

    // CA_ es la fila más barata a propósito: es a la que caía el motor cuando no encontraba el vehículo.
    private readonly clsBaseDeSicetacDePrueba _dw = new(
    [
        ("CA_", 500_000m, Ruta, "CARGADO"), ("2", 2_000_000m, Ruta, "CARGADO"), ("2L1", 1_800_000m, Ruta, "CARGADO"),
        ("2L2", 1_700_000m, Ruta, "CARGADO"), ("2L3", 1_600_000m, Ruta, "CARGADO"), ("3", 3_000_000m, Ruta, "CARGADO"),
        ("3S3", 5_000_000m, Ruta, "CARGADO"), ("2_7", 1_200_000m, Ruta, "CARGADO"),
        ("CA_", 400_000m, "CARTAGENA - MEDELLIN", "VACIO"), ("2", 900_000m, "CARTAGENA - MEDELLIN", "VACIO"),
        ("3", 1_100_000m, "CARTAGENA - MEDELLIN", "VACIO"),
    ]);

    private WebApplicationFactory<clsProgram>? _fabrica;

    public void Dispose()
    {
        _fabrica?.Dispose();
        _dw.Dispose();
    }

    private IPropuestaTarifaPort Puerto(out IServiceScope scope)
    {
        // El DWSice de mentira se registra en los servicios: la cadena de configuración se lee antes de que la prueba pueda cambiarla.
        _fabrica = fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddDbContext<clsDwSiceDbContext>(o => o.UseSqlServer(_dw.Cadena))));
        scope = _fabrica.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();
    }

    [InlineData("C2", "2", 2_000_000)]   // Sencillo: 2 en SICE-TAC (antes caía en CA_)
    [InlineData("c2", "2", 2_000_000)]   // sin importar mayúsculas
    [InlineData("C3", "3", 3_000_000)]   // Dobletroque: 3 en SICE-TAC
    [InlineData("2L1", "2L1", 1_800_000)] // Turbo: cada uno conserva su código en SICE-TAC
    [InlineData("2L2", "2L2", 1_700_000)]
    [InlineData("2L3", "2L3", 1_600_000)]
    [InlineData("3S3", "3S3", 5_000_000)] // mismo código en las dos fuentes
    [InlineData("2_7", "2_7", 1_200_000)] // solo existe en SICE-TAC
    [InlineData("CA_", "CA_", 500_000)]   // la camioneta se pide por su código, no se le quita la C
    [clsTeoriaConBaseDePruebaPropia]
    public async Task El_piso_sicetac_busca_cada_vehiculo_con_su_codigo_de_SICE_TAC(string vehiculo, string configuracionEsperada, int valorEsperado)
    {
        var puerto = Puerto(out var scope);
        using var _ = scope;

        var piso = await puerto.PisoSicetacVigenteAsync("Medellín", "Bogotá", vehiculo, tipologia: null, carroceria: null);

        Assert.NotNull(piso);
        Assert.Equal(configuracionEsperada, piso!.Configuracion);
        Assert.Equal(valorEsperado, piso.ValorPiso);
    }

    [clsHechoConBaseDePruebaPropia]
    public async Task Un_vehiculo_sin_fila_de_mapeo_se_comporta_como_antes_se_busca_con_su_propio_codigo()
    {
        var puerto = Puerto(out var scope);
        using var _ = scope;

        // "XYZ" no está en el mapeo ni en SICE-TAC: sin filas de ese vehículo se usa otro, como siempre.
        var piso = await puerto.PisoSicetacVigenteAsync("Medellín", "Bogotá", "XYZ", tipologia: null, carroceria: null);

        Assert.NotNull(piso);
        Assert.Equal("CA_", piso!.Configuracion);
    }

    [InlineData("C2", "2", 900_000)]
    [InlineData("C3", "3", 1_100_000)]
    [clsTeoriaConBaseDePruebaPropia]
    public async Task El_aprovisionamiento_del_contenedor_tambien_busca_el_vehiculo_con_su_codigo_de_SICE_TAC(string vehiculo, string configuracionEsperada, int valorEsperado)
    {
        var puerto = Puerto(out var scope);
        using var _ = scope;

        var candidato = await puerto.EstimarAprovisionamientoAsync("Cartagena", "Medellín", vehiculo);

        Assert.NotNull(candidato);
        Assert.Equal(configuracionEsperada, candidato!.Configuracion);
        Assert.Equal(valorEsperado, candidato.ValorPisoBase);
        Assert.False(candidato.VehiculoRelajado);
    }
}
