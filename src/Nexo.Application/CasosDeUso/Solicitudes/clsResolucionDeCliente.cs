using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Resuelve el clsCliente local desde clienteId o clienteBp, creando el registro local
/// si el BP existe en el catálogo DW pero todavía no tiene espejo local. Extraído de
/// clsCapturarSolicitudCommand (issue #206) para que otro caso de uso —
/// clsCrearSolicitudCommand, que hoy exige un clienteId ya resuelto y por eso nunca
/// funciona para un cliente real recién buscado— use exactamente la misma resolución.
/// Dos resoluciones divergentes crearían clientes locales duplicados según por dónde
/// entró la solicitud.
/// </summary>
public static class clsResolucionDeCliente
{
    public static async Task<Guid> ResolverAsync(
        Guid? clienteId, string? clienteBp,
        IClientesPort clientes, IClientesDwPort clientesDw, CancellationToken ct)
    {
        if (clienteId is { } id)
        {
            if (!await clientes.ExisteAsync(id, ct))
                throw new clsValidationFailedException("CLIENTE_INVALIDO", "El cliente indicado no existe.");
            return id;
        }

        if (string.IsNullOrWhiteSpace(clienteBp))
            throw new clsValidationFailedException("CLIENTE_REQUERIDO", "Indica el cliente (clienteId o clienteBp).");

        if (!clientesDw.Configurado)
            throw new clsValidationFailedException("DWCUBOS_NO_CONFIGURADO",
                "La conexión a los clientes reales no está configurada (ConnectionStrings:DwCubos).");

        var bp = clienteBp.Trim();
        var real = (await clientesDw.ListarAsync(ct)).FirstOrDefault(c => c.Bp == bp)
            ?? throw new clsValidationFailedException("CLIENTE_BP_INVALIDO", $"El BP '{bp}' no existe en el catálogo de clientes.");

        var existente = await clientes.ObtenerPorNitAsync(real.Nit, ct);
        if (existente is not null) return existente.Id;

        var nuevo = new clsCliente { Nombre = real.Nombre, Nit = real.Nit, RazonSocial = real.Nombre };
        clientes.Agregar(nuevo);
        return nuevo.Id;
    }
}
