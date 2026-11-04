namespace Nexo.Infrastructure.Persistence;

/// <summary>
/// Esquema propio de NEXO, leído de <c>Database__Esquema</c> directo del entorno (sin IConfiguration) para que funcione en runtime, herramientas de EF y tests. Falla si falta.
/// Cambiarlo sobre una base que ya tiene otro esquema no migra nada: EF crearía las tablas desde cero y dejaría el anterior huérfano; es para bases nuevas.
/// </summary>
public static class clsEsquemaNexo
{
    public static string Nombre =>
        Environment.GetEnvironmentVariable("Database__Esquema") is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException(
                "Database__Esquema no está configurado. Defina el esquema de NEXO en .env " +
                "(ver .env.example) antes de arrancar la app, correr `dotnet ef migrations " +
                "add`/`database update`, o los tests.");
}
