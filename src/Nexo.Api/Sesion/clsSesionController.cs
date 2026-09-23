using Nexo.Api.Common.Auth;
using Nexo.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Sesion;

public record clsSesionActualDto(long Id, string Nombre, RolUsuario Rol, IReadOnlyList<string> Capacidades);

/// <summary>
/// docs/contrato-integracion/README.md, sección 1: "se llama al entrar al módulo. No
/// crea sesión: cada petición posterior vuelve a validarse por su cuenta." La identidad
/// y el rol ya quedaron resueltos por clsIdentidadPortalAuthenticationHandler antes de
/// llegar acá — este controller solo empaqueta la respuesta; un 401/403 nunca llega
/// hasta aquí, los produce el esquema de autenticación.
/// </summary>
[ApiController]
[Route("api/sesion")]
public class clsSesionController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Capacidades por rol: para que el frontend decida qué pinta, no qué
    /// permite (la autorización real vive en el backend, sección 3 del contrato). Primer
    /// corte a partir de la tabla "Autorización por operación" — sujeto a que Frontend
    /// confirme el vocabulario exacto que necesita pintar.</summary>
    private static readonly IReadOnlyDictionary<RolUsuario, string[]> CapacidadesPorRol = new Dictionary<RolUsuario, string[]>
    {
        [RolUsuario.Comercial] = ["capturarSolicitud", "gestionarCotizacion", "agregarConexos"],
        [RolUsuario.CoordinadorFletes] = ["crearSolicitud", "cargarLineas", "recalcularScore", "rechazarSolicitud", "gestionarCotizacion"],
        [RolUsuario.AdminTarifas] = ["verBiblioteca", "ajustarVigencia", "editarReglasSoloSicetac", "gestionarCotizacion", "editarServiciosConexos"],
        [RolUsuario.ServiciosLogisticos] = ["editarServiciosConexos"],
        [RolUsuario.LiderProceso] = ["verMetricas"],
        [RolUsuario.Admin] = ["*"],
    };

    [HttpGet("actual")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<clsSesionActualDto> Actual()
    {
        var idPortal = long.Parse(User.FindFirst(clsIdentidadPortalAuthenticationHandler.ClaimIdPortal)!.Value);
        var rol = currentUser.Rol!.Value; // no-null: la FallbackPolicy ya exigió un rol válido para llegar aquí.
        return Ok(new clsSesionActualDto(idPortal, currentUser.Nombre!, rol, CapacidadesPorRol[rol]));
    }
}
