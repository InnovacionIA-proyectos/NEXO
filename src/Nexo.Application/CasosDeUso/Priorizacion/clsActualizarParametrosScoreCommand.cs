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
/// Recalibra los pesos del score. No recalcula aquí: guarda y publica el aviso por Outbox en la MISMA transacción,
/// así no queda parámetro guardado sin recálculo ni recálculo por un cambio revertido.
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

        // Este cambio SÍ reordena la bandeja: el puntaje se calcula al leer, así que un horizonte distinto
        // cambia el orden en la siguiente consulta.
        await publicador.PublicarAsync(
            new clsBandejaCambio(clsMotivoDeCambio.ParametrosRecalibrados, reloj.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return fila.ADto();
    }
}
