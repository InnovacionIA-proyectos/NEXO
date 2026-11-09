namespace Nexo.Application.CasosDeUso.Catalogos.Interfaces;

/// <summary>Un código de vehículo de NEXO (fila activa de <c>tbl_mapeo_vehiculo_tarifario</c>) con su nombre en SICE-TAC.</summary>
public sealed record clsCodigoVehiculoNexo(string Codigo, string Descripcion);

/// <summary>Un código de vehículo de NEXO para el selector de la Mesa de Costeo; <paramref name="EnSap"/> dice si también existe en <c>tblSAPTiposVehiculoTarifario</c>.</summary>
public sealed record clsVehiculoNexoDto(string Codigo, string Descripcion, bool EnSap);

/// <summary>Lo que decide si un vehículo es oficial: los códigos de la tabla de traducción y las carrocerías del catálogo.</summary>
public interface IVehiculosNexoPort
{
    /// <summary>Los códigos activos de <c>tbl_mapeo_vehiculo_tarifario</c>.</summary>
    Task<IReadOnlyList<clsCodigoVehiculoNexo>> ListarCodigosAsync(CancellationToken ct);

    /// <summary>Código y nombre de las carrocerías activas de <c>tbl_tipos_carroceria_catalogo</c>.</summary>
    Task<IReadOnlyList<string>> ListarCarroceriasOficialesAsync(CancellationToken ct);
}
