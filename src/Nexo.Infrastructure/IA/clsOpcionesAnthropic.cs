using Nexo.Application.Common.Ports;

namespace Nexo.Infrastructure.IA;

/// <summary>Sección <c>Anthropic</c>. La llave solo llega por entorno (Anthropic__ApiKey): nunca va en el repositorio ni en logs o mensajes.
/// Las funciones nacen apagadas. No se configuran temperature, top_p ni top_k porque la API los rechaza.</summary>
public sealed class clsOpcionesAnthropic
{
    public const string Seccion = "Anthropic";

    public const string ModeloPorDefecto = "claude-sonnet-5-5";
    // `medium` se eligió por costo: acierto equivalente a `high` en la evaluación del mapeo de Excel y menor gasto.
    // OJO: el esfuerzo es GLOBAL (lo comparten el mapeo y el asistente de cotización); si hace falta uno por función, hay que partir esta opción.
    public const string EsfuerzoPorDefecto = "medium";

    public string? ApiKey { get; set; }

    /// <summary>Interruptor general: con false todas las funciones de IA quedan apagadas aunque haya llave y funciones encendidas.</summary>
    public bool Habilitada { get; set; } = true;

    /// <summary>Tope de tokens (entrada, salida y caché) por persona y día (hora de Colombia). 0 lo desactiva.</summary>
    public long LimiteTokensPorUsuarioDia { get; set; } = 500_000;

    /// <summary>Identificador de la workspace (wrkspc_…). Una llave de toda la organización lo necesita o el proveedor rechaza la petición;
    /// no es un secreto, pero acota el gasto (el límite mensual es por workspace).</summary>
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
