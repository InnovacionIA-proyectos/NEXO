# Checklist de despliegue en un ambiente nuevo

Lo que hay que configurar y comprobar antes de abrir NEXO y el Cubicador en un ambiente nuevo, por ejemplo producción. Cada
punto dice qué hay que hacer, con qué evidencia y quién lo decide. La lista completa de variables por aplicación está en
[`.env.dokploy`](../../.env.dokploy) y el procedimiento de Dokploy en [`DOKPLOY.md`](../../DOKPLOY.md). Aquí solo van los
interruptores que, si se omiten, dejan una función apagada, abierta de más o apuntando al lugar equivocado.

Un punto que no tiene evidencia o responsable no está listo: se deja pendiente, no se enciende.

| # | Punto | Si se omite |
|---|---|---|
| 1 | Compuerta de paridad del plan de cargue | La optimización del plan de cargue responde `503 CUBICACION_EN_VERIFICACION`. |
| 2 | Llaves entre NEXO y el Cubicador | Las llamadas entre servicios fallan con `401`, o el ambiente queda con llaves de QA. |
| 3 | Inteligencia artificial | Se envían datos a un proveedor externo sin las aprobaciones requeridas. |
| 4 | Base de datos y colas | La aplicación no arranca o lee y escribe en el esquema equivocado. |

---

## 1. Compuerta de paridad del plan de cargue

**Qué es.** La optimización del plan de cargue (`POST /api/planes-cargue/{id}/optimizar`) solo se publica al Cubicador si está
comprobado que su motor da los mismos resultados que el motor original. Por defecto la compuerta está **cerrada**: responde
`503 CUBICACION_EN_VERIFICACION` sin publicar nada. La regla está en el dominio (`CompuertaDeParidad`) y la configuración la lee
`clsParidadCubicadorAdapter`.

**Qué hay que hacer.**

1. Correr la suite de paridad sobre **el mismo commit que se va a desplegar**:

   ```bash
   docker run --rm -v "<ruta a cubicador/>:/app" -w /app python:3.14-slim \
     bash -c "pip install -q -r requirements.txt && pytest tests/paridad -v"
   ```

   El resultado esperado es 27 pruebas aprobadas: los 26 casos dorados de `cubicador/tests/paridad/test_paridad_casos_dorados.py`
   más la que confirma que son exactamente 26. La misma suite corre en el CI dentro de `_cubicador-pruebas.yml`, así que la
   corrida verde de ese commit también sirve como evidencia.
2. Configurar las **tres** variables juntas en la aplicación `nexo-api`:

   | Variable | Valor |
   |---|---|
   | `Cubicacion__ParidadVerificada` | `true` |
   | `Cubicacion__ParidadEvidencia` | El SHA del commit verificado o la URL de la corrida del CI |
   | `Cubicacion__ParidadFecha` | La fecha de la corrida (no la de la configuración), por ejemplo `2026-09-14` |

   Con `ParidadVerificada=true` y sin las otras dos, la compuerta sigue cerrada y el log muestra una advertencia. Es a propósito:
   encenderla exige dejar escrito contra qué evidencia se encendió.
3. Redesplegar y comprobar: crear un plan de demostración (`POST /api/planes-cargue/demo`), optimizarlo y verificar que responde
   `202` y que `GET /api/planes-cargue/{id}` termina en estado `optimizado`.

**Evidencia.** La corrida de la suite (salida o enlace) del commit desplegado. La evidencia de un ambiente no sirve para otro
si el código cambió.

**Quién decide.** El responsable técnico del Cubicador.

**Referencia.** En QA se encendió el 2026-09-14 con el commit `82fb52ba73083b7d94ea07d42a4774a2b9b8e1e3` (27 de 27).

## 2. Llaves entre NEXO y el Cubicador

**Qué es.** NEXO y el Cubicador se llaman entre sí con llaves (Bearer), no desde el navegador. Cada ambiente tiene que tener las
suyas: nunca se reutilizan las de QA.

**Qué hay que hacer.** Generar valores nuevos y configurarlos en pares:

| En `nexo-api` | En el Cubicador | Para qué |
|---|---|---|
| `Cubicador3D__RecomendacionesApiKey` | `CUBICADOR_API_KEY_NEXO` en `cubicador-api` | NEXO pide la recomendación de vehículo (`/recomendaciones`) |
| `Cubicador3D__OutboundApiKey` | `CUBICADOR_API_KEY` en `cubicador-api` | NEXO administra los catálogos del Cubicador |
| `Cubicador3D__InboundApiKey` | `NEXO_CALLBACK_API_KEY` en `cubicador-api` y `cubicador-worker` | El Cubicador llama de vuelta a NEXO |

- `CUBICADOR_API_KEY_COTIFLY` la genera Cotifly y tiene que ser distinta de la de NEXO.
- El Cubicador **no** tiene CORS a propósito: ningún navegador lo llama directamente. No se define ninguna variable de CORS en
  sus aplicaciones. El CORS de NEXO (`Cors__OrigenesPermitidos`) sí es obligatorio y solo incluye el dominio del portal.

**Evidencia.** Con cada par configurado, una llamada de NEXO al Cubicador responde (por ejemplo, una cubicación desde la Mesa) y
una llave de QA responde `401` en el ambiente nuevo.

**Quién decide.** Quien administra el despliegue del ambiente.

## 3. Inteligencia artificial

**Qué es.** NEXO y el Cubicador pueden llamar a Claude (importación de Excel, asistente de cotización, modo sombra del
Cubicador). Las condiciones de uso están en la decisión
[D-10](../decisiones/DECISIONES-NEXO.md#d-10), que todavía tiene aprobaciones pendientes.

**Qué hay que hacer.**

- Mientras la D-10 no esté aprobada para ese ambiente, las funciones quedan **apagadas**:
  `Anthropic__Funciones__ImportacionExcel`, `__AsistenteCotizacion` y `__ApoyoCubicador` en `false`, e
  `IA_SOMBRA_HABILITADA=false` en el Cubicador. Sin IA, la importación sigue funcionando con las reglas (diccionario, alias,
  catálogos y DANE).
- Cuando se aprueben, crear una *workspace* y una llave por servicio, con su límite de gasto, siguiendo
  [`ROTACION-LLAVE-ANTHROPIC.md`](ROTACION-LLAVE-ANTHROPIC.md).

**Evidencia.** Las aprobaciones del punto 9 de la D-10 y, al encender, la verificación de salida a `api.anthropic.com` de la guía
de la llave.

**Quién decide.** Cada aprobación tiene su responsable en la D-10 (datos, cuenta de Anthropic, jurídico, infraestructura y
producto).

## 4. Base de datos y colas

**Qué hay que hacer.**

- **NEXO:** `ConnectionStrings__Nexo` y `Database__Esquema` apuntando a la base y al esquema del ambiente. `Database__Esquema` es
  obligatoria: sin ella la aplicación no arranca. `Database__SeedOnStartup=false`, porque el seeder de arranque carga datos
  ficticios de demostración.
- **Cubicador:** `DATABASE_URL` y `CUBICADOR_DB_ESQUEMA` en la API y en el worker, con los mismos valores. El Cubicador no valida
  estas variables al arrancar: si falta `DATABASE_URL` arranca contra un SQLite local que se pierde en cada despliegue. Hay que
  comprobarlo con `/health` y con los logs.
- **Esquema y catálogos:** se crean con los scripts de `database/` (NEXO) y `cubicador/database/` (Cubicador). La aplicación no
  crea tablas.
- **Colas:** si el ambiente reemplaza a otro, vaciar las colas de RabbitMQ antes de cambiar de base, para que ningún mensaje
  pendiente se procese contra datos que no existen. NEXO y el Cubicador se cambian juntos, porque cada uno guarda Id del otro.

**Evidencia.** `GET /health` de NEXO y del Cubicador en 200, y una solicitud de prueba que se costea de punta a punta.

**Quién decide.** Quien administra el despliegue del ambiente.

---

## Cómo agregar un punto

Cada punto nuevo responde las mismas tres preguntas: **qué** hay que configurar o hacer, **con qué evidencia** se comprueba
(una prueba, una corrida del CI, una confirmación de negocio) y **quién** decide encenderlo. Si alguna no tiene respuesta
todavía, el punto queda como pendiente.
