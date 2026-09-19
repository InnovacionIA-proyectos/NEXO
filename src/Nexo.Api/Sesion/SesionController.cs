using Nexo.Api.Common.Auth;
using Nexo.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Sesion;

public record SesionActualDto(long Id, string Nombre, RolUsuario Rol, IReadOnlyList<string> Capacidades);

/// <summary>
/// docs/contrato-integracion/README.md, sección 1: "se llama al entrar al módulo. No
/// crea sesión: cada petición posterior vuelve a validarse por su cuenta." La identidad
/// y el rol ya quedaron resueltos por IdentidadPortalAuthenticationHandler antes de
/// llegar acá — este controller solo empaqueta la respuesta; un 401/403 nunca llega
/// hasta aquí, los produce el esquema de autenticación (S2-HU-03.1).
/// </summary>
[ApiController]
[Route("api/sesion")]
public class SesionController(ICurrentUser currentUser) : ControllerBase
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
    public ActionResult<SesionActualDto> Actual()
    {
        var idPortal = long.Parse(User.FindFirst(IdentidadPortalAuthenticationHandler.ClaimIdPortal)!.Value);
        var rol = currentUser.Rol!.Value; // no-null: la FallbackPolicy ya exigió un rol válido para llegar aquí.
        return Ok(new SesionActualDto(idPortal, currentUser.Nombre!, rol, CapacidadesPorRol[rol]));
    }
}
