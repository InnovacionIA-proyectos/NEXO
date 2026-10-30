using Microsoft.Data.SqlClient;

namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Marca una prueba que necesita un histórico SAP REAL con volumen (dbsolucionesTI.dbo.tblSAPTarifasLog, ~26 mil filas en el entorno
/// local) — por ejemplo, para comprobar que la página 2 trae filas distintas a la 1.
///
/// SE SALTA, DICIENDO POR QUÉ, si el entorno no puede dar ese volumen: no hay <c>ConnectionStrings__DbSolucionesTi</c>, no se puede
/// consultar, o la tabla tiene <paramref name="minimoFilas"/> filas o menos. Sin eso el endpoint responde 200 con una lista vacía y la
/// prueba fallaba por falta de datos en el entorno, no por un defecto del código (corrida de CI del 2026-10-20: 10 esperadas, 0 recibidas).
/// Mismo criterio que <see cref="clsHechoConCubicadorRealAttribute"/>: el salto es explícito y dice cómo correrla. Con datos suficientes
/// corre normal y falla si la paginación está mal.
/// </summary>
public sealed class clsHechoConHistoricoSapRealAttribute : FactAttribute
{
    public const string VarConexion = "ConnectionStrings__DbSolucionesTi";

    // Una sola consulta por corrida: todas las pruebas que usan el atributo comparten el resultado.
    private static readonly Lazy<(int? Filas, string? Error)> Conteo = new(Contar);

    public clsHechoConHistoricoSapRealAttribute(int minimoFilas = 20)
    {
        var (filas, error) = Conteo.Value;
        if (filas is null)
            Skip = $"Necesita el histórico SAP real: {error}. Para correrla: {VarConexion}=<cadena de dbsolucionesTI> (ver .env.example).";
        else if (filas <= minimoFilas)
            Skip = $"Necesita un histórico SAP con más de {minimoFilas} filas para poder paginar y tblSAPTarifasLog tiene {filas} en este entorno.";
    }

    private static (int? Filas, string? Error) Contar()
    {
        var cs = Environment.GetEnvironmentVariable(VarConexion);
        if (string.IsNullOrWhiteSpace(cs)) return (null, $"{VarConexion} no está definida");
        try
        {
            using var cn = new SqlConnection(cs);
            cn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.tblSAPTarifasLog", cn) { CommandTimeout = 30 };
            return (Convert.ToInt32(cmd.ExecuteScalar()), null);
        }
        catch (Exception ex)
        {
            return (null, $"no se pudo consultar tblSAPTarifasLog ({ex.GetType().Name})");
        }
    }
}
