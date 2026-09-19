# Checklist de despliegue

Interruptores de producción que exigen una confirmación explícita antes de
encenderse — cada uno documenta QUÉ hace falta, A QUÉ evidencia está
enlazado, y DE QUIÉN es la decisión. Ninguno se ejecuta desde este checklist:
es la referencia que usa quien SÍ va a encenderlo.

---

## 1. Compuerta de paridad del plan de cargue

> ✅ **ENCENDIDA en Dokploy (ambiente Pre-Despliegue-BACKEND) el
> 2026-09-14.** Autorizado por Kevin Cano (@kcano-ux). Ver "Encendido
> confirmado" al final de esta sección para la evidencia y los valores
> exactos que se configuraron.

**Estado por defecto del código: CERRADA.** `POST /api/planes-cargue/{id}/optimizar`
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

**Hotspot H5** ("paridad automatizada") — el criterio de cierre
es **26/26** casos dorados coincidiendo exacto entre el motor C# original y
el motor Python de Cubicador (`cubicador/tests/paridad/
test_paridad_casos_dorados.py`).

> ⚠️ **Corrección**: esta sección decía que la suite corre en
> `cubicador-ci.yml`. Ese workflow **ya no existe** — fue retirado.
> Hoy la misma suite corre dentro de `_cubicador-pruebas.yml` (workflow
> reusable, job "Pruebas unitarias y de integracion (pytest)"), invocado por
> `ci-cd-cubicador-api.yml` y `ci-cd-cubicador-worker.yml` en cada push/PR
> que toque `cubicador/`.

La primera corrida ya había dado 26/26 (ver `cubicador/docs/
DECISIONES_TECNICAS.md` sección 3 y `cubicador/README.md`).

### De quién es la decisión

**Kevin Cano (@kcano-ux).** Ninguna otra iniciativa de este repo enciende esta
compuerta por defecto — el cierre de CORS y rotación de claves de Cubicador
(sección 2) explícitamente la dejó enlazada, sin ejecutar.

### Encendido confirmado (2026-09-14)

**Autorizado por Kevin Cano (@kcano-ux)** para el ambiente Dokploy
"Pre-Despliegue-BACKEND"
(`http://nexo-predesplieguebackend-ijaiqn-cdc194-10-10-40-17.sslip.io`).

**Evidencia**: no se usó una corrida de GitHub Actions (no había una
enlazada — ver el aviso de arriba sobre `cubicador-ci.yml`). En su lugar se
corrió la MISMA suite (`pytest tests/paridad -v`) en un contenedor efímero
`python:3.14-slim`, contra el código real de `cubicador/` en el estado
exacto del commit desplegado:

- **Commit**: `82fb52ba73083b7d94ea07d42a4774a2b9b8e1e3` (rama
  `Pre-Despliegue-BACKEND`)
- **Fecha de la corrida**: 2026-09-14
- **Resultado**: `27 passed in 3.60s` — los 26 casos dorados
  (`test_paridad_colisiones[01..05]`, `test_paridad_orientaciones[06..09]`,
  `test_paridad_caso_10` a `test_paridad_caso_26`) más
  `test_hay_exactamente_26_fixtures_dorados` (confirma que no falta ni sobra
  ningún fixture). Salida completa registrada en
  `docs/VERIFICACION_NEXO_CUBICADOR_2026-09-14.md`, sección 8.

**Valores configurados en Dokploy** (app de la API de NEXO,
`Cubicacion__*`, con doble guion bajo — Dokploy despliega por Dockerfile,
sin interpolación de `docker-compose.yml`):

```
Cubicacion__ParidadVerificada=true
Cubicacion__ParidadEvidencia=82fb52ba73083b7d94ea07d42a4774a2b9b8e1e3
Cubicacion__ParidadFecha=2026-09-14
```

---

## 2. Cierre de CORS y rotación de claves de Cubicador

Ya ejecutado — se deja acá como registro, no como pendiente:

- `CUBICADOR_CORS_ORIGENES` retirada por completo (código + `.env`/
  `.env.example`/`docker-compose.yml`). Confirmado antes de
  retirarla: el visor 3D en `administracion_embarqa_nexo` cerrado y
  verificado en QA, ningún navegador llama a Cubicador directo.
- `CUBICADOR_API_KEY` y el par `NEXO_CALLBACK_API_KEY`/
  `Cubicador3D__InboundApiKey` rotados. El valor que estuvo
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
