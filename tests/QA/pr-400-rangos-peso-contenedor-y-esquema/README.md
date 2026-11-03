# QA — PR #400: Rangos de peso por contenedor (#379) y esquema de BD verificado

## Resumen

PR de solo esquema de base de datos (sin código de aplicación). Dos partes:

1. **`tbl_rangos_peso_contenedor`** (`docs/Database/379_rangos_peso_contenedor.sql`,
   idempotente con `MERGE`): qué vehículo lleva un contenedor de 20 o 40 pies
   según su peso, con 10 rangos de carga inicial. `TipoVehiculo` es llave
   foránea hacia `tbl_mapeo_vehiculo_tarifario` (#377/PR #398) — se escoge la
   fila de menor `PesoMaximo` que alcance. No son los rangos de SAP que NEXO
   usa hoy (comparación y decisión documentadas en
   `docs/Database/seeder/DECISIONES-TECNICAS.md`).
2. **`ESQUEMA_NEXO.sql` reestructurado**: antes se detenía a mitad de camino
   en una base vacía porque la llave foránea de `OutboxMessage` apuntaba a un
   índice único que todavía no existía (las llaves foráneas se creaban antes
   que los índices). Ahora el orden es 1) tablas, 2) índices, 3) llaves
   foráneas, 4) triggers — así corre de una sola pasada sobre una base vacía.
   También agrega `datos_base_nexo.sql` (usuario de sistema, 9 mapeos de rol
   del portal, parámetros del score — lo que el modelo declara con `HasData`
   y que sin migraciones de EF nunca se crea solo).

Incluye los commits de #377 (PR #398, ya mergeada en `QA`); el merge con `QA`
tuvo 5 conflictos aditivos en `docs/Database/ESQUEMA_NEXO.sql` (dos tablas
nuevas en el mismo punto del archivo entre esta PR y #395/#394/#396, y la
reestructuración de secciones chocando con el orden viejo) — se resolvieron
combinando ambos lados y, en el caso de la reestructuración, verificando con
grep que no quedaran `CREATE TABLE`/índices duplicados antes de dar el merge
por bueno.

## Qué se verificó

1. Diff aislado de la PR contra su propia base (`git diff origin/377-...
   ...origin/379-...`, excluyendo los commits ya compartidos con #398): solo
   el script de rangos, su reflejo en `ESQUEMA_NEXO.sql`, el seeder CSV,
   `datos_base_nexo.sql`, documentación de decisiones técnicas y el README
   del seeder. Sin cambios `.cs`.
2. `379_rangos_peso_contenedor.sql` revisado línea por línea contra
   `docs/Database/seeder/rangos_peso_contenedor.csv`: las 10 filas del
   `MERGE` coinciden.
3. **Verificación rigurosa de la afirmación central de la PR** ("`ESQUEMA_NEXO.sql`
   corre completo, de una sola pasada, en una base vacía"): se levantó un
   SQL Server 2019 **desechable en Docker** (no la BD compartida de
   pruebas), se creó una base vacía con compatibilidad 130 y colación
   `Modern_Spanish_CI_AS` (igual que la PR dice haber usado), y se corrió
   `ESQUEMA_NEXO.sql` completo de una sola pasada → **0 errores, 40 tablas
   creadas**. Después se corrieron `datos_base_nexo.sql` y los 6 scripts de
   catálogo (304, 352, 356, 361, 377, 379) sobre ese esquema nuevo → sin
   errores.
4. **Idempotencia de `datos_base_nexo.sql`** confirmada corriéndolo dos
   veces seguidas sobre la misma base: mismo resultado ambas veces
   (`usuario_sistema=1, mapeos_rol=9, parametros_score=1`), sin duplicar.
5. `dotnet build -c Release` tras el merge → 0 errores (mismos warnings
   preexistentes de `QA`, no de esta PR).

## Datos de prueba

Ninguno contra la BD compartida de pruebas: la verificación del punto 3 se
hizo contra un contenedor Docker desechable, creado y eliminado dentro de
esta misma sesión (`docker rm -f` al final), sin tocar
`dbInformesAutomatizados_Pruebas`. Se decidió, junto con Kevin, **no**
aplicar `379_rangos_peso_contenedor.sql` ni `datos_base_nexo.sql` contra la
BD real en esta verificación — la rigurosidad del contenedor desechable ya
cubre la pregunta de si el script corre bien; aplicarlo contra la BD
compartida queda para cuando el código que lo consuma (#381/PR #406) se
verifique.

Ver `verificar.ps1` para el script runnable equivalente (reproducible en
otra máquina con Docker Desktop corriendo).

## Resultado

Verificación técnica completa, sin hallazgos. Pendiente de autorización de
Kevin para comentar/aprobar/mergear en GitHub.
