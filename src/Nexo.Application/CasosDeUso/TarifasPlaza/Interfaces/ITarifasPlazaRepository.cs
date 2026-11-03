using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;

public interface ITarifasPlazaRepository
{
    /// <summary>Con Origen/Destino/Peso ya incluidos (para resolver zona y rango sin otra consulta).</summary>
    Task<IReadOnlyList<clsTarifaPlaza>> ListarAsync(CancellationToken ct);

    Task<clsTarifaPlaza?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>Para validar duplicados al crear/editar por las 7 dimensiones; <paramref name="excluirId"/>
    /// se pasa al editar, para no chocar contra la fila que se está editando.</summary>
    Task<bool> ExisteAsync(
        string lineaNegocio, string claseViaje, int origenMunicipioId, int destinoMunicipioId,
        string? claseContenedor, int? pesoId, string? tipoVehiculo, Guid? excluirId, CancellationToken ct);

    void Agregar(clsTarifaPlaza tarifa);

    /// <summary>Elimina por Id; devuelve cuántas filas borró (0 o 1).</summary>
    Task<int> EliminarAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Carga masiva desde Excel: upsert por las 7 dimensiones (última fila del archivo gana
    /// si se repite), con reporte de error por fila (tope 20 mensajes) — mismo patrón que
    /// el importador de TarifasSicetacAdmin (retirado en la Issue #179, el código de
    /// referencia queda en el historial de git).
    /// </summary>
    Task<clsImportarTarifasPlazaResultDto> ImportarExcelAsync(Stream excel, Guid? usuarioId, CancellationToken ct);
}
