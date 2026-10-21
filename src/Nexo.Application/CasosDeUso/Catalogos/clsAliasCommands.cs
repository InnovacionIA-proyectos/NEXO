using System.Text.Json;
using Nexo.Application.CasosDeUso.Catalogos.DTOs;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.Catalogos;

/// <summary>Reglas compartidas por los casos de uso del diccionario de alias y de la bandeja de normalización.</summary>
internal static class clsAliasReglas
{
    public const int MaxAlias = 100;
    public const int MaxCodigo = 20;

    public static string ParsearTipo(string? tipo, string? porDefecto = null)
    {
        if (string.IsNullOrWhiteSpace(tipo) && porDefecto is not null) return porDefecto;
        if (!clsAliasElemento.TryParseTipo(tipo, out var t))
            throw new clsValidationFailedException("TIPO_ALIAS_INVALIDO", "El tipo debe ser 'vehiculo' o 'contenedor'.");
        return t;
    }

    /// <summary>
    /// El código oficial (con su mayúscula/minúscula canónica) si el diccionario lo acepta para ese tipo; null si no. Se aceptan los códigos
    /// que el diccionario ya usa y los del catálogo oficial: tblSAPTiposVehiculoTarifario para vehículos, las clases de contenedor de SAP
    /// para contenedores. Sin esto cualquier typo en el código crearía un alias que manda líneas a un tipo que no existe.
    /// </summary>
    public static async Task<string?> CodigoValidoAsync(
        string tipo, string codigo, IAliasCatalogoPort alias, ITiposVehiculoPort tiposVehiculo, CancellationToken ct)
    {
        var validos = new List<string>(await alias.ListarCodigosAsync(tipo, ct));
        if (tipo == clsAliasElemento.TipoVehiculo)
            validos.AddRange((await tiposVehiculo.ListarAsync(ct)).Select(t => t.Codigo.Trim()));
        else
            validos.AddRange(clsResolucionDeCatalogos.CodigosClaseContenedor);
        return validos.FirstOrDefault(v => v.Equals(codigo, StringComparison.OrdinalIgnoreCase));
    }

    public static clsAliasDto ADto(clsAliasElemento a) => new(a.Id, a.Alias, a.AliasNormalizado, a.TipoElemento, a.Codigo, a.Activo);
}

// ---------------------------------------------------------------- listar

/// <summary>S6-HU-27.2/#321: el diccionario completo (<c>tipo</c> = vehiculo | contenedor; sin tipo, los dos).</summary>
public sealed record clsListarAliasQuery(string? Tipo) : IQuery<IReadOnlyList<clsAliasDto>>;

public sealed class clsListarAliasQueryHandler(IAliasCatalogoPort alias) : IQueryHandler<clsListarAliasQuery, IReadOnlyList<clsAliasDto>>
{
    public async Task<IReadOnlyList<clsAliasDto>> Handle(clsListarAliasQuery query, CancellationToken ct = default)
    {
        var tipo = string.IsNullOrWhiteSpace(query.Tipo) ? null : clsAliasReglas.ParsearTipo(query.Tipo);
        return (await alias.ListarAsync(tipo, ct)).Select(clsAliasReglas.ADto).ToList();
    }
}

// ---------------------------------------------------------------- resolver

/// <summary>
/// S6-HU-27.2/#321: ¿a qué código corresponde este texto suelto? Usa el mismo normalizador y el mismo diccionario que la captura, así que
/// la respuesta de aquí es la que va a pasar al capturar. Solo cuentan los alias ACTIVOS del tipo pedido (vehiculo por defecto).
/// </summary>
public sealed record clsResolverAliasQuery(string? Tipo, string? Texto) : IQuery<clsResolverAliasDto>;

public sealed class clsResolverAliasQueryHandler(IAliasCatalogoPort alias) : IQueryHandler<clsResolverAliasQuery, clsResolverAliasDto>
{
    public async Task<clsResolverAliasDto> Handle(clsResolverAliasQuery query, CancellationToken ct = default)
    {
        var tipo = clsAliasReglas.ParsearTipo(query.Tipo, porDefecto: clsAliasElemento.TipoVehiculo);
        var texto = clsTexto.Limpiar(query.Texto)
            ?? throw new clsValidationFailedException("TEXTO_REQUERIDO", "Indica el texto a resolver.");

        var encontrado = await alias.BuscarPorNormalizadoAsync(clsResolucionDeCatalogos.Normalizar(texto), ct);
        if (encontrado is null || !encontrado.Activo || encontrado.TipoElemento != tipo)
            return new clsResolverAliasDto(texto, tipo, false, null, null);
        return new clsResolverAliasDto(texto, tipo, true, encontrado.Codigo, encontrado.Alias);
    }
}

// ---------------------------------------------------------------- crear

/// <summary>
/// S6-HU-27.2/#321: agrega un alias al diccionario (AdminTarifas/Admin). El código tiene que ser uno oficial del tipo (400
/// CODIGO_NO_RECONOCIDO) y el texto no puede existir ya, en ningún tipo (409 ALIAS_DUPLICADO: el normalizado es único en toda la tabla).
/// </summary>
public sealed record clsCrearAliasCommand(string? Tipo, string? Alias, string? Codigo) : ICommand<clsAliasDto>;

public sealed class clsCrearAliasCommandHandler(
    IAliasCatalogoPort alias,
    ITiposVehiculoPort tiposVehiculo,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<clsCrearAliasCommand, clsAliasDto>
{
    public async Task<clsAliasDto> Handle(clsCrearAliasCommand command, CancellationToken ct = default)
    {
        var tipo = clsAliasReglas.ParsearTipo(command.Tipo);

        var texto = clsTexto.Limpiar(command.Alias);
        var normalizado = clsResolucionDeCatalogos.Normalizar(texto);
        if (texto is null || normalizado.Length == 0)
            throw new clsValidationFailedException("ALIAS_REQUERIDO", "Indica el texto del alias.");
        if (texto.Length > clsAliasReglas.MaxAlias || normalizado.Length > clsAliasReglas.MaxAlias)
            throw new clsValidationFailedException("ALIAS_REQUERIDO", $"El alias admite máximo {clsAliasReglas.MaxAlias} caracteres.");

        var codigoTexto = clsTexto.Limpiar(command.Codigo);
        if (codigoTexto is null || codigoTexto.Length > clsAliasReglas.MaxCodigo)
            throw new clsValidationFailedException("CODIGO_NO_RECONOCIDO", "Indica el código oficial al que apunta el alias.");
        var codigo = await clsAliasReglas.CodigoValidoAsync(tipo, codigoTexto, alias, tiposVehiculo, ct)
            ?? throw new clsValidationFailedException("CODIGO_NO_RECONOCIDO",
                $"'{codigoTexto}' no es un código oficial de {tipo.ToLowerInvariant()}.");

        var existente = await alias.BuscarPorNormalizadoAsync(normalizado, ct);
        if (existente is not null)
            throw new clsConflictException("ALIAS_DUPLICADO",
                $"'{texto}' ya está en el diccionario ({existente.TipoElemento}, código {existente.Codigo}{(existente.Activo ? string.Empty : ", inactivo")}).");

        var nuevo = new clsAliasElemento { Alias = texto, AliasNormalizado = normalizado, TipoElemento = tipo, Codigo = codigo };
        alias.Agregar(nuevo);
        auditoria.Registrar(usuarioActual.Id, "alias_elementos", nuevo.Id, "crear_alias",
            JsonSerializer.Serialize(new { alias = texto, tipo, codigo }));
        await uow.SaveChangesAsync(ct);

        return clsAliasReglas.ADto(nuevo);
    }
}
