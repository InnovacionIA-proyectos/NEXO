# QA — PR #385: Anthropic — workspace configurable, guía de despliegue y Sonnet 5.5 por defecto

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/385
**Autor**: ivasco-dev
**Rama**: `351-secretos-despliegue-ia` → `QA`
**Refs**: #351 (la PR usa `refs`, no `closes`: el issue se cierra a mano)
**Depende de**: #384 (ya mergeada; esta PR estaba apilada sobre ella, el diff real son solo los cambios propios).

## Qué cambia

- `Anthropic__WorkspaceId`: una llave de organización exige la cabecera `anthropic-workspace-id`; el adaptador la envía cuando está configurada.
- Los logs de un error del proveedor ahora incluyen el detalle que devolvió (`DetalleDelProveedor`, hasta 500 caracteres, con la llave sustituida por `<llave>`); un modelo inexistente (404) se informa como error de configuración.
- Modelo por defecto `claude-sonnet-5-5` con esfuerzo `high` (antes `claude-sonnet-4-6` / `medium`); un valor de esfuerzo desconocido ahora cae a `high`.
- Plantillas de despliegue (`.env.example`, `.env.dokploy`, `.env.githubSECRETS`, `DOKPLOY.md`) y guía `docs/despliegue/ROTACION-LLAVE-ANTHROPIC.md` (configuración, verificación de salida y rotación).
- Prueba nueva `clsSecretosEnElRepositorioTests`: falla si un archivo versionado contiene una llave con forma real o un valor para `Anthropic__ApiKey`.
- No toca el composition root ni la base de datos.

## Cómo se verificó

1. Checkout de la rama con `QA` integrada: **sin conflictos**. Diff real: 10 archivos, +260/−23.
2. Revisión de las plantillas: `.env.dokploy` trae `Anthropic__ApiKey=<secreto>` (marcador), `.env.example` lo deja vacío y los tres flags `Funciones__*` en `false`. Sin valores reales.
3. Revisión de la guía de rotación: coherente con el ADR; la verificación de salida (§5b) usa la variable del propio contenedor sin imprimirla.
4. Corrida con `ANTHROPIC_API_KEY` y `Anthropic__ApiKey` forzadas a vacío:
   - Build Release: 0 errores.
   - Tests de IA (`Nexo.Api.Tests.IA`, incluye el escaneo de secretos): **40/40** ✅
   - `Nexo.Domain.Tests` **218/218** ✅ · `Nexo.Application.Tests` **533/533** ✅
   - Se acotó la regresión a IA + Domain + Application porque la PR no toca el composition root (en #384 ya se corrió el `Api.Tests` completo: 775 pasados, 10 omitidos).

## Pruebas de mutación

- **Cabecera de workspace:** al quitar `WorkspaceID = ...` del adaptador, **falla exactamente 1 test** (`Con_workspace_configurada_la_envia_en_la_cabecera_y_sin_ella_no_envia_nada`) y pasan los otros 39. Adaptador restaurado idéntico a la rama.
- **Escáner de secretos:** se agregó (solo en local, rastreado por git) un archivo con una llave **falsa** con forma real (`sk-ant-api03-` + 60 caracteres de relleno). El test `Ningun_archivo_versionado_contiene_una_llave_de_Anthropic` falla y nombra **solo la ruta del archivo**, no la llave. Archivo retirado; `git status` limpio.
- Nota de proceso: una primera corrida de la mutación sin cargar `.env.pruebas-reales.local` mostró fallas extra en `clsAsistenteIaConfiguracionTests`; no eran de la PR sino de esa corrida (el fixture de la API necesita la conexión). Repetida con el entorno cargado, el resultado es el limpio de arriba.

## Hallazgos

### 1. El log del error del proveedor no garantiza «sin contenido enviado» (no bloqueante, pero debe resolverse antes de encender cualquier función)

`DetalleDelProveedor` escribe en el log el `Message` de la excepción del proveedor (estado + cuerpo JSON del error), recortado a 500 caracteres, y solo sustituye la llave. Ningún test cubre esta propiedad.

Se verificó con un test de diagnóstico **temporal** (descartado después; no forma parte de la suite): proveedor simulado que responde `400` repitiendo en su mensaje la llave y un fragmento del contenido enviado.

- La llave **no** llega al log (aparece `<llave>`) ✅
- El mensaje que recibe el usuario no filtra nada ✅
- Pero el fragmento del contenido (un NIT y una tarifa, en la simulación) **sí queda en el log**, porque el cuerpo del proveedor se escribe tal cual.

Es decir, la afirmación de la PR y de la guía (§5c: «sin la llave ni el contenido enviado») solo es cierta si el proveedor nunca repite parte de la entrada en un error. QA no puede confirmar cómo se comporta el proveedor real. Por el ADR (§3 y §8: el contenido enviado no se guarda; nivel A nunca sale) conviene no depender de eso.

Sugerencia (decide el autor): registrar solo `error.type` y el `request_id` del proveedor, y el `message` únicamente para 401/403/404 (errores de configuración, que no repiten entrada) o a nivel Debug; y agregar un test que fije la propiedad. No hay impacto hoy: las tres funciones nacen apagadas y no hay llave desplegada.

### 2. Llave de organización en uso (§2 de la guía)

La guía documenta que la llave de pruebas es de **toda la organización** (no atada a una workspace) y que solo existe la workspace `Default`, sin límite de gasto por servicio. Una llave de organización tiene mayor alcance si se filtra. Recomendación: no desplegarla en un ambiente real; crear primero la workspace `nexo-api` con su límite de gasto y una llave atada a ella (pasos 1–3 de §3), y entonces `Anthropic__WorkspaceId` no hace falta. La guía ya lo dice; queda como condición de despliegue, y la administración de la cuenta aún lo tiene pendiente.

### 3. El ADR quedó desactualizado

El ADR de #383 (§6) fija `claude-sonnet-4-6` / `medium` como defecto y esta PR implementa `claude-sonnet-5-5` / `high`. Además §9 deja «Modelo y esfuerzo por defecto» pendiente de aprobación de Producto. Conviene actualizar el ADR (aquí o en un seguimiento) para que no contradiga el código, y dejar claro que el cambio quedó sujeto a esa aprobación. Los precios del ADR también cambiarían con el modelo.

### 4. Observaciones menores

- La PR afirma que el modelo por defecto fue «verificado contra la API real» y la guía que la llave de pruebas es válida. **QA no lo repitió**: no hay llave en este ambiente y no se pide una; lo verificará quien active la primera función.
- `LlaveAsignada` solo vigila `Anthropic__ApiKey`; no `ANTHROPIC_API_KEY`, que es el nombre que la propia guía prevé para un futuro workflow. El otro patrón (llave con forma real) igualmente la detectaría si trae un valor real.
- Cambio de comportamiento: un `Anthropic__Effort` desconocido antes caía a `medium` y ahora cae a `high`.
- La guía está fechada «2026-10-27» (día posterior a la revisión).
- Como la PR usa `refs #351`, el issue hay que cerrarlo a mano.

## Resultado

Sin hallazgos bloqueantes para el merge: las funciones de IA nacen apagadas y no hay llave desplegada, así que el hallazgo 1 no tiene impacto hoy. Se recomienda registrarlo como issue para el autor, como condición previa a encender cualquier función.
