-- ============================================================
-- Verificación de Regla 1 (regla del mayor) y Regla 2 (piso inviolable)
-- contra el pliego de prueba docs/ejemplo_licitacion.xlsx (15 líneas,
-- referencia "LIC-2026-0091", fecha 2026-09-16).
--
-- Objetivo: calcular, para cada línea, el valor esperado según la regla de
-- negocio documentada (MAX entre el piso SICE-TAC vigente y la tarifa de
-- plaza SAP vigente), antes de subir el Excel a la app real, para comparar
-- a ciegas contra la propuesta que realmente genera CabinaFletes.
--
-- Resultado y análisis completo: ver README.md de esta carpeta y
-- ../esperado-final-pliego.md (fuente autoritativa de los valores finales).
-- Este archivo es la referencia reproducible de cómo se obtuvo cada valor.
--
-- Códigos de dominio (Línea de Negocio / Clase de Viaje), fijos en el
-- código de la aplicación (TarifarioSapAdapter.cs):
--   LineaNegocio:  PuertoAInterior=1  InteriorAPuerto=2  Nacional=4
--                  DevolucionContenedor=6  Andino=7  Licitaciones=8
--   ClaseViaje:    Contenedor=1  Cupo=2  CargaSuelta=4  Extradimensionado=5
--
-- Notas de implementación relevantes para reproducir estas consultas:
--   1. TIPOVEHICULO en tblSAPTarifas almacena el ID numérico del catálogo
--      (tblSAPTiposVehiculoTarifario.ID), no el texto de CODIGO. Queda
--      vacío para Línea de Negocio Puerto<->Interior (1, 2) y Devolución de
--      Contenedor (6): esas tarifican únicamente por rango de peso.
--   2. Bogotá y Medellín se codifican como zona de grupo en tblSAPTarifas:
--      ZTC_BOGOTA / ZTC_NORTE_BOGOTA (Bogotá), ZTC_VABURRA (Medellín) — no
--      existe ZC_BOGOTA como valor real. La mayoría de las demás ciudades
--      usan directamente su zona de grupo (ZTC_<ciudad>): Cali, Bucaramanga,
--      Barranquilla, Pereira, Manizales, Neiva. Cúcuta, Cartagena y
--      Villavicencio tienen ambas formas (ZC_ y ZTC_) coexistiendo.
--      Buenaventura, Santa Marta e Ibagué son zona individual (ZC_) sin
--      forma de grupo asociada.
--   3. En tblRutasSicetac, filtrar siempre por NOMBRETIPOCARGA (ver Parte B)
--      — sin ese filtro la consulta trae todos los tipos de carga mezclados.
--   4. El catálogo de código de vehículo de SICE-TAC (columna CONFIGURACION)
--      es independiente del catálogo de SAP.
--   5. tblSAPTarifasLog (histórico) no participa del cálculo de plaza
--      vigente ni de la decisión real de la app — el motor de costeo solo
--      lee tblSAPTarifas con VIGENCIA >= hoy. El histórico se usa aquí
--      únicamente como evidencia de contexto para confirmar si una ruta
--      tuvo tarifa de plaza en el pasado.
-- ============================================================

-- ============================================================
-- PARTE A — Tarifa de plaza SAP vigente (base: dbSolucionesTI)
-- ============================================================

-- Línea 1: Buenaventura -> Bogotá | LineaNegocio=1 ClaseViaje=1 | Peso=24500
-- Sin filtro de vehículo (TIPOVEHICULO vacío para esta Línea de Negocio).
-- Resultado: $6.000.000 (zona ZTC_BOGOTA) / $6.400.000 (zona ZTC_NORTE_BOGOTA)
SELECT 1 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_BUENAVENTURA' AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 24500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 2: Buenaventura -> Medellín | LineaNegocio=1 ClaseViaje=1 | Peso=18200
-- Resultado: $5.000.000
SELECT 2 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_BUENAVENTURA' AND DESTINO IN ('ZTC_VABURRA', 'ZC_MEDELLIN')
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 18200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 3: Medellín -> Buenaventura (devolución) | LineaNegocio=6 ClaseViaje=1 | CNT40.HC
-- El peso declarado en el pliego (3800) no aplica a esta línea: el rango de
-- peso real está fijo en 4000-4000 (tara del contenedor de 40'), por eso no
-- se filtra por peso en esta consulta.
-- Resultado: $2.454.000
SELECT 3 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_VABURRA', 'ZC_MEDELLIN') AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND VIGENCIA >= '2026-09-16';

-- Línea 4: Bogotá -> Buenaventura (devolución) | LineaNegocio=6 ClaseViaje=1 | CNT20.ST
-- Resultado: sin tarifa vigente (0 filas, verificado también sin filtro de peso)
SELECT 4 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA') AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND VIGENCIA >= '2026-09-16';

-- Línea 5: Cali -> Cartagena | LineaNegocio=2 ClaseViaje=1 | Peso=32000
-- (El pliego declara ClaseViaje=CargaSuelta/4; los datos reales de tarifa
-- de plaza para esta ruta+línea de negocio existen bajo ClaseViaje=1.)
-- Resultado: sin tarifa vigente para el combo real (los candidatos
-- existentes tienen rangos de peso 18001-22000, por debajo de 32000)
SELECT 5 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_CALI' AND DESTINO IN ('ZTC_CARTAGENA', 'ZC_CARTAGENA (BOL)')
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 32000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 6: Bucaramanga -> Bogotá | LineaNegocio=4 ClaseViaje=2 | TipoVehiculo=4 (C2) | Peso=8500
-- Resultado: sin tarifa vigente — existió, vencida desde 2026-09-10
SELECT 6 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_BUCARAMANGA' AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 2 AND TIPOVEHICULO = 4
  AND 8500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 7: Barranquilla -> Buenaventura | LineaNegocio=2 ClaseViaje=1 | Peso=21000
-- Resultado: sin tarifa vigente (0 filas, verificado también sin filtro de
-- Línea de Negocio/Clase de Viaje/peso)
SELECT 7 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_BARRANQUILLA' AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 21000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 8: Cúcuta -> Bogotá | LineaNegocio=7 ClaseViaje=4 | Peso=6800
-- Resultado: sin tarifa vigente para este combo exacto (0 filas en toda la
-- historia, verificado también sin filtro de peso)
SELECT 8 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZC_CUCUTA', 'ZTC_CUCUTA') AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 7 AND CLASEVIAJE = 4 AND TIPOVEHICULO = 4
  AND 6800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 9: Bogotá -> Villavicencio | LineaNegocio=4 ClaseViaje=2 | Peso=4200
-- Resultado: sin tarifa vigente — existió, vencida desde 2026-08-31
SELECT 9 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA') AND DESTINO IN ('ZC_VILLAVICENCIO', 'ZTC_VILLAVICENCIO')
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 2
  AND 4200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 10: Pereira -> Buenaventura | LineaNegocio=2 ClaseViaje=1 | Peso=19500
-- Resultado: sin tarifa vigente para el combo real (existe una tarifa de
-- devolución de contenedor para esta ruta, no la de carga normal buscada)
SELECT 10 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_PEREIRA' AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 2 AND CLASEVIAJE = 1
  AND 19500 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 11: Buenaventura -> Cali | LineaNegocio=1 ClaseViaje=1 | Peso=25800
-- Resultado: $2.400.000
SELECT 11 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_BUENAVENTURA' AND DESTINO = 'ZTC_CALI'
  AND LINEANEGOCIO = 1 AND CLASEVIAJE = 1
  AND 25800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 12: Manizales -> Medellín | LineaNegocio=7 ClaseViaje=2 | TipoVehiculo=4 (C2) | Peso=7200
-- Resultado: sin tarifa vigente para este combo exacto (0 filas en toda la historia)
SELECT 12 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_MANIZALES' AND DESTINO IN ('ZTC_VABURRA', 'ZC_MEDELLIN')
  AND LINEANEGOCIO = 7 AND CLASEVIAJE = 2 AND TIPOVEHICULO = 4
  AND 7200 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 13: Bogotá -> Santa Marta | LineaNegocio=4 ClaseViaje=5 (Extradimensionado) | Peso=34000
-- Resultado: sin tarifa vigente (0 filas en toda la historia — ruta poco frecuente)
SELECT 13 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA') AND DESTINO = 'ZC_SANTA MARTA'
  AND LINEANEGOCIO = 4 AND CLASEVIAJE = 5
  AND 34000 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- Línea 14: Ibagué -> Buenaventura (devolución) | LineaNegocio=6 ClaseViaje=1 | CNT20.ST
-- Resultado: sin tarifa vigente (0 filas, verificado también sin filtro de peso)
SELECT 14 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZC_IBAGUE' AND DESTINO = 'ZC_BUENAVENTURA'
  AND LINEANEGOCIO = 6 AND CLASEVIAJE = 1
  AND VIGENCIA >= '2026-09-16';

-- Línea 15: Neiva -> Bogotá | LineaNegocio=8 (Licitaciones) ClaseViaje=4 | Peso=9800
-- Resultado: sin tarifa vigente — existió, vencida desde 2026-02-10
SELECT 15 AS Linea, ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP
FROM dbo.tblSAPTarifas
WHERE ORIGEN = 'ZTC_NEIVA' AND DESTINO IN ('ZTC_BOGOTA', 'ZTC_NORTE_BOGOTA')
  AND LINEANEGOCIO = 8 AND CLASEVIAJE = 4 AND TIPOVEHICULO = 4
  AND 9800 BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '2026-09-16';

-- ============================================================
-- PARTE B — Piso SICE-TAC vigente (base: DWSice)
--
-- El motor real (TarifarioSapAdapter.SicetacOficialAsync) resuelve siempre
-- CONFIGURACION='CA', NOMBRETIPOCARGA='General', CONDICIONCARGA='CARGADO',
-- con independencia del vehículo y de la tipología declarados en la línea
-- (incluidas las líneas de devolución, que también resuelven a 'General',
-- no a 'Contenedor vacío'). Confirmado por coincidencia exacta contra el
-- PDF de cotización real en 12 de las 15 líneas — ver
-- ../esperado-final-pliego.md para el detalle del hallazgo.
--
-- NOMBRERUTA no distingue dirección (contiene ambas ciudades sin importar
-- el orden); cada consulta usa el par Origen-Destino exacto de la línea del
-- pliego para traer solo la fila correcta.
-- ============================================================

SELECT 1 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BUENAVENTURA VALLE DEL CAUCA - BOGOTA BOGOTA D. C.'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $1.556.503 — coincide exacto con el PDF de cotización real

SELECT 2 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BUENAVENTURA VALLE DEL CAUCA - MEDELLIN ANTIOQUIA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $1.595.367 — coincide exacto con el PDF de cotización real

SELECT 3 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'MEDELLIN ANTIOQUIA - BUENAVENTURA VALLE DEL CAUCA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $1.594.476 — coincide exacto con el PDF de cotización real

SELECT 4 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BOGOTA BOGOTA D. C. - BUENAVENTURA VALLE DEL CAUCA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $1.613.856 — coincide exacto con el PDF de cotización real

SELECT 5 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'CALI VALLE DEL CAUCA - CARTAGENA BOLIVAR'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $3.118.351 — coincide exacto con el PDF de cotización real

SELECT 6 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BUCARAMANGA SANTANDER - BOGOTA BOGOTA D. C.'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $1.271.020 — coincide exacto con el PDF de cotización real

SELECT 7 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BARRANQUILLA ATLANTICO - BUENAVENTURA VALLE DEL CAUCA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $3.280.034 — coincide exacto con el PDF de cotización real

SELECT 8 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'CUCUTA NORTE DE SANTANDER - BOGOTA BOGOTA D. C.'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $1.822.684 — piso disponible; esta línea no aparece en el PDF
-- de cotización real (ver Desviación 2 en esperado-final-pliego.md)

SELECT 9 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BOGOTA BOGOTA D. C. - VILLAVICENCIO META'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $867.043 — coincide exacto con el PDF de cotización real

SELECT 10 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'PEREIRA RISARALDA - BUENAVENTURA VALLE DEL CAUCA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $917.268 — coincide exacto con el PDF de cotización real

SELECT 11 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BUENAVENTURA VALLE DEL CAUCA - CALI VALLE DEL CAUCA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $705.417 — piso disponible; la app lo usó en lugar de la
-- tarifa de plaza vigente y mayor ($2.400.000, ver Parte A línea 11) —
-- ver Desviación 1 en esperado-final-pliego.md

SELECT 12 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'MANIZALES CALDAS - MEDELLIN ANTIOQUIA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $960.937 — coincide exacto con el PDF de cotización real

SELECT 13 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'BOGOTA BOGOTA D. C. - SANTA MARTA MAGDALENA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $2.442.568 — piso disponible; esta línea no aparece en el PDF
-- de cotización real (ver Desviación 2 en esperado-final-pliego.md)

SELECT 14 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'IBAGUE TOLIMA - BUENAVENTURA VALLE DEL CAUCA'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Resultado: $1.132.431 — coincide exacto con el PDF de cotización real

SELECT 15 AS Linea, NOMBRERUTA, CONFIGURACION, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA = 'NEIVA HUILA - BOGOTA BOGOTA D. C.'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
-- Pendiente de reconfirmar — ver 04-confirmar-linea-15-CA.sql. Esta línea
-- no aparece en el PDF de cotización real (Desviación 2); el valor exacto
-- del piso para esta dirección específica no quedó confirmado con esta
-- consulta en la sesión original.

-- ============================================================
-- PARTE C — Regla 3 (solo SICE-TAC): ¿alguna de las 15 rutas del pliego
-- tiene la regla activa? (base: dbInformesAutomatizados_Pruebas.dbo_NEXO_prueba)
--
-- Resultado: ninguna de las 15 rutas coincide. El sistema tiene 3 reglas
-- "solo SICE-TAC" configuradas (Medellín->Bogotá 3S3, Cali->Medellín 3S3,
-- Barranquilla->Pereira 2S3) — ninguna se solapa con este pliego. La Regla 3
-- requiere un caso de prueba dedicado con una de esas 3 rutas.
-- ============================================================
SELECT * FROM dbo.reglas_solo_sicetac
WHERE (Origen, Destino) IN (
    ('Buenaventura','Bogotá'), ('Buenaventura','Medellín'), ('Medellín','Buenaventura'),
    ('Bogotá','Buenaventura'), ('Cali','Cartagena'), ('Bucaramanga','Bogotá'),
    ('Barranquilla','Buenaventura'), ('Cúcuta','Bogotá'), ('Bogotá','Villavicencio'),
    ('Pereira','Buenaventura'), ('Buenaventura','Cali'), ('Manizales','Medellín'),
    ('Bogotá','Santa Marta'), ('Ibagué','Buenaventura'), ('Neiva','Bogotá')
);
