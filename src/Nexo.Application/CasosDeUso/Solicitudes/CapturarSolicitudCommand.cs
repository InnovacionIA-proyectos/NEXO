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
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Bandeja;
using Nexo.Domain.Priorizacion.Reglas;
using Nexo.Domain.Solicitudes.Entidades;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Solicitudes;

public sealed record CapturaLineaInput(
    string Origen, string Destino, string? TipoVehiculo, string? Carroceria, string? Tipologia,
    string? LineaNegocio, string? ClaseViaje, string? ClaseContenedor, bool? Exclusivo, bool? Consolidado,
    bool? RetornaContenedor, string? CiudadDevolucionContenedor, bool? LevanteContenedorDiferente,
    string? CiudadLevanteContenedor, decimal? ValorAprovisionamiento, decimal? Peso, decimal? ValorMercancia,
    IReadOnlyList<PosicionCargaDto>? Posiciones);

public sealed record CapturarSolicitudCommand(
    Guid? ClienteId, string? ClienteBp, string Tipo, string? Modalidad, DateTimeOffset? Deadline,
    decimal? ValorEstimado, string? Referencia, decimal? HorasCargue, decimal? HorasDescargue,
    IReadOnlyList<CapturaLineaInput> Lineas) : ICommand<CapturaResultDto>;

/// <summary>
/// Captura de necesidades del comercial (licitación o spot): resuelve/crea Cliente,
/// Ruta y ConfiguracionVehiculo desde texto libre, y dispara el auto-costeo con la
/// Biblioteca (vía IPropuestaTarifaPort, ya migrada). Portado 1:1 desde
/// Nexo.Api/Services/CapturaSolicitudService.cs (método CapturarAsync +
/// AutoCostearCoreAsync) del proyecto original.
///
/// Compromiso de diseño: para cargues masivos (más de 10 líneas) el auto-costeo se
/// difiere a un Task.Run con su propio scope de DI (la petición HTTP ya respondió).
/// Esto obliga a este Handler a depender de IServiceScopeFactory — una abstracción
/// de Microsoft.Extensions.DependencyInjection, no de ASP.NET Core ni de EF Core —
/// igual que hacía el service original. Se documenta aquí y en MIGRACION.md porque
/// es una excepción deliberada a "Application no depende de infraestructura".
/// </summary>
public sealed class CapturarSolicitudCommandHandler(
    ISolicitudesRepository solicitudes, IRutasCatalogoPort rutasCatalogo,
    IConfiguracionesVehiculoPort vehiculosCatalogo, IClientesPort clientes, IClientesDwPort clientesDw,
    IPropuestaTarifaPort propuestas, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual,
    TimeProvider clock, IPublicadorDeMensajes publicador, IUnitOfWork uow, IServiceScopeFactory scopes)
    : ICommandHandler<CapturarSolicitudCommand, CapturaResultDto>
{
    private const int UmbralCosteoDiferido = 10;

    public async Task<CapturaResultDto> Handle(CapturarSolicitudCommand command, CancellationToken ct = default)
    {
        CapturarSolicitudValidador.Validar(command);

        var clienteId = await ResolverClienteAsync(command, ct);

        var rutas = await rutasCatalogo.ListarAsync(ct);
        var rutasPorClave = rutas.GroupBy(r => ResolucionDeCatalogos.ClaveRuta(r.Origen, r.Destino)).ToDictionary(g => g.Key, g => g.First());

        var vehiculos = await vehiculosCatalogo.ListarAsync(ct);
        var vehiculosPorClave = vehiculos.GroupBy(v => ResolucionDeCatalogos.ClaveVehiculo(v.TipoVehiculo, v.Carroceria)).ToDictionary(g => g.Key, g => g.First());

        var rutasCreadas = 0;
        var vehiculosCreados = 0;

        var solicitud = new Solicitud
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

        var numeroLinea = 0;
        foreach (var lr in command.Lineas)
        {
            numeroLinea++;
            var peso = CapturarSolicitudValidador.PesoResuelto(lr);

            var (ruta, rutaCreada) = ResolucionDeCatalogos.ResolverRuta(lr.Origen, lr.Destino, rutasPorClave, rutasCatalogo);
            if (rutaCreada) rutasCreadas++;

            var (vehiculo, vehiculoCreado) = ResolucionDeCatalogos.ResolverVehiculo(lr.TipoVehiculo, lr.Carroceria, vehiculosPorClave, vehiculosCatalogo);
            if (vehiculoCreado) vehiculosCreados++;

            var ciudadLevante = ResolucionDeCatalogos.Limpiar(lr.CiudadLevanteContenedor);
            var levanteDiferente = ciudadLevante is not null && ResolucionDeCatalogos.Normalizar(ciudadLevante) != ResolucionDeCatalogos.Normalizar(lr.Origen);
            if (!levanteDiferente) ciudadLevante = null;

            var ciudadDevolucion = ResolucionDeCatalogos.Limpiar(lr.CiudadDevolucionContenedor);

            solicitud.Lineas.Add(new LineaSolicitud
            {
                SolicitudId = solicitud.Id,
                RutaId = ruta.Id,
                Ruta = ruta,
                ConfigVehiculoId = vehiculo?.Id,
                ConfiguracionVehiculo = vehiculo,
                Tipologia = ParseTipologia(lr.Tipologia),
                LineaNegocio = ParseLineaNegocio(lr.LineaNegocio),
                ClaseViaje = ParseClaseViaje(lr.ClaseViaje),
                ClaseContenedor = ParseClaseContenedor(lr.ClaseContenedor),
                Exclusivo = lr.Exclusivo ?? false,
                Consolidado = lr.Consolidado ?? false,
                RetornaContenedor = (lr.RetornaContenedor ?? false) || ciudadDevolucion is not null,
                CiudadDevolucionContenedor = ciudadDevolucion,
                LevanteContenedorDiferente = (lr.LevanteContenedorDiferente ?? false) || levanteDiferente,
                CiudadLevanteContenedor = ciudadLevante,
                ValorAprovisionamiento = levanteDiferente ? lr.ValorAprovisionamiento : null,
                Peso = peso,
                ValorMercancia = lr.ValorMercancia,
                Dimensiones = lr.Posiciones is { Count: > 0 }
                    ? System.Text.Json.JsonSerializer.Serialize(lr.Posiciones, PosicionCargaDto.Json)
                    : null,
            });
        }

        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        solicitud.ScorePrioridad = (decimal)ScorePrioridadCalculator.Calcular(solicitud, parametros, clock.GetUtcNow()).Total;

        solicitudes.Agregar(solicitud);
        auditoria.Registrar(usuarioActual.Id, "solicitudes", solicitud.Id, "captura",
            $"{{\"tipo\":\"{solicitud.Tipo}\",\"lineas\":{solicitud.Lineas.Count},\"rutasCreadas\":{rutasCreadas},\"vehiculosCreados\":{vehiculosCreados}}}");

        // La bandeja tiene una fila más. Se avisa UNA vez por captura, no una por línea:
        // lo que cambia es que entró la solicitud, no cuántas líneas trae.
        await publicador.PublicarAsync(
            new BandejaCambio(MotivoDeCambio.SolicitudNueva, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);

        if (solicitud.Lineas.Count > UmbralCosteoDiferido)
        {
            LanzarAutoCosteoEnSegundoPlano(solicitud.Id, usuarioActual.Id);
            return new CapturaResultDto(
                solicitud.Id, solicitud.Lineas.Count, rutasCreadas, vehiculosCreados, solicitud.ScorePrioridad,
                0, solicitud.Lineas.Count, CosteoEnProceso: true);
        }

        var (fletes, porCostear) = await AutoCostearAsync(solicitudes, propuestas, auditoria, uow, solicitud, usuarioActual.Id, ct);
        return new CapturaResultDto(
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
        IUnitOfWork unitOfWork, Solicitud solicitud, Guid usuarioId, CancellationToken ct)
    {
        try
        {
            var propuesta = await propuestasPort.ProponerAsync(solicitud.Id, ct);
            var cotizacion = new Cotizacion { SolicitudId = solicitud.Id, Estado = EstadoCotizacion.Borrador };
            var fletes = 0;

            foreach (var linea in solicitud.Lineas)
            {
                var p = propuesta.Lineas.FirstOrDefault(x => x.LineaId == linea.Id);
                if (p?.ValorPropuesto is { } valor)
                {
                    cotizacion.Lineas.Add(new CotizacionLinea
                    {
                        CotizacionId = cotizacion.Id,
                        LineaSolicitudId = linea.Id,
                        ValorFlete = valor,
                        ValorAprovisionamiento = linea.ValorAprovisionamiento,
                        ValorRetornoContenedor = p.RetornoContenedor?.Valor,
                        Fuente = p.FuentePropuesta switch
                        {
                            "sicetac" => FuenteFlete.Sicetac,
                            "plaza" => FuenteFlete.Tarifario,
                            "historico_sap" => FuenteFlete.Historica,
                            _ => FuenteFlete.Manual,
                        },
                    });
                    linea.EstadoLinea = EstadoLinea.AutoResuelta;
                    linea.MotivoPendiente = null;
                    fletes++;
                }
                else
                {
                    linea.EstadoLinea = EstadoLinea.PorCostear;
                    linea.MotivoPendiente = p?.Justificacion;
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
            // devolver nada (p. ej. la propia Solicitud no se pudo leer), no por una fuente
            // caída en una línea puntual — eso ya lo aísla ProponerAsync internamente
            // (ver PropuestaTarifaAdapter.ProponerAsync, por-grupo). Aun así se deja un
            // motivo explícito en vez de un PorCostear mudo (issue #131).
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

    private async Task<Guid> ResolverClienteAsync(CapturarSolicitudCommand command, CancellationToken ct)
    {
        if (command.ClienteId is { } id)
        {
            if (!await clientes.ExisteAsync(id, ct))
                throw new ValidationFailedException("CLIENTE_INVALIDO", "El cliente indicado no existe.");
            return id;
        }

        if (!clientesDw.Configurado)
            throw new ValidationFailedException("DWCUBOS_NO_CONFIGURADO",
                "La conexión a los clientes reales no está configurada (ConnectionStrings:DwCubos).");

        // No nulo: CapturarSolicitudValidador ya exigió ClienteId o ClienteBp.
        var bp = command.ClienteBp!.Trim();
        var real = (await clientesDw.ListarAsync(ct)).FirstOrDefault(c => c.Bp == bp)
            ?? throw new ValidationFailedException("CLIENTE_BP_INVALIDO", $"El BP '{bp}' no existe en el catálogo de clientes.");

        var existente = await clientes.ObtenerPorNitAsync(real.Nit, ct);
        if (existente is not null) return existente.Id;

        var nuevo = new Cliente { Nombre = real.Nombre, Nit = real.Nit, RazonSocial = real.Nombre };
        clientes.Agregar(nuevo);
        return nuevo.Id;
    }

    // ------------------------------------------------------------- helpers


    private static string SerializarCondiciones(CapturarSolicitudCommand command)
    {
        var condiciones = new CondicionesServicioDto(
            string.IsNullOrWhiteSpace(command.Referencia) ? null : command.Referencia.Trim(),
            new TiemposLogisticosDto(command.HorasCargue ?? 4m, command.HorasDescargue ?? 4m));
        return System.Text.Json.JsonSerializer.Serialize(condiciones, CondicionesServicioDto.Json);
    }

    // El validador ya garantizó que parsea; aquí solo se convierte.
    private static ModalidadSolicitud? ParseModalidad(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : Enum.Parse<ModalidadSolicitud>(valor, ignoreCase: true);

    private static TipologiaCarga? ParseTipologia(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = ResolucionDeCatalogos.SinTildes(valor).Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "");
        foreach (var t in Enum.GetValues<TipologiaCarga>())
            if (normalizado.Contains(t.ToString(), StringComparison.OrdinalIgnoreCase))
                return t;
        return null;
    }

    private static LineaNegocio? ParseLineaNegocio(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var n = ResolucionDeCatalogos.SinTildes(valor).Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "").ToUpperInvariant();
        if (n.Contains("DEVOLUCION")) return LineaNegocio.DevolucionContenedor;
        if (n.Contains("ANDINO")) return LineaNegocio.Andino;
        if (n.Contains("LICITACION")) return LineaNegocio.Licitaciones;
        if (n.Contains("PUERTOAINTERIOR")) return LineaNegocio.PuertoAInterior;
        if (n.Contains("INTERIORAPUERTO")) return LineaNegocio.InteriorAPuerto;
        if (n.Contains("NACIONAL")) return LineaNegocio.Nacional;
        return null;
    }

    private static ClaseViaje? ParseClaseViaje(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var n = ResolucionDeCatalogos.SinTildes(valor).Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "").ToUpperInvariant();
        if (n.Contains("EXTRADIMENSION")) return ClaseViaje.Extradimensionado;
        if (n.Contains("CONTENEDOR")) return ClaseViaje.Contenedor;
        if (n.Contains("CUPO")) return ClaseViaje.Cupo;
        if (n.Contains("SUELTA")) return ClaseViaje.CargaSuelta;
        return null;
    }

    private static readonly string[] ClasesContenedor = ["CNT20.IT", "CNT20.RE", "CNT20.ST", "CNT40.HC", "CNT40.RE"];

    private static string? ParseClaseContenedor(string? valor)
    {
        var limpio = ResolucionDeCatalogos.Limpiar(valor);
        if (limpio is null) return null;
        var n = limpio.ToUpperInvariant().Replace(" ", "").Replace(".", "").Replace("-", "").Replace("_", "");
        foreach (var c in ClasesContenedor)
            if (n.Contains(c.Replace(".", ""))) return c;
        return limpio.Length <= 20 ? limpio : limpio[..20];
    }
}
