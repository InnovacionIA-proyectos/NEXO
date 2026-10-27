using System.Text.RegularExpressions;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Asistente.Entidades;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

public sealed record clsPreferenciaAsistenteDto(Guid Id, string Texto, DateTimeOffset CreatedAt)
{
    public static clsPreferenciaAsistenteDto De(clsPreferenciaAsistente p) => new(p.Id, p.Texto, p.CreatedAt);
}

/// <summary>Persistencia de las preferencias de cada persona para el asistente.</summary>
public interface IPreferenciasAsistenteRepository
{
    /// <summary>Las preferencias de una persona, de la más antigua a la más reciente.</summary>
    Task<IReadOnlyList<clsPreferenciaAsistente>> ListarAsync(Guid usuarioId, CancellationToken ct = default);

    Task<clsPreferenciaAsistente> CrearAsync(clsPreferenciaAsistente preferencia, CancellationToken ct = default);

    /// <summary>Borra la preferencia si es de esa persona. False si no existe o es de otra.</summary>
    Task<bool> EliminarAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
}

/// <summary>
/// Qué se acepta como preferencia. Es una segunda barrera: lo que de verdad impide que una preferencia haga proponer un valor bajo el
/// piso o un precio sin fuente es la estructura (todo precio sale de una consulta, el piso lo aplica el dominio y todo cambio se
/// confirma), no este filtro. Aquí se rechaza lo que fija un valor de dinero o intenta apagar una regla.
/// </summary>
public static partial class clsValidadorDePreferencias
{
    public const int MaxCaracteres = 300;

    [GeneratedRegex(
        @"\b(ignor\w*|salt\w+|omit\w*|olvid\w*|desactiv\w*|apag\w+|sin\s+(el\s+)?piso|bajo\s+(el\s+)?piso|debajo\s+(del|de\s+el)\s+piso|aunque\s+(sea|est[eé])|inventa\w*|estim\w*\s+(un\s+)?(valor|precio|tarifa)|sin\s+consultar|no\s+(consultes|uses|llames)|reglas?\s+del\s+sistema|instruccion\w*)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Prohibido();

    /// <summary>El texto limpio de la preferencia, o un error de validación con el motivo.</summary>
    public static string Validar(string? texto)
    {
        var limpio = (texto ?? string.Empty).ReplaceLineEndings(" ").Trim();
        limpio = Regex.Replace(limpio, @"\s{2,}", " ");

        if (limpio.Length < 5)
            throw new clsValidationFailedException("PREFERENCIA_REQUERIDA", "Escribe la regla o preferencia con un poco más de detalle.");
        if (limpio.Length > MaxCaracteres)
            throw new clsValidationFailedException("PREFERENCIA_DEMASIADO_LARGA", $"Una preferencia admite hasta {MaxCaracteres} caracteres.");
        if (clsVerificadorDeCifras.CifrasSinOrigen(limpio, []).Count > 0)
            throw new clsValidationFailedException("PREFERENCIA_CON_VALOR",
                "Una preferencia no puede fijar valores de dinero: los precios salen siempre de una consulta con su fuente.");
        if (Prohibido().IsMatch(limpio))
            throw new clsValidationFailedException("PREFERENCIA_NO_PERMITIDA",
                "Una preferencia no puede cambiar ni saltar las reglas del sistema (el piso SICE-TAC, que todo precio venga de una consulta, la confirmación de los cambios).");
        return limpio;
    }
}

/// <summary>Guarda una preferencia de quien conversa. Es lo que se ejecuta cuando la persona confirma la propuesta del asistente.</summary>
public sealed record clsGuardarPreferenciaAsistenteCommand(string? Texto, Guid? ConversacionId) : ICommand<clsPreferenciaAsistenteDto>;

public sealed class clsGuardarPreferenciaAsistenteCommandHandler(
    IPreferenciasAsistenteRepository preferencias, ICurrentUserPort usuario, clsLimitesAsistente limites)
    : ICommandHandler<clsGuardarPreferenciaAsistenteCommand, clsPreferenciaAsistenteDto>
{
    public async Task<clsPreferenciaAsistenteDto> Handle(clsGuardarPreferenciaAsistenteCommand command, CancellationToken ct = default)
    {
        var texto = clsValidadorDePreferencias.Validar(command.Texto);
        var actuales = await preferencias.ListarAsync(usuario.Id, ct);

        if (actuales.Count >= limites.MaxPreferenciasEfectivo)
            throw new clsConflictException("PREFERENCIAS_LLENAS",
                $"Ya tienes {limites.MaxPreferenciasEfectivo} preferencias guardadas. Borra alguna para agregar otra.");
        if (actuales.Any(p => string.Equals(p.Texto, texto, StringComparison.OrdinalIgnoreCase)))
            throw new clsConflictException("PREFERENCIA_REPETIDA", "Esa preferencia ya está guardada.");

        return clsPreferenciaAsistenteDto.De(await preferencias.CrearAsync(
            new clsPreferenciaAsistente { UsuarioId = usuario.Id, Texto = texto, ConversacionId = command.ConversacionId }, ct));
    }
}

/// <summary>Las preferencias de quien consulta.</summary>
public sealed record clsListarPreferenciasAsistenteQuery : IQuery<IReadOnlyList<clsPreferenciaAsistenteDto>>;

public sealed class clsListarPreferenciasAsistenteQueryHandler(IPreferenciasAsistenteRepository preferencias, ICurrentUserPort usuario)
    : IQueryHandler<clsListarPreferenciasAsistenteQuery, IReadOnlyList<clsPreferenciaAsistenteDto>>
{
    public async Task<IReadOnlyList<clsPreferenciaAsistenteDto>> Handle(clsListarPreferenciasAsistenteQuery query, CancellationToken ct = default) =>
        (await preferencias.ListarAsync(usuario.Id, ct)).Select(clsPreferenciaAsistenteDto.De).ToList();
}

/// <summary>Borra una preferencia propia. Una ajena o inexistente responde 404: no se revela que existe.</summary>
public sealed record clsEliminarPreferenciaAsistenteCommand(Guid Id) : ICommand<Unit>;

public sealed class clsEliminarPreferenciaAsistenteCommandHandler(IPreferenciasAsistenteRepository preferencias, ICurrentUserPort usuario)
    : ICommandHandler<clsEliminarPreferenciaAsistenteCommand, Unit>
{
    public async Task<Unit> Handle(clsEliminarPreferenciaAsistenteCommand command, CancellationToken ct = default)
    {
        if (!await preferencias.EliminarAsync(command.Id, usuario.Id, ct))
            throw new clsNotFoundException("PREFERENCIA_NO_ENCONTRADA", "La preferencia no existe.");
        return Unit.Value;
    }
}
