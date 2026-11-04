using Nexo.Domain.Enums;

namespace Nexo.Domain.Usuarios.Reglas;

/// <summary>Una persona puede tener varios roles en el portal pero Nexo usa uno solo: se desempata por orden de precedencia.</summary>
public static class clsResolucionRolPortal
{
    /// <summary>Del que más manda al que menos; criterio técnico (qué tanto escribe cada rol), pendiente de confirmar por negocio.</summary>
    private static readonly RolUsuario[] OrdenDePrecedencia =
    [
        RolUsuario.Admin,
        // AdminTarifas puede más que AdminBiblioteca (cotizaciones, Mesa, Cubicador completo).
        RolUsuario.AdminTarifas,
        RolUsuario.AdminBiblioteca,
        RolUsuario.Comercial,
        RolUsuario.LiderProceso,
        RolUsuario.ServiciosLogisticos,
    ];

    /// <summary>Null si <paramref name="candidatos"/> viene vacío (ningún rol del portal traduce a uno de Nexo; cierra en 403).</summary>
    public static RolUsuario? ElegirRolEfectivo(IEnumerable<RolUsuario> candidatos)
    {
        var conjunto = candidatos.ToHashSet();
        foreach (var rol in OrdenDePrecedencia)
        {
            if (conjunto.Contains(rol)) return rol;
        }
        return null;
    }
}
