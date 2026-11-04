namespace Nexo.Application.CasosDeUso.Catalogos.Interfaces;

/// <summary>Un tipo de vehículo para los desplegables de Reglas Solo-SICETAC y Tarifas de Plaza. <see cref="EsCatalogoOficial"/> se conserva por compatibilidad con el frontend y siempre es <c>true</c>.</summary>
public sealed record clsTipoVehiculoDto(string Codigo, string Descripcion, bool EsCatalogoOficial);

/// <summary>Catálogo de tipos de vehículo: tblSAPTiposVehiculoTarifario (dbSolucionesTI) tal cual; lista vacía si no está disponible. Los códigos 3S2 y 2L3 faltan en SAP y no se hardcodean.</summary>
public interface ITiposVehiculoPort
{
    Task<IReadOnlyList<clsTipoVehiculoDto>> ListarAsync(CancellationToken ct);
}
