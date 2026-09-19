using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Validadores;

/// <summary>
/// Forma de <see cref="CapturarSolicitudCommand"/>. Se ejecuta ENTERO antes de tocar
/// nada: en un pliego de 300 líneas, la 300 mal formada se descubre antes de resolver
/// clientes, rutas o vehículos, no a mitad del recorrido.
///
/// Aquí solo se mira el comando. Que el cliente exista, que el BP esté en el catálogo
/// o que el DW esté configurado son precondiciones del caso de uso: necesitan consultar
/// y viven en el handler.
/// </summary>
public static class CapturarSolicitudValidador
{
    public static void Validar(CapturarSolicitudCommand command)
    {
        // Sin ClienteId, el BP es obligatorio: es la otra forma de identificar al cliente.
        if (command.ClienteId is null && string.IsNullOrWhiteSpace(command.ClienteBp))
            throw new ValidationFailedException("CLIENTE_REQUERIDO", "Indica el cliente (clienteId o clienteBp).");

        if (!string.IsNullOrWhiteSpace(command.Modalidad)
            && !Enum.TryParse<ModalidadSolicitud>(command.Modalidad, ignoreCase: true, out _))
            throw new ValidationFailedException("ENUM_INVALIDO", $"Valor inválido para 'modalidad': '{command.Modalidad}'.");

        var numeroLinea = 0;
        foreach (var linea in command.Lineas)
        {
            numeroLinea++;
            ValidarLinea(linea, numeroLinea);
        }
    }

    private static void ValidarLinea(CapturaLineaInput lr, int numeroLinea)
    {
        if (string.IsNullOrWhiteSpace(lr.Origen) || string.IsNullOrWhiteSpace(lr.Destino))
            throw new ValidationFailedException("LINEA_INCOMPLETA", "Cada línea debe traer origen y destino.");

        var sumaPosiciones = SumaPosiciones(lr);
        var peso = PesoDeclarado(lr);

        if (peso is null or <= 0)
            throw new ValidationFailedException("PESO_REQUERIDO",
                $"La línea {numeroLinea} ({lr.Origen} → {lr.Destino}) debe traer el peso total en kg (o posiciones con peso).");

        // Tolerancia del 1%: los pesos vienen de fuentes con redondeos distintos.
        if (sumaPosiciones > 0 && Math.Abs(peso.Value - sumaPosiciones) / peso.Value > 0.01m)
            throw new ValidationFailedException("PESO_INCONSISTENTE",
                $"En la línea {numeroLinea} ({lr.Origen} → {lr.Destino}) el peso total ({peso:0.##} kg) " +
                $"no coincide con la suma de las posiciones ({sumaPosiciones:0.##} kg).");
    }

    /// <summary>Peso de la línea, ya validado. El handler lo usa sin volver a comprobar.</summary>
    public static decimal PesoResuelto(CapturaLineaInput lr) => PesoDeclarado(lr)!.Value;

    private static decimal SumaPosiciones(CapturaLineaInput lr) =>
        lr.Posiciones?.Where(p => p.PesoKg is > 0).Sum(p => p.Cantidad * p.PesoKg!.Value) ?? 0;

    private static decimal? PesoDeclarado(CapturaLineaInput lr)
    {
        var suma = SumaPosiciones(lr);
        return lr.Peso ?? (suma > 0 ? suma : (decimal?)null);
    }
}
