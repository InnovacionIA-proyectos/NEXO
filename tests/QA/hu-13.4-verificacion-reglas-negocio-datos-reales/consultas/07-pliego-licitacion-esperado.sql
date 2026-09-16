-- Insumo: docs/ejemplo_licitacion.xlsx (15 líneas reales, pliego de prueba
-- "LIC-2026-0091", fecha 2026-09-16). Objetivo: calcular el valor ESPERADO
-- (piso SICE-TAC + tarifa de plaza SAP, regla del mayor) para cada línea
-- ANTES de subir el Excel a la app — así la comparación contra lo que la app
-- realmente proponga es a ciegas, no ajustada después de ver el resultado.
--
-- Las 15 líneas ya vienen traducidas abajo con sus códigos de negocio fijos
-- (verificados en la sesión de EDA-2, no hace falta consultarlos):
--   LineaNegocio:  PuertoAInterior=1  InteriorAPuerto=2  Nacional=4
--                  DevolucionContenedor=6  Andino=7  Licitaciones=8
--   ClaseViaje:    Contenedor=1  Cupo=2  CargaSuelta=4  Extradimensionado=5
--
-- Lo que SÍ hace falta resolver contra el ambiente real (pasos 1 y 2 abajo)
-- antes de poder correr las 15 consultas del paso 3: el código de zona SAP
-- de cada ciudad, y el código numérico de cada tipo de vehículo.

-- ============================================================
-- PASO 1 — Resolver ciudad -> zona SAP.
-- Base de datos: dbsolucionesTI (confirmado en el código,
-- DbSolucionesTiDbContext — la misma que tiene tblSAPTarifas).
-- ============================================================
-- Ciudades únicas del pliego: Buenaventura, Bogotá, Medellín, Cali, Cartagena,
-- Bucaramanga, Barranquilla, Cúcuta, Villavicencio, Pereira, Manizales,
-- Santa Marta, Ibagué, Neiva.

SELECT d.CODIGOCENTROPOBLADO, d.NOMBREMUNICIPIO, d.NOMBREMOSTRAR,
       z.IDZONATARIFICACION, z.IDZONATRANSPORTE
FROM dbo.tblsapdane d
LEFT JOIN dbo.tblZonasTarificacionRelacion z ON z.IDZONATRANSPORTE = d.CODIGOCENTROPOBLADO
WHERE d.NOMBREMUNICIPIO IN (
    'Buenaventura','Bogota','Medellin','Cali','Cartagena','Bucaramanga',
    'Barranquilla','Cucuta','Villavicencio','Pereira','Manizales',
    'Santa Marta','Ibague','Neiva'
);
-- (ajustar tildes/mayúsculas según cómo estén guardadas realmente — probar
-- sin tilde primero, es lo que se confirmó como normalización real en EDA-2)

-- ============================================================
-- PASO 2 — Resolver nombre de vehículo -> código SAP.
-- Misma base: dbsolucionesTI, tabla tblSAPTiposVehiculoTarifario
-- (columnas confirmadas en el código: ID, CODIGO, DESCRIPCION, ESTADO).
-- ============================================================
-- Vehículos únicos del pliego: Tractomula 3S3, Tractomula 3S2, Mini Mula 2S2,
-- Mini Mula 2S3, Sencillo C2, Turbo, Camioneta, Doble Troque.

SELECT ID, CODIGO, DESCRIPCION, ESTADO
FROM dbo.tblSAPTiposVehiculoTarifario
WHERE DESCRIPCION LIKE '%3S3%' OR DESCRIPCION LIKE '%3S2%' OR DESCRIPCION LIKE '%2S2%'
   OR DESCRIPCION LIKE '%2S3%' OR DESCRIPCION LIKE '%Sencillo%' OR DESCRIPCION LIKE '%Turbo%'
   OR DESCRIPCION LIKE '%Camioneta%' OR DESCRIPCION LIKE '%Doble%'
   OR CODIGO LIKE '%3S3%' OR CODIGO LIKE '%3S2%' OR CODIGO LIKE '%2S2%' OR CODIGO LIKE '%2S3%';

-- ============================================================
-- PASO 3 — Tarifa de plaza SAP vigente por línea (dbSolucionesTI)
-- Reemplazar <<ZONA_...>> y <<VEHCOD_...>> con lo que salga de los pasos 1 y 2.
-- ============================================================

-- Línea 1: Buenaventura->Bogotá | LineaNegocio=1 | ClaseViaje=1 | Peso=24500 | Tractomula 3S3
SELECT 1 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BUENAVENTURA>>' AND DESTINO = '<<ZONA_BOGOTA>>'
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 24500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_3S3>>' AND VIGENCIA >= '2026-09-16';

-- Línea 2: Buenaventura->Medellín | LineaNegocio=1 | ClaseViaje=1 | Peso=18200 | Tractomula 3S2
SELECT 2 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BUENAVENTURA>>' AND DESTINO = '<<ZONA_MEDELLIN>>'
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 18200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_3S2>>' AND VIGENCIA >= '2026-09-16';

-- Línea 3: Medellín->Buenaventura | LineaNegocio=6 (DevolucionContenedor) | ClaseViaje=1 | Peso=3800 | Mini Mula 2S2
SELECT 3 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_MEDELLIN>>' AND DESTINO = '<<ZONA_BUENAVENTURA>>'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND 3800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_2S2>>' AND VIGENCIA >= '2026-09-16';

-- Línea 4: Bogotá->Buenaventura | LineaNegocio=6 | ClaseViaje=1 | Peso=3200 | Mini Mula 2S3
SELECT 4 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BOGOTA>>' AND DESTINO = '<<ZONA_BUENAVENTURA>>'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND 3200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_2S3>>' AND VIGENCIA >= '2026-09-16';

-- Línea 5: Cali->Cartagena | LineaNegocio=2 (InteriorAPuerto) | ClaseViaje=4 (CargaSuelta) | Peso=32000 | Tractomula 3S3 | Consolidado
SELECT 5 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_CALI>>' AND DESTINO = '<<ZONA_CARTAGENA>>'
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 4
  AND 32000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_3S3>>' AND VIGENCIA >= '2026-09-16';

-- Línea 6: Bucaramanga->Bogotá | LineaNegocio=4 (Nacional) | ClaseViaje=2 (Cupo) | Peso=8500 | Sencillo C2
SELECT 6 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BUCARAMANGA>>' AND DESTINO = '<<ZONA_BOGOTA>>'
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 2
  AND 8500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_C2>>' AND VIGENCIA >= '2026-09-16';
-- OJO: esta ruta ya se vio antes con 2 tablas de precio paralelas (Bogotá
-- normal vs ZTC_NORTE_BOGOTA) — si no aparece nada bajo la zona normal,
-- probar también con la zona Norte antes de concluir "sin tarifa".

-- Línea 7: Barranquilla->Buenaventura | LineaNegocio=2 | ClaseViaje=1 | Peso=21000 | Tractomula 3S3 | Exclusivo
SELECT 7 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BARRANQUILLA>>' AND DESTINO = '<<ZONA_BUENAVENTURA>>'
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 21000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_3S3>>' AND VIGENCIA >= '2026-09-16';

-- Línea 8: Cúcuta->Bogotá | LineaNegocio=7 (Andino) | ClaseViaje=4 | Peso=6800 | Turbo
SELECT 8 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_CUCUTA>>' AND DESTINO = '<<ZONA_BOGOTA>>'
  AND LINEANEGOCIO = 7 AND CLASEVIAJE = 4
  AND 6800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_TURBO>>' AND VIGENCIA >= '2026-09-16';
-- Esta ruta ya se documentó antes con 4 líneas de captura MANUAL desde Excel
-- (no reproducible por el algoritmo) — si no aparece nada, no forzar, puede
-- ser el mismo caso conocido.

-- Línea 9: Bogotá->Villavicencio | LineaNegocio=4 | ClaseViaje=2 | Peso=4200 | Camioneta
SELECT 9 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BOGOTA>>' AND DESTINO = '<<ZONA_VILLAVICENCIO>>'
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 2
  AND 4200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_CAMIONETA>>' AND VIGENCIA >= '2026-09-16';

-- Línea 10: Pereira->Buenaventura | LineaNegocio=2 | ClaseViaje=1 | Peso=19500 | Tractomula 3S2
SELECT 10 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_PEREIRA>>' AND DESTINO = '<<ZONA_BUENAVENTURA>>'
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 19500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_3S2>>' AND VIGENCIA >= '2026-09-16';

-- Línea 11: Buenaventura->Cali | LineaNegocio=1 | ClaseViaje=1 | Peso=25800 | Doble Troque
SELECT 11 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BUENAVENTURA>>' AND DESTINO = '<<ZONA_CALI>>'
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 25800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_DOBLETROQUE>>' AND VIGENCIA >= '2026-09-16';

-- Línea 12: Manizales->Medellín | LineaNegocio=7 | ClaseViaje=2 | Peso=7200 | Sencillo C2
SELECT 12 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_MANIZALES>>' AND DESTINO = '<<ZONA_MEDELLIN>>'
  AND LINEANEGOCIO = 7 AND CLASEVIAJE = 2
  AND 7200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_C2>>' AND VIGENCIA >= '2026-09-16';

-- Línea 13: Bogotá->Santa Marta | LineaNegocio=4 | ClaseViaje=5 (Extradimensionado) | Peso=34000 | Tractomula 3S3 | Exclusivo
SELECT 13 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_BOGOTA>>' AND DESTINO = '<<ZONA_SANTAMARTA>>'
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 5
  AND 34000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_3S3>>' AND VIGENCIA >= '2026-09-16';
-- Candidato fuerte a "sin tarifa de plaza" (extradimensionado es infrecuente)
-- — si no aparece nada, no es un error, gobierna el piso SICE-TAC en solitario
-- o queda para atención manual si tampoco hay piso.

-- Línea 14: Ibagué->Buenaventura | LineaNegocio=6 | ClaseViaje=1 | Peso=3500 | Mini Mula 2S2
SELECT 14 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_IBAGUE>>' AND DESTINO = '<<ZONA_BUENAVENTURA>>'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND 3500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_2S2>>' AND VIGENCIA >= '2026-09-16';

-- Línea 15: Neiva->Bogotá | LineaNegocio=8 (Licitaciones) | ClaseViaje=4 | Peso=9800 | Turbo
SELECT 15 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE ORIGEN = '<<ZONA_NEIVA>>' AND DESTINO = '<<ZONA_BOGOTA>>'
  AND LINEANEGOCIO = 8 AND CLASEVIAJE = 4
  AND 9800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND TIPOVEHICULO = '<<VEHCOD_TURBO>>' AND VIGENCIA >= '2026-09-16';

-- ============================================================
-- PASO 4 — Piso SICE-TAC vigente por línea (DWSice, tblRutasSicetac)
-- SICE-TAC usa nombre de ciudad en texto libre (NOMBRERUTA) y CONFIGURACION
-- suele coincidir con el código que ya trae el nombre del vehículo
-- (ej. "3S3", "2S2") — para Turbo/Camioneta buscar la categoría "Liviano"
-- con descripción pegada (match por prefijo, no igualdad exacta).
-- PERIODO real más reciente disponible: usar el que arroje
-- `SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac` (probablemente 202609).
-- ============================================================

SELECT 1 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Buenaventura%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION = '3S3' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 2 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Buenaventura%' AND NOMBRERUTA LIKE '%Medell%'
  AND CONFIGURACION = '3S2' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 3 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Medell%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '2S2' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 4 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bogot%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '2S3' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 5 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Cali%' AND NOMBRERUTA LIKE '%Cartagena%'
  AND CONFIGURACION = '3S3' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 6 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bucaramanga%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION = 'C2' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 7 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Barranquilla%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '3S3' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 8 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%C%cuta%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION LIKE 'Turbo%'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 9 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bogot%' AND NOMBRERUTA LIKE '%Villavicencio%'
  AND CONFIGURACION LIKE '%Liviano%'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- ("Camioneta" — probar CONFIGURACION LIKE '2L%' si 'Liviano' no trae nada)

SELECT 10 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Pereira%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '3S2' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 11 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Buenaventura%' AND NOMBRERUTA LIKE '%Cali%'
  AND CONFIGURACION LIKE '%C3%' -- "Doble Troque" — confirmar código exacto en catálogo, no asumir
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 12 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Manizales%' AND NOMBRERUTA LIKE '%Medell%'
  AND CONFIGURACION = 'C2' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 13 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bogot%' AND NOMBRERUTA LIKE '%Santa Marta%'
  AND CONFIGURACION = '3S3' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 14 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Ibagu%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '2S2' AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

SELECT 15 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Neiva%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION LIKE 'Turbo%'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

-- ============================================================
-- PASO 5 — Regla "solo SICE-TAC": ¿alguna de estas 15 rutas ya tiene la
-- regla activa? (app DB, CabinaFletes)
-- ============================================================
SELECT * FROM dbo.reglas_solo_sicetac
WHERE (Origen, Destino) IN (
    ('Buenaventura','Bogotá'), ('Buenaventura','Medellín'), ('Medellín','Buenaventura'),
    ('Bogotá','Buenaventura'), ('Cali','Cartagena'), ('Bucaramanga','Bogotá'),
    ('Barranquilla','Buenaventura'), ('Cúcuta','Bogotá'), ('Bogotá','Villavicencio'),
    ('Pereira','Buenaventura'), ('Buenaventura','Cali'), ('Manizales','Medellín'),
    ('Bogotá','Santa Marta'), ('Ibagué','Buenaventura'), ('Neiva','Bogotá')
);
