-- Insumo: docs/ejemplo_licitacion.xlsx (15 líneas reales, pliego de prueba
-- "LIC-2026-0091", fecha 2026-09-16). Objetivo: calcular el valor ESPERADO
-- (piso SICE-TAC + tarifa de plaza SAP, regla del mayor) para cada línea
-- ANTES de subir el Excel a la app, para comparar a ciegas contra lo que
-- realmente proponga CabinaFletes.
--
-- Documento único y final (reemplaza los archivos de diagnóstico 07b-07h,
-- que ya cumplieron su propósito y se eliminaron). El resultado final de
-- cada consulta ya está en el README.md de esta carpeta y en
-- ../esperado-final-pliego.md — este archivo queda como referencia
-- reproducible de cómo se obtuvo cada valor.
--
-- Líneas de negocio y clase de viaje ya traducidas a los códigos SAP fijos
-- (verificados en la sesión de EDA-2, Habilitador B8):
--   LineaNegocio:  PuertoAInterior=1  InteriorAPuerto=2  Nacional=4
--                  DevolucionContenedor=6  Andino=7  Licitaciones=8
--   ClaseViaje:    Contenedor=1  Cupo=2  CargaSuelta=4  Extradimensionado=5
--
-- Lecciones clave de esta sesión (para no repetir el tanteo):
--   1. TIPOVEHICULO en tblSAPTarifas guarda el ID numérico del catálogo
--      (tblSAPTiposVehiculoTarifario.ID), no el texto de CODIGO — y viene
--      BLANCO para LineaNegocio Puerto↔Interior (1, 2) y DevolucionContenedor
--      (6): esas tarifican solo por rango de peso, no filtrar por vehículo.
--   2. Bogotá y Medellín son zonas de GRUPO en tblSAPTarifas: ZTC_BOGOTA /
--      ZTC_NORTE_BOGOTA (Bogotá), ZTC_VABURRA (Medellín) — no existe
--      ZC_BOGOTA como valor real. La mayoría de las demás ciudades sí usan
--      su zona de grupo directo (ZTC_<ciudad>): Cali, Bucaramanga,
--      Barranquilla, Pereira, Manizales, Neiva. Cúcuta, Cartagena y
--      Villavicencio tienen AMBAS formas (ZC_ y ZTC_) coexistiendo.
--      Buenaventura, Santa Marta e Ibagué son zona individual (ZC_) sin
--      forma de grupo.
--   3. En SICE-TAC (tblRutasSicetac), filtrar siempre por
--      NOMBRETIPOCARGA = 'General' (o 'Contenedor vacío' para líneas de
--      devolución) — sin ese filtro trae todos los tipos de carga mezclados.
--   4. El código de vehículo de SICE-TAC (columna CONFIGURACION) es un
--      catálogo DISTINTO al de SAP: el código SAP "C2" (Rígidos, Turbo o
--      Sencillo) corresponde al código SICE-TAC "2" (sin sufijo, camión
--      rígido de 2 ejes) — no asumir que los códigos de un sistema sirven
--      en el otro.
--   5. `tblSAPTarifasLog` (histórico) NO participa del cálculo del
--      "esperado" ni de la decisión real de la app (`CandidatoSapAsync`
--      solo lee `tblSAPTarifas` con `Vigencia >= hoy`) — se usa solo como
--      evidencia de contexto para confirmar si una ruta tuvo tarifa antes.

-- ============================================================
-- PARTE A — Tarifa de plaza SAP vigente (base: dbSolucionesTI)
-- ============================================================

-- Línea 1: Buenaventura->Bogotá | LN=1 CV=1 | Peso=24500 | sin filtro de vehículo (blanco)
-- Resultado: $6.000.000 (ZTC_BOGOTA) / $6.400.000 (ZTC_NORTE_BOGOTA) — ambigüedad de zona conocida
SELECT 1 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_BUENAVENTURA' AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 24500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 2: Buenaventura->Medellín | LN=1 CV=1 | Peso=18200 | sin filtro de vehículo
-- Resultado: $5.000.000
SELECT 2 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_BUENAVENTURA' AND DESTINO IN ('ZTC_VABURRA', 'ZC_MEDELLIN')
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 18200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 3: Medellín->Buenaventura (devolución) | LN=6 CV=1 | CNT40.HC | sin filtro de vehículo
-- OJO: el peso real del pliego (3800) es ruido para esta línea — el rango
-- real está fijo en 4000-4000 (tara del contenedor de 40'), no se filtra por
-- el peso declarado.
-- Resultado: $2.454.000
SELECT 3 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_VABURRA', 'ZC_MEDELLIN') AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND VIGENCIA >= '2026-09-16';

-- Línea 4: Bogotá->Buenaventura (devolución) | LN=6 CV=1 | CNT20.ST
-- Resultado: sin tarifa (0 filas, confirmado sin filtro de peso también)
SELECT 4 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA') AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND VIGENCIA >= '2026-09-16';

-- Línea 5: Cali->Cartagena | LN=2 CV=1 (el pliego decía CV=4, la data real usa CV=1) | Peso=32000
-- Resultado: sin tarifa para el combo real (los candidatos que sí existen
-- tienen rangos de peso 18001-22000, muy por debajo de 32000)
SELECT 5 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_CALI' AND DESTINO IN ('ZTC_CARTAGENA', 'ZC_CARTAGENA (BOL)')
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 32000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 6: Bucaramanga->Bogotá | LN=4 CV=2 | TIPOVEHICULO=4 (C2) | Peso=8500
-- Resultado: sin tarifa VIGENTE — existió, venció 2026-09-10 (6 días antes)
SELECT 6 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_BUCARAMANGA' AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 2 AND TIPOVEHICULO = 4
  AND 8500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 7: Barranquilla->Buenaventura | LN=2 CV=1 | Peso=21000 | sin filtro de vehículo
-- Resultado: sin tarifa (0 filas, confirmado sin filtro de negocio/peso también)
SELECT 7 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_BARRANQUILLA' AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 21000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 8: Cúcuta->Bogotá | LN=7 CV=4 | Peso=6800 | "Turbo" comparte código C2=4
-- Resultado: sin tarifa VIGENTE — existió, venció 2026-08-10 (37 días antes)
SELECT 8 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZC_CUCUTA', 'ZTC_CUCUTA') AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 7 AND CLASEVIAJE = 4 AND TIPOVEHICULO = 4
  AND 6800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 9: Bogotá->Villavicencio | LN=4 CV=2 | Peso=4200 | "Camioneta" (código sin confirmar en catálogo SAP, no aplica igual)
-- Resultado: sin tarifa VIGENTE — existió, venció 2026-08-31 (16 días antes)
SELECT 9 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA') AND DESTINO IN ('ZC_VILLAVICENCIO', 'ZTC_VILLAVICENCIO')
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 2
  AND 4200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 10: Pereira->Buenaventura | LN=2 CV=1 | Peso=19500 | sin filtro de vehículo
-- Resultado: sin tarifa para el combo real (solo existe una tarifa de
-- devolución de contenedor para esta ruta, no la de carga normal)
SELECT 10 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_PEREIRA' AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 19500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 11: Buenaventura->Cali | LN=1 CV=1 | Peso=25800 | sin filtro de vehículo
-- Resultado: $2.400.000
SELECT 11 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_BUENAVENTURA' AND DESTINO = 'ZTC_CALI'
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 25800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 12: Manizales->Medellín | LN=7 CV=2 | TIPOVEHICULO=4 (C2) | Peso=7200
-- Resultado: sin tarifa VIGENTE — existió, venció 2026-09-03 (13 días antes)
SELECT 12 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_MANIZALES' AND DESTINO IN ('ZTC_VABURRA', 'ZC_MEDELLIN')
  AND LINEANEGOCIO = 7 AND CLASEVIAJE = 2 AND TIPOVEHICULO = 4
  AND 7200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 13: Bogotá->Santa Marta | LN=4 CV=5 (Extradimensionado) | Peso=34000
-- Resultado: sin tarifa (ruta poco frecuente, confirmado sin filtro de negocio/peso también)
SELECT 13 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA') AND DESTINO = 'ZC_SANTA MARTA'
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 5
  AND 34000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 14: Ibagué->Buenaventura (devolución) | LN=6 CV=1 | CNT20.ST
-- Resultado: sin tarifa (0 filas, confirmado sin filtro de peso también)
SELECT 14 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_IBAGUE' AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND VIGENCIA >= '2026-09-16';

-- Línea 15: Neiva->Bogotá | LN=8 CV=4 (Licitaciones) | Peso=9800 | "Turbo" comparte código C2=4
-- Resultado: sin tarifa VIGENTE — existió, venció 2026-02-10 (7 meses antes)
SELECT 15 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_NEIVA' AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 8 AND CLASEVIAJE = 4 AND TIPOVEHICULO = 4
  AND 9800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- ============================================================
-- PARTE B — Piso SICE-TAC vigente (base: DWSice)
-- Filtrar SIEMPRE por NOMBRETIPOCARGA ('General' o 'Contenedor vacío' para
-- devoluciones) y por el PERIODO más reciente disponible.
-- ============================================================

SELECT 1 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Buenaventura%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION = '3S3' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $4.907.956 (Buenaventura->Bogotá)

SELECT 2 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Buenaventura%' AND NOMBRERUTA LIKE '%Medell%'
  AND CONFIGURACION = '3S2' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $4.675.605 (Buenaventura->Medellín)

SELECT 3 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Medell%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '2S2' AND NOMBRETIPOCARGA = 'Contenedor vacío'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $2.454.868 (Medellín->Buenaventura, casi empate con plaza)

SELECT 4 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bogot%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '2S3' AND NOMBRETIPOCARGA = 'Contenedor vacío'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $2.656.946 (Bogotá->Buenaventura)

SELECT 5 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Cali%' AND NOMBRERUTA LIKE '%Cartagena%'
  AND CONFIGURACION = '3S3' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $9.841.527 (Cali->Cartagena)

SELECT 6 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bucaramanga%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION = '2' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $2.107.491 (Bucaramanga->Bogotá) — código '2', no 'C2' (catálogo SICE-TAC distinto al de SAP)

SELECT 7 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Barranquilla%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '3S3' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $10.419.069 (Barranquilla->Buenaventura)

SELECT 8 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%C%cuta%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION = '2' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $3.015.093 (Cúcuta->Bogotá)

SELECT 9 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bogot%' AND NOMBRERUTA LIKE '%Villavicencio%'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac);
-- Resultado: SIN FILAS EN ABSOLUTO para esta ruta (confirmado dos veces, sin
-- ningún filtro de vehículo/tipo de carga) — única línea junto con la 12 sin
-- ninguna fuente real. Requiere atención manual.

SELECT 10 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Pereira%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '3S2' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $2.624.742 (Pereira->Buenaventura)

SELECT 11 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Buenaventura%' AND NOMBRERUTA LIKE '%Cali%'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac);
-- Resultado: SIN FILAS EN ABSOLUTO (confirmado dos veces) — gobierna la
-- plaza en solitario, $2.400.000 (ver Parte A, línea 11).

SELECT 12 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Manizales%' AND NOMBRERUTA LIKE '%Medell%'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac);
-- Resultado: SIN FILAS EN ABSOLUTO — junto con la 9, la única sin ninguna
-- fuente real. Requiere atención manual.

SELECT 13 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Bogot%' AND NOMBRERUTA LIKE '%Santa Marta%'
  AND CONFIGURACION = '3S3' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $7.683.100 (Bogotá->Santa Marta)

SELECT 14 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Ibagu%' AND NOMBRERUTA LIKE '%Buenaventura%'
  AND CONFIGURACION = '2S2' AND NOMBRETIPOCARGA = 'Contenedor vacío'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $1.575.917 (Ibagué->Buenaventura)

SELECT 15 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Neiva%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION = '2' AND NOMBRETIPOCARGA = 'General'
  AND PERIODO = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac)
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;
-- Resultado: $1.719.671 (Neiva->Bogotá)

-- ============================================================
-- PARTE C — Regla 3 (solo SICE-TAC): ¿alguna de las 15 rutas ya tiene la
-- regla activa? (base: CabinaFletes, la app) — PENDIENTE DE CORRER
-- ============================================================
SELECT * FROM dbo.reglas_solo_sicetac
WHERE (Origen, Destino) IN (
    ('Buenaventura','Bogotá'), ('Buenaventura','Medellín'), ('Medellín','Buenaventura'),
    ('Bogotá','Buenaventura'), ('Cali','Cartagena'), ('Bucaramanga','Bogotá'),
    ('Barranquilla','Buenaventura'), ('Cúcuta','Bogotá'), ('Bogotá','Villavicencio'),
    ('Pereira','Buenaventura'), ('Buenaventura','Cali'), ('Manizales','Medellín'),
    ('Bogotá','Santa Marta'), ('Ibagué','Buenaventura'), ('Neiva','Bogotá')
);
