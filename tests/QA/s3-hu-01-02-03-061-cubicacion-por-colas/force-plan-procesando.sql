-- QA — S3-HU-06.1 (PR #123): simula un worker caido a mitad de proceso, forzando un
-- plan a quedar en 'procesando' con un timestamp viejo, para poder probar el
-- vigilante de timeouts sin esperar el timeout real completo.
--
-- Uso: contra la base de Cubicador (Postgres), reemplazando :plan_id por el id real
-- del plan creado con verify-vigilante-timeout.mjs:
--   psql -U cubicador -d cubicador -v plan_id="'<id>'" -f force-plan-procesando.sql
-- (o pegar el UPDATE con el id ya reemplazado directamente).

UPDATE planes_cargue
SET estado = 'procesando',
    updated_at = NOW() - INTERVAL '10 minutes'
WHERE id = :plan_id;

SELECT id, estado, updated_at FROM planes_cargue WHERE id = :plan_id;
