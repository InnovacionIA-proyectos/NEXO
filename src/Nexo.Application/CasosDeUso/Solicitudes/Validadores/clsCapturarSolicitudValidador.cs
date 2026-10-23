using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Solicitudes.Validadores;

/// <summary>
/// Forma de <see cref="clsCapturarSolicitudCommand"/>. Se ejecuta ENTERO antes de tocar
/// nada: en un pliego de 300 líneas, la 300 mal formada se descubre antes de resolver
/// clientes, rutas o vehículos, no a mitad del recorrido.
///
/// Aquí solo se mira el comando. Que el cliente exista, que el BP esté en el catálogo
/// o que el DW esté configurado son precondiciones del caso de uso: necesitan consultar
/// y viven en el handler.
/// </summary>
public static class clsCapturarSolicitudValidador
{
    /// <summary>#304: tope de unidades de una línea = el del Cubicador por petición (10.000 en total). Se valida acá, al
    /// capturar, y no al pedir la recomendación, para que el error aparezca donde se originó.</summary>
    public const int MaxUnidadesPorLinea = 10_000;

    /// <summary>Margen del 1 % con el que la suma de las posiciones puede superar el peso total sin rechazar la línea.</summary>
    private const decimal ToleranciaRedondeoPeso = 0.01m;

    public static void Validar(clsCapturarSolicitudCommand command)
    {
        // Sin ClienteId, el BP es obligatorio: es la otra forma de identificar al cliente.
        if (command.ClienteId is null && string.IsNullOrWhiteSpace(command.ClienteBp))
            throw new clsValidationFailedException("CLIENTE_REQUERIDO", "Indica el cliente (clienteId o clienteBp).");

        if (!string.IsNullOrWhiteSpace(command.Modalidad)
            && !Enum.TryParse<ModalidadSolicitud>(command.Modalidad, ignoreCase: true, out _))
            throw new clsValidationFailedException("ENUM_INVALIDO", $"Valor inválido para 'modalidad': '{command.Modalidad}'.");

        var numeroLinea = 0;
        foreach (var linea in command.Lineas)
        {
            numeroLinea++;
            ValidarLinea(linea, numeroLinea);
        }
    }

    private static void ValidarLinea(clsCapturaLineaInput lr, int numeroLinea)
    {
        if (string.IsNullOrWhiteSpace(lr.Origen) || string.IsNullOrWhiteSpace(lr.Destino))
            throw new clsValidationFailedException("LINEA_INCOMPLETA", "Cada línea debe traer origen y destino.");

        var unidades = lr.Posiciones?.Sum(p => (long)p.Cantidad) ?? 0;
        if (unidades > MaxUnidadesPorLinea)
            throw new clsValidationFailedException("CUBICACION_DEMASIADO_GRANDE",
                $"La línea {numeroLinea} ({lr.Origen} → {lr.Destino}) trae {unidades:N0} unidades en sus posiciones; " +
                $"el máximo es {MaxUnidadesPorLinea:N0} (el Cubicador no recomienda cargas más grandes).");

        var sumaPosiciones = SumaPosiciones(lr);
        var peso = PesoDeclarado(lr);

        if (peso is null or <= 0)
            throw new clsValidationFailedException("PESO_REQUERIDO",
                $"La línea {numeroLinea} ({lr.Origen} → {lr.Destino}) debe traer el peso total en kg (o posiciones con peso).");

        // El peso total incluye la tara (embalaje, estibas, contenedor vacío) y lo escribe quien captura, mientras que cada
        // posición trae el peso de su mercancía sin tara. Por eso el peso total puede ser mayor que la suma de las posiciones;
        // lo que no tiene sentido físico es que las posiciones sumen MÁS que el peso total. Tolerancia del 1 %: los pesos
        // vienen de fuentes con redondeos distintos.
        if (sumaPosiciones > peso.Value * (1m + ToleranciaRedondeoPeso))
            throw new clsValidationFailedException("PESO_INCONSISTENTE",
                $"En la línea {numeroLinea} ({lr.Origen} → {lr.Destino}) las posiciones suman {sumaPosiciones:0.##} kg, " +
                $"más que el peso total ({peso:0.##} kg). El peso total incluye la tara y debe ser mayor o igual a la suma de las posiciones.");
    }

    /// <summary>Peso de la línea, ya validado. El handler lo usa sin volver a comprobar.</summary>
    public static decimal PesoResuelto(clsCapturaLineaInput lr) => PesoDeclarado(lr)!.Value;

    private static decimal SumaPosiciones(clsCapturaLineaInput lr) =>
        lr.Posiciones?.Where(p => p.PesoKg is > 0).Sum(p => p.Cantidad * p.PesoKg!.Value) ?? 0;

    private static decimal? PesoDeclarado(clsCapturaLineaInput lr)
    {
        var suma = SumaPosiciones(lr);
        return lr.Peso ?? (suma > 0 ? suma : (decimal?)null);
    }
}
