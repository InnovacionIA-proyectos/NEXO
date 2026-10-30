namespace Nexo.Application.CasosDeUso.MapeoExcel.Normalizacion;

/// <summary>Un texto libre de una columna del Excel. <paramref name="Campo"/>: <c>tipoVehiculo</c>, <c>carroceria</c>, <c>lineaNegocio</c> o <c>ciudad</c> (origen, destino y demás ciudades).</summary>
public sealed record clsValorANormalizar(string? Campo, string? Valor);

/// <summary>Un municipio que podría ser el que el texto nombra.</summary>
public sealed record clsCandidatoCiudadDto(int MunicipioId, string CodigoDane, string Municipio, string Departamento);

/// <summary>
/// Qué se resolvió para un texto.
/// </summary>
/// <param name="Estado"><c>resuelto</c> (hay valor de NEXO), <c>ambiguo</c> (varios municipios con ese nombre: ver <paramref name="Candidatos"/>) o <c>sin_resolver</c>.</param>
/// <param name="Codigo">Tipo de vehículo: su código · carrocería: el nombre del catálogo · línea de negocio: su código (<c>puertoAInterior</c>) · ciudad: el código DANE. Null si no se resolvió.</param>
/// <param name="Descripcion">Una descripción legible del valor resuelto (la del tipo de vehículo, o «Municipio, Departamento»).</param>
/// <param name="MunicipioId">Solo para una ciudad resuelta: el id de <c>tbl_municipios</c>.</param>
/// <param name="Origen">
/// <c>diccionario</c> (el del cliente), <c>alias</c> (el global de vehículos), <c>regla</c> (un sinónimo inequívoco de la línea de negocio: impo, expo), <c>catalogo</c> (coincide con el catálogo oficial), <c>dane</c> (el texto era un código DANE),
/// <c>ia</c> (lo propuso el modelo y una regla lo validó; hay que confirmarlo) o <c>ninguno</c>.
/// </param>
/// <param name="Confianza">1 si lo resolvió una regla; la del modelo (0 a 1) con <c>ia</c>; 0 sin resolver.</param>
public sealed record clsValorNormalizadoDto(
    string Campo, string Valor, string Estado, string? Codigo, string? Descripcion, int? MunicipioId,
    string Origen, double Confianza, string? Nota, IReadOnlyList<clsCandidatoCiudadDto>? Candidatos);

/// <param name="TotalDistintos">Cuántos valores distintos (por campo y texto normalizado) se procesaron; la IA se llama una sola vez por todos los pendientes.</param>
/// <param name="MotivoSinIa"><c>SIN_PENDIENTES</c>, <c>IA_NO_DISPONIBLE</c> o el código <c>IA_*</c> del error del proveedor. Null cuando sí participó.</param>
public sealed record clsResultadoNormalizacionDto(
    bool IaUsada, string? MotivoSinIa, int TotalDistintos, IReadOnlyList<clsValorNormalizadoDto> Valores);
