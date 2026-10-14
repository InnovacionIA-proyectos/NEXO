namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Marca una prueba que habla con el Cubicador REAL (docker compose, cubicador/docker-compose.yml,
/// puerto 8081): confirma que el contrato que NEXO habla hoy es el que el Cubicador entiende.
///
/// SE SALTA SI EL ENTORNO NO TRAE EL CUBICADOR — <c>Cubicador3D__OutboundBaseUrl</c> y la llave que
/// esa prueba usa (<paramref name="variableLlave"/>). Sin ellas la aplicación responde 503 por
/// diseño ("no configurado") y la prueba fallaría por algo que no es un defecto del código. Es el
/// caso del runner de GitHub Actions, que no tiene el stack del Cubicador levantado: S5-HU-18 las
/// metió en la suite y el CI quedó en rojo con 6 × <c>ServiceUnavailable</c> (corrida del 2026-10-14).
///
/// Mismo criterio que <c>clsHechoConCorreoRealAttribute</c>: el salto es explícito y dice cómo
/// correrla, no un fallo mudo. Con las variables definidas (como en <c>.env</c> local, o con los
/// secretos <c>CUBICADOR3D_*</c> en el workflow) corre normal, y si el Cubicador no responde FALLA:
/// ahí sí es una señal real.
/// </summary>
public sealed class clsHechoConCubicadorRealAttribute : FactAttribute
{
    public const string VarUrl = "Cubicador3D__OutboundBaseUrl";

    public clsHechoConCubicadorRealAttribute(string variableLlave)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VarUrl)) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableLlave)))
            Skip =
                $"Habla con el Cubicador REAL. Para correrla: {VarUrl}=<url del Cubicador> y " +
                $"{variableLlave}=<llave> (ver .env.example) y el stack de cubicador/docker-compose.yml arriba.";
    }
}
