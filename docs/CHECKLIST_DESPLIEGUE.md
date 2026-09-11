# Checklist de despliegue

Interruptores de producción que exigen una confirmación explícita antes de
encenderse — cada uno documenta QUÉ hace falta, A QUÉ evidencia está
enlazado, y DE QUIÉN es la decisión. Ninguno se ejecuta desde este checklist:
es la referencia que usa quien SÍ va a encenderlo.

---

## 1. Compuerta de paridad del plan de cargue (S2-HU-08.2, issue #30)

**Estado por defecto: CERRADA.** `POST /api/planes-cargue/{id}/optimizar`
responde `503 CUBICACION_EN_VERIFICACION` sin publicar nada a la cola —
fail-closed, sin fallback al PoC original (no operativo). La regla vive en
`Domain.PlanDeCargue.Reglas.CompuertaDeParidad` y la lee
`ParidadCubicadorAdapter` desde configuración.

### Qué hace falta para encenderla

Las **tres** claves siguientes, juntas, en la configuración de NEXO
(`appsettings.{Environment}.json` o variables de entorno
`Cubicacion__*`):

| Clave | Qué es | Ejemplo |
|---|---|---|
| `Cubicacion:ParidadVerificada` | El interruptor (`true`/`false`) | `true` |
| `Cubicacion:ParidadEvidencia` | A QUÉ corrida de paridad se refiere — la URL de la corrida verde de `cubicador-ci.yml` (job de paridad) o el commit/SHA que la confirmó | `https://github.com/.../actions/runs/12345` |
| `Cubicacion:ParidadFecha` | CUÁNDO se confirmó (fecha, no de "cuándo se configuró") | `2026-09-16` |

**Las tres son obligatorias a la vez.** `ParidadVerificada=true` sin las
otras dos deja la compuerta CERRADA igual — a propósito, para que encenderla
sea más que voltear un booleano: exige dejar dicho contra qué evidencia se
prendió (`ParidadCubicadorAdapter` incluso deja un `LogWarning` si detecta
esta configuración a medias).

### A qué evidencia está enlazado

**S2-HU-07.3** (hotspot H5, "paridad automatizada") — el criterio de cierre
es **26/26** casos dorados coincidiendo exacto entre el motor C# original y
el motor Python de Cubicador (`cubicador/tests/paridad/
test_paridad_casos_dorados.py`, corrido en `cubicador-ci.yml`). La primera
corrida ya dio 26/26 (ver `cubicador/docs/DECISIONES_TECNICAS.md` sección 3
y `cubicador/README.md`) — pero esa corrida verde **todavía no está
enlazada** como `Cubicacion:ParidadEvidencia` en ningún ambiente: encenderla
es un paso deliberado, no automático, aunque el 26/26 ya exista.

### De quién es la decisión

**Kevin Cano (@kcano-ux).** Ninguna otra HU de este repo enciende esta
compuerta — S3-HU-05 (issue #118) explícitamente la deja enlazada, sin
ejecutar. Ver `PENDIENTES.md`, sección "Encendido de la función de plan de
cargue", para el detalle completo de la decisión original (S2-HU-08.2).

---

## 2. Cierre de CORS y rotación de claves de Cubicador (S3-HU-05)

Ya ejecutado — se deja acá como registro, no como pendiente:

- `CUBICADOR_CORS_ORIGENES` retirada por completo (código + `.env`/
  `.env.example`/`docker-compose.yml`) — issue #116. Confirmado antes de
  retirarla: S3-HU-04 (visor 3D en `administracion_embarqa_nexo`) cerrada y
  verificada en QA, ningún navegador llama a Cubicador directo.
- `CUBICADOR_API_KEY` y el par `NEXO_CALLBACK_API_KEY`/
  `Cubicador3D__InboundApiKey` rotados — issue #117. El valor que estuvo
  expuesto en el bundle público del portal queda inválido.

**Pendiente de quien tenga acceso al ambiente real (fuera de este repo)**:
aplicar el valor nuevo de `CUBICADOR_API_KEY` en el despliegue de producción
de Cubicador y confirmar ahí — no solo en `.env` local — que el valor viejo
ya no autentica ninguna llamada. Este checklist no tiene acceso a esa
infraestructura para verificarlo por sí mismo.

---

## Cómo agregar un nuevo interruptor a este checklist

Cada entrada nueva debe responder las mismas tres preguntas que la de
arriba: **qué** claves/pasos hacen falta, **a qué evidencia** está enlazado
(un test, una corrida de CI, una confirmación de negocio), y **de quién** es
la decisión de ejecutarlo. Si no se puede responder alguna de las tres
todavía, no está listo para encenderse — se documenta como pendiente, no
como checklist.
