-- QA — S3-HU-02.3 (PR #127): fuerza el estado 'procesando' en la fila espejo de
-- NEXO (SQL Server), para probar de forma determinista el guard de
-- SolicitarOptimizacionCommand (409 PLAN_CARGUE_EN_PROCESO) sin depender de ganar
-- la ventana real de la carrera contra el Outbox de MassTransit.
--
-- Uso: reemplazar @PlanId por el id real del plan (creado con
-- verify-guard-carrera-crear-plan.mjs) y correr contra la base de NEXO.

DECLARE @PlanId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- reemplazar

UPDATE [db_NEXO].[planes_cargue]
SET Estado = 'procesando'
WHERE Id = @PlanId;

SELECT Id, Estado FROM [db_NEXO].[planes_cargue] WHERE Id = @PlanId;
