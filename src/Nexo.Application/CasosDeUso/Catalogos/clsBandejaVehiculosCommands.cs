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

// Bandeja de normalización de vehículos: configuraciones en cuarentena (EsCatalogoOficial = false) que un humano MAPEA a un código
// (queda un alias) o DESCARTA. Ninguna borra la fila, porque las líneas que ya la usan siguen apuntándole.

public sealed record clsListarVehiculosCuarentenaQuery : IQuery<IReadOnlyList<clsVehiculoCuarentenaDto>>;

/// <summary>Lista las configuraciones en cuarentena no descartadas cuyo tipo de vehículo aún no tiene alias activo (id, tipo, carrocería, rango de peso/fecha).</summary>
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

/// <summary>
/// Mapea una configuración en cuarentena a un código oficial creando el alias. 404 VEHICULO_NO_ENCONTRADO; 409 VEHICULO_NO_ESTA_EN_CUARENTENA,
/// VEHICULO_YA_DESCARTADO o ALIAS_DUPLICADO (texto ya apuntando a OTRO código); 400 CODIGO_NO_RECONOCIDO. Si ya apuntaba al mismo código, <c>AliasCreado = false</c>.
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

/// <summary>Descarta una configuración en cuarentena (sale de la bandeja, no se borra). 404 VEHICULO_NO_ENCONTRADO; 409 VEHICULO_NO_ESTA_EN_CUARENTENA o VEHICULO_YA_DESCARTADO.</summary>
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
