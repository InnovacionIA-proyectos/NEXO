using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Domain.Priorizacion.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Priorizacion.Mappers;

public static class ParametrosScoreMappers
{
    public static ParametrosScoreDto ADto(this ParametrosScore p) =>
        new(p.PesoUrgencia, p.PesoValor, p.PesoProbabilidad, p.PesoTipo,
            p.HorizonteHoras, p.ValorReferencia,
            p.PuntajeLicitacion, p.PuntajeSpot,
            p.ValorPorDefectoSinValor, p.ProbabilidadPorDefecto);

    /// <summary>
    /// Copia el DTO sobre la entidad con seguimiento.
    ///
    /// Se copia campo por campo sobre la fila existente en vez de reemplazarla: la fila
    /// es única (Id = 1) y tiene token de concurrencia. Cambiar la instancia perdería
    /// el token y dos recalibraciones a la vez se pisarían en silencio.
    /// </summary>
    public static void Copiar(this ParametrosScoreDto d, ParametrosScore destino)
    {
        destino.PesoUrgencia = d.PesoUrgencia;
        destino.PesoValor = d.PesoValor;
        destino.PesoProbabilidad = d.PesoProbabilidad;
        destino.PesoTipo = d.PesoTipo;
        destino.HorizonteHoras = d.HorizonteHoras;
        destino.ValorReferencia = d.ValorReferencia;
        destino.PuntajeLicitacion = d.PuntajeLicitacion;
        destino.PuntajeSpot = d.PuntajeSpot;
        destino.ValorPorDefectoSinValor = d.ValorPorDefectoSinValor;
        destino.ProbabilidadPorDefecto = d.ProbabilidadPorDefecto;
    }
}
