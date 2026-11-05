namespace Nexo.Application.CasosDeUso.LecturaExcel.Interfaces;

/// <summary>Lee un archivo Excel y lo deja como hojas crudas (texto de cada celda y celdas combinadas), sin interpretar. No persiste nada.</summary>
public interface ILectorDeHojasPort
{
    /// <exception cref="Common.Exceptions.clsValidationFailedException"><c>EXCEL_INVALIDO</c> si no es un .xlsx legible; <c>EXCEL_SIN_DATOS</c> si no tiene hojas con contenido.</exception>
    Task<IReadOnlyList<clsHojaCruda>> LeerAsync(Stream archivo, CancellationToken ct = default);
}
