namespace Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;

/// <summary>Plantilla de Excel descargable por tipo de servicio conexo: cada tipo pide columnas distintas, pero el backend sigue siendo un único importador tolerante.</summary>
public interface IPlantillaServicioConexoPort
{
    /// <summary>"almacenamiento" | "itr" | "precinto" | "acompanamiento".</summary>
    (byte[] Contenido, string NombreArchivo) Generar(string tipo);
}
