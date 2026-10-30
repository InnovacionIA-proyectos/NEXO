namespace Nexo.Infrastructure.Persistence;

/// <summary>
/// Nombre del esquema propio de NEXO dentro de la base de datos que apunte
/// ConnectionStrings:Nexo / TEST_DB_CONNECTION. Se lee desde la variable de entorno
/// <c>Database__Esquema</c> (mismo criterio que <see cref="Api.Common.clsNexoDbContextFactory"/>
/// usa para la cadena de conexión: lectura directa de <see cref="Environment"/>, sin
/// pasar por IConfiguration, para que funcione igual en runtime, en las herramientas
/// de diseño de EF y en los tests — ninguno de esos tres arranca necesariamente con
/// el host/DI completo).
///
/// <see cref="clsNexoDbContext"/> (<c>OnModelCreating</c>) y todas las migraciones ya
/// generadas (Migrations/*.cs) referencian esta propiedad en vez de repetir un
/// literal, así que apuntar la app a una base nueva con otro nombre de esquema no
/// implica tocar código, solo el .env. Fail-closed a propósito (sin default
/// hardcodeado, mismo criterio que IdentidadPortal/RabbitMq/CORS en clsProgram.cs):
/// si falta la variable, el arranque -o `dotnet ef migrations add`/`database
/// update`- debe fallar con un mensaje claro, no adivinar un esquema.
///
/// OJO: cambiar este valor mientras se sigue apuntando a una base de datos que YA
/// tiene el esquema anterior no migra nada de un esquema a otro — EF va a ver el
/// esquema nuevo vacío y, si Database:MigrateOnStartup está encendido, va a crear las
/// tablas ahí desde cero, dejando huérfano (intacto, pero fuera de uso) el esquema
/// viejo en esa misma base. Pensado para apuntar a una base NUEVA, no para renombrar
/// una existente.
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
