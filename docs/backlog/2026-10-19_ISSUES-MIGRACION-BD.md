# Migrar las bases de pruebas de NEXO y del Cubicador a bases propias

_Fecha: 2026-10-19_

Dos issues para Sara (datos y tablas). Motivo: hoy las dos bases de pruebas son **compartidas** con otra aplicación, cada una separada solo por esquema, no por base. La idea es que cada servicio tenga su propia base.

Alcance: **solo el ambiente de pruebas/QA**. No incluye producción.

**Dónde está hoy cada una (verificado en el código, 2026-10-19):**

| Servicio | Base actual | Servidor | Esquema propio | Comparte con |
|---|---|---|---|---|
| NEXO | `dbInformesAutomatizados_Pruebas` | 10.10.40.85 | `db_NEXO` | Otra app ajena a NEXO, esquema `dbo_prueba` |
| Cubicador | `dbTarifario_Nexo_Pruebas` | 10.10.40.85 | `dbo_cubicador` | Cotifly, esquema `dbo_cotifly` |

Nombre y servidor de las bases nuevas: **[NOMBRE_BASE_NUEVA]** y **[SERVIDOR_NUEVO]** — completar antes de crear la issue en GitHub.

---

## DATA-5 — Migrar la base de pruebas de NEXO a una base propia

Labels: `nexo`, `datos`, `infraestructura`

```
CONTEXTO (verificado)
- NEXO en pruebas vive en dbInformesAutomatizados_Pruebas.db_NEXO (servidor 10.10.40.85), compartida con otra app que usa el esquema dbo_prueba (encabezado de docs/Database/ESQUEMA_NEXO.sql).
- 31 tablas reales (tbl_*), más InboxState/OutboxMessage/OutboxState (mensajería), __EFMigrationsHistory y el trigger TRG_tarifas_plaza_historico.
- Este proyecto NO usa migraciones de EF Core: el esquema se aplica corriendo docs/Database/ESQUEMA_NEXO.sql completo contra una base vacía, y cualquier cambio futuro se edita a mano en ese archivo y se aplica a mano en cada base.
- La cadena de conexión es la variable ConnectionStrings__Nexo y el nombre del esquema es Database__Esquema (hoy db_NEXO); ninguna de las dos tiene un valor fijo en el código.

QUÉ HACER
1. Crear la base nueva [NOMBRE_BASE_NUEVA] en [SERVIDOR_NUEVO].
2. Correr docs/Database/ESQUEMA_NEXO.sql completo contra la base vacía (crea las 31 tablas, los índices y el trigger).
3. Copiar los datos de la base actual a la nueva, tabla por tabla, respetando las llaves foráneas.
4. Verificar que el número de filas de cada tabla coincide entre el origen y el destino.
5. Actualizar ConnectionStrings__Nexo y Database__Esquema del ambiente de QA para que apunten a la base nueva (en la configuración de Dokploy o el .env que corresponda; no escribir la cadena real en ningún documento del repo).
6. Coordinar con Ismael una ventana para el cambio: mientras se copian los datos, NEXO no debe escribir en la base vieja.
7. Decidir qué pasa con el esquema db_NEXO de la base vieja una vez verificado todo: borrarlo, o dejarlo un tiempo como respaldo.

RIESGOS
- Es una base compartida con otra app: avisar a quien la administra antes de tocar nada, y no tocar el esquema dbo_prueba.
- Sin migraciones de EF, cualquier diferencia de esquema entre la base vieja y la nueva hay que detectarla a mano (comparar columnas con sys.columns, no solo confiar en el .sql del repo).

CHECKLIST
- [ ] Base nueva creada
- [ ] ESQUEMA_NEXO.sql aplicado completo, sin errores
- [ ] Datos copiados y verificados (conteo de filas igual en origen y destino, tabla por tabla)
- [ ] Conexión de QA apuntando a la base nueva
- [ ] NEXO probado contra la base nueva (con Ismael o QA)
- [ ] Decisión tomada sobre la base vieja

CRITERIOS DE ACEPTACION
- [ ] NEXO en QA lee y escribe contra la base nueva, sin tocar dbInformesAutomatizados_Pruebas.
- [ ] Todas las tablas y el trigger existen en la base nueva, con los mismos datos que tenía la base vieja al migrar.
- [ ] Las pruebas que usan base real (Nexo.Api.Tests) pasan contra la base nueva.
```

---

## DATA-6 — Migrar la base de pruebas del Cubicador a una base propia

Labels: `nexo`, `datos`, `infraestructura` · Depende de: inventariar las tablas reales antes de migrar (paso 1)

```
CONTEXTO (verificado)
- El Cubicador en pruebas vive en dbTarifario_Nexo_Pruebas.dbo_cubicador (mismo servidor, 10.10.40.85), compartida con Cotifly, que usa el esquema dbo_cotifly.
- cubicador/database/esquema/ESQUEMA_CUBICADOR.sql documenta 9 tablas (vehiculos, TiposCarroceria, categoriaMercancia, tiposMercancia, relacionVehiculosCarroceria, reglasIncompatibilidad, parametrosMotor, recomendacionVehiculo, recomendacionVehiculoMercancia).
- OJO, riesgo conocido y documentado por el propio equipo del Cubicador (cubicador/docs/DECISIONES_TECNICAS.md): el esquema real en QA puede tener MÁS tablas que las que están en ese .sql, porque parte del catálogo se aplicó antes directo contra QA y nunca quedó documentado en el repo. No usar solo el .sql del repo como inventario: hay que consultarlo directo contra la base real.
- Tampoco usa migraciones automáticas: el esquema se aplica a mano, igual que en NEXO.
- La cadena de conexión es la variable DATABASE_URL y el esquema es CUBICADOR_DB_ESQUEMA (hoy dbo_cubicador).

QUÉ HACER
1. Primero que nada: listar las tablas reales de dbo_cubicador en QA con una consulta a sys.tables (filtrando por ese esquema), y comparar la lista contra ESQUEMA_CUBICADOR.sql. Documentar cualquier tabla que exista en QA y no esté en el script.
2. Crear la base nueva [NOMBRE_BASE_NUEVA_CUBICADOR] en [SERVIDOR_NUEVO] (puede ser la misma base nueva de NEXO en un esquema aparte, o una base separada: a definir con Ismael).
3. Aplicar ESQUEMA_CUBICADOR.sql y recrear a mano cualquier tabla que haya aparecido en el paso 1 y no esté en el script, dejándola documentada esta vez en el repo.
4. Copiar los datos y verificar que el número de filas coincide, tabla por tabla.
5. Actualizar DATABASE_URL y CUBICADOR_DB_ESQUEMA del ambiente de QA del Cubicador.
6. Avisar al equipo de Cotifly antes de tocar dbTarifario_Nexo_Pruebas: su esquema dbo_cotifly no se toca, pero es la misma base física y conviene que sepan que hay un cambio en curso ahí.
7. Decidir qué pasa con el esquema dbo_cubicador de la base vieja una vez verificado todo.

RIESGOS
- El riesgo principal es migrar un esquema incompleto si no se inventarían primero las tablas reales (paso 1 es obligatorio, no opcional).
- Es una base compartida con Cotifly: avisar antes de tocarla, aunque el esquema que se toca es distinto al suyo.

CHECKLIST
- [ ] Inventario de tablas reales de dbo_cubicador en QA, comparado contra ESQUEMA_CUBICADOR.sql
- [ ] Base nueva creada
- [ ] Esquema completo aplicado (incluidas las tablas no documentadas, si las hubo)
- [ ] Datos copiados y verificados
- [ ] Conexión de QA del Cubicador apuntando a la base nueva
- [ ] Cotifly avisado antes del cambio
- [ ] Cubicador probado contra la base nueva
- [ ] Decisión tomada sobre el esquema viejo

CRITERIOS DE ACEPTACION
- [ ] El Cubicador en QA lee y escribe contra la base nueva, sin tocar dbTarifario_Nexo_Pruebas.
- [ ] Todas las tablas reales (documentadas y las que se encuentren sin documentar) existen en la base nueva con los mismos datos.
- [ ] Cotifly confirma que su esquema dbo_cotifly sigue funcionando sin cambios.
```
