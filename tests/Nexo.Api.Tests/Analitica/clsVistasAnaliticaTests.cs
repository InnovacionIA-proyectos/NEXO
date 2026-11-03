using Nexo.Api.Tests.Infra;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Analitica;

/// <summary>
/// Issue #93: las 13 vistas `analitica.*` (S2-HU-10.1) se crean con SQL crudo
/// (<c>clsVistasAnaliticaSoloLectura</c>, luego corregidas por
/// <c>clsRecrearVistasAnaliticaEnDboNexoPrueba</c>) — EF Core no las conoce como
/// entidades, así que ningún test existente las tocaba. Por eso una migración que
/// movió el esquema de las tablas base pudo dejar las 13 rotas
/// ("Invalid object name") sin que la suite se diera cuenta.
///
/// No verifica columnas ni datos — cada vista ya tiene su propósito documentado en
/// docs/analitica/DICCIONARIO_DATOS.md. Solo confirma que el objeto existe y resuelve
/// contra el esquema real de las tablas base, que es exactamente lo que se rompió.
/// </summary>
[Trait("Categoria", "Integracion")]
[Collection("api")]
public class clsVistasAnaliticaTests(clsApiFixture fixture)
{
    [Theory]
    [InlineData("clientes")]
    [InlineData("rutas")]
    [InlineData("configuraciones_vehiculo")]
    [InlineData("servicios_conexos")]
    [InlineData("usuarios")]
    [InlineData("solicitudes")]
    [InlineData("lineas_solicitud")]
    [InlineData("cotizaciones")]
    [InlineData("cotizacion_lineas")]
    [InlineData("cotizacion_conexos")]
    [InlineData("tarifas_historicas")]
    [InlineData("audit_log")]
    [InlineData("inconsistencias")]
    public async Task La_vista_resuelve_contra_las_tablas_reales(string vista)
    {
        // No WithDb: es síncrono (Dispose del scope apenas retorna el delegado), y una
        // consulta async necesita el scope vivo hasta que termine, no hasta que empiece.
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();

        // El nombre de la vista es uno de los 13 literales fijos de arriba (InlineData),
        // nunca un valor externo — y un identificador de tabla no se puede pasar como
        // parámetro de SqlQuery, que solo parametriza valores.
#pragma warning disable EF1002
        var total = await db.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM analitica.{vista}")
            .SingleAsync();
#pragma warning restore EF1002

        Assert.True(total >= 0);
    }
}
