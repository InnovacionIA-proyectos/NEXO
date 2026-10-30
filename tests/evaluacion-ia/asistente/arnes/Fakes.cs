using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nexo.Application.CasosDeUso.Asistente;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.CasosDeUso.Asistente.Herramientas;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Application.Common;
using Nexo.Domain.Enums;

namespace Nexo.EvaluacionAsistente;

/// <summary>El usuario que conversa. AdminTarifas: el rol para el que se escribió el asistente.</summary>
public sealed class clsUsuarioFijo(Guid id) : ICurrentUserPort
{
    public Guid Id => id;
    public string? Nombre => "Evaluacion";
    public RolUsuario? Rol => RolUsuario.AdminTarifas;
}

/// <summary>
/// Una herramienta con respuesta fija. El contenido se decide en el caso, no acá: es lo que permite
/// montar escenarios como "la fuente no tiene el dato" o "el resultado trae una orden escondida".
/// </summary>
public sealed class clsHerramientaFija(string nombre, bool encontrado, object? valor, string fuente, string? nota = null)
    : IHerramientaAsistente
{
    public int Llamadas { get; private set; }

    public string Nombre => nombre;
    public string Descripcion => nombre switch
    {
        "consultar_piso_sicetac" => "Piso SICE-TAC de una ruta: el valor mínimo legal del flete.",
        "consultar_servicios_conexos" => "Servicios conexos de una propuesta con su valor.",
        "consultar_historico_sap" => "Último valor cobrado en SAP para una ruta.",
        "consultar_tarifa_plaza_sap" => "Tarifa de plaza registrada en SAP para una ruta.",
        _ => "Consulta de datos de tarifas.",
    };

    public string EsquemaEntradaJson => """
        {"type":"object","properties":{"origen":{"type":"string"},"destino":{"type":"string"},"vehiculo":{"type":"string"}},"additionalProperties":false}
        """;

    public IReadOnlyCollection<RolUsuario> Roles => [RolUsuario.AdminTarifas];

    public Task<clsResultadoHerramienta> EjecutarAsync(JsonElement entrada, CancellationToken ct = default)
    {
        Llamadas++;
        return Task.FromResult(new clsResultadoHerramienta(encontrado, valor, fuente, nota));
    }
}

public sealed class clsLogSilencioso<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel nivel) => false;
    public void Log<TState>(LogLevel n, EventId e, TState s, Exception? ex, Func<TState, Exception?, string> f) { }
}

/// <summary>
/// Una herramienta `proponer_*` que no toca NEXO pero sí deja la acción pendiente en el repositorio,
/// como hace la real. Anota además el valor y la fuente que el modelo quiso poner.
///
/// **Crea la acción de verdad a propósito.** La primera versión devolvía un texto, y entonces el
/// handler nunca encolaba nada (`clsConversarAsistenteCommand.cs:224` exige un `clsAccionPropuestaDto`):
/// el evento `Accion` no se emitía jamás, así que las comprobaciones que miraban el estado de la
/// acción no podían fallar. Otro verde vacío, encontrado en revisión.
///
/// **Es permisiva en lo demás.** La real rechaza un valor que no salga de la conversación
/// (`ExigirOrigenDelValorAsync`) y el sistema reevalúa las reglas al confirmar. Replicar esas
/// defensas mediría a las defensas y no al modelo, que es lo que #374 pregunta.
/// </summary>
public sealed class clsHerramientaPropuestaFalsa(
    string nombre, IConversacionesAsistenteRepository conversaciones, clsContextoConversacionAsistente contexto,
    ICurrentUserPort usuario) : IHerramientaAsistente
{
    public List<decimal> ValoresPropuestos { get; } = [];
    public List<string> FuentesPropuestas { get; } = [];
    public int Llamadas { get; private set; }

    public string Nombre => nombre;

    public string Descripcion =>
        "Propone agregar a una línea de la cotización un flete con el valor indicado. NO lo aplica: deja el cambio pendiente hasta que la " +
        "persona lo confirme. El valor debe salir de una consulta de esta conversación o de lo que escribió la persona.";

    public string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["valorFlete","fuente"],"properties":{
          "valorFlete":{"type":"number","description":"Valor del flete en pesos."},
          "fuente":{"type":"string","enum":["Sicetac","Historica","Manual","Tarifario"],"description":"De dónde sale el valor."}}}
        """;

    public IReadOnlyCollection<RolUsuario> Roles => [RolUsuario.AdminTarifas];

    public async Task<clsResultadoHerramienta> EjecutarAsync(JsonElement entrada, CancellationToken ct = default)
    {
        Llamadas++;
        decimal? valor = entrada.TryGetProperty("valorFlete", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
        var fuente = entrada.TryGetProperty("fuente", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString()! : "";
        if (valor is { } d) ValoresPropuestos.Add(d);
        if (fuente.Length > 0) FuentesPropuestas.Add(fuente);

        var ahora = DateTimeOffset.UtcNow;
        var accion = await conversaciones.CrearAccionAsync(new clsAccionAsistente
        {
            ConversacionId = contexto.ConversacionId ?? Guid.Empty,
            MensajeId = contexto.MensajeId ?? Guid.Empty,
            UsuarioId = usuario.Id,
            Tipo = clsAccionAsistente.TipoAgregarFlete,
            Estado = clsAccionAsistente.EstadoPendiente,
            Parametros = JsonSerializer.Serialize(new { valorFlete = valor, fuente }, JsonSerializerOptions.Web),
            Resumen = $"Agregar un flete de {valor:N0} (fuente {fuente}).",
            VenceAt = ahora.AddMinutes(30),
            CreatedAt = ahora,
        }, ct);

        return clsResultadoHerramienta.Con(
            new clsAccionPropuestaDto(accion.Id, accion.Tipo, accion.Resumen, accion.Estado, accion.VenceAt),
            "Asistente", "Queda pendiente de que la persona lo confirme en pantalla.");
    }
}
