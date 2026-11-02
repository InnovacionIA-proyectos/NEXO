using Nexo.Application.Common.Ports;

namespace Nexo.Infrastructure.IA;

/// <summary>
/// Configuración de la sección <c>Anthropic</c> (variables de entorno <c>Anthropic__ApiKey</c>, <c>Anthropic__Model</c>…).
///
/// La llave solo llega por entorno (Dokploy o GitHub Secrets); nunca va en el repositorio ni se escribe en logs ni mensajes.
/// Todas las funciones nacen apagadas: una función solo se usa cuando <c>Anthropic:Funciones:&lt;nombre&gt;</c> es true.
///
/// El modelo y el esfuerzo son configuración y no código: cambiarlos es cambiar una variable. No se configuran
/// <c>temperature</c>, <c>top_p</c> ni <c>top_k</c> porque la API los rechaza.
/// </summary>
public sealed class clsOpcionesAnthropic
{
    public const string Seccion = "Anthropic";

    public const string ModeloPorDefecto = "claude-sonnet-5-5";
    // `medium` sale de la evaluacion del mapeo de Excel (#353/#363): 93,8 % de acierto contra 91,7 % de `high`, 0 falsos positivos
    // en las dos y US$ 0,0196 por Excel contra US$ 0,0217. `high` sirve igual -la diferencia de acierto esta dentro del ruido del
    // dataset, donde una columna mueve unos 6 puntos-, asi que esto se elige por costo, no porque `high` este mal.
    //
    // OJO: el esfuerzo es GLOBAL, lo comparten el mapeo y el asistente de cotizacion, y al asistente nadie lo ha medido en `medium`.
    // Si llega a hacer falta un esfuerzo distinto por funcion, hay que partir esta opcion; hoy no lo es.
    public const string EsfuerzoPorDefecto = "medium";

    public string? ApiKey { get; set; }

    /// <summary>Interruptor general: con false todas las funciones de IA quedan apagadas aunque haya llave y funciones encendidas.</summary>
    public bool Habilitada { get; set; } = true;

    /// <summary>
    /// Tope de tokens (entrada, salida y caché) que una persona puede consumir por día (hora de Colombia). 0 lo desactiva.
    /// </summary>
    public long LimiteTokensPorUsuarioDia { get; set; } = 500_000;

    /// <summary>
    /// Identificador de la workspace (<c>wrkspc_…</c>). Una llave atada a una workspace no lo necesita; una llave de toda la
    /// organización sí, o el proveedor rechaza cada petición. No es un secreto, pero sí es lo que acota el gasto: el límite
    /// mensual se fija por workspace.
    /// </summary>
    public string? WorkspaceId { get; set; }
    public string Model { get; set; } = ModeloPorDefecto;

    /// <summary>low, medium, high, xhigh o max. Cualquier otro valor cae a high, el esfuerzo por defecto.</summary>
    public string Effort { get; set; } = EsfuerzoPorDefecto;

    /// <summary>Con true el modelo decide cuánto razonar según el esfuerzo; con false no razona.</summary>
    public bool RazonamientoAdaptativo { get; set; } = true;

    public int TimeoutSegundos { get; set; } = 60;
    public int MaxReintentos { get; set; } = 2;
    public int MaxTokensSalida { get; set; } = 4096;

    /// <summary>Interruptor por función, por el nombre de <see cref="clsFuncionIa"/> (sin distinguir mayúsculas).</summary>
    public Dictionary<string, bool> Funciones { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Configurada => !string.IsNullOrWhiteSpace(ApiKey);

    public long LimiteTokensPorUsuarioDiaEfectivo => Math.Max(0, LimiteTokensPorUsuarioDia);

    public bool FuncionEncendida(clsFuncionIa funcion) =>
        Funciones.Any(f => string.Equals(f.Key, funcion.ToString(), StringComparison.OrdinalIgnoreCase) && f.Value);

    public TimeSpan TimeoutEfectivo => TimeSpan.FromSeconds(Math.Clamp(TimeoutSegundos, 5, 300));
    public int ReintentosEfectivos => Math.Clamp(MaxReintentos, 0, 5);
    public int MaxTokensEfectivo => Math.Clamp(MaxTokensSalida, 256, 32_000);
    public string? WorkspaceIdEfectivo => string.IsNullOrWhiteSpace(WorkspaceId) ? null : WorkspaceId.Trim();
    public string ModeloEfectivo => string.IsNullOrWhiteSpace(Model) ? ModeloPorDefecto : Model.Trim();
}
