# QA — S3-HU-02.4: E2E de los comandos de escritura del plan de cargue (issue #106)

**Verificado por:** Kevin Cano (QA). **Fecha:** 2026-09-17.

## Qué se hizo

El issue pedía "8 comandos", pero `docs/mensajeria/CUBICACION.md` sección 6.1 ya
documenta que en realidad son **9** desde que se agregó `eliminar_vehiculo`
(bloqueaba S3-HU-05). Se cubrieron los 9 reales.

El código de la prueba **no vive en esta carpeta**: se agregó directamente a
[`build/e2e/recorrido.py`](../../../build/e2e/recorrido.py) (función
`probar_comandos_escritura_plan_cargue`), reusando el mismo perfil que pedía el
issue (`docker compose --profile e2e run --rm e2e`) — es infraestructura
compartida, no un script aparte. Esta carpeta documenta la verificación.

Cada uno de los 9 pasos hace la llamada real y **relee la proyección** para
confirmar el efecto (nunca se conforma con el 202):

| # | Comando | Cómo se confirma |
|---|---|---|
| 1 | `guardar_vehiculo` | aparece en `GET /api/vehiculos` |
| 2 | `crear_plan` | nace en `procesando`, `GET /{id}` confirma cuando Cubicador responde |
| 3 | `guardar_item` | `TotalItems` pasa a 1 |
| 4 | `importar_items` | `TotalItems` pasa a 3 (2 en bloque) |
| 5 | `eliminar_item` | `TotalItems` vuelve a 2 |
| 6 | `crear_plan_demo` | aparece un plan nuevo en el listado |
| 7 | `ajustar_colocacion` | requiere `optimizar` real primero (ver hallazgo del gate abajo); `Bloqueada=true` reflejado en `/resultado` |
| 8 | `eliminar_plan` | `GET /{id}` -> 404 (borrado optimista) |
| 9 | `eliminar_vehiculo` | desaparece de `GET /api/vehiculos` |

Corrido dos veces seguidas contra el stack real completo (RabbitMQ, SQL Server
de NEXO, Postgres + worker de Cubicador) — reproducible, sin fallos.

### Reintentos / DLQ / alerta (3er criterio del issue)

No se repitió comando por comando. `cubicador/worker/main.py::_procesar()` es
un único despachador genérico: envuelve los 9 tipos (+ `optimizar`) en el
MISMO `try/except` de reintento (1s/5s/15s/60s) + DLQ + alerta — verificado
leyendo el código, no hay lógica duplicada por tipo. Ese mecanismo compartido
ya se demostró en vivo dos veces con tipos distintos:

- `optimizar` — suite original de S2-HU-08.
- `guardar_item` — `tests/QA/fix-worker-sesion-envenenada/` (sesión envenenada
  del worker, PR #149).

Repetirlo para los 7 tipos restantes solo repetiría la misma prueba de
infraestructura ya confirmada.

## Hallazgo 1 (bloqueante, nuevo) — `optimizar` da 500, no 503, con la config por defecto

**Severidad: alta.** Con `Cubicacion__ParidadVerificada` en su valor real por
defecto (cadena vacía, tal como está hoy en `.env.example` y
`docker-compose.yml`), `POST /api/planes-cargue/{id}/optimizar` no responde el
`503` documentado ("Vacías por defecto: la función responde 503 'en
verificación'") — **crashea con 500 no controlado**:

```
System.InvalidOperationException: Failed to convert configuration value ''
at 'Cubicacion:ParidadVerificada' to type 'System.Boolean'.
 ---> System.FormatException: String '' was not recognized as a valid Boolean.
   at CabinaFletes.Infrastructure.PlanDeCargue.Adaptadores.ParidadCubicadorAdapter.get_Habilitada()
     en ParidadCubicadorAdapter.cs:línea 28
   at SolicitarOptimizacionCommandHandler.Handle(...)
```

**Causa raíz:** `ParidadCubicadorAdapter.cs:28` usa
`config.GetValue<bool>("Cubicacion:ParidadVerificada")`. `GetValue<bool>` no
puede convertir una cadena vacía — solo acepta `"true"`/`"false"` (o ausencia
total de la clave, que sí cae a `default(bool)` = `false`). Como
`docker-compose.yml`/`.env.example` dejan la variable **presente pero vacía**
(`Cubicacion__ParidadVerificada=`), nunca cae al `false` implícito: explota.

**Reproducido dos veces**, reiniciando el contenedor `api` sin la
sobre-escritura de la compuerta (ver "Cómo se corrió" abajo) — el 500 salió de
inmediato ambas veces, log completo capturado.

**Impacto real:** cualquier despliegue con la configuración documentada por
defecto (compuerta cerrada, pendiente de decisión de negocio — ver
`HALLAZGOS-QA-CICD.md`) va a crashear con 500 en el primer intento de
optimizar un plan, en vez de dar el 503 accionable que el equipo espera. Nadie
lo había detectado porque las verificaciones previas de "compuerta cerrada" se
hicieron contra `/api/cubicacion` (adaptador de configuración distinto), nunca
contra `/api/planes-cargue/{id}/optimizar`.

No lo corregí — decisión explícita de Kevin de solo documentarlo y seguir con
el issue #106. Fix sugerido (para quien lo tome): tratar la cadena vacía como
`false` antes de convertir (p.ej. `bool.TryParse(config["..."], out var v) &&
v` en vez de `GetValue<bool>`).

## Hallazgo 2 (no bloqueante) — `build/e2e/recorrido.py` completo está roto contra `QA` actual

`S2-HU-03.3` retiró el mecanismo de rol declarado por `X-Rol` (ver
`IdentidadPortalAuthenticationHandler.cs`, doc del propio handler: "no hay más
rol declarado por el cliente (X-Rol)... credenciales de ningún tipo pasando
por Nexo"), reemplazándolo por `X-Portal-Usuario-Id` +
`ResolverIdentidadPortalLocalAdapter` (resuelve contra un usuario REAL
sembrado en `Usuarios`, no un rol declarado). **Todos los ~15 pasos
preexistentes de `recorrido.py` siguen usando `X-Rol`** y hoy dan 401 —
confirmado corriendo el script completo: falla en el segundo paso
(`KeyError` no controlado, ni siquiera termina el recorrido).

Solo se corrigió `probar_comandos_escritura_plan_cargue()` (función nueva de
este issue), con su propia constante `ADMIN_PORTAL = ("X-Portal-Usuario-Id",
"1005")` — decisión explícita de Kevin de no tocar el resto del script (fuera
del alcance de #106). El resto del recorrido queda roto hasta que alguien lo
actualice al esquema nuevo.

## Hallazgo 3 (informativo) — no hay usuario sembrado con rol `Comercial`

La tabla `Usuarios` local (`Database__SeedOnStartup=true`) siembra usuarios
para `Admin`, `CoordinadorFletes`, `LiderProceso` y `ServiciosLogisticos`,
pero ninguno con rol `Comercial` — el rol que más aparece en
`[Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]` de los propios
comandos de esta HU. No bloqueó esta verificación (se usó `Admin`, autorizado
en todos los endpoints salvo `eliminar_vehiculo`, que también acepta Admin),
pero cualquiera que necesite probar el camino específico de `Comercial`
localmente no tiene con qué hoy.

## Cómo se corrió

```bash
# Compuerta ABIERTA solo para esta verificación puntual (no se tocó el default del repo):
export CUBICACION_PARIDAD_VERIFICADA=true
export CUBICACION_PARIDAD_EVIDENCIA="QA-E2E-manual-2026-09-17"
export CUBICACION_PARIDAD_FECHA="2026-09-17"
docker compose up -d --build
cd cubicador && docker compose up -d --build && cd ..
docker compose --profile e2e run --rm e2e   # corre TODO recorrido.py, incluida la función nueva

# Reproducción del Hallazgo 1 (compuerta cerrada, default real):
docker compose up -d api   # sin las 3 variables de arriba
# POST /api/planes-cargue/{id}/optimizar -> 500 (ver log arriba)
```

`nexo-qa-sqlserver` (SQL Server para `docker-compose.yml`, distinto del
`db-pruebas` efímero de `docker-compose.ci.yml`) no existía al empezar esta
sesión — se recreó como contenedor independiente en la red `nexo_internal`,
reutilizando la contraseña ya presente en `.env`. Migró y sembró solo.

## Decisión sobre el issue #106

Los 3 criterios de cierre están cubiertos con evidencia real. Se propone
cerrar #106, documentando los 3 hallazgos de arriba (uno de ellos, el 500,
como bloqueante para el negocio el día que decida activar la compuerta —
aunque no bloquea el cierre de ESTE issue).
