using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Interfaces;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Mappers;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Validadores;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Bandeja;
using CabinaFletes.Mensajes.Priorizacion;

namespace CabinaFletes.Application.CasosDeUso.Priorizacion;

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
public sealed record ActualizarParametrosScoreCommand(ParametrosScoreDto Parametros, Guid UsuarioId)
    : ICommand<ParametrosScoreDto>;

public sealed class ActualizarParametrosScoreCommandHandler(
    IParametrosScorePort parametros,
    IPublicadorDeMensajes publicador,
    TimeProvider reloj,
    IUnitOfWork uow)
    : ICommandHandler<ActualizarParametrosScoreCommand, ParametrosScoreDto>
{
    public async Task<ParametrosScoreDto> Handle(ActualizarParametrosScoreCommand command, CancellationToken ct = default)
    {
        ActualizarParametrosScoreValidador.Validar(command.Parametros);

        var fila = await parametros.ParaEditarAsync(ct);
        command.Parametros.Copiar(fila);

        await publicador.PublicarAsync(
            new ParametrosDePriorizacionCambiaron(
                CadenaDePriorizacion.Unica, command.UsuarioId, reloj.GetUtcNow()),
            ct);

        // Y este cambio SÍ reordena la bandeja de verdad, a diferencia del refresco del
        // puntaje persistido: la bandeja calcula el puntaje al leer, así que un horizonte
        // distinto le cambia el orden en la siguiente consulta.
        await publicador.PublicarAsync(
            new BandejaCambio(MotivoDeCambio.ParametrosRecalibrados, reloj.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return fila.ADto();
    }
}
