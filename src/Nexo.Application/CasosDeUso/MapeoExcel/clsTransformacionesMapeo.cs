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

    /// <summary>Campos cuyo valor es un número.</summary>
    public static readonly IReadOnlyList<string> CamposNumericos =
        ["peso", "valorMercancia", "horasCargue", "horasDescargue", "cantidad", "largoCm", "anchoCm", "altoCm", "pesoKg"];

    /// <summary>
    /// Si la conversión tiene sentido para el campo. Pasar toneladas a kilos solo vale para un peso; partir «ORIGEN - DESTINO», para el origen o el
    /// destino; la coma decimal, para un número; la fecha, para el campo de fecha. Una columna que no se usa (<c>ignorar</c>) no se convierte.
    /// </summary>
    public static bool Aplica(string campo, string transformacion) => transformacion switch
    {
        Ninguna => true,
        PesoToneladasAKg => campo is "peso" or "pesoKg",
        DividirOrigenDestino => campo is "origen" or "destino",
        NumeroComaDecimal => CamposNumericos.Contains(campo),
        Fecha => campo == "deadline",
        _ => false,
    };
}
