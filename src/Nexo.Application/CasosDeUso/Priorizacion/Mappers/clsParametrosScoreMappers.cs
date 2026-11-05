using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Domain.Priorizacion.Entidades;

namespace Nexo.Application.CasosDeUso.Priorizacion.Mappers;

public static class clsParametrosScoreMappers
{
    public static clsParametrosScoreDto ADto(this clsParametrosScore p) =>
        new(p.PesoUrgencia, p.PesoValor, p.PesoProbabilidad, p.PesoTipo,
            p.HorizonteHoras, p.ValorReferencia,
            p.PuntajeLicitacion, p.PuntajeSpot,
            p.ValorPorDefectoSinValor, p.ProbabilidadPorDefecto);

    /// <summary>
    /// Copia campo por campo sobre la fila existente: es única (Id = 1) y lleva token de concurrencia; reemplazar la instancia lo perdería.
    /// </summary>
    public static void Copiar(this clsParametrosScoreDto d, clsParametrosScore destino)
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
