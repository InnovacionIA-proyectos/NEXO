using Nexo.Domain.Cubicacion.Entidades;
using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>
/// S5-HU-18.5/#277: consume mercancia.creada/mercancia.actualizada/mercancia.eliminada y
/// mantiene sincronizada la proyección de NEXO (<c>TiposMercanciaCatalogo</c>) — alimenta
/// el desplegable de tipo de mercancía en Captura (S5-HU-18.2/#274) sin que NEXO tenga que
/// preguntarle al Cubicador en caliente. Mismo criterio que
/// <see cref="clsVehiculoCatalogoConsumer"/>: upsert idempotente por Id, borrar lo que no
/// está no es un error.
/// </summary>
public sealed class clsMercanciaCatalogoConsumer(clsNexoDbContext db)
    : IConsumer<MercanciaCreada>, IConsumer<MercanciaActualizada>, IConsumer<MercanciaEliminada>
{
    public async Task Consume(ConsumeContext<MercanciaCreada> context)
    {
        await UpsertAsync(db, context.Message.Mercancia, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<MercanciaActualizada> context)
    {
        await UpsertAsync(db, context.Message.Mercancia, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<MercanciaEliminada> context)
    {
        var mercancia = await db.TiposMercanciaCatalogo.FirstOrDefaultAsync(m => m.Id == context.Message.MercanciaId, context.CancellationToken);
        if (mercancia is null)
            return;

        db.TiposMercanciaCatalogo.Remove(mercancia);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private static async Task UpsertAsync(clsNexoDbContext db, clsTipoMercanciaCatalogoEvento e, CancellationToken ct)
    {
        var mercancia = await db.TiposMercanciaCatalogo.FirstOrDefaultAsync(m => m.Id == e.Id, ct);
        if (mercancia is null)
        {
            mercancia = new clsTipoMercanciaCatalogo { Id = e.Id };
            db.TiposMercanciaCatalogo.Add(mercancia);
        }

        mercancia.CodigoMercancia = e.CodigoMercancia;
        mercancia.NombreMercancia = e.NombreMercancia;
        mercancia.Categoria = e.Categoria;
        mercancia.Cubicable = e.Cubicable;
        mercancia.CondicionadaCubicacion = e.CondicionadaCubicacion;
        mercancia.CarroceriaPrincipal = e.CarroceriaPrincipal;
        mercancia.CarroceriaSecundaria = e.CarroceriaSecundaria;
        mercancia.CarroceriaSecundariaCondicionada = e.CarroceriaSecundariaCondicionada;
        mercancia.ControlAmbientalRequerido = e.ControlAmbientalRequerido;
        mercancia.Apilable = e.Apilable;
        mercancia.Fragil = e.Fragil;
        mercancia.RotacionLibre = e.RotacionLibre;
        mercancia.ComentarioCondicion = e.ComentarioCondicion;
        mercancia.Activo = e.Activo;
        mercancia.Version = e.Version;
        mercancia.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
