using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>Diccionario de términos por cliente sobre <c>tbl_diccionario_cliente</c>.</summary>
public sealed class clsEfDiccionarioClienteRepository(clsNexoDbContext db) : IDiccionarioClienteRepository
{
    public async Task<IReadOnlyList<clsTerminoCliente>> ListarAsync(
        Guid? clienteId, bool incluirGenerales, string? tipo, string? buscar, bool soloActivos, CancellationToken ct)
    {
        var q = db.TerminosCliente.AsNoTracking().Include(t => t.Cliente).AsQueryable();

        if (clienteId is { } id)
            q = incluirGenerales ? q.Where(t => t.ClienteId == id || t.ClienteId == null) : q.Where(t => t.ClienteId == id);
        if (tipo is not null) q = q.Where(t => t.Tipo == tipo);
        if (soloActivos) q = q.Where(t => t.Activo);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            q = q.Where(t => t.Termino.Contains(texto) || t.Concepto.Contains(texto));
        }

        // Primero los generales, luego por cliente; dentro de cada uno por tipo y término.
        return await q.OrderBy(t => t.ClienteId != null).ThenBy(t => t.Cliente!.Nombre).ThenBy(t => t.Tipo).ThenBy(t => t.Termino).ToListAsync(ct);
    }

    public Task<clsTerminoCliente?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.TerminosCliente.Include(t => t.Cliente).FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<clsTerminoCliente?> BuscarDuplicadoAsync(
        Guid? clienteId, string terminoNormalizado, string tipo, string? contexto, Guid? excluirId, CancellationToken ct) =>
        db.TerminosCliente.AsNoTracking().FirstOrDefaultAsync(t =>
            t.ClienteId == clienteId && t.TerminoNormalizado == terminoNormalizado && t.Tipo == tipo && t.Contexto == contexto
            && (excluirId == null || t.Id != excluirId), ct);

    public Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct) =>
        db.Clientes.AsNoTracking().AnyAsync(c => c.Id == clienteId, ct);

    public void Agregar(clsTerminoCliente termino) => db.TerminosCliente.Add(termino);

    public void Eliminar(clsTerminoCliente termino) => db.TerminosCliente.Remove(termino);
}
