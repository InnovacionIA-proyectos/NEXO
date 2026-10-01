namespace Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;

/// <summary>Plantilla de Excel descargable por tipo de servicio conexo (S4-HU-15.3/#249):
/// Almacenamiento e ITR piden columnas distintas entre sí, y lo mismo pasa en Adicionales
/// entre Precinto (pide Ruta) y Acompañante vehicular (pide Origen+Destino). El backend
/// sigue siendo un único importador genérico y tolerante — solo cambia qué plantilla
/// ofrece descargar cada botón del frontend.</summary>
public interface IPlantillaServicioConexoPort
{
    /// <summary>"almacenamiento" | "itr" | "precinto" | "acompanamiento".</summary>
    (byte[] Contenido, string NombreArchivo) Generar(string tipo);
}
