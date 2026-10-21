using System.Text.Json;
using Nexo.Application.CasosDeUso.Catalogos.DTOs;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.Catalogos;

// S6-HU-27.3/#322 — bandeja de normalización de vehículos: las configuraciones que la captura creó en cuarentena porque nadie sabía a qué
// código oficial correspondían (EsCatalogoOficial = false). Un humano las MAPEA a un código (queda un alias, así que el mismo texto no
// vuelve a caer en cuarentena) o las DESCARTA. Las dos salen de la lista; ninguna borra la fila, porque las líneas que ya la usan siguen
// apuntándole.

// ---------------------------------------------------------------- listar

public sealed record clsListarVehiculosCuarentenaQuery : IQuery<IReadOnlyList<clsVehiculoCuarentenaDto>>;

/// <summary>
/// Lista las configuraciones con <c>EsCatalogoOficial = false</c> que nadie descartó y cuyo tipo de vehículo AÚN no tiene alias activo: un
/// tipo ya mapeado (aquí o agregando el alias a mano) sale de la lista aunque su fila siga en cuarentena. Por cada fila: id, tipo,
/// carrocería (y rango de peso/fecha, por si ayudan a decidir).
/// </summary>
public sealed class clsListarVehiculosCuarentenaQueryHandler(IConfiguracionesVehiculoPort vehiculos, IAliasCatalogoPort alias)
    : IQueryHandler<clsListarVehiculosCuarentenaQuery, IReadOnlyList<clsVehiculoCuarentenaDto>>
{
    public async Task<IReadOnlyList<clsVehiculoCuarentenaDto>> Handle(clsListarVehiculosCuarentenaQuery query, CancellationToken ct = default)
    {
        var filas = await vehiculos.ListarEnCuarentenaAsync(ct);
        var mapa = await alias.MapaActivoAsync(clsAliasElemento.TipoVehiculo, ct);
        return filas
            .Where(v => !mapa.ContainsKey(clsResolucionDeCatalogos.Normalizar(v.TipoVehiculo)))
            .Select(v => new clsVehiculoCuarentenaDto(v.Id, v.TipoVehiculo, v.Carroceria, v.RangoPeso, v.CreatedAt))
            .ToList();
    }
}

// ---------------------------------------------------------------- mapear

/// <summary>
/// Mapea una configuración en cuarentena a un código oficial: crea el alias (texto del tipo de vehículo → código) en el diccionario. 404
/// VEHICULO_NO_ENCONTRADO; 409 VEHICULO_NO_ESTA_EN_CUARENTENA (ya es oficial) o VEHICULO_YA_DESCARTADO; 400 CODIGO_NO_RECONOCIDO; 409
/// ALIAS_DUPLICADO si el texto ya apunta a OTRO código (si ya apuntaba al mismo, no hay nada que crear y el resultado lo dice con
/// <c>AliasCreado = false</c>). Las líneas que ya usan la fila no se tocan: se cambian con "cambiar vehículo de la línea".
/// </summary>
public sealed record clsMapearVehiculoCuarentenaCommand(Guid VehiculoId, string? Codigo) : ICommand<clsMapeoVehiculoDto>;

public sealed class clsMapearVehiculoCuarentenaCommandHandler(
    IConfiguracionesVehiculoPort vehiculos,
    IAliasCatalogoPort alias,
    ITiposVehiculoPort tiposVehiculo,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<clsMapearVehiculoCuarentenaCommand, clsMapeoVehiculoDto>
{
    public async Task<clsMapeoVehiculoDto> Handle(clsMapearVehiculoCuarentenaCommand command, CancellationToken ct = default)
    {
        var v = await vehiculos.ObtenerAsync(command.VehiculoId, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "La configuración de vehículo no existe.");
        if (v.EsCatalogoOficial)
            throw new clsConflictException("VEHICULO_NO_ESTA_EN_CUARENTENA", "Esa configuración ya es oficial: no está en la bandeja.");
        if (v.Descartada)
            throw new clsConflictException("VEHICULO_YA_DESCARTADO", "Esa configuración ya fue descartada.");

        var codigoTexto = clsTexto.Limpiar(command.Codigo);
        if (codigoTexto is null || codigoTexto.Length > clsAliasReglas.MaxCodigo)
            throw new clsValidationFailedException("CODIGO_NO_RECONOCIDO", "Indica el código oficial al que se mapea.");
        var codigo = await clsAliasReglas.CodigoValidoAsync(clsAliasElemento.TipoVehiculo, codigoTexto, alias, tiposVehiculo, ct)
            ?? throw new clsValidationFailedException("CODIGO_NO_RECONOCIDO", $"'{codigoTexto}' no es un código oficial de vehículo.");

        var texto = clsTexto.Limpiar(v.TipoVehiculo)!;
        var normalizado = clsResolucionDeCatalogos.Normalizar(texto);
        var existente = await alias.BuscarPorNormalizadoAsync(normalizado, ct);

        var creado = false;
        if (existente is null)
        {
            if (texto.Length > clsAliasReglas.MaxAlias || normalizado.Length > clsAliasReglas.MaxAlias)
                throw new clsValidationFailedException("ALIAS_REQUERIDO", $"El tipo de vehículo supera los {clsAliasReglas.MaxAlias} caracteres que admite un alias.");
            alias.Agregar(new clsAliasElemento
            {
                Alias = texto, AliasNormalizado = normalizado, TipoElemento = clsAliasElemento.TipoVehiculo, Codigo = codigo,
            });
            creado = true;
        }
        else if (existente.TipoElemento != clsAliasElemento.TipoVehiculo
                 || !existente.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)
                 || !existente.Activo)
        {
            throw new clsConflictException("ALIAS_DUPLICADO",
                $"'{texto}' ya está en el diccionario ({existente.TipoElemento}, código {existente.Codigo}{(existente.Activo ? string.Empty : ", inactivo")}).");
        }

        auditoria.Registrar(usuarioActual.Id, "configuraciones_vehiculo", v.Id, "mapear_vehiculo_cuarentena",
            JsonSerializer.Serialize(new { tipoVehiculo = v.TipoVehiculo, carroceria = v.Carroceria, codigo, aliasCreado = creado }));
        await uow.SaveChangesAsync(ct);

        return new clsMapeoVehiculoDto(v.Id, v.TipoVehiculo, v.Carroceria, codigo, texto, creado);
    }
}

// ---------------------------------------------------------------- descartar

/// <summary>
/// Descarta una configuración en cuarentena (no corresponde a ningún vehículo real). Sale de la bandeja; no se borra. 404
/// VEHICULO_NO_ENCONTRADO; 409 VEHICULO_NO_ESTA_EN_CUARENTENA si ya es oficial; 409 VEHICULO_YA_DESCARTADO.
/// </summary>
public sealed record clsDescartarVehiculoCuarentenaCommand(Guid VehiculoId) : ICommand<Unit>;

public sealed class clsDescartarVehiculoCuarentenaCommandHandler(
    IConfiguracionesVehiculoPort vehiculos,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<clsDescartarVehiculoCuarentenaCommand, Unit>
{
    public async Task<Unit> Handle(clsDescartarVehiculoCuarentenaCommand command, CancellationToken ct = default)
    {
        var v = await vehiculos.ObtenerAsync(command.VehiculoId, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "La configuración de vehículo no existe.");
        if (v.EsCatalogoOficial)
            throw new clsConflictException("VEHICULO_NO_ESTA_EN_CUARENTENA", "Esa configuración ya es oficial: no se puede descartar.");
        if (v.Descartada)
            throw new clsConflictException("VEHICULO_YA_DESCARTADO", "Esa configuración ya fue descartada.");

        v.Descartada = true;
        auditoria.Registrar(usuarioActual.Id, "configuraciones_vehiculo", v.Id, "descartar_vehiculo_cuarentena",
            JsonSerializer.Serialize(new { tipoVehiculo = v.TipoVehiculo, carroceria = v.Carroceria }));
        await uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
