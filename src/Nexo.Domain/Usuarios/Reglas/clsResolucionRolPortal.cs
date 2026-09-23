using Nexo.Domain.Enums;

namespace Nexo.Domain.Usuarios.Reglas;

/// <summary>
/// Una persona puede tener varios roles asignados en el portal a la vez (caso real
/// documentado en docs/contrato-integracion/README.md, Pendiente P3): Nexo solo trabaja
/// con un rol efectivo por usuario, así que hace falta un orden de precedencia para
/// desempatar. Función pura: no consulta nada, solo elige entre los candidatos que ya
/// se resolvieron contra la tabla de mapeo.
/// </summary>
public static class clsResolucionRolPortal
{
    /// <summary>
    /// Del que más manda al que menos. Propuesta técnica pendiente de confirmar por
    /// negocio (Pendiente P3): sale de qué tan a menudo autoriza cada rol operaciones
    /// de escritura en los controladores actuales, no de una decisión de negocio.
    /// </summary>
    private static readonly RolUsuario[] OrdenDePrecedencia =
    [
        RolUsuario.Admin,
        RolUsuario.CoordinadorFletes,
        RolUsuario.AdminTarifas,
        RolUsuario.Comercial,
        RolUsuario.LiderProceso,
        RolUsuario.ServiciosLogisticos,
    ];

    /// <summary>Null si <paramref name="candidatos"/> viene vacío (la persona no tiene
    /// ningún rol del portal que traduzca a un rol de Nexo — cierra en 403).</summary>
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
