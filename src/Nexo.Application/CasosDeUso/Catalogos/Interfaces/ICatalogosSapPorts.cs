namespace Nexo.Application.CasosDeUso.Catalogos.Interfaces;

public sealed record clsLineaNegocioDto(int Id, string Descripcion);
public sealed record clsClaseViajeDto(int Id, string Descripcion);
public sealed record clsClaseContenedorDto(int Id, string Descripcion, int? TamanoContenedor, int? PesoInfCnt, int? PesoSupCnt);

/// <summary>Línea de negocio (tblSAPLineaNegocio, dbSolucionesTI) para el desplegable de Tarifas de Plaza (S4-HU-10).</summary>
public interface ILineasNegocioPort
{
    Task<IReadOnlyList<clsLineaNegocioDto>> ListarAsync(CancellationToken ct);
}

/// <summary>Clase de viaje (tblSAPClaseViaje, dbSolucionesTI) para el desplegable de Tarifas de Plaza (S4-HU-10).</summary>
public interface IClasesViajePort
{
    Task<IReadOnlyList<clsClaseViajeDto>> ListarAsync(CancellationToken ct);
}

/// <summary>Clase de contenedor (tblSAPClaseContenedor, dbSolucionesTI) para el desplegable de Tarifas de
/// Plaza (S4-HU-10) — incluye tamaño y rango de peso (tara) para sugerir el rango de peso al elegirla.</summary>
public interface IClasesContenedorPort
{
    Task<IReadOnlyList<clsClaseContenedorDto>> ListarAsync(CancellationToken ct);
}
