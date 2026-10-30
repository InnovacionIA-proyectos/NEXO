namespace Nexo.Application.CasosDeUso.Catalogos.Interfaces;

/// <summary>Un tipo de vehículo para los desplegables de Reglas Solo-SICETAC (S4-HU-09.2)
/// y Tarifas de Plaza (S4-HU-10.x). S5-HU-22.1/#288: <see cref="EsCatalogoOficial"/> se conserva
/// por compatibilidad con el frontend, pero ya siempre es <c>true</c>: el catálogo es solo el de
/// tblSAPTiposVehiculoTarifario (se eliminaron los 5 casos especiales escritos a mano).</summary>
public sealed record clsTipoVehiculoDto(string Codigo, string Descripcion, bool EsCatalogoOficial);

/// <summary>
/// Catálogo de tipos de vehículo: tblSAPTiposVehiculoTarifario (dbSolucionesTI), tal cual.
/// S5-HU-22.1/#288: ya no se le suman códigos inventados; si dbSolucionesTI no está disponible
/// devuelve una lista vacía. Los códigos 3S2 y 2L3 faltan en SAP — decisión de negocio, no se
/// hardcodean.
/// </summary>
public interface ITiposVehiculoPort
{
    Task<IReadOnlyList<clsTipoVehiculoDto>> ListarAsync(CancellationToken ct);
}
