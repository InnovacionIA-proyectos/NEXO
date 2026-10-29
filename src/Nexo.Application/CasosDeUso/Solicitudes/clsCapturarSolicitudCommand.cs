using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Validadores;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Bandeja;
using Nexo.Domain.Priorizacion.Reglas;
using Nexo.Domain.Solicitudes.Entidades;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.Solicitudes;

public sealed record clsCapturaLineaInput(
    string Origen, string Destino, string? TipoVehiculo, string? Carroceria, string? Tipologia,
    string? LineaNegocio, string? ClaseViaje, string? ClaseContenedor, bool? Exclusivo, bool? Consolidado,
    bool? RetornaContenedor, string? CiudadDevolucionContenedor, bool? LevanteContenedorDiferente,
    string? CiudadLevanteContenedor, decimal? ValorAprovisionamiento, decimal? Peso, decimal? ValorMercancia,
    IReadOnlyList<clsPosicionCargaDto>? Posiciones,
    /// <summary>S4-HU-15.5/#251: horas de ESTA línea — null usa el default
    /// (clsResolucionHorasLinea.Resolver: 1h+1h si la línea ES una devolución de contenedor, si no las de la
    /// solicitud completa; #300: "retorna contenedor" ya no las cambia).</summary>
    decimal? HorasCargue = null, decimal? HorasDescargue = null,
    /// <summary>S5-HU-16.1/#254: Id de tbl_municipios cuando Origen/Destino se escogió
    /// desde el buscador de ciudades — null si viene de texto libre (formulario viejo,
    /// importación de Excel sin ese dato).</summary>
    int? OrigenMunicipioId = null, int? DestinoMunicipioId = null,
    /// <summary>S5-HU-18.3/#275: id de ruta que Cotifly usa para agrupar las líneas de una
    /// misma carga heterogénea — lo pone SIEMPRE Cotifly, nunca NEXO.</summary>
    Guid? GrupoRutaId = null,
    /// <summary>S5-HU-18.2/#274: id de la recomendación del Cubicador usada para capturar
    /// esta línea — null si se capturó sin pasar por el Cubicador.</summary>
    string? IdRecomendacionVehiculo = null);

public sealed record clsCapturarSolicitudCommand(
    Guid? ClienteId, string? ClienteBp, string Tipo, string? Modalidad, DateTimeOffset? Deadline,
    decimal? ValorEstimado, string? Referencia, decimal? HorasCargue, decimal? HorasDescargue,
    IReadOnlyList<clsCapturaLineaInput> Lineas,
    /// <summary>Notas u observaciones comerciales (S4-HU-18.10/#227) — distinto de
    /// <see cref="Referencia"/> (referencia/PO del cliente).</summary>
    string? Notas = null) : ICommand<clsCapturaResultDto>;

/// <summary>
/// Captura de necesidades del comercial (licitación o spot): resuelve/crea clsCliente,
/// clsRuta y clsConfiguracionVehiculo desde texto libre, y dispara el auto-costeo con la
/// Biblioteca (vía IPropuestaTarifaPort, ya migrada). Portado 1:1 desde
/// Nexo.Api/Services/CapturaSolicitudService.cs (método CapturarAsync +
/// AutoCostearCoreAsync) del proyecto original.
///
/// Compromiso de diseño: para cargues masivos (más de 10 líneas) el auto-costeo se
/// difiere a un Task.Run con su propio scope de DI (la petición HTTP ya respondió).
/// Esto obliga a este Handler a depender de IServiceScopeFactory — una abstracción
/// de Microsoft.Extensions.DependencyInjection, no de ASP.NET Core ni de EF Core —
/// igual que hacía el service original: es una excepción deliberada a
/// "Application no depende de infraestructura".
/// </summary>
public sealed class clsCapturarSolicitudCommandHandler(
    ISolicitudesRepository solicitudes, IRutasCatalogoPort rutasCatalogo,
    IConfiguracionesVehiculoPort vehiculosCatalogo, IAliasCatalogoPort aliasCatalogo, IClientesPort clientes, IClientesDwPort clientesDw,
    IPropuestaTarifaPort propuestas, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual,
    TimeProvider clock, IPublicadorDeMensajes publicador, IUnitOfWork uow, IServiceScopeFactory scopes)
    : ICommandHandler<clsCapturarSolicitudCommand, clsCapturaResultDto>
{
    private const int UmbralCosteoDiferido = 10;

    public async Task<clsCapturaResultDto> Handle(clsCapturarSolicitudCommand command, CancellationToken ct = default)
    {
        clsCapturarSolicitudValidador.Validar(command);

        var clienteId = await clsResolucionDeCliente.ResolverAsync(command.ClienteId, command.ClienteBp, clientes, clientesDw, ct);

        var rutas = await rutasCatalogo.ListarAsync(ct);
        var rutasPorClave = rutas.GroupBy(r => clsResolucionDeCatalogos.ClaveRuta(r.Origen, r.Destino)).ToDictionary(g => g.Key, g => g.First());

        var vehiculos = await vehiculosCatalogo.ListarAsync(ct);
        var vehiculosPorClave = clsResolucionDeCatalogos.IndexarVehiculosPorClave(vehiculos);
        var aliasVehiculo = await aliasCatalogo.MapaActivoAsync(clsAliasElemento.TipoVehiculo, ct);
        var aliasContenedor = await aliasCatalogo.MapaActivoAsync(clsAliasElemento.TipoContenedor, ct);

        var rutasCreadas = 0;
        var vehiculosCreados = 0;

        var solicitud = new clsSolicitud
        {
            ClienteId = clienteId,
            Tipo = Enum.Parse<TipoSolicitud>(command.Tipo, ignoreCase: true),
            Modalidad = ParseModalidad(command.Modalidad),
            Deadline = command.Deadline,
            ValorEstimado = command.ValorEstimado,
            CondicionesServicio = SerializarCondiciones(command),
            Estado = EstadoSolicitud.Pendiente,
            CreadoPor = usuarioActual.Id,
        };

        // S4-HU-15.5/#251: default de la solicitud completa, usado solo cuando una línea
        // no trae horas propias ni devuelve contenedor — mismo valor que ya serializa
        // SerializarCondiciones (4h+4h), no uno nuevo.
        var horasCargueSolicitud = command.HorasCargue ?? 4m;
        var horasDescargueSolicitud = command.HorasDescargue ?? 4m;

        var numeroLinea = 0;
        foreach (var lr in command.Lineas)
        {
            numeroLinea++;
            var peso = clsCapturarSolicitudValidador.PesoResuelto(lr);

            var (ruta, rutaCreada) = clsResolucionDeCatalogos.ResolverRuta(
                lr.Origen, lr.Destino, rutasPorClave, rutasCatalogo, lr.OrigenMunicipioId, lr.DestinoMunicipioId);
            if (rutaCreada) rutasCreadas++;

            var (vehiculo, vehiculoCreado) = clsResolucionDeCatalogos.ResolverVehiculo(lr.TipoVehiculo, lr.Carroceria, vehiculosPorClave, vehiculosCatalogo, aliasVehiculo);
            if (vehiculoCreado) vehiculosCreados++;

            var ciudadLevante = clsResolucionDeCatalogos.Limpiar(lr.CiudadLevanteContenedor);
            var levanteDiferente = ciudadLevante is not null && clsResolucionDeCatalogos.Normalizar(ciudadLevante) != clsResolucionDeCatalogos.Normalizar(lr.Origen);
            if (!levanteDiferente) ciudadLevante = null;

            var ciudadDevolucion = clsResolucionDeCatalogos.Limpiar(lr.CiudadDevolucionContenedor);
            var retornaContenedor = (lr.RetornaContenedor ?? false) || ciudadDevolucion is not null;
            // #300: solo una línea que ES una devolución de contenedor arranca con 1h+1h. "Retorna contenedor"
            // de un viaje cargado ya no toca las horas de la línea (RN-03): el tramo vacío de retorno se calcula
            // aparte, con sus propias 1h+1h (clsPropuestaTarifaAdapter).
            var lineaNegocio = clsLineasDeNegocio.Reconocer(lr.LineaNegocio);
            if (lineaNegocio is null && !string.IsNullOrWhiteSpace(lr.LineaNegocio))
                throw new clsValidationFailedException("LINEA_NEGOCIO_INVALIDO",
                    $"Línea {numeroLinea}: la línea de negocio «{lr.LineaNegocio.Trim()}» no es válida. Usa: {clsLineasDeNegocio.NombresSeparados}.");
            var (horasCargue, horasDescargue) = clsResolucionHorasLinea.Resolver(
                lr.HorasCargue, lr.HorasDescargue, lineaNegocio == LineaNegocio.DevolucionContenedor,
                horasCargueSolicitud, horasDescargueSolicitud);

            solicitud.Lineas.Add(new clsLineaSolicitud
            {
                SolicitudId = solicitud.Id,
                RutaId = ruta.Id,
                clsRuta = ruta,
                ConfigVehiculoId = vehiculo?.Id,
                clsConfiguracionVehiculo = vehiculo,
                Tipologia = ParseTipologia(lr.Tipologia),
                LineaNegocio = lineaNegocio,
                ClaseViaje = ParseClaseViaje(lr.ClaseViaje),
                ClaseContenedor = clsResolucionDeCatalogos.ResolverClaseContenedor(lr.ClaseContenedor, aliasContenedor),
                Exclusivo = lr.Exclusivo ?? false,
                Consolidado = lr.Consolidado ?? false,
                RetornaContenedor = retornaContenedor,
                CiudadDevolucionContenedor = ciudadDevolucion,
                LevanteContenedorDiferente = (lr.LevanteContenedorDiferente ?? false) || levanteDiferente,
                CiudadLevanteContenedor = ciudadLevante,
                ValorAprovisionamiento = levanteDiferente ? lr.ValorAprovisionamiento : null,
                HorasCargue = horasCargue,
                HorasDescargue = horasDescargue,
                Peso = peso,
                ValorMercancia = lr.ValorMercancia,
                Dimensiones = lr.Posiciones is { Count: > 0 }
                    ? System.Text.Json.JsonSerializer.Serialize(lr.Posiciones, clsPosicionCargaDto.Json)
                    : null,
                GrupoRutaId = lr.GrupoRutaId,
                NumeroLinea = numeroLinea,
                IdRecomendacionVehiculo = lr.IdRecomendacionVehiculo,
            });
        }

        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        solicitud.ScorePrioridad = (decimal)clsScorePrioridadCalculator.Calcular(solicitud, parametros, clock.GetUtcNow()).Total;

        solicitudes.Agregar(solicitud);
        auditoria.Registrar(usuarioActual.Id, "solicitudes", solicitud.Id, "captura",
            $"{{\"tipo\":\"{solicitud.Tipo}\",\"lineas\":{solicitud.Lineas.Count},\"rutasCreadas\":{rutasCreadas},\"vehiculosCreados\":{vehiculosCreados}}}");

        // La bandeja tiene una fila más. Se avisa UNA vez por captura, no una por línea:
        // lo que cambia es que entró la solicitud, no cuántas líneas trae.
        await publicador.PublicarAsync(
            new clsBandejaCambio(clsMotivoDeCambio.SolicitudNueva, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);

        if (solicitud.Lineas.Count > UmbralCosteoDiferido)
        {
            LanzarAutoCosteoEnSegundoPlano(solicitud.Id, usuarioActual.Id);
            return new clsCapturaResultDto(
                solicitud.Id, solicitud.Lineas.Count, rutasCreadas, vehiculosCreados, solicitud.ScorePrioridad,
                0, solicitud.Lineas.Count, CosteoEnProceso: true);
        }

        var (fletes, porCostear) = await AutoCostearAsync(solicitudes, propuestas, auditoria, uow, solicitud, usuarioActual.Id, ct);
        return new clsCapturaResultDto(
            solicitud.Id, solicitud.Lineas.Count, rutasCreadas, vehiculosCreados, solicitud.ScorePrioridad,
            fletes, porCostear, CosteoEnProceso: false);
    }

    /// <summary>
    /// Auto-costeo al capturar: precarga como flete el VALOR MÁS ALTO propuesto por
    /// la Biblioteca. Best-effort: si la Biblioteca no responde, la captura no falla
    /// y todo queda por costear (revisión manual del Administrador de Fletes).
    /// Estático y con SOLO las dependencias que necesita (repo/puerto de propuestas/
    /// auditoría/UoW): así el trabajador en segundo plano puede resolverlas desde su
    /// PROPIO scope de DI sin arrastrar los demás puertos del Handler (que viven en
    /// el scope de la petición HTTP ya terminada — usarlos causaría
    /// ObjectDisposedException sobre el DbContext).
    /// </summary>
    private static async Task<(int Fletes, int PorCostear)> AutoCostearAsync(
        ISolicitudesRepository solicitudesRepo, IPropuestaTarifaPort propuestasPort, IAuditoriaPort auditoriaPort,
        IUnitOfWork unitOfWork, clsSolicitud solicitud, Guid usuarioId, CancellationToken ct)
    {
        try
        {
            var propuesta = await propuestasPort.ProponerAsync(solicitud.Id, ct);
            var cotizacion = new clsCotizacion { SolicitudId = solicitud.Id, Estado = EstadoCotizacion.Borrador, EsAutoCosteo = true };
            var fletes = 0;

            foreach (var linea in solicitud.Lineas)
            {
                var p = propuesta.Lineas.FirstOrDefault(x => x.LineaId == linea.Id);

                // El flete se guarda solo cuando hay un valor propuesto y ningún motivo de pendiente, sea cual sea el camino de
                // decisión (solo SICE-TAC, plaza que gobierna sola o proceso completo con su valor recomendado).
                // Aprovisionamiento y retorno del contenedor salen de la propuesta (tramos vacíos de SICE-TAC), no de lo que
                // mande el cliente.
                if (p is { Autocosteable: true, ValorPropuesto: { } valor })
                {
                    cotizacion.Lineas.Add(new clsCotizacionLinea
                    {
                        CotizacionId = cotizacion.Id,
                        LineaSolicitudId = linea.Id,
                        ValorFlete = valor,
                        ValorAprovisionamiento = p.AprovisionamientoContenedor?.Valor,
                        ValorRetornoContenedor = p.RetornoContenedor?.Valor,
                        Fuente = p.FuentePropuesta switch
                        {
                            "sicetac" => FuenteFlete.Sicetac,
                            "plaza" => FuenteFlete.Tarifario,
                            "historico_sap" => FuenteFlete.Historica,
                            _ => FuenteFlete.Manual,
                        },
                    });
                    linea.ValorAprovisionamiento = p.AprovisionamientoContenedor?.Valor;
                    linea.EstadoLinea = EstadoLinea.AutoResuelta;
                    linea.MotivoPendiente = null;
                    fletes++;
                }
                else if (p is not null)
                {
                    // Pendiente: sin ningún valor propuesto, o con un motivo que impide guardarlo solo (línea de Cupo sin
                    // vehículo, SICE-TAC no vigente, tramo vacío sin piso). Si hay un valor recomendado se deja a la vista.
                    linea.EstadoLinea = EstadoLinea.PorCostear;
                    if (linea.LevanteContenedorDiferente) linea.ValorAprovisionamiento = p.AprovisionamientoContenedor?.Valor;
                    linea.MotivoPendiente = p.MotivoPendiente ?? (p.ValorPropuesto is { } recomendado
                        ? $"El Administrador de Tarifas debe validar la tarifa (recomendado: {clsMoneda.Pesos(recomendado)} - {p.FuentePropuesta})."
                        : "Ninguna fuente trae un valor para la línea: el Administrador de Tarifas debe ingresar la tarifa.");
                }
                else
                {
                    // Toda línea de la solicitud tiene su propuesta; esta rama es una red de seguridad.
                    linea.EstadoLinea = EstadoLinea.PorCostear;
                    linea.MotivoPendiente = "No se pudo consultar la Biblioteca para esta línea.";
                }
            }

            if (fletes > 0)
            {
                cotizacion.Total = cotizacion.Lineas.Sum(l => l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0));
                solicitudesRepo.AgregarCotizacion(cotizacion);
                auditoriaPort.Registrar(usuarioId, "cotizaciones", cotizacion.Id, "auto_costeo",
                    $"{{\"fletes\":{fletes},\"total\":{cotizacion.Total}}}");
            }
            await unitOfWork.SaveChangesAsync(ct);
            return (fletes, solicitud.Lineas.Count - fletes);
        }
        catch (Exception ex)
        {
            // Última red de seguridad: solo se llega aquí si ProponerAsync falla ANTES de
            // devolver nada (p. ej. la propia clsSolicitud no se pudo leer), no por una fuente
            // caída en una línea puntual — eso ya lo aísla ProponerAsync internamente
            // (ver clsPropuestaTarifaAdapter.ProponerAsync, por-grupo). Aun así se deja un
            // motivo explícito en vez de un PorCostear mudo.
            foreach (var linea in solicitud.Lineas)
                linea.MotivoPendiente = $"No se pudo consultar la Biblioteca: {ex.Message}";
            return (0, solicitud.Lineas.Count);
        }
    }

    /// <summary>
    /// Cargues masivos: el auto-costeo tomaría minutos (consulta los DW línea a
    /// línea), así que corre en segundo plano con su PROPIO scope de DI (la
    /// petición HTTP ya respondió, y el scope de este Handler se dispone con ella).
    /// Fire-and-forget deliberado, igual que el original (Task.Run sin awaitear).
    /// </summary>
    private void LanzarAutoCosteoEnSegundoPlano(Guid solicitudId, Guid usuarioId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ISolicitudesRepository>();
                var props = scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();
                var audit = scope.ServiceProvider.GetRequiredService<IAuditoriaPort>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var solicitud = await repo.ObtenerConLineasAsync(solicitudId, CancellationToken.None);
                if (solicitud is null) return;

                await AutoCostearAsync(repo, props, audit, unitOfWork, solicitud, usuarioId, CancellationToken.None);
            }
            catch (Exception)
            {
                // Best-effort en segundo plano: sin logger inyectable aquí (Application no
                // depende de Microsoft.Extensions.Logging por ahora); las líneas quedan
                // PorCostear y se atienden manualmente si esto falla.
            }
        });
    }


    // ------------------------------------------------------------- helpers


    private static string SerializarCondiciones(clsCapturarSolicitudCommand command)
    {
        var condiciones = new clsCondicionesServicioDto(
            string.IsNullOrWhiteSpace(command.Referencia) ? null : command.Referencia.Trim(),
            new clsTiemposLogisticosDto(command.HorasCargue ?? 4m, command.HorasDescargue ?? 4m),
            Notas: string.IsNullOrWhiteSpace(command.Notas) ? null : command.Notas.Trim());
        return System.Text.Json.JsonSerializer.Serialize(condiciones, clsCondicionesServicioDto.Json);
    }

    // El validador ya garantizó que parsea; aquí solo se convierte.
    private static ModalidadSolicitud? ParseModalidad(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : Enum.Parse<ModalidadSolicitud>(valor, ignoreCase: true);

    private static TipologiaCarga? ParseTipologia(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = clsResolucionDeCatalogos.SinTildes(valor).Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "");
        foreach (var t in Enum.GetValues<TipologiaCarga>())
            if (normalizado.Contains(t.ToString(), StringComparison.OrdinalIgnoreCase))
                return t;
        return null;
    }

    private static ClaseViaje? ParseClaseViaje(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var n = clsResolucionDeCatalogos.SinTildes(valor).Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "").ToUpperInvariant();
        if (n.Contains("EXTRADIMENSION")) return ClaseViaje.Extradimensionado;
        if (n.Contains("CONTENEDOR")) return ClaseViaje.Contenedor;
        if (n.Contains("CUPO")) return ClaseViaje.Cupo;
        if (n.Contains("SUELTA")) return ClaseViaje.CargaSuelta;
        return null;
    }
}
