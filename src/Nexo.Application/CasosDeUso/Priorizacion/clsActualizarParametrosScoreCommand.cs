using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.CasosDeUso.Priorizacion.Interfaces;
using Nexo.Application.CasosDeUso.Priorizacion.Mappers;
using Nexo.Application.CasosDeUso.Priorizacion.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Bandeja;
using Nexo.Mensajes.Priorizacion;

namespace Nexo.Application.CasosDeUso.Priorizacion;

/// <summary>
/// Recalibra los pesos del score.
///
/// NO RECALCULA AQUÍ. Guarda los parámetros, publica el aviso y responde. Recalcular
/// todas las activas dentro de esta petición dejaría al Admin mirando una pantalla
/// girando por algo que no necesita esperar, y un fallo a mitad del recálculo le
/// devolvería un error por un cambio que sí se guardó.
///
/// El aviso sale por el Outbox, o sea en la MISMA transacción que el cambio. Eso cierra
/// el hueco obvio: no puede quedar el parámetro guardado sin recálculo, ni un recálculo
/// disparado por un cambio que se acabó revirtiendo.
/// </summary>
public sealed record clsActualizarParametrosScoreCommand(clsParametrosScoreDto Parametros, Guid UsuarioId)
    : ICommand<clsParametrosScoreDto>;

public sealed class clsActualizarParametrosScoreCommandHandler(
    IParametrosScorePort parametros,
    IPublicadorDeMensajes publicador,
    TimeProvider reloj,
    IUnitOfWork uow)
    : ICommandHandler<clsActualizarParametrosScoreCommand, clsParametrosScoreDto>
{
    public async Task<clsParametrosScoreDto> Handle(clsActualizarParametrosScoreCommand command, CancellationToken ct = default)
    {
        clsActualizarParametrosScoreValidador.Validar(command.Parametros);

        var fila = await parametros.ParaEditarAsync(ct);
        command.Parametros.Copiar(fila);

        await publicador.PublicarAsync(
            new clsParametrosDePriorizacionCambiaron(
                clsCadenaDePriorizacion.Unica, command.UsuarioId, reloj.GetUtcNow()),
            ct);

        // Y este cambio SÍ reordena la bandeja de verdad, a diferencia del refresco del
        // puntaje persistido: la bandeja calcula el puntaje al leer, así que un horizonte
        // distinto le cambia el orden en la siguiente consulta.
        await publicador.PublicarAsync(
            new clsBandejaCambio(clsMotivoDeCambio.ParametrosRecalibrados, reloj.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return fila.ADto();
    }
}
