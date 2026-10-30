using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexo.Api.Common;

/// <summary>
/// Construye el DbContext para las herramientas de EF en tiempo de DISEÑO, no de
/// ejecución. Solo la usa <c>dotnet ef</c> al generar o aplicar migraciones.
///
/// Sin esto, EF intenta arrancar el host de la Api para conseguir un DbContext, y ese
/// host exige configuración real: cadenas de conexión, autenticación, todo. El
/// resultado es que generar una migración obliga a tener un entorno completo, y desde
/// un contenedor limpio directamente no se puede.
///
/// La cadena que devuelve NO se conecta a nada al generar una migración: EF solo
/// necesita saber que el proveedor es SQL Server para traducir el modelo. Para
/// APLICARLA sí hay que pasar una real por TEST_DB_CONNECTION o Nexo.
///
/// Vive en el proyecto de arranque, que es donde EF busca la configuración de diseño
/// y donde el paquete de herramientas ya estaba. Ponerla en Infrastructure obligaba a
/// agregar ese paquete allí, y eso arrastraba ocho avisos de vulnerabilidad en cada
/// compilación por una dependencia transitiva de MSBuild.
///
/// Genera migraciones así, desde un contenedor y sin configurar nada:
///
///   dotnet ef migrations add Nombre ///     --project src/Nexo.Infrastructure ///     --startup-project src/Nexo.Api ///     --context clsNexoDbContext --output-dir Persistence/Migrations
/// </summary>
public sealed class clsNexoDbContextFactory : IDesignTimeDbContextFactory<clsNexoDbContext>
{
    private const string CadenaDeDiseno =
        "Server=localhost;Database=Nexo_Design;Trusted_Connection=True;TrustServerCertificate=True;";

    public clsNexoDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
                     ?? Environment.GetEnvironmentVariable("ConnectionStrings__Nexo")
                     ?? CadenaDeDiseno;

        var opciones = new DbContextOptionsBuilder<clsNexoDbContext>()
            .UseSqlServer(cadena)
            .Options;

        return new clsNexoDbContext(opciones);
    }
}
