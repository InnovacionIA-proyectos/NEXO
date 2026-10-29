# Llave de Anthropic: configuración, verificación de salida y rotación

Guía operativa de la llave con la que NEXO llama a Claude. Las decisiones de fondo (la llave solo en el servidor, qué datos salen) están en [`docs/decisiones/ia-claude-seguridad-y-datos.md`](../decisiones/ia-claude-seguridad-y-datos.md); aquí van los pasos.

## 1. Reglas

- **La llave vive solo en variables de entorno de Dokploy.** Nunca en el repositorio, en `appsettings`, en el front ni en logs. Una prueba del repositorio (`clsSecretosEnElRepositorioTests`) falla si un archivo versionado contiene una llave con forma real o un valor para `Anthropic__ApiKey`.
- **Una *workspace* y una llave por servicio** (`nexo-api` y `cubicador-api`). El gasto se limita por *workspace*, así que sin una por servicio no se puede acotar ni revocar cada uno por separado.
- **GitHub Secrets: no se agrega.** El CI nunca llama a la API (el adaptador se prueba con un proveedor simulado) y el despliegue lo hace Dokploy con sus propias variables. Si más adelante un workflow bajo demanda (evaluación de prompts) necesita llamar al proveedor, se agrega entonces `ANTHROPIC_API_KEY` como secret de **ese** workflow, con su propio entorno protegido.

## 2. Estado verificado el 2026-10-27

- La llave de pruebas es **válida** (con una inválida el proveedor responde `401 invalid x-api-key`).
- Es una llave de **toda la organización**, no atada a una *workspace*: sin la cabecera `anthropic-workspace-id` el proveedor responde `400` («This API key is not scoped to a workspace…»). Por eso existe `Anthropic__WorkspaceId`.
- Solo existe la *workspace* `Default`. Aún no hay una por servicio ni un límite de gasto por servicio.
- La *workspace* `Default` muestra residencia de datos en `us` y geografía de inferencia `global`: es un dato para la aprobación pendiente de región y retención del ADR.

## 3. Pasos manuales en la consola de Anthropic (por servicio)

Los hace quien administra la cuenta; no se pueden hacer desde el repositorio.

1. **Crear la *workspace*** del servicio (por ejemplo `nexo-api`).
2. **Fijar el límite de gasto mensual** de esa *workspace* y, si la consola lo ofrece, un aviso por umbral.
3. **Crear una llave dentro de esa *workspace*.** Una llave atada a una *workspace* no necesita `Anthropic__WorkspaceId`.
4. Copiar la llave una sola vez a Dokploy (§4). No se pega en chats, tickets ni documentos.

> Con la llave de organización actual se puede seguir trabajando poniendo `Anthropic__WorkspaceId` con el ID de la *workspace* (no es un secreto), pero el **límite de gasto por servicio** solo existe cuando hay una *workspace* por servicio.

## 4. Variables en Dokploy (app `nexo-api`, pestaña Environment)

| Variable | Valor | Nota |
|---|---|---|
| `Anthropic__ApiKey` | la llave de la *workspace* de `nexo-api` | Secreto. Vacía = la IA queda apagada y la aplicación arranca igual. |
| `Anthropic__WorkspaceId` | `wrkspc_…` | Solo si la llave es de organización. |
| `Anthropic__Habilitada` | `true` | Interruptor general: `false` apaga toda la IA aunque haya llave y funciones encendidas. |
| `Anthropic__LimiteTokensPorUsuarioDia` | `500000` | Tope de tokens por persona y por día (hora de Colombia). `0` lo desactiva. Al superarlo, `429 IA_TOPE_DIARIO`. |
| `Anthropic__Model` | `claude-sonnet-5-5` | Configuración, no código. `claude-sonnet-5` y `claude-sonnet-4-6` también funcionan con esta cuenta. |
| `Anthropic__Effort` | `high` | `low`, `medium`, `high`, `xhigh` o `max`. |
| `Anthropic__Funciones__ImportacionExcel` / `AsistenteCotizacion` / `ApoyoCubicador` | `false` | Cada función se enciende por separado, solo cuando esté aprobada en el ADR. `ImportacionExcel` habilita la sugerencia de mapeo de columnas (`POST /api/captura-solicitudes/sugerir-mapeo`) y la normalización de textos libres (`POST /api/captura-solicitudes/normalizar-valores`); con ella apagada esos endpoints siguen respondiendo con lo que resuelven las reglas (diccionario del cliente, alias, catálogos y DANE). |

Las demás (`RazonamientoAdaptativo`, `TimeoutSegundos`, `MaxReintentos`, `MaxTokensSalida`) tienen valores por defecto y solo se agregan para cambiarlos. Un cambio de variables exige redesplegar la app.

## 5. Verificar la salida a internet desde el entorno desplegado

Desde la terminal del contenedor de `nexo-api` en Dokploy (la imagen trae `curl`).

**a) Alcance de la red, sin llave.** Cualquier respuesta HTTP prueba que hay salida; `401` es lo esperado:

```bash
curl -sS -m 15 -o /dev/null -w "HTTP %{http_code}\n" https://api.anthropic.com/v1/models -H "anthropic-version: 2023-06-01"
```

Si no responde, `Could not resolve host` o un *timeout* indican que falta abrir la salida a `api.anthropic.com` (puerto 443) en el firewall o proxy. Se la pide a infraestructura.

**b) La llave configurada, sin imprimirla** (usa la variable del propio contenedor):

```bash
curl -sS -m 30 -w "\nHTTP %{http_code}\n" "https://api.anthropic.com/v1/models?limit=1" \
  -H "x-api-key: $Anthropic__ApiKey" ${Anthropic__WorkspaceId:+-H "anthropic-workspace-id: $Anthropic__WorkspaceId"} \
  -H "anthropic-version: 2023-06-01"
```

| Respuesta | Qué significa |
|---|---|
| `200` con una lista de modelos | La llave y la *workspace* funcionan. |
| `401 invalid x-api-key` | La llave está mal copiada o fue revocada. |
| `400 … not scoped to a workspace` | Llave de organización: falta `Anthropic__WorkspaceId`. |
| `403` / `429` | Sin permiso o límite de uso alcanzado. |

**c) Desde la aplicación.** Con una función encendida, el primer uso real deja en el log una línea `IA <función> modelo <modelo>: <n> tokens de entrada, <n> de salida, <ms> ms, ok`. Si falla, la línea de advertencia trae el código (`IA_*`) y el detalle que devolvió el proveedor, sin la llave ni el contenido enviado.

## 6. Rotación (sin dejar el servicio sin IA)

Rutinaria (sugerido cada 90 días) y de inmediato si la llave pudo exponerse.

1. En la consola, **crear una llave nueva en la misma *workspace***. La anterior sigue activa.
2. En Dokploy, **actualizar `Anthropic__ApiKey`** con la llave nueva y **redesplegar** `nexo-api`.
3. **Verificar** con §5b y con `GET /health` (200).
4. **Revocar la llave anterior** en la consola.
5. Revisar el uso de las últimas horas por si la llave vieja se usó desde otro lado.

## 7. Si la llave se filtró o el gasto se dispara

1. **Revocar la llave** en la consola (es lo que corta el uso de inmediato).
2. **Apagar la IA** en Dokploy: `Anthropic__Habilitada=false` y redesplegar (corta todo sin tocar la llave ni las funciones); o `Anthropic__Funciones__*=false`, o dejar `Anthropic__ApiKey` vacía.
3. Crear y configurar la llave nueva (§3 y §4).
4. Revisar el uso del período en la consola y en NEXO (`GET /api/ia/uso/gasto`, solo Admin, y la tabla `tbl_ia_uso` por usuario y función) para ver qué función y qué usuario la consumieron.
5. Si la llave estuvo en un archivo versionado, retirarla del historial y tratarla como comprometida aunque ya se haya revocado.

## 7. Cubicador: modo sombra (llave y límite propios)

El servicio Python del Cubicador usa su **propia llave, en su propia *workspace*** (por ejemplo `cubicador-api`), con su propio límite de gasto mensual: no se reutiliza la de NEXO, así que se puede revocar, rotar y acotar por separado. Los pasos 1 a 4 de §3 se repiten para esa *workspace*.

En Dokploy, app `cubicador-api` (pestaña Environment). Nada de esto va al repositorio ni a la imagen:

| Variable | Valor | Nota |
|---|---|---|
| `IA_SOMBRA_HABILITADA` | `false` | **Interruptor de apagado.** Con `true` y una llave, cada `POST /recomendaciones` agenda una comparación con Claude en segundo plano. Nace apagado. |
| `ANTHROPIC_API_KEY` | la llave de la *workspace* del Cubicador | Secreto. Vacía = la función no hace nada y no se toca la red. |
| `ANTHROPIC_WORKSPACE_ID` | `wrkspc_…` | Solo si la llave es de organización. |
| `ANTHROPIC_MODEL` / `ANTHROPIC_EFFORT` | `claude-sonnet-5-5` / `high` | Configuración, no código. |
| `IA_SOMBRA_MUESTREO` | `1.0` | Fracción de recomendaciones que se comparan (0 a 1): sirve para acotar el gasto sin apagarla. |
| `IA_SOMBRA_TOPE_TOKENS_DIA` | `1000000` | Tope de tokens (entrada + salida) por día, hora de Colombia; al alcanzarlo no se compara hasta el día siguiente. `0` lo desactiva. Va **además** del límite mensual de la *workspace*, que es el que de verdad corta el gasto. |
| `IA_SOMBRA_TIMEOUT_SEGUNDOS`, `IA_SOMBRA_MAX_CONCURRENTES`, `IA_SOMBRA_MAX_PENDIENTES`, `IA_SOMBRA_MAX_TOKENS_SALIDA` | `60`, `2`, `20`, `4096` | Solo para cambiarlos. Con más comparaciones esperando que `MAX_PENDIENTES`, las nuevas se descartan: la API nunca se encola detrás del modelo. |

**Antes de encenderlo** hay que aplicar `cubicador/database/esquema/367_ia_sombra.sql` (tabla `dbo_cubicador.iaSombra`). Cada comparación, o intento que falla, deja una fila con el motivo en `Estado`; el contenido enviado al modelo no se guarda.

**Apagarlo** es poner `IA_SOMBRA_HABILITADA=false` (o vaciar la llave) y redesplegar `cubicador-api`; la respuesta al usuario es la misma encendido o apagado. **Verificar la salida** a `api.anthropic.com` desde el contenedor con §5a y §5b (la imagen del Cubicador no trae `curl`: usar `python -c` con `httpx`, o probar desde el contenedor de NEXO que sí lo trae).
