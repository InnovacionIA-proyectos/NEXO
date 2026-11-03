using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>El NIT en la forma con que se compara: sin puntos, espacios ni dígito de verificación.</summary>
public static class clsNit
{
    /// <summary>
    /// <c>900.123.456-7</c>, <c>900 123 456</c> y <c>900123456</c> dan <c>900123456</c>. Lo que va después del primer guion es el dígito de
    /// verificación y se descarta. Null si no queda nada con qué comparar.
    /// </summary>
    public static string? Normalizar(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return null;
        var baseNit = nit.Trim().Split('-')[0];
        var limpio = new string(baseNit.Where(char.IsLetterOrDigit).ToArray());
        return limpio.Length == 0 ? null : limpio.ToUpperInvariant();
    }
}

/// <summary>
/// Identifica al cliente de una importación de Excel por su NIT, que es lo único que tienen en común el portal, Cotifly y los prospectos.
/// Las lecturas solo buscan el espejo local y no crean nada; guardar una plantilla sí lo crea (desde el catálogo de clientes del DW o, si es
/// un prospecto, desde la tabla de prospectos) para que el mismo NIT siempre termine en el mismo cliente local.
/// </summary>
public sealed class clsClienteDeImportacion(IClientesPort clientes, IClientesDwPort clientesDw, IProspectosDwPort prospectosDw)
{
    /// <summary>El cliente local con ese NIT, o null si no existe (o el NIT no trae nada). Nunca crea.</summary>
    public async Task<Guid?> BuscarLocalAsync(string? nit, CancellationToken ct)
    {
        var normalizado = clsNit.Normalizar(nit);
        return normalizado is null ? null : (await clientes.ObtenerPorNitAsync(normalizado, ct))?.Id;
    }

    /// <summary>
    /// El cliente local con ese NIT; si no existe lo crea (sin guardar) con los datos del DW: primero el catálogo de clientes y, si no está,
    /// el de prospectos. 400 <c>CLIENTE_NIT_INVALIDO</c> si el NIT no está en ninguno.
    /// </summary>
    public async Task<Guid> ObtenerOCrearAsync(string nit, CancellationToken ct)
    {
        var normalizado = clsNit.Normalizar(nit)
            ?? throw new clsValidationFailedException("CLIENTE_NIT_INVALIDO", "El NIT del cliente no es válido.");

        if ((await clientes.ObtenerPorNitAsync(normalizado, ct)) is { } existente) return existente.Id;

        (string Nit, string Nombre)? origen = null;
        if (clientesDw.Configurado)
        {
            var real = (await clientesDw.ListarAsync(ct)).FirstOrDefault(c => clsNit.Normalizar(c.Nit) == normalizado);
            if (real is not null) origen = (real.Nit, real.Nombre);
        }
        if (origen is null && prospectosDw.Configurado)
        {
            var prospecto = (await prospectosDw.ListarAsync(ct)).FirstOrDefault(p => clsNit.Normalizar(p.Nit) == normalizado);
            if (prospecto is not null) origen = (prospecto.Nit, prospecto.Nombre);
        }
        if (origen is null)
            throw new clsValidationFailedException("CLIENTE_NIT_INVALIDO", $"El NIT '{nit.Trim()}' no corresponde a ningún cliente ni prospecto.");

        var nuevo = new clsCliente { Nombre = origen.Value.Nombre, Nit = origen.Value.Nit, RazonSocial = origen.Value.Nombre };
        clientes.Agregar(nuevo);
        return nuevo.Id;
    }
}
