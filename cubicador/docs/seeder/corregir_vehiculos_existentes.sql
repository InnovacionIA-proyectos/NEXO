-- Corrección puntual de las filas de `vehiculos_cubicacion` que ya existen.
--
-- POR QUÉ HACE FALTA: `listar_con_siembra` siembra POR NOMBRE y solo agrega los
-- presets que faltan — nunca actualiza una fila existente. La corrección que se
-- hizo en `application/plan_cargue/vehiculos.py::presets()` solo aplica a bases
-- nuevas. Como los datos se migran a SQL Server en vez de arrancar vacío, las
-- filas viejas viajan con las medidas inventadas.
--
-- CUÁNDO CORRERLO: como paso de datos de la migración a SQL Server, DESPUÉS de
-- crear el esquema y ANTES del primer arranque de la aplicación.
--
-- IDEMPOTENTE: se puede correr más de una vez sin efecto adicional.
--
-- Fuente de los valores: Excel "Dimenciones y Peso por configuracion para
-- Cubicaje" (Operaciones). La capacidad del Excel es PAYLOAD, no peso bruto.
-- Tara, puerta y ejes no tienen fuente: solo se tocan donde la puerta o un eje
-- quedarían fuera de la sección/largo nuevos y `_validar` los rechazaría.

-- Turbo 2L2 — era 6200x2200x2000 / 6.300 kg. El eje trasero estaba en 4.600 mm,
-- fuera del largo nuevo, así que se reubican los dos.
UPDATE vehiculos_cubicacion
   SET largo_interno_mm = 4500,
       ancho_interno_mm = 2200,
       alto_interno_mm  = 2300,
       capacidad_kg     = 4000,
       ejes_json        = '[{"posicionMm": 750, "capacidadKg": 3500}, {"posicionMm": 3350, "capacidadKg": 7000}]',
       updated_at       = SYSDATETIMEOFFSET()
 WHERE nombre = 'Turbo con estacas';

-- Sencillo C2 — era 7200x2350x2200.
UPDATE vehiculos_cubicacion
   SET largo_interno_mm = 6500,
       ancho_interno_mm = 2400,
       alto_interno_mm  = 2400,
       capacidad_kg     = 8500,
       updated_at       = SYSDATETIMEOFFSET()
 WHERE nombre = 'Camion sencillo (estacas)';

-- Dobletroque C3 — era 7800x2400x2300.
UPDATE vehiculos_cubicacion
   SET largo_interno_mm = 8000,
       ancho_interno_mm = 2400,
       alto_interno_mm  = 2600,
       capacidad_kg     = 17000,
       updated_at       = SYSDATETIMEOFFSET()
 WHERE nombre = 'Camion rigido dobletroque';

-- Tractomula 3S3 (plancha / estacas) — era 12200x2500x2800 / 27.500 kg.
-- La puerta de 2500x2800 excedería la sección interna nueva.
UPDATE vehiculos_cubicacion
   SET largo_interno_mm = 12200,
       ancho_interno_mm = 2400,
       alto_interno_mm  = 2600,
       capacidad_kg     = 34000,
       puerta_ancho_mm  = 2400,
       puerta_alto_mm   = 2600,
       updated_at       = SYSDATETIMEOFFSET()
 WHERE nombre = 'Plataforma (tractomula)';

-- Tractomula Furgón 3S3 — era 13600x2450x2700 / 28.000 kg. El nombre quedó
-- desfasado (son 12,2 m, no 13,6): el renombre va por CRUD, no por acá, porque
-- la siembra es por nombre y renombrar crearía un duplicado.
UPDATE vehiculos_cubicacion
   SET largo_interno_mm = 12200,
       ancho_interno_mm = 2400,
       alto_interno_mm  = 2600,
       capacidad_kg     = 30000,
       puerta_ancho_mm  = 2400,
       puerta_alto_mm   = 2600,
       updated_at       = SYSDATETIMEOFFSET()
 WHERE nombre = 'Tractocamion furgon 13.6 m';

-- Minimula 2S2 — faltaba por completo en el catálogo. El Excel no trae tara ni
-- ejes: tara en 0 (no se usa en ningún cálculo del motor) y sin ejes, que el
-- motor soporta (no estima peso por eje).
-- Si la aplicación ya arrancó contra esta base, `listar_con_siembra` la habrá
-- insertado sola y este INSERT no hace nada.
INSERT INTO vehiculos_cubicacion (
        id, nombre, descripcion,
        largo_interno_mm, ancho_interno_mm, alto_interno_mm,
        capacidad_kg, tara_kg,
        puerta_ancho_mm, puerta_alto_mm, puerta_posicion,
        ejes_json, tolerancia_paredes_mm, tolerancia_entre_unidades_mm,
        es_preset, created_at, updated_at)
SELECT NEWID(), 'Minimula 2S2', 'Preset del sistema (editable)',
       12200, 2400, 2600,
       18000, 0,
       2400, 2600, 'trasera',
       '[]', 0, 0,
       1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
 WHERE NOT EXISTS (SELECT 1 FROM vehiculos_cubicacion WHERE nombre = 'Minimula 2S2');

-- Verificación: las 6 configuraciones del Excel, con sus medidas reales.
SELECT nombre, largo_interno_mm, ancho_interno_mm, alto_interno_mm, capacidad_kg
  FROM vehiculos_cubicacion
 WHERE nombre IN ('Turbo con estacas', 'Camion sencillo (estacas)',
                  'Camion rigido dobletroque', 'Minimula 2S2',
                  'Plataforma (tractomula)', 'Tractocamion furgon 13.6 m')
 ORDER BY largo_interno_mm;
