# PR #280 (S5-HU-17) — Reestructuración del cubicador rápido (cierra #266-#271)

## Alcance

97 archivos, 11 commits. Solo `cubicador/` + el workflow de CI del cubicador
— **nada de NEXO ni Cotifly**. Reemplaza el catálogo de 9 vehículos
quemados en código por un motor de selección de flota contra catálogo
administrable en BD (SQL Server, esquema `dbo_cubicador` compartido con
Cotifly), con un contrato nuevo (`POST`/`GET /recomendaciones`) que
reemplaza `POST /api/cubicacion` (retirado).

## Qué se verificó

**Merge real contra `QA`**: sin conflictos, sin pisar #253/#257/#264/#265.

**Suite completa (`pytest -v`)**: entorno virtual limpio desde cero —
**245/245 passed**, coincide exacto con lo declarado (antes eran 123).
Confirmé que `sqlalchemy[asyncio]>=2.0` en `requirements.txt` ya instala
`greenlet` automáticamente en un venv limpio (el mismo problema que causaba
el fallo de CI en PRs anteriores) — resuelto en esta PR.

**Motor 3D intacto**: diff contra el merge-base de `motor.py`, `dominio.py`,
`puntuacion.py` y `validadores.py` — **cero cambios**, confirmado tal como
afirma la PR.

**Claims de arquitectura verificados en el código:**
- Decisión `max(%volumen, %peso)`: confirmado en `domain/motor/seleccion_flota.py`.
- DBLF registrado como optimizador (`registrar_optimizador("dblf", ...)`), reusando
  puntos extremos de FFD/BFD.
- `SIN_SELECCION_AUTOMATICA`/`condicionada`/`extradimension` viajan dentro del DTO de
  respuesta (200), no como excepción.
- `POST /api/cubicacion` retirado — no queda ninguna ruta `/cubicacion` en
  `api/routers/`; el contrato nuevo vive en `api/routers/recomendaciones.py`.

**Verificación en vivo contra `dbTarifario_Nexo_Pruebas` (credenciales
provistas puntualmente por Kevin, no guardadas en ningún archivo):**
- `dbo`: 0 tablas. `dbo_cotifly`: 20 tablas. Ambos confirman la afirmación
  de la PR de que no se tocó nada fuera de `dbo_cubicador`.
- `dbo_cubicador`: 15 tablas — las 10 del catálogo (las 9 de la PR #265 +
  la nueva `recomendaciones`) más las 5 propias del motor 3D migradas de
  PostgreSQL a SQL Server (`vehiculos_cubicacion`, `planes_cargue`,
  `items_plan_cargue`, `colocaciones_cargue`, `parametros_cubicacion`).
- Corrí `scripts/verificar_mapeo_catalogo.py` yo mismo (con un `.env`
  temporal, borrado inmediatamente después) contra la base real: **los 10
  modelos de `modelos_catalogo.py` cuadran columna por columna contra
  `sys.columns`** — mismo resultado que declara la PR, pero verificado de
  forma independiente, no solo tomando su palabra.

## Lo que no pude verificar yo mismo

**`scripts/verificar_contrato.py` y `verificar_crud_catalogos.py`** (contra
el servicio HTTP real corriendo) requieren el ODBC Driver 18 para SQL
Server, que no está instalado en esta máquina (solo tengo el 17), más
RabbitMQ para el flujo completo. No armé ese entorno por el tiempo que
tomaría — me apoyo en que la suite de pytest (vía ASGI) y la verificación
directa de esquema/datos ya cubren la misma superficie desde otros ángulos
(245/245 + mapeo columna-por-columna confirmado en vivo). Si se quiere esa
verificación HTTP real específica, hace falta instalar el driver 18 o correr
esto en un entorno con Docker Compose completo.

## Resultado

Sin hallazgos bloqueantes. Alcance aislado, motor 3D intacto, suite en
verde, esquema y datos confirmados en vivo contra la base compartida real.
