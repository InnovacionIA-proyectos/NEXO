using CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.CasosDeUso.Metricas.Interfaces;
using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using CabinaFletes.Application.CasosDeUso.ServiciosConexos.Interfaces;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Interfaces;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.CasosDeUso.TarifasGanadas.Interfaces;
using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Infrastructure.AjustesVigenciaPlaza.Adaptadores;
using CabinaFletes.Infrastructure.AjustesVigenciaPlaza.Repositorios;
using CabinaFletes.Infrastructure.Biblioteca.Adaptadores;
using CabinaFletes.Infrastructure.BibliotecaSapHistorico.Adaptadores;
using CabinaFletes.Infrastructure.Common;
using CabinaFletes.Infrastructure.Cotizaciones.Adaptadores;
using CabinaFletes.Infrastructure.Cotizaciones.Repositorios;
using CabinaFletes.Infrastructure.External;
using CabinaFletes.Infrastructure.Metricas.Adaptadores;
using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Infrastructure.ReglasSoloSicetac.Adaptadores;
using CabinaFletes.Infrastructure.ReglasSoloSicetac.Repositorios;
using CabinaFletes.Infrastructure.ServiciosConexos.Repositorios;
using CabinaFletes.Infrastructure.Solicitudes.Adaptadores;
using CabinaFletes.Infrastructure.Priorizacion.Repositorios;
using CabinaFletes.Infrastructure.Solicitudes.Repositorios;
using CabinaFletes.Infrastructure.TarifasGanadas.Repositorios;
using CabinaFletes.Infrastructure.TarifasSicetacAdmin.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los adapters de todos los módulos de negocio migrados: Cotizaciones
    /// (piloto), Reglas Solo-SICETAC, Ajustes de Vigencia de Plaza, Biblioteca
    /// (TarifarioSap + PropuestaTarifa + matriz SICE-TAC + histórico/plaza SAP en
    /// vivo), Solicitudes/Captura, Servicios Conexos, Tarifas Ganadas, Tarifas
    /// SICE-TAC admin y Métricas. Cubicación 3D y Plan de Cargue quedan fuera de
    /// alcance por decisión explícita (microservicio Python aparte) — ver PENDIENTES.md.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<CabinaFletesDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("CabinaFletes"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        // Comercial responsable del cliente (DWCubosEBSSAP, SOLO LECTURA). Opcional: si la
        // cadena no está configurada, ComercialResponsableSapAdapter.Configurado = false.
        var dwCubosSap = config.GetConnectionString("DwCubosSap");
        if (!string.IsNullOrWhiteSpace(dwCubosSap))
        {
            services.AddDbContext<DwCubosSapDbContext>(options =>
                options.UseSqlServer(dwCubosSap, sql =>
                {
                    sql.UseCompatibilityLevel(100); // el DW corre SQL Server 2008 R2
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                }));
        }

        // Datacenter corporativo (dbsolucionesTI, SOLO LECTURA): tarifario SAP TM
        // (TarifarioSapAdapter/PropuestaTarifaAdapter) y vigencia de tarifas de plaza.
        // Opcional: si la cadena no está configurada, esos adapters se degradan (null).
        var dbSolucionesTi = config.GetConnectionString("DbSolucionesTi");
        var dbSolucionesTiConfigurado = !string.IsNullOrWhiteSpace(dbSolucionesTi);
        if (dbSolucionesTiConfigurado)
        {
            services.AddDbContext<DbSolucionesTiDbContext>(options =>
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
            services.AddDbContext<DwSiceDbContext>(options =>
                options.UseSqlServer(dwSice, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
        }

        // DW de cubos de operaciones (DWCubosOperacionesEBS.DimBPSolicitante, SOLO
        // LECTURA): catálogo de clientes reales para la Captura. Opcional: sin esta
        // cadena, ClientesDwAdapter.Configurado = false y la captura usa la tabla local.
        var dwCubos = config.GetConnectionString("DwCubos");
        if (!string.IsNullOrWhiteSpace(dwCubos))
        {
            services.AddDbContext<DwCubosDbContext>(options =>
                options.UseSqlServer(dwCubos, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
        }

        services.AddMemoryCache();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Priorización: los parámetros del score y las activas por lotes para refrescarlo.
        services.AddScoped<IParametrosScorePort, EfParametrosScoreRepository>();
        services.AddScoped<ISolicitudesParaRecalcularPort, EfSolicitudesParaRecalcularRepository>();
        services.AddScoped<ICotizacionRepository, EfCotizacionRepository>();
        services.AddScoped<ISolicitudesPort, EfSolicitudesRepository>();
        services.AddScoped<ILineasSolicitudPort, EfLineasSolicitudRepository>();
        services.AddScoped<EfTarifasHistoricasRepository>();
        services.AddScoped<ITarifasHistoricasPort>(sp => sp.GetRequiredService<EfTarifasHistoricasRepository>());
        services.AddScoped<IHistoricoTarifasPort>(sp => sp.GetRequiredService<EfTarifasHistoricasRepository>());
        services.AddScoped<IAuditoriaPort, EfAuditoriaRepository>();

        services.AddScoped<IComercialResponsablePort, ComercialResponsableSapAdapter>();
        services.AddScoped<ICorreoPort, SmtpCorreoAdapter>();
        services.AddSingleton<IExportadorCotizacionExcelPort, ExportadorCotizacionExcelClosedXml>(); // sin estado

        // Reglas "solo SICE-TAC" (el mismo repositorio implementa el puerto de lectura
        // cacheada que consume Biblioteca).
        services.AddScoped<EfReglasSoloSicetacRepository>();
        services.AddScoped<IReglasSoloSicetacRepository>(sp => sp.GetRequiredService<EfReglasSoloSicetacRepository>());
        services.AddScoped<IClavesSoloSicetacPort>(sp => sp.GetRequiredService<EfReglasSoloSicetacRepository>());
        services.AddScoped<IExcelReglasSoloSicetacPort, ExcelReglasSoloSicetacAdapter>();

        // Ajustes de vigencia de tarifas de plaza
        services.AddScoped<IAjustesVigenciaPlazaRepository, EfAjustesVigenciaPlazaRepository>();
        services.AddScoped<IVigenciaSapPlazaPort, VigenciaSapPlazaAdapter>();

        // Biblioteca: tarifario SAP TM + propuesta de tarifa (cierra el bloqueo de
        // "Aplicar propuestas" en Cotizaciones) + matriz SICE-TAC de solo lectura.
        services.AddScoped<ITarifarioSapPort, TarifarioSapAdapter>();
        services.AddScoped<IPropuestaTarifaPort, PropuestaTarifaAdapter>();
        if (dwSiceConfigurado)
            services.AddScoped<IBibliotecaSicetacPort, BibliotecaSicetacAdapter>();
        else
            services.AddScoped<IBibliotecaSicetacPort, BibliotecaSicetacNoConfiguradoAdapter>();

        // Biblioteca: histórico y tarifas de plaza SAP TM leídos en vivo (SOLO LECTURA,
        // mismo datacenter que AjustesVigenciaPlaza). Sin ConnectionStrings:DbSolucionesTi,
        // responde 503 accionable en vez de fallar la inyección de dependencias.
        if (dbSolucionesTiConfigurado)
            services.AddScoped<ISapHistoricoPort, SapHistoricoAdapter>();
        else
            services.AddScoped<ISapHistoricoPort, SapHistoricoNoConfiguradoAdapter>();

        // Solicitudes/Captura
        services.AddScoped<ISolicitudesRepository, EfSolicitudesCatalogoRepository>();
        services.AddScoped<IRutasCatalogoPort, EfRutasCatalogoRepository>();
        services.AddScoped<IConfiguracionesVehiculoPort, EfConfiguracionesVehiculoRepository>();
        services.AddScoped<IClientesPort, EfClientesRepository>();
        services.AddScoped<IClientesDwPort, ClientesDwAdapter>();
        services.AddScoped<IExcelSolicitudesPort, ExcelSolicitudesAdapter>();
        services.AddScoped<ITarifasCosteoPort, EfTarifasCosteoRepository>();

        // Servicios conexos (catálogo administrado por Servicios Logísticos; el mismo
        // repositorio también implementa IServiciosConexosPort para Cotizaciones).
        services.AddScoped<EfServiciosConexosRepository>();
        services.AddScoped<IServiciosConexosRepository>(sp => sp.GetRequiredService<EfServiciosConexosRepository>());
        services.AddScoped<IServiciosConexosPort>(sp => sp.GetRequiredService<EfServiciosConexosRepository>());

        // Caché local del piso SICE-TAC (import periódico admin)
        services.AddScoped<ITarifasSicetacRepository, EfTarifasSicetacRepository>();

        // Métricas (Tablero del Líder)
        services.AddScoped<IMetricasPort, MetricasAdapter>();

        return services;
    }
}
