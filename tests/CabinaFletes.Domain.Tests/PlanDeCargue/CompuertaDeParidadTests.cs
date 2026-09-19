using CabinaFletes.Domain.PlanDeCargue.Reglas;

namespace CabinaFletes.Domain.Tests.PlanDeCargue;

public class CompuertaDeParidadTests
{
    private static readonly DateOnly UnaFecha = new(2026, 8, 27);

    [Fact]
    public void Con_los_tres_campos_la_compuerta_esta_habilitada()
    {
        Assert.True(CompuertaDeParidad.EstaHabilitada(true, "cubicador-ci.yml run #123, 26/26", UnaFecha));
    }

    [Fact]
    public void Sin_verificar_la_compuerta_esta_cerrada_aunque_haya_evidencia()
    {
        Assert.False(CompuertaDeParidad.EstaHabilitada(false, "cubicador-ci.yml run #123, 26/26", UnaFecha));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verificada_true_sin_evidencia_concreta_no_habilita_nada(string? evidencia)
    {
        // Es exactamente lo que pide la #30: no es un booleano suelto sin control. Alguien
        // no puede simplemente poner "true" y listo.
        Assert.False(CompuertaDeParidad.EstaHabilitada(true, evidencia, UnaFecha));
    }

    [Fact]
    public void Verificada_true_con_evidencia_pero_sin_fecha_no_habilita_nada()
    {
        Assert.False(CompuertaDeParidad.EstaHabilitada(true, "cubicador-ci.yml run #123, 26/26", null));
    }
}
