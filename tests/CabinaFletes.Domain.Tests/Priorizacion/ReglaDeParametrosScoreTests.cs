using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Priorizacion.Reglas;

namespace CabinaFletes.Domain.Tests.Priorizacion;

/// <summary>
/// La entidad decía desde siempre que los pesos "deberían sumar 1.0" y nada lo
/// verificaba, porque tampoco había forma de editarlos. Al abrir el endpoint de
/// recalibración eso deja de ser un comentario.
///
/// IMPORTANTE PARA NO MALINTERPRETAR ESTAS PRUEBAS: hoy los pesos no entran en el total.
/// El calculador devuelve exclusivamente la urgencia por deadline, por decisión de
/// negocio, así que cambiar un peso no mueve ningún puntaje —comprobado contra la
/// aplicación levantada: 12 solicitudes revisadas, cero actualizadas—. El único parámetro
/// que mueve el total es HorizonteHoras.
///
/// La regla vale igual: mantiene coherente una configuración que está dormida, para que
/// el día que el total vuelva a ser una suma ponderada no arranque ya torcida.
/// </summary>
public class ReglaDeParametrosScoreTests
{
    [Fact]
    public void Los_valores_por_defecto_son_validos()
    {
        // Si esto fallara, la regla estaría rechazando la configuración con la que el
        // sistema lleva funcionando. Es la primera prueba a escribir.
        Assert.Empty(ReglaDeParametrosScore.Validar(new ParametrosScore()));
    }

    [Theory]
    [InlineData(0.40, 0.30, 0.20, 0.30)]  // suma 1.20
    [InlineData(0.10, 0.10, 0.10, 0.10)]  // suma 0.40
    public void Pesos_que_no_suman_uno_se_rechazan(double urgencia, double valor, double prob, double tipo)
    {
        var motivos = ReglaDeParametrosScore.Validar(new ParametrosScore
        {
            PesoUrgencia = urgencia, PesoValor = valor, PesoProbabilidad = prob, PesoTipo = tipo
        });

        Assert.Contains(motivos, m => m.Contains("deben sumar 1.0"));
    }

    [Fact]
    public void La_suma_admite_el_error_de_los_decimales_binarios()
    {
        // 0.40 + 0.30 + 0.20 + 0.10 no da exactamente 1.0 en binario. Sin tolerancia,
        // la regla rechazaría los valores por defecto y nadie entendería por qué.
        var suma = 0.40 + 0.30 + 0.20 + 0.10;
        Assert.NotEqual(1.0, suma);
        Assert.Empty(ReglaDeParametrosScore.Validar(new ParametrosScore
        {
            PesoUrgencia = 0.40, PesoValor = 0.30, PesoProbabilidad = 0.20, PesoTipo = 0.10
        }));
    }

    [Fact]
    public void El_horizonte_en_cero_se_rechaza_porque_divide()
    {
        var motivos = ReglaDeParametrosScore.Validar(new ParametrosScore { HorizonteHoras = 0 });
        Assert.Contains(motivos, m => m.Contains("divisor de la urgencia"));
    }

    [Fact]
    public void El_valor_de_referencia_en_cero_se_rechaza()
    {
        var motivos = ReglaDeParametrosScore.Validar(new ParametrosScore { ValorReferencia = 0m });
        Assert.Contains(motivos, m => m.Contains("ValorReferencia"));
    }

    [Fact]
    public void Un_puntaje_fuera_de_la_escala_se_rechaza()
    {
        var motivos = ReglaDeParametrosScore.Validar(new ParametrosScore { PuntajeLicitacion = 140 });
        Assert.Contains(motivos, m => m.Contains("PuntajeLicitacion"));
    }

    [Fact]
    public void Se_devuelven_todos_los_motivos_juntos()
    {
        // Uno por respuesta obligaría al Admin a descubrir los errores de a uno.
        var motivos = ReglaDeParametrosScore.Validar(new ParametrosScore
        {
            PesoUrgencia = 0.9, PesoValor = 0.9, PesoProbabilidad = 0.9, PesoTipo = 0.9,
            HorizonteHoras = -1, ValorReferencia = 0m
        });

        Assert.True(motivos.Count >= 3, $"Se esperaban al menos 3 motivos y hubo {motivos.Count}.");
    }
}
