namespace Nexo.Application.CasosDeUso.Catalogos.Interfaces;

/// <summary>Un tipo de vehículo para los desplegables de Reglas Solo-SICETAC (S4-HU-09.2)
/// y Tarifas de Plaza (S4-HU-10.x). <see cref="EsCatalogoOficial"/> distingue los códigos
/// que sí vienen de tblSAPTiposVehiculoTarifario de los casos reales de negocio que
/// todavía no tienen código en ningún catálogo (ver <see cref="ITiposVehiculoPort"/>).</summary>
public sealed record clsTipoVehiculoDto(string Codigo, string Descripcion, bool EsCatalogoOficial);

/// <summary>
/// Catálogo de tipos de vehículo (tblSAPTiposVehiculoTarifario, dbSolucionesTI) más los
/// casos reales de negocio que ese catálogo todavía no cubre (confirmados con datos
/// reales del seeder de S4-HU-10.1: 4 valores de devolución de contenedor +
/// "PBV 7500-8000") — la implementación de Infrastructure documenta cada caso especial.
/// </summary>
public interface ITiposVehiculoPort
{
    Task<IReadOnlyList<clsTipoVehiculoDto>> ListarAsync(CancellationToken ct);
}
