-- Base de datos: DWSice
-- El PDF real de la app dio valores muy distintos a mi "esperado" calculado
-- a mano. En vez de seguir adivinando el algoritmo leyendo código, busco
-- directamente el valor EXACTO que salió, para ver qué combinación real
-- (CONFIGURACION, NOMBRETIPOCARGA, NOMBREUNIDADTRANSPORTE, etc.) lo produce.

-- Línea 1: Buenaventura->Bogotá, obtenido $1.556.503
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 1556503) < 1;

-- Línea 5: Cali->Cartagena, obtenido $3.118.351
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 3118351) < 1;

-- Línea 6: Bucaramanga->Bogotá, obtenido $1.271.020
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 1271020) < 1;

-- Línea 9: Bogotá->Villavicencio, obtenido $867.043 (esta es la más rara —
-- yo había confirmado DOS VECES que esta ruta no tiene NINGUNA fila en
-- tblRutasSicetac para el período más reciente, sin ningún filtro)
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 867043) < 1;

-- Si la línea 9 no aparece en absoluto (ni buscando por valor exacto en TODA
-- la tabla, cualquier período), eso confirmaría que el $867.043 no vino de
-- tblRutasSicetac — vendría de otra fuente (ej. la caché local de la app,
-- tabla propia `tarifas_sicetac`, importada por Excel — ver el comentario en
-- src/CabinaFletes.Domain/BibliotecaTarifas/Entidades/Tarifas.cs).
