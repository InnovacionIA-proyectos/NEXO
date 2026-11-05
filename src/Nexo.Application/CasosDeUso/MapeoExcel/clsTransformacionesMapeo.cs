namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>Conversiones que el mapeo puede pedir a una columna: lista cerrada que el código aplica; nada de lo que diga el modelo se ejecuta.</summary>
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

    /// <summary>Si la conversión tiene sentido para el campo (toneladas a kilos solo para peso, partir origen-destino solo para origen o destino…); una columna <c>ignorar</c> no se convierte.</summary>
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
