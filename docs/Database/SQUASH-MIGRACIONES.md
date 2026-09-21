# Squash de migraciones: una sola migración para producción

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
update` manual, EF va a crear TODO el esquema — las 27 tablas, FKs, índices y
la fila singleton de `clsParametrosScore` — de una sola pasada, ejecutando
únicamente `InitialCreate`. Después del primer arranque exitoso, cambia
`MIGRATE_ON_STARTUP` de vuelta a `false` (mismo criterio que ya documenta
`DOKPLOY.md`).

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
