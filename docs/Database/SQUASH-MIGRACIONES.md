# Squash de migraciones: una sola migración para producción

> **Actualización (2026-09-26, Claude Code):** este runbook ya se ejecutó, con dos
> ajustes sobre lo escrito originalmente — ver "Hallazgo 2026-09-26" al final del
> documento antes de usar los valores de Paso 1/2 de abajo, que están desactualizados
> (quedan como referencia del procedimiento, no como los valores reales a usar).

Objetivo (pedido explícito del negocio): que producción arranque **en cero**
con **una sola migración estructural** (`InitialCreate`), sin arrastrar las 29
migraciones incrementales que documentan la historia de desarrollo. Sin datos
de más: la migración crea tablas/índices/FKs únicamente — la única fila que
trae es la que EF ya declara como parte del modelo (`clsParametrosScore`
singleton vía `HasData` en `clsNexoDbContext.cs`), no es un seed manual nuevo.
`clsDataSeeder.cs` (datos ficticios de demo) queda fuera de esto, como ya está
(`SEED_ON_STARTUP=false` en `.env.dokploy`).

**Quién debe correr esto**: requiere `dotnet` + `dotnet-ef` instalados (esta
sesión de Cowork no tiene el SDK de .NET disponible en el puente a este
equipo, así que este documento es el runbook para que TÚ o la sesión de
Claude Code en VS Code lo ejecuten). Avisa a quien esté trabajando en el repo
en paralelo antes de borrar las migraciones — es una operación que toca a
todo el mundo que tenga el repo clonado.

## Por qué no es tan simple como borrar y regenerar

`dbInformesAutomatizados_Pruebas.db_NEXO` (la base de pruebas REAL, no
efímera) ya tiene las 29 migraciones actuales aplicadas — su tabla
`__EFMigrationsHistory` las tiene registradas. Si generamos una migración
`InitialCreate` nueva y le corremos `dotnet ef database update` tal cual, EF
intentará ejecutar el `CREATE TABLE` de tablas que YA EXISTEN ahí — falla.

Además, el login de pruebas (`userInformesOw`) **no tiene permisos DDL** (ver
`docs/Testing/PRUEBAS-LOCALES.md`, sección "Qué NO hacer") — ni aunque
quisiéramos, no podría recrear el esquema.

La solución estándar de EF Core para esto es "fake apply": generar la
migración nueva, pero en vez de ejecutarla contra una base que ya tiene ese
esquema, insertar a mano la fila en `__EFMigrationsHistory` que dice "esta
migración ya se aplicó" — sin tocar ni una tabla.

## Paso 1 — Generar la migración InitialCreate

En PowerShell, desde la raíz del repo, **todo en la misma consola** (mismo
criterio que `docs/Testing/PRUEBAS-LOCALES.md`: si `Database__Esquema` no
llega al proceso, `clsEsquemaNexo` truena por ser fail-closed):

```powershell
Get-Content .env | Where-Object { $_ -match '^(ConnectionStrings__Nexo|Database__Esquema)=' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    Set-Item -Path "env:$k" -Value $v
}

# Borra las 29 migraciones actuales + el snapshot (dotnet ef las regenera).
Remove-Item src/Nexo.Infrastructure/Persistence/Migrations/*.cs

dotnet ef migrations add InitialCreate `
    --project src/Nexo.Infrastructure `
    --startup-project src/Nexo.Api `
    --output-dir Persistence/Migrations

dotnet build Nexo.Clean.sln -c Release
```

Verifica que solo quedaron 3 archivos nuevos en `Persistence/Migrations/`:
`<timestamp>_InitialCreate.cs`, `<timestamp>_InitialCreate.Designer.cs`, y
`clsNexoDbContextModelSnapshot.cs` regenerado. Anota el `<timestamp>` exacto
(ej. `20260921143000`) — lo necesitas en el paso 2. Anota también el
`ProductVersion` que EF escribió en el `.Designer.cs` (línea
`HasAnnotation("ProductVersion", "...")`, cerca del final) — normalmente algo
como `10.0.0`.

**No corras `dotnet ef database update` contra
`dbInformesAutomatizados_Pruebas` con esta migración nueva** — seguí al
paso 2 primero.

## Paso 2 — "Fake apply" en la base de pruebas real

Conéctate a `dbInformesAutomatizados_Pruebas` (10.10.40.85) con una cuenta que
tenga al menos permiso de INSERT/DELETE sobre
`db_NEXO.__EFMigrationsHistory` (es una tabla común, no requiere DDL — si
`userInformesOw` no alcanza, pregunta a quien administre esa base). Antes de
nada, respalda el estado actual por si hay que revertir:

```sql
SELECT * INTO db_NEXO.__EFMigrationsHistory_backup_20260921
FROM db_NEXO.__EFMigrationsHistory;
```

Luego, reemplaza las 29 filas por la única fila nueva (usa el
`<timestamp>_InitialCreate` y el `ProductVersion` anotados en el paso 1):

```sql
BEGIN TRAN;

DELETE FROM db_NEXO.__EFMigrationsHistory;

INSERT INTO db_NEXO.__EFMigrationsHistory ([MigrationId], [ProductVersion])
VALUES (N'<timestamp>_InitialCreate', N'<ProductVersion, ej. 10.0.0>');

COMMIT;
```

Esto NO toca ninguna tabla de datos — solo le dice a EF "ya corriste esta
migración, no la vuelvas a ejecutar". El esquema real (tablas/columnas/datos
existentes) queda exactamente igual que antes.

## Paso 3 — Verificar

```powershell
dotnet ef migrations list --project src/Nexo.Infrastructure --startup-project src/Nexo.Api
```

Debe mostrar solo `InitialCreate`, sin el signo de "pendiente". Después, corre
la suite completa contra la base real (`docs/Testing/PRUEBAS-LOCALES.md`,
Opción 1 o 2) — debe pasar exactamente igual que antes, porque el esquema no
cambió, solo se consolidó su historial.

## Paso 4 — Producción (base nueva, "entra en 0")

Para un despliegue nuevo contra una base que NUNCA ha tenido NEXO (el caso
real de producción): con `Database__MigrateOnStartup=true` en el primer
arranque (ver `DOKPLOY.md`, sección de migraciones) o `dotnet ef database
update` manual, EF va a crear TODO el esquema — las 28 tablas (incluida
`tbl_municipios`, S4-HU-09.1), FKs, índices y
la fila singleton de `clsParametrosScore` — de una sola pasada, ejecutando
únicamente `InitialCreate`. Después del primer arranque exitoso, cambia
`MIGRATE_ON_STARTUP` de vuelta a `false` (mismo criterio que ya documenta
`DOKPLOY.md`).

### Paso 4.1 — Llenado automático de `tbl_municipios` (S4-HU-09.1)

`InitialCreate` crea la tabla `tbl_municipios` vacía (es solo estructura,
igual que cualquier otra tabla). Para que quede LLENA con el catálogo
(3.804 municipios de Colombia/Venezuela/Ecuador, cruzados contra SICE-TAC y
Tarifario SAP) no hace falta correr nada aparte a mano — se resuelve con una
variable de entorno más, en el mismo primer arranque:

```
Database__SembrarMunicipiosOnStartup=true
```

Qué hace esto al arrancar la Api (ver el bloque correspondiente en
`clsProgram.cs`, justo después del de `MigrateOnStartup`/`SeedOnStartup`):

1. Si `ConnectionStrings__DbSolucionesTi` no está configurada en ese
   ambiente, no rompe el arranque — solo deja un log de advertencia y sigue
   (la tabla queda creada pero vacía; se puede sembrar después a mano, ver
   abajo).
2. Si la tabla YA tiene filas (por ejemplo, en un redeploy), no hace nada —
   es idempotente, seguro dejarlo en `true` de forma permanente.
3. Si está configurado y la tabla está vacía, construye el catálogo
   completo en vivo (lee `dbSolucionesTI`: `tblSAPDANE`,
   `tblZonasTarificacionRelacion`, `tblSAPTarifas`/`tblSAPTarifasLog`),
   incluidas las 6 excepciones de corregimientos/centros poblados
   resueltas a mano (Doradal, Catambuco, Bolombolo, Paraguachón,
   Jerusalén-Sonsón con código inventado `05756000-1`, Guayabal De
   Síquima), y lo inserta de una sola vez.

Es decir: **para que la persona que despliega en cero termine con la tabla
creada y llena, solo necesita dos variables en el primer arranque**
(`Database__MigrateOnStartup=true` y `Database__SembrarMunicipiosOnStartup=true`,
ya están las dos en `.env.dokploy` como plantilla). Después del primer
arranque exitoso, `MigrateOnStartup` se vuelve a `false` (regla de siempre);
`SembrarMunicipiosOnStartup` se puede dejar en `true` sin riesgo (punto 2) o
apagar también, es indistinto.

**Alternativa manual** (si no se quiere activar el flag de arranque, o para
volver a intentarlo en un ambiente donde el arranque fue con
`DbSolucionesTi` mal configurada): `POST
/api/captura-solicitudes/municipios/sembrar` (rol `Admin`) hace exactamente
lo mismo bajo demanda. Devuelve `409 Conflict` si la tabla ya tiene filas
(mismo guard de "una sola vez") y `503 Service Unavailable` si
`DbSolucionesTi` no está configurada.

Para consultar el catálogo ya sembrado: `GET
/api/captura-solicitudes/municipios` (roles `Comercial,Admin`) — devuelve
todas las filas y columnas (`id`, `codigoDane`, `municipio`, `departamento`,
`pais`, `nombreTarifasPlaza`, `zonaTarificacion`).

## Hallazgo importante: 10 FKs que faltan en la base de pruebas real

Al comparar `ESQUEMA_NEXO.sql` contra un DDL exportado directamente de
`dbInformesAutomatizados_Pruebas.db_NEXO` (2026-09-21) se encontró que 10
foreign keys que el modelo C# actual declara (y que migraciones ya aplicadas
crearon en su momento: `RelacionesYValidaciones`, `OutboxMassTransit`) NO
existen hoy en esa base — alguien las quitó a mano, fuera de EF. Dos de esas
10 (`FK_solicitudes_clientes_ClienteId`, `FK_planes_cargue_vehiculos_cubicacion_VehiculoId`)
nunca las creó NINGUNA de las 29 migraciones, pese a que el modelo ya las
declara — un cambio de modelo que nunca se migró, previo a este squash.

Las 10 FKs afectadas: `FK_solicitudes_clientes_ClienteId`,
`FK_solicitudes_usuarios_CreadoPor`,
`FK_planes_cargue_vehiculos_cubicacion_VehiculoId`,
`FK_tarifas_historicas_cotizaciones_CotizacionId`,
`FK_servicios_conexos_usuarios_ActualizadoPor`,
`FK_tarifas_historicas_configuraciones_vehiculo_ConfigVehiculoId`,
`FK_tarifas_historicas_rutas_RutaId`, `FK_tarifas_sicetac_rutas_RutaId`,
`FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId`,
`FK_OutboxMessage_OutboxState_OutboxId`.

Decisión tomada: no tocar `dbInformesAutomatizados_Pruebas` por ahora — el
DDL/InitialCreate (que sí trae las 10 FKs correctamente, verificado línea por
línea) es la fuente que de verdad importa para producción. Consecuencia
directa para el Paso 4: esto SOLO queda resuelto si producción arranca "en
cero" y `InitialCreate` corre de verdad (crea las 10 FKs junto con todo lo
demás). Si en cambio producción se siembra restaurando/clonando una copia de
`dbInformesAutomatizados_Pruebas` (en vez de dejar correr la migración), esa
copia arrastra el mismo hueco — hay que agregar esas 10 FKs a mano ahí antes
o durante el fake-apply, revisando primero que no haya filas huérfanas que
lo impidan (ClienteId/RutaId/etc. apuntando a un Id que ya no existe).

Los 5 índices únicos filtrados (WHERE ... IS NOT NULL, ej.
`IX_usuarios_IdentidadPortalId`) que el DDL exportado tampoco mostraba se
descartaron como hallazgo real: se extrajeron con DBeaver, que no siempre
scriptea el predicado WHERE de un índice filtrado. El modelo C#
(`clsNexoDbContext.cs`, `HasFilter(...)`) y `ESQUEMA_NEXO.sql` sí lo traen
bien — no requiere cambios.

## Hallazgo 2026-09-22: MetricasJson faltante + mapeo de clsCliente roto

Probando `/api/planes-cargue` en vivo (deploy de prueba) aparecieron dos
errores SQL en TODA lectura/escritura del módulo de plan de cargue:

```
Invalid column name 'MetricasJson'.
Invalid column name 'clsCliente'.
```

Dos causas distintas, dos fixes distintos:

1. **`MetricasJson` (tbl_planes_cargue) — columna que de verdad nunca existió.**
   `clsPlanCargue.cs` la declara desde que NEXO empezó a guardar el `metricas`
   que llega en el evento `plan-de-cargue.generado` (cubicador/CONTRATO.md
   §3.2), pero ni `ESQUEMA_NEXO.sql` ni la base real la tenían. Se agregó al
   DDL (este commit). Para una base ya creada (no arranca "en cero"), el fix
   es un `ALTER TABLE` aditivo, idempotente y sin downtime:
   ```sql
   IF NOT EXISTS (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_planes_cargue]') AND name = 'MetricasJson'
   )
       ALTER TABLE [db_NEXO].[tbl_planes_cargue] ADD [MetricasJson] nvarchar(max) NULL;
   ```

2. **`clsCliente` (tbl_items_plan_cargue) — NO era una columna faltante.**
   La columna real siempre fue `[Cliente]` (`ESQUEMA_NEXO.sql` línea ~430);
   el modelo C# (`clsItemPlanCargue.clsCliente`, un dato de texto suelto del
   ítem — cliente al que pertenece esa mercancía, no navegación a la entidad
   `clsCliente`) nunca declaró el mapeo, así que EF asumía por convención que
   la columna se llamaba igual que la propiedad. Fix en
   `clsNexoDbContext.cs`: `e.Property(x => x.clsCliente).HasColumnName("Cliente")`.
   Cero cambio de esquema, cero cambio de contrato (JSON de la API y mensaje
   con Cubicador siguen llamándose `clsCliente`/`cliente` igual que siempre).

Consecuencia práctica: hasta que el hotfix del punto 1 corra, cualquier plan
de cargue creado (incluso por `POST /api/planes-cargue/demo`, que es async)
queda en la base sin poder leerse ni borrarse por API — el mismo 500 aplica
a `GET`, `DELETE` y a `POST .../items`.

## Otros entornos con la base ya creada (dev local de cada quien)

Si alguien más tiene una base de desarrollo LOCAL (no la compartida de
pruebas) con las 29 migraciones ya aplicadas, lo más simple es que la borre y
la deje recrear desde cero con `InitialCreate` — es una base descartable, no
hace falta el fake-apply ahí. El fake-apply del Paso 2 es solo para
`dbInformesAutomatizados_Pruebas`, que es compartida y no se puede tirar.

## Después de mergear

- `docs/Database/ESQUEMA_NEXO.sql` ya quedó actualizado para reflejar que la
  fuente de la verdad es ahora una sola migración (este documento cierra ese
  pendiente) — no debería requerir más cambios, el esquema final no cambia,
  solo su historial.
- Avisa a la sesión de VS Code / a cualquiera con el repo clonado que hizo
  falta borrar y recrear las migraciones, para que no se queden con una copia
  vieja de `Migrations/` fuera de sync.

## Hallazgo 2026-09-26 (Claude Code, VS Code): un bug real + una tabla nueva en el mismo paquete

Al ejecutar este runbook (a raíz de S4-HU-09.1, que agrega la tabla
`tbl_municipios`) aparecieron dos cosas que cambian los valores de arriba:

**1. Bug real encontrado: `MigrationsHistoryTable` nunca estaba configurado.**
Ni `clsNexoDbContextFactory.cs` (diseño) ni `clsDependencyInjection.cs`
(runtime) le decían a EF que `__EFMigrationsHistory` vive en el esquema
`db_NEXO` — sin esa configuración, EF asume el esquema `dbo` por defecto.
Verificado contra `ESQUEMA_NEXO.sql` (generado del catálogo real) que la
tabla real SÍ está en `db_NEXO.__EFMigrationsHistory` — o sea, el código
apuntaba a una tabla equivocada (`dbo.__EFMigrationsHistory`, que ni existe)
para el historial de migraciones. Si este runbook se hubiera ejecutado tal
cual sin corregir esto, el fake-apply del Paso 2 habría escrito en el lugar
equivocado, dejando el historial real (`db_NEXO.__EFMigrationsHistory`, con
las 29 migraciones viejas) intacto — cualquier `dotnet ef database update`
futuro seguiría intentando recrear todo el esquema viejo y fallando. **Ya
corregido** (commit de esta HU): se agregó
`sql.MigrationsHistoryTable("__EFMigrationsHistory", clsEsquemaNexo.Nombre)`
en ambos lugares.

**2. La migración `InitialCreate` regenerada ya NO es "cero cambios de
esquema"** — incluye `tbl_municipios` (S4-HU-09.1), que es genuinamente nueva
en `dbInformesAutomatizados_Pruebas`. El Paso 2 de arriba (fake-apply puro)
ya no aplica tal cual: hay que crear esa tabla de verdad ANTES de marcar la
migración como aplicada. Valores reales a usar (reemplazan los de Paso 1):

- MigrationId: `20260926195944_InitialCreate`
- ProductVersion: `10.0.0`

### Paso 2 corregido — crear `tbl_municipios` de verdad + fake-apply del resto

Requiere una cuenta con permiso DDL sobre `db_NEXO` (no `userInformesOw`) para
crear la tabla, y permiso de INSERT/DELETE sobre
`db_NEXO.__EFMigrationsHistory` para el fake-apply — pídele a quien
administre `dbInformesAutomatizados_Pruebas` (10.10.40.85) que corra esto:

```sql
-- Respaldo antes de nada, por si hay que revertir.
SELECT * INTO db_NEXO.__EFMigrationsHistory_backup_20260926
FROM db_NEXO.__EFMigrationsHistory;

BEGIN TRAN;

-- 1) Lo único genuinamente nuevo: la tabla de S4-HU-09.1. Esto SÍ se ejecuta
--    de verdad (no es fake) porque no existe en ningún lado todavía.
CREATE TABLE [db_NEXO].[tbl_municipios] (
    [Id] int NOT NULL IDENTITY,
    [CodigoDane] nvarchar(20) NOT NULL,
    [Municipio] nvarchar(150) NOT NULL,
    [Departamento] nvarchar(150) NOT NULL,
    [Pais] nvarchar(2) NOT NULL,
    [NombreTarifasPlaza] nvarchar(60) NULL,
    [ZonaTarificacion] nvarchar(60) NULL,
    CONSTRAINT [PK_tbl_municipios] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_tbl_municipios_CodigoDane_Pais]
    ON [db_NEXO].[tbl_municipios] ([CodigoDane], [Pais]);

CREATE INDEX [IX_tbl_municipios_NombreTarifasPlaza]
    ON [db_NEXO].[tbl_municipios] ([NombreTarifasPlaza]);

-- 2) El resto de tablas ya existen — fake-apply: solo actualizar el
--    checklist de EF, sin tocarlas.
DELETE FROM db_NEXO.__EFMigrationsHistory;

INSERT INTO db_NEXO.__EFMigrationsHistory ([MigrationId], [ProductVersion])
VALUES (N'20260926195944_InitialCreate', N'10.0.0');

COMMIT;
```

Después, sembrar el catálogo llamando una vez
`POST /api/captura-solicitudes/municipios/sembrar` (rol Admin) contra ese
mismo ambiente — eso sí lo hace la aplicación, no un script SQL, porque
necesita leer en vivo de `dbSolucionesTI` (ver
`clsSembrarMunicipiosCommandHandler`).

**Ya ejecutado (2026-09-26):** la siembra corrió contra
`dbInformesAutomatizados_Pruebas.db_NEXO` — 3.804 municipios (Colombia 1.128,
Venezuela 1.424, Ecuador 1.252), 591 con código de Tarifario, 97 con zona
oficial. Verificado uno por uno: los 6 casos especiales (Doradal, Catambuco,
Bolombolo, Paraguachón, Jerusalén-Sonsón con código inventado
`05756000-1`, y Guayabal De Síquima) quedaron correctos.

**Corrección puntual aplicada después de la siembra (Guayabal De Síquima):**
el match automático no cubría este caso (el código real `ZC_GUAYABAL SIQUIMA`
omite la palabra "DE" que sí tiene el nombre oficial de DANE — la comparación
por sub-cadena no lo resuelve). Se agregó el alias en
`clsResolucionMunicipios.AliasCodigoZcAMunicipio` (para que cualquier siembra
futura en otra base lo resuelva sola) y, como esta base ya estaba sembrada
(el guard de "una sola vez" no permite volver a correr el endpoint), se
corrigió la fila existente a mano:

```sql
UPDATE db_NEXO.tbl_municipios
SET NombreTarifasPlaza = 'ZC_GUAYABAL SIQUIMA'
WHERE CodigoDane = '25328000' AND Pais = 'CO';
```

(`ZonaTarificacion` queda `NULL` a propósito — ese código no está en la
relación oficial `tblZonasTarificacionRelacion`, verificado.)

Verificación (Paso 3 de arriba) sigue igual: `dotnet ef migrations list`
debe mostrar solo `InitialCreate`, sin pendientes.
