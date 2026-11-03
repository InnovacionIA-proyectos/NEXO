using System.Text.Json;
using Nexo.Application.CasosDeUso.DiccionarioCliente.DTOs;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.DiccionarioCliente;

/// <summary>Reglas compartidas por crear y editar un término del diccionario de clientes.</summary>
internal static class clsDiccionarioClienteReglas
{
    public const int MaxTermino = 200;
    public const int MaxConcepto = 100;
    public const int MaxEjemplo = 500;

    public static clsTerminoClienteDto ADto(clsTerminoCliente t) => new(
        t.Id, t.ClienteId, t.Cliente?.Nombre, t.Termino, t.Tipo, t.Concepto, t.Contexto, t.Ejemplo, t.Activo, t.CreatedAt, t.UpdatedAt);

    public static string ParsearTipo(string? tipo) =>
        clsTerminoCliente.Canonico(clsTerminoCliente.Tipos, tipo)
        ?? throw new clsValidationFailedException("TIPO_TERMINO_INVALIDO",
            $"El tipo debe ser uno de: {string.Join(", ", clsTerminoCliente.Tipos)}.");

    /// <summary>Los campos de un término ya validados y con su forma canónica; lanza 400 con el código del primer problema.</summary>
    public static (string Termino, string Normalizado, string Tipo, string Concepto, string? Contexto, string? Ejemplo) Validar(
        string? termino, string? tipo, string? concepto, string? contexto, string? ejemplo)
    {
        var texto = clsTexto.Limpiar(termino);
        var normalizado = clsResolucionDeCatalogos.Normalizar(texto);
        if (texto is null || normalizado.Length == 0)
            throw new clsValidationFailedException("TERMINO_REQUERIDO", "Indica el término tal como lo escribe el cliente.");
        if (texto.Length > MaxTermino || normalizado.Length > MaxTermino)
            throw new clsValidationFailedException("TERMINO_REQUERIDO", $"El término admite máximo {MaxTermino} caracteres.");

        var tipoCanonico = ParsearTipo(tipo);
        var conceptoLimpio = clsTexto.Limpiar(concepto);
        if (conceptoLimpio is null)
            throw new clsValidationFailedException("CONCEPTO_REQUERIDO", "Indica a qué concepto de NEXO corresponde el término.");
        if (conceptoLimpio.Length > MaxConcepto)
            throw new clsValidationFailedException("CONCEPTO_REQUERIDO", $"El concepto admite máximo {MaxConcepto} caracteres.");

        string? contextoCanonico = null;
        switch (tipoCanonico)
        {
            case clsTerminoCliente.TipoColumna:
                conceptoLimpio = clsTerminoCliente.Canonico(clsTerminoCliente.CamposDestino, conceptoLimpio)
                    ?? throw new clsValidationFailedException("CAMPO_DESTINO_INVALIDO",
                        $"'{conceptoLimpio}' no es un campo de NEXO. Valores válidos: {string.Join(", ", clsTerminoCliente.CamposDestino)}.");
                break;
            case clsTerminoCliente.TipoUnidad:
                conceptoLimpio = clsTerminoCliente.Canonico(clsTerminoCliente.Unidades, conceptoLimpio)
                    ?? throw new clsValidationFailedException("UNIDAD_INVALIDA",
                        $"'{conceptoLimpio}' no es una unidad canónica. Valores válidos: {string.Join(", ", clsTerminoCliente.Unidades)}.");
                break;
            default:
                // El valor de un cliente puede no estar en el catálogo oficial (el Turbo de un cliente es 2_7): no se valida contra él.
                contextoCanonico = clsTerminoCliente.Canonico(clsTerminoCliente.Contextos, contexto)
                    ?? throw new clsValidationFailedException("CONTEXTO_INVALIDO",
                        $"Un término de tipo valor necesita el campo al que pertenece. Valores válidos: {string.Join(", ", clsTerminoCliente.Contextos)}.");
                break;
        }
        if (tipoCanonico != clsTerminoCliente.TipoValor && !string.IsNullOrWhiteSpace(contexto))
            throw new clsValidationFailedException("CONTEXTO_NO_APLICA", "El contexto solo se usa en los términos de tipo valor.");

        var ejemploLimpio = clsTexto.Limpiar(ejemplo);
        if (ejemploLimpio is { Length: > MaxEjemplo })
            throw new clsValidationFailedException("EJEMPLO_MUY_LARGO", $"El ejemplo admite máximo {MaxEjemplo} caracteres.");

        return (texto, normalizado, tipoCanonico, conceptoLimpio, contextoCanonico, ejemploLimpio);
    }

    public static clsConflictException Duplicado(clsTerminoCliente existente) => new("TERMINO_DUPLICADO",
        $"'{existente.Termino}' ya está en el diccionario de {(existente.ClienteId is null ? "todos los clientes" : "este cliente")} " +
        $"({existente.Tipo}, concepto {existente.Concepto}{(existente.Activo ? string.Empty : ", inactivo")}).");

    public static string DiffJson(clsTerminoCliente t) =>
        JsonSerializer.Serialize(new { clienteId = t.ClienteId, termino = t.Termino, tipo = t.Tipo, concepto = t.Concepto, contexto = t.Contexto, activo = t.Activo });
}

// ---------------------------------------------------------------- listar

/// <summary>
/// Los términos del diccionario. Con <paramref name="ClienteId"/> trae los de ese cliente y, por defecto, también los generales; sin él,
/// los de todos. <paramref name="Tipo"/> = columna | unidad | valor.
/// </summary>
public sealed record clsListarDiccionarioClienteQuery(Guid? ClienteId, bool IncluirGenerales, string? Tipo, string? Buscar, bool SoloActivos)
    : IQuery<IReadOnlyList<clsTerminoClienteDto>>;

public sealed class clsListarDiccionarioClienteQueryHandler(IDiccionarioClienteRepository repositorio)
    : IQueryHandler<clsListarDiccionarioClienteQuery, IReadOnlyList<clsTerminoClienteDto>>
{
    public async Task<IReadOnlyList<clsTerminoClienteDto>> Handle(clsListarDiccionarioClienteQuery query, CancellationToken ct = default)
    {
        var tipo = string.IsNullOrWhiteSpace(query.Tipo) ? null : clsDiccionarioClienteReglas.ParsearTipo(query.Tipo);
        var filas = await repositorio.ListarAsync(query.ClienteId, query.IncluirGenerales, tipo, clsTexto.Limpiar(query.Buscar), query.SoloActivos, ct);
        return filas.Select(clsDiccionarioClienteReglas.ADto).ToList();
    }
}

// ---------------------------------------------------------------- opciones

public sealed record clsObtenerOpcionesDiccionarioClienteQuery : IQuery<clsOpcionesDiccionarioClienteDto>;

public sealed class clsObtenerOpcionesDiccionarioClienteQueryHandler
    : IQueryHandler<clsObtenerOpcionesDiccionarioClienteQuery, clsOpcionesDiccionarioClienteDto>
{
    public Task<clsOpcionesDiccionarioClienteDto> Handle(clsObtenerOpcionesDiccionarioClienteQuery query, CancellationToken ct = default) =>
        Task.FromResult(new clsOpcionesDiccionarioClienteDto(
            clsTerminoCliente.Tipos, clsTerminoCliente.CamposDestino, clsTerminoCliente.Unidades, clsTerminoCliente.Contextos));
}

// ---------------------------------------------------------------- crear

/// <summary>
/// Agrega un término. 400 si el tipo, el concepto o el contexto no son válidos; 400 CLIENTE_NO_ENCONTRADO si el cliente no existe; 409
/// TERMINO_DUPLICADO si el cliente ya tiene ese término (normalizado) con el mismo tipo y contexto.
/// </summary>
public sealed record clsCrearTerminoClienteCommand(
    Guid? ClienteId, string? Termino, string? Tipo, string? Concepto, string? Contexto, string? Ejemplo, bool Activo)
    : ICommand<clsTerminoClienteDto>;

public sealed class clsCrearTerminoClienteCommandHandler(
    IDiccionarioClienteRepository repositorio, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsCrearTerminoClienteCommand, clsTerminoClienteDto>
{
    public async Task<clsTerminoClienteDto> Handle(clsCrearTerminoClienteCommand command, CancellationToken ct = default)
    {
        var (termino, normalizado, tipo, concepto, contexto, ejemplo) =
            clsDiccionarioClienteReglas.Validar(command.Termino, command.Tipo, command.Concepto, command.Contexto, command.Ejemplo);

        if (command.ClienteId is { } clienteId && !await repositorio.ClienteExisteAsync(clienteId, ct))
            throw new clsValidationFailedException("CLIENTE_NO_ENCONTRADO", "El cliente no existe.");

        var existente = await repositorio.BuscarDuplicadoAsync(command.ClienteId, normalizado, tipo, contexto, excluirId: null, ct);
        if (existente is not null) throw clsDiccionarioClienteReglas.Duplicado(existente);

        var nuevo = new clsTerminoCliente
        {
            ClienteId = command.ClienteId, Termino = termino, TerminoNormalizado = normalizado, Tipo = tipo,
            Concepto = concepto, Contexto = contexto, Ejemplo = ejemplo, Activo = command.Activo,
        };
        repositorio.Agregar(nuevo);
        auditoria.Registrar(usuarioActual.Id, "diccionario_cliente", nuevo.Id, "crear_termino", clsDiccionarioClienteReglas.DiffJson(nuevo));
        await uow.SaveChangesAsync(ct);

        // Con el nombre del cliente ya resuelto para la respuesta.
        var guardado = await repositorio.ObtenerAsync(nuevo.Id, ct) ?? nuevo;
        return clsDiccionarioClienteReglas.ADto(guardado);
    }
}

// ---------------------------------------------------------------- editar

/// <summary>
/// Reemplaza los campos editables de un término (el cliente no cambia: para pasarlo a otro se borra y se crea). 404 si no existe; las
/// mismas validaciones que al crear; 409 TERMINO_DUPLICADO si el cambio choca con otro término del mismo cliente.
/// </summary>
public sealed record clsEditarTerminoClienteCommand(
    Guid Id, string? Termino, string? Tipo, string? Concepto, string? Contexto, string? Ejemplo, bool Activo)
    : ICommand<clsTerminoClienteDto>;

public sealed class clsEditarTerminoClienteCommandHandler(
    IDiccionarioClienteRepository repositorio, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow, TimeProvider reloj)
    : ICommandHandler<clsEditarTerminoClienteCommand, clsTerminoClienteDto>
{
    public async Task<clsTerminoClienteDto> Handle(clsEditarTerminoClienteCommand command, CancellationToken ct = default)
    {
        var actual = await repositorio.ObtenerAsync(command.Id, ct)
            ?? throw new clsNotFoundException("TERMINO_NO_ENCONTRADO", "El término no existe.");

        var (termino, normalizado, tipo, concepto, contexto, ejemplo) =
            clsDiccionarioClienteReglas.Validar(command.Termino, command.Tipo, command.Concepto, command.Contexto, command.Ejemplo);

        var existente = await repositorio.BuscarDuplicadoAsync(actual.ClienteId, normalizado, tipo, contexto, excluirId: actual.Id, ct);
        if (existente is not null) throw clsDiccionarioClienteReglas.Duplicado(existente);

        var antes = clsDiccionarioClienteReglas.DiffJson(actual);
        actual.Termino = termino;
        actual.TerminoNormalizado = normalizado;
        actual.Tipo = tipo;
        actual.Concepto = concepto;
        actual.Contexto = contexto;
        actual.Ejemplo = ejemplo;
        actual.Activo = command.Activo;
        actual.UpdatedAt = reloj.GetUtcNow();

        auditoria.Registrar(usuarioActual.Id, "diccionario_cliente", actual.Id, "editar_termino",
            JsonSerializer.Serialize(new { antes = JsonSerializer.Deserialize<JsonElement>(antes), despues = JsonSerializer.Deserialize<JsonElement>(clsDiccionarioClienteReglas.DiffJson(actual)) }));
        await uow.SaveChangesAsync(ct);

        return clsDiccionarioClienteReglas.ADto(actual);
    }
}

// ---------------------------------------------------------------- eliminar

/// <summary>Borra un término. 404 si no existe.</summary>
public sealed record clsEliminarTerminoClienteCommand(Guid Id) : ICommand<Unit>;

public sealed class clsEliminarTerminoClienteCommandHandler(
    IDiccionarioClienteRepository repositorio, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsEliminarTerminoClienteCommand, Unit>
{
    public async Task<Unit> Handle(clsEliminarTerminoClienteCommand command, CancellationToken ct = default)
    {
        var termino = await repositorio.ObtenerAsync(command.Id, ct)
            ?? throw new clsNotFoundException("TERMINO_NO_ENCONTRADO", "El término no existe.");

        repositorio.Eliminar(termino);
        auditoria.Registrar(usuarioActual.Id, "diccionario_cliente", termino.Id, "eliminar_termino", clsDiccionarioClienteReglas.DiffJson(termino));
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
