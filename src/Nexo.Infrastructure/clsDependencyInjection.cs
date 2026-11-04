using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.PlantillasMapeo.Interfaces;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.Priorizacion.Interfaces;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.TarifasGanadas.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.AjustesVigenciaPlaza.Adaptadores;
using Nexo.Infrastructure.AjustesVigenciaPlaza.Repositorios;
using Nexo.Infrastructure.Biblioteca.Adaptadores;
using Nexo.Infrastructure.BibliotecaSapHistorico.Adaptadores;
using Nexo.Infrastructure.Common;
using Nexo.Infrastructure.Cotizaciones.Adaptadores;
using Nexo.Infrastructure.Cotizaciones.Repositorios;
using Nexo.Infrastructure.Catalogos.Adaptadores;
using Nexo.Infrastructure.Cubicacion.Adaptadores;
using Nexo.Infrastructure.External;
using Nexo.Infrastructure.Identidad.Adaptadores;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Infrastructure.Asistente;
using Nexo.Infrastructure.IA;
using Nexo.Infrastructure.IA.Repositorios;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Infrastructure.Metricas.Adaptadores;
using Nexo.Infrastructure.Persistence;
using Nexo.Infrastructure.RangosPeso.Repositorios;
using Nexo.Infrastructure.TarifasPlaza.Adaptadores;
using Nexo.Infrastructure.TarifasPlaza.Repositorios;
using Nexo.Infrastructure.ReglasSoloSicetac.Adaptadores;
using Nexo.Infrastructure.ReglasSoloSicetac.Repositorios;
using Nexo.Infrastructure.ServiciosConexos.Adaptadores;
using Nexo.Infrastructure.ServiciosConexos.Repositorios;
using Nexo.Infrastructure.Solicitudes.Adaptadores;
using Nexo.Infrastructure.Priorizacion.Repositorios;
using Nexo.Infrastructure.Notificaciones.Adaptadores;
using Nexo.Infrastructure.PlanDeCargue.Adaptadores;
using Nexo.Infrastructure.PlanDeCargue.Repositorios;
using Nexo.Infrastructure.Solicitudes.Repositorios;
using Nexo.Infrastructure.TarifasGanadas.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Nexo.Infrastructure;

public static class clsDependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<clsNexoDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("Nexo"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// Datacenter corporativo (DbSolucionesTi, solo lectura). Opcional: sin la cadena, esos adapters se degradan (null).
        var dbSolucionesTi = config.GetConnectionString("DbSolucionesTi");
        var dbSolucionesTiConfigurado = !string.IsNullOrWhiteSpace(dbSolucionesTi);
        if (dbSolucionesTiConfigurado)
        {
            services.AddDbContext<clsDbSolucionesTiDbContext>(options =>
                options.UseSqlServer(dbSolucionesTi, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
        }

        // DWSice (solo lectura). Opcional: sin la cadena, Biblioteca registra el adapter "no configurado" (503) en vez de fallar la DI.
        var dwSice = config.GetConnectionString("DwSice");
        var dwSiceConfigurado = !string.IsNullOrWhiteSpace(dwSice);
        if (dwSiceConfigurado)
        {
            services.AddDbContext<clsDwSiceDbContext>(options =>
                options.UseSqlServer(dwSice, sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                    // tblRutasSicetac (~2.14M filas) no tiene índices sobre los campos de filtro y NEXO no gestiona ese esquema:
                    // el timeout alto evita el 500 en consultas lentas; el índice es una petición al equipo de datos.
                    sql.CommandTimeout(90);
                }));
        }

        // DWCubos (solo lectura): clientes reales para la Captura. Sin la cadena, la captura usa la tabla local.
        var dwCubos = config.GetConnectionString("DwCubos");
        if (!string.IsNullOrWhiteSpace(dwCubos))
        {
            services.AddDbContext<clsDwCubosDbContext>(options =>
                options.UseSqlServer(dwCubos, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
        }

        // Identidad del portal (solo lectura). Sin la cadena no se degrada: clsProgram.cs impide arrancar así en Producción;
        // en Desarrollo/QA se usa un adapter local contra la tabla Usuarios.
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

        // Salida de correo según configuración: con MailSender:BaseUrl va por el microservicio (el relay SOAP no adjunta
        // el archivo de la cotización); sin ella cae al SMTP directo, que sirve para correo sin adjunto.
        var mailSender = config[clsMailSenderCorreoAdapter.ClaveBaseUrl];
        if (!string.IsNullOrWhiteSpace(mailSender))
        {
            // HttpClient tipado por IHttpClientFactory: reutiliza conexiones y respeta el reciclado de DNS.
            services.AddHttpClient<ICorreoPort, clsMailSenderCorreoAdapter>(cliente =>
            {
                cliente.BaseAddress = new Uri(mailSender.TrimEnd('/') + "/");
                // Corto a propósito: el MailSender solo encola y responde 202; si tarda más, conviene reintentar.
                cliente.Timeout = TimeSpan.FromSeconds(
                    int.TryParse(config["MailSender:TimeoutSegundos"], out var s) ? Math.Clamp(s, 5, 120) : 30);
            });
        }
        else
        {
            services.AddScoped<ICorreoPort, clsSmtpCorreoAdapter>();
        }
        services.AddSingleton<IExportadorCotizacionExcelPort, clsExportadorCotizacionExcelClosedXml>();

        // La llave llega solo por entorno (Anthropic__ApiKey). Sin llave o con la función apagada la app arranca y el puerto responde 503.
        // Cliente único por proceso, creado por fábrica para que el SDK use su propio HttpClient.
        services.Configure<clsOpcionesAnthropic>(config.GetSection(clsOpcionesAnthropic.Seccion));
        services.AddSingleton(sp => new clsAnthropicAdapter(
            sp.GetRequiredService<IOptions<clsOpcionesAnthropic>>(), sp.GetRequiredService<ILogger<clsAnthropicAdapter>>()));
        // El adaptador envuelto aplica el tope diario y registra cada llamada.
        services.AddScoped<IUsoIaRepository, clsEfUsoIaRepository>();
        services.AddScoped<IAsistenteIaPort>(sp => new clsAsistenteIaConRegistro(
            sp.GetRequiredService<clsAnthropicAdapter>(), sp.GetRequiredService<IUsoIaRepository>(),
            sp.GetRequiredService<IOptions<clsOpcionesAnthropic>>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<clsAsistenteIaConRegistro>>()));
        services.AddScoped<IConversacionIaPort>(sp => new clsConversacionIaConRegistro(
            sp.GetRequiredService<clsAnthropicAdapter>(), sp.GetRequiredService<IUsoIaRepository>(),
            sp.GetRequiredService<IOptions<clsOpcionesAnthropic>>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<clsConversacionIaConRegistro>>()));
        services.AddScoped<IConversacionesAsistenteRepository, clsEfConversacionesAsistenteRepository>();
        services.AddScoped<IPreferenciasAsistenteRepository, clsEfPreferenciasAsistenteRepository>();
        services.AddScoped<IDestinatariosDeAlertaPort, clsDestinatariosDeAlertaAdapter>();

        // El mismo repositorio implementa el puerto de lectura cacheada de Biblioteca.
        services.AddScoped<clsEfReglasSoloSicetacRepository>();
        services.AddScoped<IReglasSoloSicetacRepository>(sp => sp.GetRequiredService<clsEfReglasSoloSicetacRepository>());
        services.AddScoped<IClavesSoloSicetacPort>(sp => sp.GetRequiredService<clsEfReglasSoloSicetacRepository>());
        services.AddScoped<IExcelReglasSoloSicetacPort, clsExcelReglasSoloSicetacAdapter>();
        services.AddScoped<IPlantillaReglasSoloSicetacPort, clsPlantillaReglasSoloSicetacAdapter>();
        services.AddScoped<IExportadorReglasSoloSicetacExcelPort, clsExportadorReglasSoloSicetacExcelAdapter>();
        services.AddScoped<ITiposVehiculoPort, clsTiposVehiculoAdapter>();
        services.AddScoped<ILineasNegocioPort, clsLineasNegocioAdapter>();
        services.AddScoped<IClasesViajePort, clsClasesViajeAdapter>();
        services.AddScoped<IClasesContenedorPort, clsClasesContenedorAdapter>();
        services.AddScoped<IRangosPesoRepository, clsEfRangosPesoRepository>();
        services.AddScoped<ITarifasPlazaRepository, clsEfTarifasPlazaRepository>();
        services.AddScoped<IExportadorTarifasPlazaExcelPort, clsExportadorTarifasPlazaExcelAdapter>();

        services.AddScoped<IAjustesVigenciaPlazaRepository, clsEfAjustesVigenciaPlazaRepository>();
        services.AddScoped<IVigenciaSapPlazaPort, clsVigenciaSapPlazaAdapter>();

        // El mapeo de vehículos se lee con su propio alcance de base de datos: lo consultan costeos en paralelo.
        services.AddSingleton<IMapeoVehiculoTarifarioPort, clsMapeoVehiculoTarifarioAdapter>();
        services.AddSingleton<IRangosPesoContenedorPort, clsRangosPesoContenedorAdapter>();
        services.AddScoped<ITarifarioSapPort, clsTarifarioSapAdapter>();
        services.AddScoped<IPropuestaTarifaPort, clsPropuestaTarifaAdapter>();

        // Un solo adaptador para plaza e histórico (tablas propias de NEXO).
        services.AddScoped<clsTarifaPlazaInternaAdapter>();
        services.AddScoped<ITarifaPlazaInternaPort>(sp => sp.GetRequiredService<clsTarifaPlazaInternaAdapter>());
        services.AddScoped<IHistoricoPlazaInternoPort>(sp => sp.GetRequiredService<clsTarifaPlazaInternaAdapter>());

        // Plaza SAP en vivo: "Deshabilitado" (default) y "Real" resuelven al mismo adaptador sin candidato mientras no exista el real.
        // Se conmuta solo por config: SapTarifaFleteEnVivo:Modo.
        var modoSapEnVivo = config["SapTarifaFleteEnVivo:Modo"];
        if (string.Equals(modoSapEnVivo, "Mock", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<ISapTarifaFleteEnVivoPort, clsSapTarifaFleteEnVivoMockAdapter>();
        else
            services.AddScoped<ISapTarifaFleteEnVivoPort, clsSapTarifaFleteEnVivoDeshabilitadoAdapter>();
        if (dwSiceConfigurado)
            services.AddScoped<IBibliotecaSicetacPort, clsBibliotecaSicetacAdapter>();
        else
            services.AddScoped<IBibliotecaSicetacPort, clsBibliotecaSicetacNoConfiguradoAdapter>();

        // Histórico y plaza SAP TM en vivo (solo lectura). Sin DbSolucionesTi responde 503 en vez de fallar la DI.
        if (dbSolucionesTiConfigurado)
            services.AddScoped<ISapHistoricoPort, clsSapHistoricoAdapter>();
        else
            services.AddScoped<ISapHistoricoPort, clsSapHistoricoNoConfiguradoAdapter>();

        services.AddScoped<ISolicitudesRepository, clsEfSolicitudesCatalogoRepository>();
        services.AddScoped<IRutasCatalogoPort, clsEfRutasCatalogoRepository>();
        services.AddScoped<IConfiguracionesVehiculoPort, clsEfConfiguracionesVehiculoRepository>();
        services.AddScoped<IAliasCatalogoPort, clsEfAliasCatalogoRepository>();
        services.AddScoped<IDiccionarioClienteRepository, clsEfDiccionarioClienteRepository>();
        services.AddScoped<IPlantillasMapeoRepository, clsEfPlantillasMapeoRepository>();
        services.AddScoped<IClientesPort, clsEfClientesRepository>();
        services.AddScoped<IClientesDwPort, clsClientesDwAdapter>();
        services.AddScoped<IProspectosDwPort, clsProspectosDwAdapter>();
        services.AddScoped<IMunicipiosRepository, clsEfMunicipiosRepository>();
        services.AddScoped<IExcelSolicitudesPort, clsExcelSolicitudesAdapter>();
        services.AddScoped<ITarifasCosteoPort, clsEfTarifasCosteoRepository>();

        // El mismo repositorio implementa IServiciosConexosPort para Cotizaciones.
        services.AddScoped<clsEfServiciosConexosRepository>();
        services.AddScoped<IServiciosConexosRepository>(sp => sp.GetRequiredService<clsEfServiciosConexosRepository>());
        services.AddScoped<IServiciosConexosPort>(sp => sp.GetRequiredService<clsEfServiciosConexosRepository>());
        services.AddScoped<IExportadorServiciosConexosExcelPort, clsExportadorServiciosConexosExcelAdapter>();
        services.AddScoped<IPlantillaServicioConexoPort, clsPlantillaServicioConexoAdapter>();

        services.AddScoped<IMetricasPort, clsMetricasAdapter>();

        // ICubicacionPort lo registra AddMensajeria(), no este método.
        services.AddScoped<IPlanCargueRepository, clsEfPlanCargueRepository>();
        services.AddScoped<IRechazoCubicacionLineaPort, clsRechazoCubicacionLineaAdapter>();
        services.AddScoped<IVehiculoCubicacionRepository, clsEfVehiculoCubicacionRepository>();
        services.AddScoped<IParidadCubicadorPort, clsParidadCubicadorAdapter>();
        services.AddScoped<ILineaSolicitudCubicacionPort, clsLineaSolicitudCubicacionAdapter>();

        // Único tramo de Cubicación que es HTTP saliente (ver ICubicacionRapidaPort). RecomendacionesApiKey es la llave dedicada de NEXO;
        // sin las dos claves se registra el adapter "no configurado" (503).
        var cubicadorOutboundBaseUrl = config["Cubicador3D:OutboundBaseUrl"];
        var cubicadorRecomendacionesApiKey = config["Cubicador3D:RecomendacionesApiKey"];
        if (!string.IsNullOrWhiteSpace(cubicadorOutboundBaseUrl) && !string.IsNullOrWhiteSpace(cubicadorRecomendacionesApiKey))
        {
            services.AddHttpClient<ICubicacionRapidaPort, clsCubicacionRapidaAdapter>(cliente =>
            {
                cliente.BaseAddress = new Uri(cubicadorOutboundBaseUrl.TrimEnd('/') + "/");
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cubicadorRecomendacionesApiKey);
                // Cálculo en memoria sin I/O de base: si tarda más, la pantalla debe enterarse pronto.
                cliente.Timeout = TimeSpan.FromSeconds(
                    int.TryParse(config["Cubicador3D:OutboundTimeoutSegundos"], out var s) ? Math.Clamp(s, 5, 60) : 15);
            });
        }
        else
        {
            services.AddScoped<ICubicacionRapidaPort, clsCubicacionRapidaNoConfiguradoAdapter>();
        }

        // El texto lo escribe un modelo y tarda segundos: tiene su propio timeout (30 s por defecto). Sin configurar, la explicación sale en null.
        if (!string.IsNullOrWhiteSpace(cubicadorOutboundBaseUrl) && !string.IsNullOrWhiteSpace(cubicadorRecomendacionesApiKey))
        {
            services.AddHttpClient<ICubicadorExplicacionPort, clsCubicadorExplicacionAdapter>(cliente =>
            {
                cliente.BaseAddress = new Uri(cubicadorOutboundBaseUrl.TrimEnd('/') + "/");
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cubicadorRecomendacionesApiKey);
                cliente.Timeout = TimeSpan.FromSeconds(
                    int.TryParse(config["Cubicador3D:ExplicacionTimeoutSegundos"], out var se) ? Math.Clamp(se, 5, 120) : 30);
            });
        }
        else
        {
            services.AddScoped<ICubicadorExplicacionPort, clsCubicadorExplicacionNoConfiguradoAdapter>();
        }

        // Passthrough de los CRUD de catálogo del Cubicador. OutboundApiKey es la llave genérica, distinta de RecomendacionesApiKey.
        var cubicadorOutboundApiKey = config["Cubicador3D:OutboundApiKey"];
        if (!string.IsNullOrWhiteSpace(cubicadorOutboundBaseUrl) && !string.IsNullOrWhiteSpace(cubicadorOutboundApiKey))
        {
            services.AddHttpClient<ICubicadorCatalogoPort, clsCubicadorCatalogoAdapter>(cliente =>
            {
                cliente.BaseAddress = new Uri(cubicadorOutboundBaseUrl.TrimEnd('/') + "/");
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cubicadorOutboundApiKey);
                cliente.Timeout = TimeSpan.FromSeconds(
                    int.TryParse(config["Cubicador3D:OutboundTimeoutSegundos"], out var s2) ? Math.Clamp(s2, 5, 60) : 15);
            });
        }
        else
        {
            services.AddScoped<ICubicadorCatalogoPort, clsCubicadorCatalogoNoConfiguradoAdapter>();
        }

        return services;
    }
}
