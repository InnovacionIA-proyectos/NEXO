using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.Priorizacion.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.TarifasGanadas.Interfaces;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.AjustesVigenciaPlaza.Adaptadores;
using Nexo.Infrastructure.AjustesVigenciaPlaza.Repositorios;
using Nexo.Infrastructure.Biblioteca.Adaptadores;
using Nexo.Infrastructure.BibliotecaSapHistorico.Adaptadores;
using Nexo.Infrastructure.Common;
using Nexo.Infrastructure.Cotizaciones.Adaptadores;
using Nexo.Infrastructure.Cotizaciones.Repositorios;
using Nexo.Infrastructure.Cubicacion.Adaptadores;
using Nexo.Infrastructure.External;
using Nexo.Infrastructure.Identidad.Adaptadores;
using Nexo.Infrastructure.Metricas.Adaptadores;
using Nexo.Infrastructure.Persistence;
using Nexo.Infrastructure.ReglasSoloSicetac.Adaptadores;
using Nexo.Infrastructure.ReglasSoloSicetac.Repositorios;
using Nexo.Infrastructure.ServiciosConexos.Repositorios;
using Nexo.Infrastructure.Solicitudes.Adaptadores;
using Nexo.Infrastructure.Priorizacion.Repositorios;
using Nexo.Infrastructure.Notificaciones.Adaptadores;
using Nexo.Infrastructure.PlanDeCargue.Adaptadores;
using Nexo.Infrastructure.PlanDeCargue.Repositorios;
using Nexo.Infrastructure.Solicitudes.Repositorios;
using Nexo.Infrastructure.TarifasGanadas.Repositorios;
using Nexo.Infrastructure.TarifasSicetacAdmin.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace Nexo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los adapters de todos los módulos de negocio migrados: Cotizaciones
    /// (piloto), Reglas Solo-SICETAC, Ajustes de Vigencia de Plaza, Biblioteca
    /// (TarifarioSap + PropuestaTarifa + matriz SICE-TAC + histórico/plaza SAP en
    /// vivo), Solicitudes/Captura, Servicios Conexos, Tarifas Ganadas, Tarifas
    /// SICE-TAC admin, Métricas y Plan de Cargue (orquestación por colas contra el
    /// motor Cubicador, microservicio Python aparte). Única
    /// excepción HTTP declarada a ese "por colas": <see cref="ICubicacionRapidaPort"/>,
    /// ver el comentario de esa interfaz.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<clsNexoDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("Nexo"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// Datacenter corporativo (dbsolucionesTI, SOLO LECTURA): tarifario SAP TM
        // (clsTarifarioSapAdapter/clsPropuestaTarifaAdapter) y vigencia de tarifas de plaza.
        // Opcional: si la cadena no está configurada, esos adapters se degradan (null).
        var dbSolucionesTi = config.GetConnectionString("DbSolucionesTi");
        var dbSolucionesTiConfigurado = !string.IsNullOrWhiteSpace(dbSolucionesTi);
        if (dbSolucionesTiConfigurado)
        {
            services.AddDbContext<clsDbSolucionesTiDbContext>(options =>
                options.UseSqlServer(dbSolucionesTi, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
        }

        // Data Warehouse SICE-TAC (DWSice, SOLO LECTURA): matriz oficial de tarifas y
        // piso SICE-TAC del motor de propuesta. Opcional: sin esta cadena, Biblioteca
        // registra el adapter "no configurado" (503 accionable) en vez de fallar la DI.
        var dwSice = config.GetConnectionString("DwSice");
        var dwSiceConfigurado = !string.IsNullOrWhiteSpace(dwSice);
        if (dwSiceConfigurado)
        {
            services.AddDbContext<clsDwSiceDbContext>(options =>
                options.UseSqlServer(dwSice, sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                    // tblRutasSicetac (~2.14M filas) no tiene NINGÚN índice sobre
                    // Periodo/NombreRuta/Configuracion/NombreUnidadTransporte - NEXO no
                    // gestiona el esquema de esa base (clsDwSiceDbContext es solo lectura,
                    // sin migraciones propias: la mantiene el ETL del DW corporativo), así
                    // que el índice real es una petición al equipo de datos, no un fix de
                    // código acá. El default de 30s de SqlClient coincide con el síntoma
                    // reportado ("Execution Timeout Expired" al filtrar) - subirlo evita el
                    // 500 duro en consultas legítimamente lentas sin resolver la causa raíz.
                    sql.CommandTimeout(90);
                }));
        }

        // DW de cubos de operaciones (DWCubosOperacionesEBS.DimBPSolicitante, SOLO
        // LECTURA): catálogo de clientes reales para la Captura. Opcional: sin esta
        // cadena, clsClientesDwAdapter.Configurado = false y la captura usa la tabla local.
        var dwCubos = config.GetConnectionString("DwCubos");
        if (!string.IsNullOrWhiteSpace(dwCubos))
        {
            services.AddDbContext<clsDwCubosDbContext>(options =>
                options.UseSqlServer(dwCubos, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
        }

        // Identidad del portal de la Administración (docs/contrato-integracion, sección
        // 7): SOLO LECTURA, misma familia que los demás DW. A diferencia de esos, sin
        // esta cadena NO se degrada a un adapter "no configurado" — clsProgram.cs impide
        // arrancar así en Producción (fail-closed); en Desarrollo/QA sin la fuente real
        // todavía, se registra un adapter local contra la tabla Usuarios
        // (clsResolverIdentidadPortalLocalAdapter) para no bloquear el trabajo del equipo.
        var identidadPortal = config.GetConnectionString("IdentidadPortal");
        var identidadPortalConfigurada = !string.IsNullOrWhiteSpace(identidadPortal);
        if (identidadPortalConfigurada)
        {
            services.AddDbContext<clsIdentidadPortalDbContext>(options =>
                options.UseSqlServer(identidadPortal, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
        }

        services.AddMemoryCache();

        services.AddScoped<IUnitOfWork, clsEfUnitOfWork>();

        // Priorización: los parámetros del score y las activas por lotes para refrescarlo.
        services.AddScoped<IParametrosScorePort, clsEfParametrosScoreRepository>();
        services.AddScoped<ISolicitudesParaRecalcularPort, clsEfSolicitudesParaRecalcularRepository>();
        services.AddScoped<ICotizacionRepository, clsEfCotizacionRepository>();
        services.AddScoped<ISolicitudesPort, clsEfSolicitudesRepository>();
        services.AddScoped<ILineasSolicitudPort, clsEfLineasSolicitudRepository>();
        services.AddScoped<clsEfTarifasHistoricasRepository>();
        services.AddScoped<ITarifasHistoricasPort>(sp => sp.GetRequiredService<clsEfTarifasHistoricasRepository>());
        services.AddScoped<IHistoricoTarifasPort>(sp => sp.GetRequiredService<clsEfTarifasHistoricasRepository>());
        services.AddScoped<IAuditoriaPort, clsEfAuditoriaRepository>();

        if (identidadPortalConfigurada)
        {
            services.AddScoped<IIdentidadPortalPort, clsIdentidadPortalAdapter>();
            services.AddScoped<IResolverIdentidadPortalPort, clsResolverIdentidadPortalAdapter>();
        }
        else
        {
            services.AddScoped<IResolverIdentidadPortalPort, clsResolverIdentidadPortalLocalAdapter>();
        }

        // SALIDA DE CORREO: se elige por configuración.
        //
        // Con MailSender:BaseUrl puesta va por el microservicio de la compañía, que es el
        // camino previsto — el relay SOAP corporativo solo adjunta archivos que ya están
        // en una ruta suya, y el formato de la cotización llega como archivo en la
        // petición. Sin esa URL cae al SMTP directo, que sigue sirviendo para correo sin
        // adjunto y evita que un entorno ya configurado se quede sin notificación.
        //
        // Mismo criterio que con el transporte del bus: el adaptador lo decide la
        // configuración, no una constante en el código.
        var mailSender = config[clsMailSenderCorreoAdapter.ClaveBaseUrl];
        if (!string.IsNullOrWhiteSpace(mailSender))
        {
            // clsCliente tipado por IHttpClientFactory: reutiliza conexiones y respeta el
            // reciclado de DNS. Un HttpClient creado a mano por llamada agota puertos.
            services.AddHttpClient<ICorreoPort, clsMailSenderCorreoAdapter>(cliente =>
            {
                cliente.BaseAddress = new Uri(mailSender.TrimEnd('/') + "/");
                // Corto a propósito: el MailSender solo encola y responde 202. Si tarda
                // más que esto, algo va mal y conviene reintentar en vez de esperar.
                cliente.Timeout = TimeSpan.FromSeconds(
                    int.TryParse(config["MailSender:TimeoutSegundos"], out var s) ? Math.Clamp(s, 5, 120) : 30);
            });
        }
        else
        {
            services.AddScoped<ICorreoPort, clsSmtpCorreoAdapter>();
        }
        services.AddSingleton<IExportadorCotizacionExcelPort, clsExportadorCotizacionExcelClosedXml>(); // sin estado
        services.AddScoped<IDestinatariosDeAlertaPort, clsDestinatariosDeAlertaAdapter>();

        // Reglas "solo SICE-TAC" (el mismo repositorio implementa el puerto de lectura
        // cacheada que consume Biblioteca).
        services.AddScoped<clsEfReglasSoloSicetacRepository>();
        services.AddScoped<IReglasSoloSicetacRepository>(sp => sp.GetRequiredService<clsEfReglasSoloSicetacRepository>());
        services.AddScoped<IClavesSoloSicetacPort>(sp => sp.GetRequiredService<clsEfReglasSoloSicetacRepository>());
        services.AddScoped<IExcelReglasSoloSicetacPort, clsExcelReglasSoloSicetacAdapter>();

        // Ajustes de vigencia de tarifas de plaza
        services.AddScoped<IAjustesVigenciaPlazaRepository, clsEfAjustesVigenciaPlazaRepository>();
        services.AddScoped<IVigenciaSapPlazaPort, clsVigenciaSapPlazaAdapter>();

        // Biblioteca: tarifario SAP TM + propuesta de tarifa (cierra el bloqueo de
        // "Aplicar propuestas" en Cotizaciones) + matriz SICE-TAC de solo lectura.
        services.AddScoped<ITarifarioSapPort, clsTarifarioSapAdapter>();
        services.AddScoped<IPropuestaTarifaPort, clsPropuestaTarifaAdapter>();

        // Conexión SAP nueva (plaza en vivo), todavía inexistente. "Deshabilitado"
        // (default) y "Real" (sin adaptador real todavía) resuelven al
        // MISMO adaptador que nunca produce candidato — las fuentes de lectura reales
        // que hoy operan (DwSice, DwCubos, DbSolucionesTi) no se tocan. Sin conmutar
        // código, solo config: SapTarifaFleteEnVivo:Modo.
        var modoSapEnVivo = config["SapTarifaFleteEnVivo:Modo"];
        if (string.Equals(modoSapEnVivo, "Mock", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<ISapTarifaFleteEnVivoPort, clsSapTarifaFleteEnVivoMockAdapter>();
        else
            services.AddScoped<ISapTarifaFleteEnVivoPort, clsSapTarifaFleteEnVivoDeshabilitadoAdapter>();
        if (dwSiceConfigurado)
            services.AddScoped<IBibliotecaSicetacPort, clsBibliotecaSicetacAdapter>();
        else
            services.AddScoped<IBibliotecaSicetacPort, clsBibliotecaSicetacNoConfiguradoAdapter>();

        // Biblioteca: histórico y tarifas de plaza SAP TM leídos en vivo (SOLO LECTURA,
        // mismo datacenter que AjustesVigenciaPlaza). Sin ConnectionStrings:DbSolucionesTi,
        // responde 503 accionable en vez de fallar la inyección de dependencias.
        if (dbSolucionesTiConfigurado)
            services.AddScoped<ISapHistoricoPort, clsSapHistoricoAdapter>();
        else
            services.AddScoped<ISapHistoricoPort, clsSapHistoricoNoConfiguradoAdapter>();

        // Solicitudes/Captura
        services.AddScoped<ISolicitudesRepository, clsEfSolicitudesCatalogoRepository>();
        services.AddScoped<IRutasCatalogoPort, clsEfRutasCatalogoRepository>();
        services.AddScoped<IConfiguracionesVehiculoPort, clsEfConfiguracionesVehiculoRepository>();
        services.AddScoped<IClientesPort, clsEfClientesRepository>();
        services.AddScoped<IClientesDwPort, clsClientesDwAdapter>();
        services.AddScoped<IExcelSolicitudesPort, clsExcelSolicitudesAdapter>();
        services.AddScoped<ITarifasCosteoPort, clsEfTarifasCosteoRepository>();

        // Servicios conexos (catálogo administrado por Servicios Logísticos; el mismo
        // repositorio también implementa IServiciosConexosPort para Cotizaciones).
        services.AddScoped<clsEfServiciosConexosRepository>();
        services.AddScoped<IServiciosConexosRepository>(sp => sp.GetRequiredService<clsEfServiciosConexosRepository>());
        services.AddScoped<IServiciosConexosPort>(sp => sp.GetRequiredService<clsEfServiciosConexosRepository>());

        // Caché local del piso SICE-TAC (import periódico admin)
        services.AddScoped<ITarifasSicetacRepository, clsEfTarifasSicetacRepository>();

        // Métricas (Tablero del Líder)
        services.AddScoped<IMetricasPort, clsMetricasAdapter>();

        // Plan de cargue: orquestación por colas contra el motor Cubicador
        // (ICubicacionPort lo registra AddMensajeria(), no este método) + compuerta de
        // paridad.
        services.AddScoped<IPlanCargueRepository, clsEfPlanCargueRepository>();
        // Catálogo de vehículos, proyectado por clsVehiculoCatalogoConsumer.
        services.AddScoped<IVehiculoCubicacionRepository, clsEfVehiculoCubicacionRepository>();
        services.AddScoped<IParidadCubicadorPort, clsParidadCubicadorAdapter>();
        // Puente de solo lectura hacia Solicitudes para el Cubicador (crear-desde-línea).
        services.AddScoped<ILineaSolicitudCubicacionPort, clsLineaSolicitudCubicacionAdapter>();

        // cubicar() (captura-api.service.ts): único método de
        // Cubicación sin cola ni proyección posibles (cálculo puro, sin persistencia) — ver
        // ICubicacionRapidaPort para por qué esto SÍ es una llamada HTTP saliente, a
        // diferencia de todo lo demás de este módulo. Sin las dos claves configuradas, se
        // registra el adapter "no configurado" (503 accionable) en vez de fallar la DI —
        // mismo criterio que BibliotecaSicetac/SapHistorico más arriba.
        var cubicadorOutboundBaseUrl = config["clsCubicador3D:OutboundBaseUrl"];
        var cubicadorOutboundApiKey = config["clsCubicador3D:OutboundApiKey"];
        if (!string.IsNullOrWhiteSpace(cubicadorOutboundBaseUrl) && !string.IsNullOrWhiteSpace(cubicadorOutboundApiKey))
        {
            services.AddHttpClient<ICubicacionRapidaPort, clsCubicacionRapidaAdapter>(cliente =>
            {
                cliente.BaseAddress = new Uri(cubicadorOutboundBaseUrl.TrimEnd('/') + "/");
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cubicadorOutboundApiKey);
                // Cálculo en memoria del lado de Cubicador, sin I/O de base de datos: si
                // tarda más que esto, conviene que la pantalla se entere pronto en vez de
                // dejar al usuario mirando un spinner.
                cliente.Timeout = TimeSpan.FromSeconds(
                    int.TryParse(config["clsCubicador3D:OutboundTimeoutSegundos"], out var s) ? Math.Clamp(s, 5, 60) : 15);
            });
        }
        else
        {
            services.AddScoped<ICubicacionRapidaPort, clsCubicacionRapidaNoConfiguradoAdapter>();
        }

        return services;
    }
}
