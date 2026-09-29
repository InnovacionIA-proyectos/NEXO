namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;

/// <summary>Genera la plantilla Excel descargable para la carga masiva de reglas "solo SICE-TAC".</summary>
public interface IPlantillaReglasSoloSicetacPort
{
    (byte[] Contenido, string NombreArchivo) Generar();
}
