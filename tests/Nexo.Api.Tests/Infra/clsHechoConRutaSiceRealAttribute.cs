using Microsoft.Data.SqlClient;

namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Marca una prueba que necesita una ruta concreta en el SICE-TAC real (DWSice, tblRutasSicetac) para el período del mes actual:
/// origen y destino en código DANE tal como los guarda esa tabla (sin el cero inicial del departamento), configuración de vehículo
/// y condición de carga (CARGADO o VACIO).
///
/// SE SALTA, diciendo por qué, si el entorno no tiene DWSice (<c>ConnectionStrings__DwSice</c>), no se puede consultar, o la ruta
/// no tiene filas en el período vigente. Las pruebas que la usan no afirman valores fijos —el SICE-TAC cambia cada mes—, solo
/// relaciones que deben cumplirse con cualquier tarifa; por eso basta con que la ruta exista.
/// </summary>
public sealed class clsHechoConRutaSiceRealAttribute : FactAttribute
{
    public const string VarConexion = "ConnectionStrings__DwSice";

    public clsHechoConRutaSiceRealAttribute(string origenDane, string destinoDane, string configuracion, string condicionCarga = "CARGADO")
    {
        var (filas, error) = Contar(origenDane, destinoDane, configuracion, condicionCarga);
        if (filas is null)
            Skip = $"Necesita el SICE-TAC real: {error}. Para correrla: {VarConexion}=<cadena de DWSice> (ver .env.example).";
        else if (filas == 0)
            Skip = $"Necesita filas SICE-TAC {condicionCarga} de {origenDane} a {destinoDane} ({configuracion}) en el período vigente y no hay ninguna en este entorno.";
    }

    private static (int? Filas, string? Error) Contar(string origen, string destino, string configuracion, string condicion)
    {
        var cs = Environment.GetEnvironmentVariable(VarConexion);
        if (string.IsNullOrWhiteSpace(cs)) return (null, $"{VarConexion} no está definida");
        try
        {
            using var cn = new SqlConnection(cs);
            cn.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM tblRutasSicetac WHERE PERIODO = @p AND ORIGEN = @o AND DESTINO = @d AND CONFIGURACION = @c AND CONDICIONCARGA = @cc", cn)
            { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@p", int.Parse(DateTime.UtcNow.ToString("yyyyMM")));
            cmd.Parameters.AddWithValue("@o", origen);
            cmd.Parameters.AddWithValue("@d", destino);
            cmd.Parameters.AddWithValue("@c", configuracion);
            cmd.Parameters.AddWithValue("@cc", condicion);
            return (Convert.ToInt32(cmd.ExecuteScalar()), null);
        }
        catch (Exception ex)
        {
            return (null, $"no se pudo consultar tblRutasSicetac ({ex.GetType().Name})");
        }
    }
}
