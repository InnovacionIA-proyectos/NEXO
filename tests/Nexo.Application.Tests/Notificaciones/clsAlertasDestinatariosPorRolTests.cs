using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Moq;

namespace Nexo.Application.Tests.Notificaciones;

/// <summary>
/// S6-HU-25.1/#307: a quién le llega cada alerta tras el cambio de roles. La de "línea que no pudo costearse" pasa a AdminTarifas (el rol
/// que resuelve la Mesa de Costeo; AdminBiblioteca ya no entra ahí); la de "tarifa de plaza vencida" sigue llegando a la Biblioteca, a
/// AdminTarifas y a ServiciosLogisticos.
/// </summary>
public class clsAlertasDestinatariosPorRolTests
{
    private readonly Mock<IDestinatariosDeAlertaPort> _destinatarios = new();
    private readonly Mock<ICorreoPort> _correo = new();
    private clsCorreoParaEnviar? _enviado;

    public clsAlertasDestinatariosPorRolTests()
    {
        _correo.SetupGet(c => c.Configurado).Returns(true);
        _correo.Setup(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()))
            .Callback<clsCorreoParaEnviar, CancellationToken>((m, _) => _enviado = m)
            .ReturnsAsync("id-relay");
    }

    private void Rol(RolUsuario rol, params string[] correos) =>
        _destinatarios.Setup(d => d.EmailsPorRolAsync(rol, It.IsAny<CancellationToken>())).ReturnsAsync(correos);

    [Fact]
    public async Task La_linea_que_no_pudo_costearse_le_llega_a_AdminTarifas()
    {
        Rol(RolUsuario.AdminTarifas, "tarifas@boterosoto.com.co");
        Rol(RolUsuario.AdminBiblioteca, "biblioteca@boterosoto.com.co");
        var handler = new clsNotificarLineaFalloTecnicoCommandHandler(_destinatarios.Object, _correo.Object);

        var r = await handler.Handle(new clsNotificarLineaFalloTecnicoCommand(Guid.NewGuid(), Guid.NewGuid(), "timeout"));

        Assert.True(r.Enviado);
        Assert.Equal(["tarifas@boterosoto.com.co"], _enviado!.Destinatarios);
        // AdminBiblioteca ya no entra a la Mesa donde se resuelve esa línea: ni se le pregunta.
        _destinatarios.Verify(d => d.EmailsPorRolAsync(RolUsuario.AdminBiblioteca, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_usuarios_AdminTarifas_la_alerta_de_linea_no_se_envia_y_el_motivo_lo_dice()
    {
        Rol(RolUsuario.AdminTarifas);
        var handler = new clsNotificarLineaFalloTecnicoCommandHandler(_destinatarios.Object, _correo.Object);

        var r = await handler.Handle(new clsNotificarLineaFalloTecnicoCommand(Guid.NewGuid(), Guid.NewGuid(), "timeout"));

        Assert.False(r.Enviado);
        Assert.Contains("AdminTarifas", r.Motivo);
        _correo.Verify(c => c.EnviarAsync(It.IsAny<clsCorreoParaEnviar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task La_tarifa_vencida_le_llega_a_la_Biblioteca_a_AdminTarifas_y_a_ServiciosLogisticos()
    {
        Rol(RolUsuario.AdminBiblioteca, "biblioteca@boterosoto.com.co");
        Rol(RolUsuario.AdminTarifas, "tarifas@boterosoto.com.co");
        Rol(RolUsuario.ServiciosLogisticos, "servicios@boterosoto.com.co");
        var handler = new clsNotificarTarifaVencidaCommandHandler(_destinatarios.Object, _correo.Object);

        var r = await handler.Handle(new clsNotificarTarifaVencidaCommand(Guid.NewGuid(), "La plaza venció.", DateTimeOffset.UtcNow));

        Assert.True(r.Enviado);
        Assert.Equivalent(
            new[] { "biblioteca@boterosoto.com.co", "tarifas@boterosoto.com.co", "servicios@boterosoto.com.co" },
            _enviado!.Destinatarios);
    }
}
