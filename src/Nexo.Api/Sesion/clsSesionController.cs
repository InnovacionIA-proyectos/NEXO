using Nexo.Api.Common.Auth;
using Nexo.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Sesion;

public record clsSesionActualDto(long Id, string Nombre, RolUsuario Rol, IReadOnlyList<string> Capacidades);

/// <summary>Se llama al entrar al módulo; no crea sesión (cada petición se valida por su cuenta). Identidad y rol ya los resolvió clsIdentidadPortalAuthenticationHandler.</summary>
[ApiController]
[Route("api/sesion")]
public class clsSesionController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Capacidades por rol, para que el frontend decida qué pinta (la autorización real vive en el backend).</summary>
    private static readonly IReadOnlyDictionary<RolUsuario, string[]> CapacidadesPorRol = new Dictionary<RolUsuario, string[]>
    {
        [RolUsuario.Comercial] = ["capturarSolicitud", "gestionarCotizacion", "agregarConexos"],
        // AdminBiblioteca: solo la Biblioteca, sin Bandeja, Mesa ni cotizaciones. Crear solicitud, cargar líneas y recalcular el puntaje son solo de Admin (le llegan por el "*").
        [RolUsuario.AdminBiblioteca] = ["verBiblioteca", "ajustarVigencia", "editarReglasSoloSicetac"],
        [RolUsuario.AdminTarifas] = ["verBiblioteca", "ajustarVigencia", "editarReglasSoloSicetac", "gestionarCotizacion", "editarServiciosConexos"],
        [RolUsuario.ServiciosLogisticos] = ["editarServiciosConexos"],
        [RolUsuario.LiderProceso] = ["verMetricas"],
        [RolUsuario.Admin] = ["*"],
    };

    /// <summary>Devuelve la identidad del usuario autenticado (id del portal, nombre, rol) y sus capacidades.</summary>
    [HttpGet("actual")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<clsSesionActualDto> Actual()
    {
        var idPortal = long.Parse(User.FindFirst(clsIdentidadPortalAuthenticationHandler.ClaimIdPortal)!.Value);
        var rol = currentUser.Rol!.Value; // no-null: la FallbackPolicy ya exigió un rol válido para llegar aquí.
        return Ok(new clsSesionActualDto(idPortal, currentUser.Nombre!, rol, CapacidadesPorRol[rol]));
    }
}
