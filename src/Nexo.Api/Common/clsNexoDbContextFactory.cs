using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexo.Api.Common;

/// <summary>
/// DbContext para las herramientas de EF en tiempo de diseño (<c>dotnet ef</c>): evita arrancar el host de la Api, que exige configuración real.
/// La cadena no se conecta a nada al generar una migración; para aplicarla hay que pasar una real por TEST_DB_CONNECTION.
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
