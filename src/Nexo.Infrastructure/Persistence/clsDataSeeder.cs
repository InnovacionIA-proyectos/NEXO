using Nexo.Domain.Auditoria.Entidades;
using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Priorizacion.Reglas;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Persistence;

/// <summary>
/// Datos de demostración realistas (escenarios del PRD + casos edge) para poblar la BD
/// y poder construir las pantallas con contenido. Idempotente: no hace nada si ya hay clientes.
/// </summary>
public static class clsDataSeeder
{
    public static async Task<bool> SeedAsync(clsNexoDbContext db)
    {
        if (await db.Clientes.AnyAsync()) return false; // ya sembrado

        var parametros = await db.clsParametrosScore.AsNoTracking().FirstAsync();
        var now = DateTimeOffset.UtcNow;

        // ----------------------------------------------------------------- Usuarios (11)
        //
        // IdentidadPortalId: id que un desarrollador manda en X-Portal-clsUsuario-Id para
        // entrar localmente como esta persona cuando ConnectionStrings:clsIdentidadPortal NO
        // está configurada (clsResolverIdentidadPortalLocalAdapter, ver DependencyInjection.cs).
        // No todas tienen: coord3/lider2/sl2 simulan a alguien que todavía no inició sesión
        // por el portal ("edge: sin identidad del portal").
        var coord1 = new clsUsuario { Nombre = "Carlos Herrera", Email = "cherrera@boterosoto.com.co", Rol = RolUsuario.CoordinadorFletes, IdentidadPortalId = 1001 };
        var coord2 = new clsUsuario { Nombre = "Yined Zapata", Email = "yzapata@boterosoto.com.co", Rol = RolUsuario.CoordinadorFletes, IdentidadPortalId = 1002 };
        var coord3 = new clsUsuario { Nombre = "Édinson Molina", Email = "emolina@boterosoto.com.co", Rol = RolUsuario.CoordinadorFletes };
        var coord4 = new clsUsuario { Nombre = "Mónica Barrera", Email = "mbarrera@boterosoto.com.co", Rol = RolUsuario.CoordinadorFletes, Activo = false }; // edge: inactivo
        var lider1 = new clsUsuario { Nombre = "Luisa Chaverra", Email = "lchaverra@boterosoto.com.co", Rol = RolUsuario.LiderProceso, IdentidadPortalId = 1003 };
        var lider2 = new clsUsuario { Nombre = "Andrés Pardo", Email = "apardo@boterosoto.com.co", Rol = RolUsuario.LiderProceso };
        var sl1 = new clsUsuario { Nombre = "Sandra Nieto", Email = "snieto@boterosoto.com.co", Rol = RolUsuario.ServiciosLogisticos, IdentidadPortalId = 1004 };
        var sl2 = new clsUsuario { Nombre = "Jorge Organista", Email = "jorganista@boterosoto.com.co", Rol = RolUsuario.ServiciosLogisticos }; // edge: sin identidad del portal
        var admin1 = new clsUsuario { Nombre = "Paola Restrepo", Email = "prestrepo@boterosoto.com.co", Rol = RolUsuario.Admin, IdentidadPortalId = 1005 };
        var admin2 = new clsUsuario { Nombre = "Albert Blanco", Email = "ablanco@boterosoto.com.co", Rol = RolUsuario.Admin, IdentidadPortalId = 1006 };
        // Issue #187: faltaba un Comercial sembrado - la mayoría de los comandos de
        // escritura de S3-HU-02 (plan de cargue) exigen ese rol, y sin este usuario no
        // había forma de probarlos localmente con X-Portal-clsUsuario-Id sin sembrar uno a mano.
        var com1 = new clsUsuario { Nombre = "Mariana Ospina", Email = "mospina@boterosoto.com.co", Rol = RolUsuario.Comercial, IdentidadPortalId = 1007 };
        db.Usuarios.AddRange(coord1, coord2, coord3, coord4, lider1, lider2, sl1, sl2, admin1, admin2, com1);

        // ----------------------------------------------------------------- Clientes (10)
        var cPostobon = new clsCliente { Nombre = "Postobón S.A.", Nit = "890903939-1", RazonSocial = "Postobón S.A.", Telefonos = "[\"6044448888\",\"3104448888\"]" };
        var cNutresa = new clsCliente { Nombre = "Grupo Nutresa S.A.", Nit = "811000339-1", RazonSocial = "Grupo Nutresa S.A.", Telefonos = "[\"6044444444\"]" };
        var cArgos = new clsCliente { Nombre = "Cementos Argos S.A.", Nit = "890100251-0", RazonSocial = "Cementos Argos S.A.", Telefonos = "[\"6053309000\"]" };
        var cSofasa = new clsCliente { Nombre = "Sofasa S.A.", Nit = "890905055-9", RazonSocial = "Sociedad de Fabricación de Automotores S.A." };
        var cFamilia = new clsCliente { Nombre = "Productos Familia S.A.", Nit = "800050178-9", RazonSocial = "Productos Familia S.A.", Telefonos = "[\"6043617080\"]" };
        var cBavaria = new clsCliente { Nombre = "Bavaria S.A.", Nit = "860005224-6", RazonSocial = "Bavaria S.A." };
        var cCorona = new clsCliente { Nombre = "Corona", Nit = "860005741-2" }; // edge: sin razón social ni teléfonos
        var cExito = new clsCliente { Nombre = "Almacenes Éxito S.A.", Nit = "890900608-9", RazonSocial = "Almacenes Éxito S.A." }; // edge: sin teléfonos
        var cColanta = new clsCliente { Nombre = "Colanta", Nit = "890901352-2", RazonSocial = "Cooperativa Colanta", Telefonos = "[\"6044455555\"]" };
        var cPremex = new clsCliente { Nombre = "Premex S.A.", Nit = "811008362-7" }; // edge: sin razón social ni teléfonos
        var clientes = new[] { cPostobon, cNutresa, cArgos, cSofasa, cFamilia, cBavaria, cCorona, cExito, cColanta, cPremex };
        db.Clientes.AddRange(clientes);

        // ----------------------------------------------------------------- Rutas (12)
        var r1 = new clsRuta { Origen = "Dosquebradas (RIS)", Destino = "Rionegro (ANT)" };
        var r2 = new clsRuta { Origen = "Medellín (ANT)", Destino = "Bogotá D.C. (CUN)" };
        var r3 = new clsRuta { Origen = "Cartagena de Indias (BOL)", Destino = "Soacha (CUN)" };
        var r4 = new clsRuta { Origen = "Medellín (ANT)", Destino = "Guarne (ANT)" };
        var r5 = new clsRuta { Origen = "Copacabana (ANT)", Destino = "Mosquera (CUN)" };
        var r6 = new clsRuta { Origen = "Cúcuta (NSA)", Destino = "Santiago de Cali (VAC)" };
        var r7 = new clsRuta { Origen = "Buenaventura (VAC)", Destino = "Bogotá D.C. (CUN)" };
        var r8 = new clsRuta { Origen = "Barranquilla (ATL)", Destino = "Medellín (ANT)" };
        var r9 = new clsRuta { Origen = "Bogotá D.C. (CUN)", Destino = "Bucaramanga (SAN)" };
        var r10 = new clsRuta { Origen = "Yumbo (VAC)", Destino = "Pereira (RIS)" };
        var r11 = new clsRuta { Origen = "Santa Marta (MAG)", Destino = "Bogotá D.C. (CUN)" };
        var r12 = new clsRuta { Origen = "Funza (CUN)", Destino = "Manizales (CAL)" };
        var rutas = new[] { r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12 };
        db.Rutas.AddRange(rutas);

        // ----------------------------------------------------------------- Configuraciones de vehículo (10)
        var v1 = new clsConfiguracionVehiculo { TipoVehiculo = "C2", Carroceria = "Estacas", RangoPeso = "8-10 ton" };
        var v2 = new clsConfiguracionVehiculo { TipoVehiculo = "C3", Carroceria = "Furgón", RangoPeso = "15-17 ton" };
        var v3 = new clsConfiguracionVehiculo { TipoVehiculo = "C2", Carroceria = "Furgón", RangoPeso = "9 ton" };
        var v4 = new clsConfiguracionVehiculo { TipoVehiculo = "2S2", Carroceria = "Plataforma", RangoPeso = "24 ton" };
        var v5 = new clsConfiguracionVehiculo { TipoVehiculo = "3S2", Carroceria = "Estibas", RangoPeso = "30 ton" };
        var v6 = new clsConfiguracionVehiculo { TipoVehiculo = "3S3", Carroceria = "Furgón Refrigerado", RangoPeso = "34 ton" };
        var v7 = new clsConfiguracionVehiculo { TipoVehiculo = "C3", Carroceria = "Volco", RangoPeso = "18 ton" };
        var v8 = new clsConfiguracionVehiculo { TipoVehiculo = "2S3", Carroceria = "Tanque", RangoPeso = "32 ton" };
        var v9 = new clsConfiguracionVehiculo { TipoVehiculo = "3S3", Carroceria = "Plataforma", RangoPeso = "35 ton" };
        var v10 = new clsConfiguracionVehiculo { TipoVehiculo = "C2" }; // edge: carrocería y rango nulos
        var configs = new[] { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10 };
        db.ConfiguracionesVehiculo.AddRange(configs);

        // ----------------------------------------------------------------- Servicios conexos (10)
        var sCargue = new clsServicioConexo { Nombre = "Cargue", ReglaCobro = "{\"tipo\":\"por_tonelada\",\"valor\":12000}", ValorBase = 12000m, VigenteHasta = new DateOnly(2026, 12, 31), ActualizadoPorUsuario = sl1 };
        var sDescargue = new clsServicioConexo { Nombre = "Descargue", ReglaCobro = "{\"tipo\":\"por_tonelada\",\"valor\":12000}", ValorBase = 12000m, VigenteHasta = new DateOnly(2026, 12, 31), ActualizadoPorUsuario = sl1 };
        var sStandby = new clsServicioConexo { Nombre = "Stand-by", ReglaCobro = "{\"tipo\":\"por_hora\",\"valor\":45000}", ValorBase = 45000m, VigenteHasta = new DateOnly(2026, 12, 31), ActualizadoPorUsuario = sl1 };
        var sCandado = new clsServicioConexo { Nombre = "Candado satelital", ReglaCobro = "{\"tipo\":\"fijo\",\"valor\":85000}", ValorBase = 85000m, VigenteHasta = new DateOnly(2026, 12, 31), ActualizadoPorUsuario = sl2 };
        var sAlmac = new clsServicioConexo { Nombre = "Almacenamiento", ReglaCobro = "{\"tipo\":\"por_dia_m2\",\"valor\":3500}", ValorBase = 3500m, VigenteHasta = new DateOnly(2026, 12, 31), ActualizadoPorUsuario = sl2 };
        var sMultipunto = new clsServicioConexo { Nombre = "Distribución multipunto", ReglaCobro = "{\"tipo\":\"por_punto\",\"valor\":60000}", ValorBase = 60000m, VigenteHasta = new DateOnly(2026, 12, 31), ActualizadoPorUsuario = sl1 };
        var sItr = new clsServicioConexo { Nombre = "ITR", ReglaCobro = "{\"tipo\":\"fijo\",\"valor\":120000}", ValorBase = 120000m, VigenteHasta = new DateOnly(2026, 9, 30), ActualizadoPorUsuario = sl1 };
        var sEscolta = new clsServicioConexo { Nombre = "Escolta", ReglaCobro = "{\"tipo\":\"por_viaje\",\"valor\":900000}", ValorBase = 900000m, VigenteHasta = new DateOnly(2026, 6, 30), ActualizadoPorUsuario = sl2 }; // edge: por vencer
        var sReexp = new clsServicioConexo { Nombre = "Reexpedición", ReglaCobro = "{\"tipo\":\"por_viaje\",\"valor\":250000}", ValorBase = 250000m, VigenteHasta = new DateOnly(2026, 3, 31), ActualizadoPorUsuario = sl1 }; // edge: vencida
        var sMonta = new clsServicioConexo { Nombre = "Montacargas", ValorBase = 70000m, Activo = false, ActualizadoPorUsuario = sl2 }; // edge: inactivo y regla nula
        db.ServiciosConexos.AddRange(sCargue, sDescargue, sStandby, sCandado, sAlmac, sMultipunto, sItr, sEscolta, sReexp, sMonta);

        // ----------------------------------------------------------------- Tarifas SICE-TAC (12)
        clsTarifaSicetac Sice(int periodo, clsRuta ruta, string oDane, string dDane, string config, string tipo, string unidad,
            CondicionCarga cond, decimal valor, decimal? valorHora, decimal? horasPact, decimal? horasRec, bool via) => new()
        {
            Periodo = periodo,
            clsRuta = ruta,
            OrigenDane = oDane,
            DestinoDane = dDane,
            NombreRuta = $"{ruta.Origen} - {ruta.Destino}".ToUpperInvariant(),
            Configuracion = config,
            TipoCarga = tipo,
            UnidadTransporte = unidad,
            CondicionCarga = cond,
            ValorPiso = valor,
            ValorHora = valorHora,
            HorasPactadas = horasPact,
            HorasRecorrido = horasRec,
            ValorSiceHora = valorHora.HasValue && horasPact.HasValue ? valor + valorHora.Value * horasPact.Value : null,
            ViaEstandar = via,
            FechaCreacionSice = new DateTimeOffset(2026, 6, 1, 9, 16, 0, TimeSpan.Zero),
        };

        var sicetac = new[]
        {
            Sice(202606, r1, "66170000", "05615000", "2L2", "General", "Estibas", CondicionCarga.Cargado, 995457m, 40112m, 8m, 6.38m, true),
            Sice(202606, r1, "66170000", "05615000", "2L2", "Sin carga", "Furgón", CondicionCarga.Vacio, 788496m, null, null, 5.19m, true), // edge: vacío, sin valor hora
            Sice(202606, r1, "66170000", "05615000", "2L1", "Granel Sólido", "Estacas", CondicionCarga.Cargado, 1046361m, 44306m, 8m, 6.38m, true),
            Sice(202606, r1, "66170000", "05615000", "3S3", "Carga Refrigerada", "Furgón Refrigerado", CondicionCarga.Cargado, 2280098m, 92437m, 8m, 8.13m, true),
            Sice(202605, r1, "66170000", "05615000", "2L2", "General", "Estibas", CondicionCarga.Cargado, 980000m, 39500m, 8m, 6.38m, true), // edge: periodo anterior (vencida)
            Sice(202606, r2, "05001000", "11001000", "3S2", "General", "Estibas", CondicionCarga.Cargado, 1850000m, 60000m, 10m, 9.20m, true),
            Sice(202606, r2, "05001000", "11001000", "3S3", "Carga Refrigerada", "Furgón Refrigerado", CondicionCarga.Cargado, 2950000m, 95000m, 10m, 9.20m, true),
            Sice(202606, r3, "13001000", "25754000", "3S3", "General", "Plataforma", CondicionCarga.Cargado, 3200000m, 98000m, 12m, 18.50m, true),
            Sice(202606, r6, "54001000", "76001000", "3S2", "General", "Furgón", CondicionCarga.Cargado, 2600000m, 80000m, 10m, 14.10m, false), // edge: vía no estándar
            Sice(202606, r7, "76109000", "11001000", "3S3", "General", "Plataforma", CondicionCarga.Cargado, 2100000m, 70000m, 10m, 9.80m, true),
            Sice(202606, r8, "08001000", "05001000", "3S2", "Granel Sólido", "Volco", CondicionCarga.Cargado, 2400000m, 75000m, 10m, 13.20m, true),
            Sice(202606, r9, "11001000", "68001000", "C3", "General", "Furgón", CondicionCarga.Cargado, 1250000m, 50000m, 8m, 6.50m, true),
        };
        db.TarifasSicetac.AddRange(sicetac);

        // ----------------------------------------------------------------- Solicitudes + líneas (17)
        var tipos = new[] { TipologiaCarga.CargaSeca, TipologiaCarga.GranelSolido, TipologiaCarga.Refrigerados, TipologiaCarga.Contenedor, TipologiaCarga.Alimentos, TipologiaCarga.CargaSuelta, TipologiaCarga.GranelLiquido };
        var estadosLinea = new[] { EstadoLinea.PorCostear, EstadoLinea.AutoResuelta, EstadoLinea.Revisada };

        clsLineaSolicitud Linea(clsRuta r, clsConfiguracionVehiculo c, TipologiaCarga t, decimal? peso, decimal? valorMerc, EstadoLinea est) =>
            new() { clsRuta = r, clsConfiguracionVehiculo = c, Tipologia = t, Peso = peso, ValorMercancia = valorMerc, EstadoLinea = est };

        clsSolicitud Crear(clsCliente cliente, clsUsuario creador, TipoSolicitud tipo, ModalidadSolicitud? mod,
            DateTimeOffset? deadline, decimal? prob, decimal? valor, EstadoSolicitud estado, IEnumerable<clsLineaSolicitud> lineas)
        {
            var s = new clsSolicitud
            {
                clsCliente = cliente,
                Creador = creador,
                Tipo = tipo,
                Modalidad = mod,
                Deadline = deadline,
                ProbabilidadCierre = prob,
                ValorEstimado = valor,
                Estado = estado,
            };
            foreach (var l in lineas) s.Lineas.Add(l);
            s.ScorePrioridad = (decimal)clsScorePrioridadCalculator.Calcular(s, parametros, now).Total;
            return s;
        }

        // Pliego de licitación grande (representa el pliego de ~500 rutas con muchas líneas).
        var lineasPliego = new List<clsLineaSolicitud>();
        for (var i = 0; i < 12; i++)
            lineasPliego.Add(Linea(rutas[i % rutas.Length], configs[i % configs.Length], tipos[i % tipos.Length],
                10000m + i * 1000m, i % 3 == 0 ? null : 50_000_000m + i * 1_000_000m, estadosLinea[i % estadosLinea.Length]));

        var lineasBavaria = new List<clsLineaSolicitud>();
        for (var i = 0; i < 6; i++)
            lineasBavaria.Add(Linea(rutas[i % rutas.Length], configs[(i + 2) % configs.Length], tipos[i % tipos.Length], 12000m + i * 800m, null, estadosLinea[i % estadosLinea.Length]));

        var lineasCorona = new List<clsLineaSolicitud>();
        for (var i = 0; i < 8; i++)
            lineasCorona.Add(Linea(rutas[(i + 3) % rutas.Length], configs[i % configs.Length], tipos[(i + 1) % tipos.Length], 9000m + i * 1200m, i % 2 == 0 ? 30_000_000m : null, estadosLinea[i % estadosLinea.Length]));

        // S1..S12 activas (bandeja) + S13..S17 cerradas (historia)
        var s1 = Crear(cPostobon, coord1, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero), 60m, 4_500_000m, EstadoSolicitud.EnCosteo,
            new[] { Linea(r1, v1, TipologiaCarga.CargaSeca, 9500m, 4_500_000m, EstadoLinea.AutoResuelta) }); // edge: deadline vencido
        var s2 = Crear(cNutresa, coord2, TipoSolicitud.Licitacion, null, new DateTimeOffset(2026, 6, 30, 17, 0, 0, TimeSpan.Zero), 70m, 320_000_000m, EstadoSolicitud.Pendiente, lineasPliego); // edge: modalidad nula, pliego grande
        var s3 = Crear(cArgos, coord1, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero), null, 6_000_000m, EstadoSolicitud.Pendiente,
            new[] { Linea(r8, v7, TipologiaCarga.GranelSolido, 18000m, null, EstadoLinea.PorCostear) }); // edge: prob nula
        var s4 = Crear(cSofasa, coord3, TipoSolicitud.Spot, ModalidadSolicitud.Importacion, null, 40m, null, EstadoSolicitud.Pendiente,
            new[] { Linea(r3, v9, TipologiaCarga.Contenedor, 24000m, 200_000_000m, EstadoLinea.PorCostear) }); // edge: deadline y valor nulos
        var s5 = Crear(cBavaria, coord2, TipoSolicitud.Licitacion, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 7, 10, 17, 0, 0, TimeSpan.Zero), 85m, 500_000_000m, EstadoSolicitud.EnCosteo, lineasBavaria);
        var s6 = Crear(cFamilia, coord1, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, now.AddHours(6), 55m, 3_200_000m, EstadoSolicitud.Pendiente,
            new[] { Linea(r5, v3, TipologiaCarga.CargaSuelta, 8000m, 3_200_000m, EstadoLinea.PorCostear) }); // urgente
        var s7 = Crear(cExito, coord3, TipoSolicitud.Spot, ModalidadSolicitud.Exportacion, new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero), 65m, 8_000_000m, EstadoSolicitud.Entregada,
            new[] { Linea(r7, v9, TipologiaCarga.Contenedor, 22000m, 180_000_000m, EstadoLinea.Revisada) });
        var s8 = Crear(cColanta, coord2, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero), 75m, 5_500_000m, EstadoSolicitud.Entregada,
            new[] { Linea(r2, v6, TipologiaCarga.Refrigerados, 16000m, 90_000_000m, EstadoLinea.Revisada) });
        var s9 = Crear(cCorona, coord3, TipoSolicitud.Licitacion, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 6, 25, 17, 0, 0, TimeSpan.Zero), 50m, 210_000_000m, EstadoSolicitud.Pendiente, lineasCorona);
        var s10 = Crear(cPremex, coord1, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero), 30m, 2_800_000m, EstadoSolicitud.Descartada,
            new[] { Linea(r10, v1, TipologiaCarga.CargaSeca, 7000m, null, EstadoLinea.PorCostear) }); // edge: descartada
        var s11 = Crear(cArgos, coord2, TipoSolicitud.Spot, ModalidadSolicitud.Extradimensionada, new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero), 45m, 12_000_000m, EstadoSolicitud.Pendiente,
            new[] { Linea(r11, v4, TipologiaCarga.CargaSuelta, 26000m, 300_000_000m, EstadoLinea.PorCostear) });
        var s12 = Crear(cPostobon, coord3, TipoSolicitud.Spot, ModalidadSolicitud.CargaGeneral, new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero), 80m, 7_000_000m, EstadoSolicitud.EnCosteo,
            new[] { Linea(r4, v2, TipologiaCarga.Alimentos, 14000m, 60_000_000m, EstadoLinea.AutoResuelta) });

        // Cerradas (historia) — respaldan cotizaciones ganadas/perdidas
        var s13 = Crear(cColanta, coord1, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero), 70m, 5_000_000m, EstadoSolicitud.Entregada,
            new[] { Linea(r2, v5, TipologiaCarga.GranelSolido, 28000m, 70_000_000m, EstadoLinea.Revisada) });
        var s14 = Crear(cBavaria, coord2, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero), 60m, 4_800_000m, EstadoSolicitud.Entregada,
            new[] { Linea(r9, v2, TipologiaCarga.CargaSeca, 15000m, 50_000_000m, EstadoLinea.Revisada) });
        var s15 = Crear(cNutresa, coord3, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero), 80m, 6_200_000m, EstadoSolicitud.Entregada,
            new[] { Linea(r4, v2, TipologiaCarga.Alimentos, 13000m, 65_000_000m, EstadoLinea.Revisada) });
        var s16 = Crear(cArgos, coord1, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero), 75m, 9_000_000m, EstadoSolicitud.Entregada,
            new[] { Linea(r8, v7, TipologiaCarga.GranelSolido, 19000m, null, EstadoLinea.Revisada) });
        var s17 = Crear(cFamilia, coord2, TipoSolicitud.Spot, ModalidadSolicitud.Nacional, new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero), 55m, 3_900_000m, EstadoSolicitud.Entregada,
            new[] { Linea(r5, v3, TipologiaCarga.CargaSuelta, 9000m, 40_000_000m, EstadoLinea.Revisada) });

        db.Solicitudes.AddRange(s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, s16, s17);

        // ----------------------------------------------------------------- Cotizaciones + líneas + conexos (10)
        clsCotizacion Cotiza(clsSolicitud s, EstadoCotizacion estado, ResultadoCotizacion resultado, clsUsuario? entregadaPor,
            DateTimeOffset? entregadaAt, IEnumerable<(clsLineaSolicitud linea, decimal valor, FuenteFlete fuente)> fletes,
            IEnumerable<(clsServicioConexo serv, decimal cant, decimal valor)> conexos)
        {
            var c = new clsCotizacion { clsSolicitud = s, Estado = estado, Resultado = resultado, EntregadaPorUsuario = entregadaPor, EntregadaAt = entregadaAt };
            decimal total = 0;
            foreach (var (linea, valor, fuente) in fletes)
            {
                c.Lineas.Add(new clsCotizacionLinea { clsLineaSolicitud = linea, ValorFlete = valor, Fuente = fuente });
                total += valor;
            }
            foreach (var (serv, cant, valor) in conexos)
            {
                c.Conexos.Add(new clsCotizacionConexo { clsServicioConexo = serv, Cantidad = cant, ValorAplicado = valor });
                total += valor;
            }
            c.Total = total;
            return c;
        }

        var c1 = Cotiza(s1, EstadoCotizacion.Borrador, ResultadoCotizacion.SinDefinir, null, null,
            new[] { (s1.Lineas.First(), 995457m, FuenteFlete.Sicetac) },
            new[] { (sCargue, 9.5m, 114000m), (sDescargue, 9.5m, 114000m) });
        var c5 = Cotiza(s5, EstadoCotizacion.Borrador, ResultadoCotizacion.SinDefinir, null, null,
            s5.Lineas.Take(3).Select(l => (l, 1_900_000m, FuenteFlete.Sicetac)),
            new[] { (sStandby, 4m, 180000m) });
        var c7 = Cotiza(s7, EstadoCotizacion.Entregada, ResultadoCotizacion.SinDefinir, coord3, new DateTimeOffset(2026, 6, 17, 9, 0, 0, TimeSpan.Zero),
            new[] { (s7.Lineas.First(), 2_100_000m, FuenteFlete.Historica) },
            new[] { (sCandado, 1m, 85000m) });
        var c8 = Cotiza(s8, EstadoCotizacion.Entregada, ResultadoCotizacion.Ganada, coord2, new DateTimeOffset(2026, 6, 16, 15, 0, 0, TimeSpan.Zero),
            new[] { (s8.Lineas.First(), 2_950_000m, FuenteFlete.Sicetac) },
            Array.Empty<(clsServicioConexo, decimal, decimal)>()); // edge: sin conexos
        var c12 = Cotiza(s12, EstadoCotizacion.Borrador, ResultadoCotizacion.SinDefinir, null, null,
            new[] { (s12.Lineas.First(), 1_250_000m, FuenteFlete.Manual) },
            new[] { (sCargue, 14m, 168000m) });
        var c13 = Cotiza(s13, EstadoCotizacion.Entregada, ResultadoCotizacion.Ganada, coord1, new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero),
            new[] { (s13.Lineas.First(), 1_820_000m, FuenteFlete.Sicetac) },
            new[] { (sCargue, 28m, 336000m) });
        var c14 = Cotiza(s14, EstadoCotizacion.Entregada, ResultadoCotizacion.Perdida, coord2, new DateTimeOffset(2026, 5, 23, 11, 0, 0, TimeSpan.Zero),
            new[] { (s14.Lineas.First(), 1_300_000m, FuenteFlete.Manual) },
            Array.Empty<(clsServicioConexo, decimal, decimal)>());
        var c15 = Cotiza(s15, EstadoCotizacion.Entregada, ResultadoCotizacion.Ganada, coord3, new DateTimeOffset(2026, 5, 26, 14, 0, 0, TimeSpan.Zero),
            new[] { (s15.Lineas.First(), 980_000m, FuenteFlete.Historica) },
            new[] { (sMultipunto, 3m, 180000m) });
        var c16 = Cotiza(s16, EstadoCotizacion.Entregada, ResultadoCotizacion.Ganada, coord1, new DateTimeOffset(2026, 5, 29, 9, 30, 0, TimeSpan.Zero),
            new[] { (s16.Lineas.First(), 2_400_000m, FuenteFlete.Sicetac) },
            new[] { (sItr, 1m, 120000m) });
        var c17 = Cotiza(s17, EstadoCotizacion.Entregada, ResultadoCotizacion.Perdida, coord2, new DateTimeOffset(2026, 5, 31, 16, 0, 0, TimeSpan.Zero),
            new[] { (s17.Lineas.First(), 850_000m, FuenteFlete.Manual) },
            Array.Empty<(clsServicioConexo, decimal, decimal)>());
        db.Cotizaciones.AddRange(c1, c5, c7, c8, c12, c13, c14, c15, c16, c17);

        // ----------------------------------------------------------------- Tarifas históricas / Biblioteca (12)
        clsTarifaHistorica Hist(clsRuta r, clsConfiguracionVehiculo c, TipologiaCarga t, decimal valor, DateOnly fecha, clsCotizacion? origen) =>
            new() { clsRuta = r, clsConfiguracionVehiculo = c, Tipologia = t, Valor = valor, Fecha = fecha, clsCotizacion = origen };

        db.TarifasHistoricas.AddRange(
            // Provenientes de cotizaciones ganadas (realimentan la Biblioteca)
            Hist(r2, v5, TipologiaCarga.GranelSolido, 1_820_000m, new DateOnly(2026, 5, 21), c13),
            Hist(r4, v2, TipologiaCarga.Alimentos, 980_000m, new DateOnly(2026, 5, 26), c15),
            Hist(r8, v7, TipologiaCarga.GranelSolido, 2_400_000m, new DateOnly(2026, 5, 29), c16),
            Hist(r2, v6, TipologiaCarga.Refrigerados, 2_950_000m, new DateOnly(2026, 6, 16), c8),
            // Históricas cargadas del tarifario legado (sin cotización de origen — edge)
            Hist(r1, v1, TipologiaCarga.CargaSeca, 950_000m, new DateOnly(2026, 4, 10), null),
            Hist(r3, v9, TipologiaCarga.Contenedor, 3_050_000m, new DateOnly(2026, 3, 5), null),
            Hist(r6, v2, TipologiaCarga.CargaSeca, 2_500_000m, new DateOnly(2025, 12, 18), null), // antigua
            Hist(r7, v9, TipologiaCarga.Contenedor, 2_050_000m, new DateOnly(2026, 2, 22), null),
            Hist(r9, v2, TipologiaCarga.CargaSeca, 1_200_000m, new DateOnly(2026, 1, 30), null),
            Hist(r11, v4, TipologiaCarga.CargaSuelta, 3_400_000m, new DateOnly(2024, 11, 15), null), // muy antigua (edge)
            Hist(r5, v3, TipologiaCarga.CargaSuelta, 820_000m, new DateOnly(2026, 5, 31), null), // legado (no de cotización perdida)
            Hist(r2, v2, TipologiaCarga.CargaSeca, 1_870_000m, new DateOnly(2026, 6, 1), null)
        );

        // ----------------------------------------------------------------- Auditoría (12)
        clsAuditLog Audit(clsUsuario u, string entidad, Guid entidadId, string accion, string diff, DateTimeOffset at) =>
            new() { clsUsuario = u, Entidad = entidad, EntidadId = entidadId, Accion = accion, Diff = diff, CreatedAt = at };

        db.AuditLogs.AddRange(
            Audit(admin1, "clientes", cPostobon.Id, "crear", "{\"nombre\":\"Postobón S.A.\"}", new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero)),
            Audit(coord1, "solicitudes", s1.Id, "crear", "{\"tipo\":\"spot\"}", new DateTimeOffset(2026, 6, 15, 8, 30, 0, TimeSpan.Zero)),
            Audit(coord1, "solicitudes", s1.Id, "actualizar", "{\"estado\":[\"pendiente\",\"en_costeo\"]}", new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero)),
            Audit(coord2, "solicitudes", s2.Id, "crear", "{\"tipo\":\"licitacion\",\"lineas\":12}", new DateTimeOffset(2026, 6, 14, 10, 0, 0, TimeSpan.Zero)),
            Audit(coord3, "cotizaciones", c7.Id, "entregar", "{\"total\":2185000}", new DateTimeOffset(2026, 6, 17, 9, 0, 0, TimeSpan.Zero)),
            Audit(coord2, "cotizaciones", c8.Id, "entregar", "{\"total\":2950000}", new DateTimeOffset(2026, 6, 16, 15, 0, 0, TimeSpan.Zero)),
            Audit(coord2, "cotizaciones", c8.Id, "marcar_resultado", "{\"resultado\":\"ganada\"}", new DateTimeOffset(2026, 6, 16, 18, 0, 0, TimeSpan.Zero)),
            Audit(coord1, "cotizaciones", c13.Id, "marcar_resultado", "{\"resultado\":\"ganada\"}", new DateTimeOffset(2026, 5, 21, 17, 0, 0, TimeSpan.Zero)),
            Audit(coord2, "cotizaciones", c14.Id, "marcar_resultado", "{\"resultado\":\"perdida\"}", new DateTimeOffset(2026, 5, 23, 17, 0, 0, TimeSpan.Zero)),
            Audit(lider1, "solicitudes", s10.Id, "actualizar", "{\"estado\":[\"pendiente\",\"descartada\"]}", new DateTimeOffset(2026, 6, 16, 13, 0, 0, TimeSpan.Zero)),
            Audit(sl1, "servicios_conexos", sStandby.Id, "actualizar", "{\"valor_base\":[42000,45000]}", new DateTimeOffset(2026, 6, 2, 11, 0, 0, TimeSpan.Zero)),
            Audit(admin2, "tarifas_sicetac", sicetac[0].Id, "importar", "{\"periodo\":202606,\"filas\":12}", new DateTimeOffset(2026, 6, 1, 9, 20, 0, TimeSpan.Zero))
        );

        await db.SaveChangesAsync();
        return true;
    }
}
