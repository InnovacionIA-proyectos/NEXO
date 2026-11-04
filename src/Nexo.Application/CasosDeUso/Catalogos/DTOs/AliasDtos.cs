namespace Nexo.Application.CasosDeUso.Catalogos.DTOs;

/// <summary>Una fila del diccionario. <paramref name="TipoElemento"/> es "Vehiculo" o "Contenedor".</summary>
public sealed record clsAliasDto(Guid Id, string Alias, string AliasNormalizado, string TipoElemento, string Codigo, bool Activo);

/// <summary>
/// ¿Qué es este texto? <paramref name="Reconocido"/> false = el diccionario no lo conoce (<paramref name="Codigo"/> y <paramref name="Alias"/> null):
/// en la captura caería en cuarentena (vehículo) o quedaría como texto libre (contenedor).
/// </summary>
public sealed record clsResolverAliasDto(string Texto, string TipoElemento, bool Reconocido, string? Codigo, string? Alias);

/// <summary>Una configuración de vehículo en cuarentena, para la bandeja de normalización.</summary>
public sealed record clsVehiculoCuarentenaDto(Guid Id, string TipoVehiculo, string? Carroceria, string? RangoPeso, DateTimeOffset CreatedAt);

/// <summary>Resultado de mapear una configuración en cuarentena a un código oficial.</summary>
public sealed record clsMapeoVehiculoDto(Guid Id, string TipoVehiculo, string? Carroceria, string Codigo, string Alias, bool AliasCreado);
