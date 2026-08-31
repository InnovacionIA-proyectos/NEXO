using CabinaFletes.Application.CasosDeUso.Biblioteca.DTOs;
using CabinaFletes.Application.Common.Dtos;

namespace CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>
/// S2-HU-09.1 (#31): contrato de la conexión SAP NUEVA — la tarifa de plaza en vivo
/// (equivalente en tiempo real a <c>getConsultarTarifaFlete</c>), que hoy NO existe
/// (ni real ni como réplica): "el equipo de conexiones" todavía no la construye. NO
/// confundir con <see cref="ITarifarioSapPort"/>, que sigue siendo la fuente REAL de
/// plaza (réplica de <c>DbSolucionesTi</c>) y no se toca — esta HU solo mockea lo NUEVO.
///
/// CONTRATO DE DATOS (obligatorio documentar por el criterio de cierre de #31):
///
/// Entrada — reutiliza <see cref="ConsultaTarifarioSap"/> (ya documentado, ya es la
/// forma que consume el resto del motor de propuesta): Origen/Destino (nombres de
/// ciudad, texto libre normalizado por quien implementa), LineaNegocio/ClaseViaje/
/// ClaseContenedor (catálogo SAP TM local), ConfigVehiculo/Carroceria (texto local),
/// Peso (kg, puede ser null), Exclusivo/Consolidado/RetornaContenedor/CiudadDevolucion.
///
/// Salida — <see cref="CandidatoTarifaDto"/>:
///   - <c>Fuente</c>: SIEMPRE un valor que empiece por <c>"sap_en_vivo"</c> (nunca
///     <c>"plaza"</c> a secas, que es el tag de la fuente real vía DbSolucionesTi) —
///     así la trazabilidad de origen (#33) nunca confunde una propuesta con datos
///     provisionales con una de la fuente real.
///   - <c>Valor</c>: tarifa de plaza en COP, ya neta (sin impuestos), positiva.
///   - <c>Detalle</c>: descripción legible para mostrar al usuario (ruta, vehículo,
///     vigencia).
///   - <c>Criterio</c>: qué se usó para resolver (para depurar, no se muestra).
///   - <c>Coincidencias</c>: cuántos candidatos había antes de elegir el que se
///     devuelve (1 si el adaptador no distingue).
///
/// Vigencia: a diferencia de <c>TarifasPlazaSap</c> (que tiene una columna de vigencia
/// explícita y ajustable, ver AjustesVigenciaPlaza), esta consulta es SIEMPRE
/// "al momento" — no hay concepto de tarifa vencida en una llamada en vivo. Un
/// adaptador real que la implemente no necesita filtrar por vigencia: la respuesta ES
/// la vigente ahora.
///
/// Null: sin candidato para esa ruta/vehículo (no es un error, es "no hay dato"),
/// exactamente igual semántica que <see cref="ITarifarioSapPort"/>.
///
/// SUSTITUCIÓN FUTURA (criterio central de la HU): el día que "el equipo de
/// conexiones" construya la integración real, se agrega un adaptador nuevo que
/// implemente esta MISMA interfaz y se cambia UNA línea en DependencyInjection.cs
/// (configuración `SapTarifaFleteEnVivo:Modo`). Ni <c>ReglaDeCosteo</c> ni
/// <c>PropuestaTarifaAdapter</c> ni ninguna pantalla se enteran.
/// </summary>
public interface ISapTarifaFleteEnVivoPort
{
    /// <summary>False cuando el modo es "Deshabilitado" (por defecto) o "Real" sin
    /// adaptador real todavía disponible — degrada, nunca falla.</summary>
    bool Configurado { get; }

    Task<CandidatoTarifaDto?> ConsultarPlazaAsync(ConsultaTarifarioSap consulta, CancellationToken ct = default);
}
