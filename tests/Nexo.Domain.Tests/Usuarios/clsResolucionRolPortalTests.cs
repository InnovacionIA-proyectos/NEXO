using Nexo.Domain.Enums;
using Nexo.Domain.Usuarios.Reglas;

namespace Nexo.Domain.Tests.Usuarios;

public class clsResolucionRolPortalTests
{
    [Fact]
    public void Sin_candidatos_no_hay_rol_efectivo()
    {
        Assert.Null(clsResolucionRolPortal.ElegirRolEfectivo([]));
    }

    [Fact]
    public void Un_solo_candidato_es_el_rol_efectivo()
    {
        Assert.Equal(RolUsuario.Comercial, clsResolucionRolPortal.ElegirRolEfectivo([RolUsuario.Comercial]));
    }

    [Fact]
    public void Con_varios_roles_gana_el_de_mayor_precedencia()
    {
        // Caso real documentado en el contrato (Pendiente P3): alguien con permisos de
        // tarifas comerciales Y de administración de tarifas a la vez.
        var efectivo = clsResolucionRolPortal.ElegirRolEfectivo([RolUsuario.Comercial, RolUsuario.AdminTarifas]);
        Assert.Equal(RolUsuario.AdminTarifas, efectivo);
    }

    [Fact]
    public void Admin_le_gana_a_cualquier_otro_rol()
    {
        var todos = Enum.GetValues<RolUsuario>();
        Assert.Equal(RolUsuario.Admin, clsResolucionRolPortal.ElegirRolEfectivo(todos));
    }

    [Fact]
    public void ServiciosLogisticos_es_el_de_menor_precedencia()
    {
        var efectivo = clsResolucionRolPortal.ElegirRolEfectivo([RolUsuario.ServiciosLogisticos, RolUsuario.LiderProceso]);
        Assert.Equal(RolUsuario.LiderProceso, efectivo);
    }
}
