namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Las conversiones que el mapeo puede pedirle a una columna antes de usar su valor. Son una lista cerrada: el modelo elige entre estas y
/// el código las aplica, así que nada de lo que diga el modelo se ejecuta ni se convierte en un valor.
/// </summary>
public static class clsTransformacionesMapeo
{
    public const string Ninguna = "ninguna";

    /// <summary>El valor viene en toneladas y NEXO lo guarda en kilos.</summary>
    public const string PesoToneladasAKg = "peso_toneladas_a_kg";

    /// <summary>Una sola celda trae «ORIGEN - DESTINO» y hay que partirla en las dos ciudades.</summary>
    public const string DividirOrigenDestino = "dividir_origen_destino";

    /// <summary>Números escritos con coma decimal (<c>1.234,56</c>).</summary>
    public const string NumeroComaDecimal = "numero_coma_decimal";

    public const string Fecha = "fecha";

    public static readonly IReadOnlyList<string> Todas =
        [Ninguna, PesoToneladasAKg, DividirOrigenDestino, NumeroComaDecimal, Fecha];
}
