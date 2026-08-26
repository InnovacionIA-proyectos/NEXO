using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Tests.Trabajos;

/// <summary>
/// El aviso en vivo y la consulta de respaldo tienen que decir lo mismo.
///
/// La forma de garantizarlo no es escribirlos parecidos, es que el aviso se CONSTRUYA
/// desde el estado que devuelve la consulta. Estas pruebas fijan esa construcción: si
/// alguien agrega un campo al estado y se olvida del aviso, o al revés, aquí se ve.
///
/// Lo que el aviso NO lleva es la lista de pendientes, y es deliberado. Un pliego de
/// 300 líneas con 50 pendientes pesa entre 12 y 25 KB por aviso; multiplicado por los
/// 300 avisos del pliego son varios megas por cliente conectado. Con solo los
/// contadores son unos 200 bytes. El aviso mueve la barra de progreso; el detalle lo
/// trae la consulta cuando alguien lo necesita.
/// </summary>
public class AvisoDeTrabajoTests
{
    private static EstadoTrabajoDto Estado(
        string estado = "Procesando", int? total = 300, int resueltas = 187,
        int tecnicas = 1, int manuales = 2, int pendientesEnLista = 3) =>
        new(
            Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
            estado,
            new AvanceTrabajoDto(total, resueltas, tecnicas + manuales, tecnicas, manuales),
            new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero),
            estado == "Procesando" ? null : new DateTimeOffset(2026, 8, 25, 12, 30, 0, TimeSpan.Zero),
            Enumerable.Range(0, pendientesEnLista)
                .Select(i => new PendienteDeCosteoDto(
                    Guid.NewGuid(), TipoPendienteDeCosteo.Manual, $"Motivo {i}", DateTimeOffset.UtcNow))
                .ToList());

    [Fact]
    public void El_aviso_repite_el_estado_y_el_avance_de_la_consulta()
    {
        var estado = Estado();

        var aviso = AvisoDeTrabajo.Desde(estado);

        Assert.Equal(estado.TrabajoId, aviso.TrabajoId);
        Assert.Equal(estado.Estado, aviso.Estado);
        Assert.Equal(estado.Avance, aviso.Avance);
        Assert.Equal(estado.TerminadoEn, aviso.TerminadoEn);
    }

    [Fact]
    public void El_aviso_NO_lleva_la_lista_de_pendientes()
    {
        var estado = Estado(pendientesEnLista: 50);

        var aviso = AvisoDeTrabajo.Desde(estado);

        // Si algún día alguien agrega la lista al aviso, esta prueba lo frena: son
        // varios megas por pliego y por cliente, y el detalle ya lo da la consulta.
        var propiedades = typeof(AvisoDeTrabajo).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Pendientes", propiedades);
    }

    [Fact]
    public void Un_trabajo_terminado_avisa_su_fecha_de_fin()
    {
        var aviso = AvisoDeTrabajo.Desde(Estado(estado: "Parcial"));

        Assert.Equal("Parcial", aviso.Estado);
        Assert.NotNull(aviso.TerminadoEn);
    }

    [Fact]
    public void Antes_del_reparto_el_total_viaja_nulo_y_no_en_cero()
    {
        // Cero y "todavía no se sabe" son cosas distintas: con cero, una barra de
        // progreso mostraría 0 de 0 y parecería terminada.
        var aviso = AvisoDeTrabajo.Desde(Estado(total: null, resueltas: 0, tecnicas: 0, manuales: 0));

        Assert.Null(aviso.Avance.Total);
    }
}
