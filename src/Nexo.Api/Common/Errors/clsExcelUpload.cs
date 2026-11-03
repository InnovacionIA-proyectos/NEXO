using Nexo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Nexo.Api.Common.Errors;

/// <summary>
/// Validación uniforme de archivos Excel subidos: presencia, tamaño y extensión.
/// Lanza <see cref="clsValidationFailedException"/> (Application) para que el error salga
/// con la MISMA forma que el resto del API vía el middleware, en vez de objetos ad-hoc.
/// </summary>
public static class clsExcelUpload
{
    private static readonly string[] ExtensionesPermitidas = { ".xlsx", ".xlsm" };

    /// <summary>Valida el archivo y devuelve un stream de lectura; el llamador lo usa con `await using`.</summary>
    public static Stream AbrirValidado(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            throw new clsValidationFailedException("ARCHIVO_REQUERIDO", "Adjunta un archivo Excel (.xlsx).");

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            throw new clsValidationFailedException("FORMATO_NO_SOPORTADO", "Solo se aceptan archivos .xlsx o .xlsm.");

        return archivo.OpenReadStream();
    }
}
