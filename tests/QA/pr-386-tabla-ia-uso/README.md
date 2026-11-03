# QA — PR #386: Tabla `tbl_ia_uso` para registrar el uso de los modelos de lenguaje

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/386
**Autor**: ivasco-dev
**Rama**: `352-tabla-ia-uso` → `QA`
**Cierra**: #352 (el issue ya figura **cerrado**, asignado a `scalderon-bs`, con sus dos criterios marcados; el «Cierra #352» no tendrá efecto)
**Depende de**: nada (aditiva). La usarán las PRs de funciones de IA que registren el uso.

## Qué cambia

Solo SQL, sin código C#:

- `docs/Database/352_ia_uso.sql`: script **idempotente** para bases existentes. Crea `db_NEXO.tbl_ia_uso` (una fila por llamada a un modelo: función, usuario, modelo, tokens de entrada/salida/caché, costo estimado, latencia, resultado), con `CHECK` de cifras no negativas, FK a `tbl_usuarios` y 2 índices (tope por usuario/día y gasto por función/mes). Trae la reversa documentada (`DROP TABLE`).
- `docs/Database/ESQUEMA_NEXO.sql`: la misma tabla, su FK y sus 2 índices, porque ese archivo es la fuente de la verdad del esquema (el proyecto no usa migraciones de EF).
- **No guarda contenido** (ni el enviado ni la respuesta): solo cifras y el resultado, como exige el ADR (#383, §3 y §8).

## Cómo se verificó

PR de DDL: la verificación es que el SQL **se ejecuta** y deja lo descrito, por los dos caminos que usa el proyecto. No se tocó ninguna base corporativa: [`verificar.ps1`](./verificar.ps1) levanta un SQL Server **local y descartable** en Docker (sin puertos publicados, contraseña aleatoria que no se imprime) y lo elimina al terminar. Se probó en **SQL Server 2019 (15.0.4480) y 2022 (16.0.4265)**, porque la versión de la base corporativa no está documentada en el repo.

Resultado: **64 comprobaciones OK, 0 fallos**, sin contenedores residuales.

**A) Base existente** (esquema previo a la PR + script `352`):
- El script corre sin errores y deja 1 tabla, 12 columnas, 2 índices y 1 FK.
- **Idempotente:** una segunda corrida no falla ni duplica nada.
- **Reversa:** `DROP TABLE` funciona, no queda rastro, y se puede volver a aplicar.
- **Restricciones**, con datos ficticios (12 casos): tokens de entrada/salida/caché negativos, latencia negativa, costo negativo y usuario inexistente → error 547 (CHECK/FK); `Funcion` o `Resultado` nulos → error 515; no se puede borrar un usuario que tiene uso registrado → 547. Los dos insertos válidos (mínimo, y completo con usuario real) entran; los valores por defecto se aplican (cifras en 0, usuario y costo `NULL`, `CreatedAt` con valor); las filas inválidas no entran.

**B) Base nueva** (`ESQUEMA_NEXO.sql` de la PR sobre una base vacía):
- La PR **no agrega ningún error nuevo** al esquema: los errores son exactamente los mismos que ya tenía el esquema previo (ver «Hallazgo ajeno a la PR»), y ninguno menciona `tbl_ia_uso`.
- La tabla y su FK quedan creadas; los 2 índices de la PR, ejecutados aparte, corren sin errores.
- **A y B dejan una estructura idéntica** (columnas, tipos, defaults, CHECK, PK, FK e índices).

Lectura del código: las 12 columnas, el `CHECK`, la FK y los índices coinciden texto a texto entre el script y `ESQUEMA_NEXO.sql`; el nombre `352_ia_uso.sql` sigue la convención de los scripts numerados existentes (`304_`, `307_`). Ninguna prueba ni código del repo depende de la tabla todavía.

## Hallazgo ajeno a la PR: `ESQUEMA_NEXO.sql` no se puede aplicar completo sobre una base vacía

(Anterior a esta PR: se reproduce idéntico con el esquema previo, en 2019 y 2022.)

La cabecera de `ESQUEMA_NEXO.sql` dice que se corre «completo, de una sola pasada, contra una base vacía». No es así: la FK `FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId` (sección de llaves foráneas) referencia una clave única de `InboxState` (`AK_InboxState_MessageId_ConsumerId`) que se crea **después**, en la sección de índices. SQL Server responde `Msg 1776` / `Msg 1750`, y ese error **aborta el lote**: entre los dos únicos `GO` del archivo están todas las FK y todos los índices.

| | Declarado en el archivo | Creado en una base nueva |
|---|---|---|
| Esquema previo a la PR | 30 FK · 48 `CREATE INDEX` | 17 FK · **0 índices** |
| Esquema de la PR | 31 FK · 50 `CREATE INDEX` | 18 FK · **0 índices** |

Es decir, una base creada desde ese archivo queda **sin ningún índice secundario** y con FK faltantes. No se sabe si la base corporativa (`dbInformesAutomatizados_Pruebas.db_NEXO`) se creó así: el archivo dice que los cambios «se aplican a mano contra cada base existente» y QA no la consultó. Merece un issue aparte.

Sugerencia: crear `AK_InboxState_MessageId_ConsumerId` antes de la FK (o declararla como `UNIQUE` dentro de `InboxState`), separar los bloques con `GO` para que un fallo no arrastre al resto, y agregar al CI una aplicación del esquema sobre un SQL Server vacío con `-b` (como hace `verificar.ps1`).

## Observaciones (no bloquean)

1. **Día del tope por usuario:** `CreatedAt` usa `sysdatetimeoffset()`, es decir, la zona del servidor de la base. Quien implemente «lo consumido hoy» debe calcular la ventana del día con la zona de Colombia de forma explícita, no con la del servidor.
2. **FK a `tbl_usuarios` sin `ON DELETE`:** no se puede borrar un usuario con uso registrado (error 547 comprobado). Es lo deseable para auditoría; si algún día se purgan usuarios (habeas data, ADR §5) habrá que anonimizar (`UsuarioId` admite `NULL`).
3. **Sin política de retención** de la tabla (crece una fila por llamada). Sin impacto hoy.
4. **La tabla no está mapeada todavía** en `NexoDbContext` (nada la usa): cuando se mapee, validar nombres y tipos contra este script.
5. **QA no aplicó el script en ninguna base corporativa** ni consultó si ya está aplicado: el despliegue del script (antes del código que registre el uso, como dice la PR) le toca a quien administra esa base.

## Resultado

Sin hallazgos bloqueantes. El SQL es correcto, idempotente, reversible y consistente por los dos caminos de aplicación, en dos versiones del motor.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-386-tabla-ia-uso/verificar.ps1
```

Requiere Docker y las imágenes `mcr.microsoft.com/mssql/server:2019-latest` y `:2022-latest` (se descargan si faltan). No usa VPN ni `.env.pruebas-reales.local`.
