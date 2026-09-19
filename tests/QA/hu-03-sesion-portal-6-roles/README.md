# Verificación QA — S2-HU-03 (issues #9, #10, #11, PR #77)

Rama: `9-s2-hu-031-validar-sesion-del-portal` (ivasco-dev). PR grande: retira por
completo Azure AD y `DevAuthenticationHandler` (el `X-Rol` que usé en **toda** esta
sesión de QA hasta ahora) y los reemplaza por `IdentidadPortalAuthenticationHandler`,
que valida `X-Portal-Usuario-Id` contra la identidad real del portal en cada
petición. Verificación hecha antes de aprobar/mergear. Sin conflictos de merge.

## Qué trae el PR

- **`IdentidadPortalAuthenticationHandler`**: valida `X-Portal-Usuario-Id` (id crudo
  del portal) en cada petición, sin caché — reemplaza a Azure AD (JwtBearer) y a
  `DevAuthenticationHandler` (`X-Rol`), retirados por completo.
- **`MapeoRolPortal`** + **`ResolucionRolPortal`**: traduce los roles del portal
  (catálogo ajeno) a uno de los 6 roles de Nexo, con precedencia cuando una persona
  tiene varios a la vez (Admin > CoordinadorFletes > AdminTarifas > Comercial >
  LiderProceso > ServiciosLogisticos — propuesta técnica, pendiente P3 de confirmar
  por negocio).
- **Autenticación vs. autorización separadas a propósito**: una identidad válida sin
  rol de Nexo SÍ se autentica (evita un 401 genérico) pero cae en 403 vía la
  `FallbackPolicy` — aplica a TODO endpoint, no solo a los que declaran
  `[Authorize(Roles=...)]`.
- **Fail-closed en Producción**: sin `ConnectionStrings:IdentidadPortal`, el arranque
  falla. En Desarrollo/QA sin esa fuente real, `ResolverIdentidadPortalLocalAdapter`
  resuelve contra la tabla `Usuarios` local por `IdentidadPortalId` — mismo invariante
  de seguridad (no confía en nada que declare el cliente), solo cambia la fuente de
  verdad.
- **Autoría real**: se elimina el usuario-sistema placeholder; el `AuditLog` queda con
  el `Usuario.Id` real de quien hizo la operación.
- Migración EF (`Usuario.AdObjectId` → `IdentidadPortalId`, tabla `mapeo_rol_portal`).
- 3 pendientes de negocio/TI heredados del contrato, documentados honestamente en
  `PENDIENTES.md` (P1: columnas reales de la tabla del portal sin confirmar por TI;
  P2: solo 3 de los 6 roles tienen mapeo sembrado — falta `CoordinadorFletes`,
  `LiderProceso`, `ServiciosLogisticos`; P3: la precedencia de roles es propuesta
  técnica, no decisión de negocio).

## Verificación — reproducida de forma independiente, no solo leída

### Lo que el autor pidió confirmar: SesionTests.cs contra el arnés real

```
docker compose -f docker-compose.ci.yml run --rm pruebas \
  dotnet test tests/CabinaFletes.Api.Tests --filter FullyQualifiedName~SesionTests
```

```
Passed CabinaFletes.Api.Tests.SesionTests.Con_identidad_pero_sin_rol_de_Nexo_queda_sin_permiso
Passed CabinaFletes.Api.Tests.SesionTests.Con_rol_valido_devuelve_la_sesion_resuelta
Passed CabinaFletes.Api.Tests.SesionTests.Sin_identidad_del_portal_no_entra
```

**Confirmado: sí pasan contra el arnés Docker real.**

### Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | **108/108** (103 + 5 nuevas de `ResolucionRolPortalTests`) |
| `Application.Tests` | 113/113 |
| `Api.Tests` | **94/94** (91 + 3 nuevas de `SesionTests`, +2 saltadas por diseño de otra HU) |

Build `--no-cache`: 0 errores.

### El mecanismo real, con la app viva (no `ApiFixture`)

Levantada la app SIN `ConnectionStrings:IdentidadPortal` (usa
`ResolverIdentidadPortalLocalAdapter`, contra `Usuarios` sembrada con
`IdentidadPortalId` 1001-1006):

```
1) /health sigue público, sin auth                                 -> 200
2) /api/sesion/actual SIN X-Portal-Usuario-Id                       -> 401 SESION_INVALIDA
3) X-Rol: Admin (el viejo mecanismo)                                -> 401 (confirmado: genuinamente retirado)
4) X-Portal-Usuario-Id: 1005 (Admin, Paola Restrepo)                -> 200, rol=Admin, capacidades=["*"]
5) X-Portal-Usuario-Id: 1001 (CoordinadorFletes, Carlos Herrera)    -> 200, rol correcto, capacidades correctas
6) X-Portal-Usuario-Id: 1003 (LiderProceso, Luisa Chaverra)         -> 200, rol correcto
7) X-Portal-Usuario-Id: 9999999 (inexistente)                       -> 401 SESION_INVALIDA
8) ServiciosLogisticos (1004) contra POST /api/solicitudes
   (requiere CoordinadorFletes,Admin)                               -> 403 SIN_PERMISO_CABINA_FLETES
```

El punto 3 es el más importante de verificar: confirma que el mecanismo viejo que
usé en **toda la sesión hasta ahora** (`X-Rol`) queda genuinamente inoperante, no
solo removido del código de un lado.

### Autoría real, verificada contra la base

Creé una solicitud como el CoordinadorFletes real (`X-Portal-Usuario-Id: 1001`) y
consulté `audit_log` directamente:

```sql
SELECT a.UsuarioId, u.Nombre, u.IdentidadPortalId, a.Accion
FROM audit_log a JOIN usuarios u ON u.Id = a.UsuarioId
WHERE a.EntidadId = '<id de la solicitud creada>'
```

```
UsuarioId: 609CF162-BC0F-4FA4-9B19-2DE991B5CF51
Nombre: Carlos Herrera
IdentidadPortalId: 1001
Accion: crear
```

**El `Usuario.Id` real de Carlos Herrera, no un placeholder de sistema** — confirma
la afirmación del PR de punta a punta, no solo en el código.

### Carga y estrés — nuevo: resolución de identidad SIN caché, bajo concurrencia real

El riesgo específico de resolver la identidad en cada petición sin caché es una fuga
entre scopes de DI bajo carga concurrente: que una petición reciba, por una condición
de carrera, la identidad de otro usuario que se está resolviendo al mismo tiempo.
[`verify-identidad-carga.mjs`](verify-identidad-carga.mjs): **60 peticiones
concurrentes** a `/api/sesion/actual`, alternando entre las **6 identidades reales
sembradas** (10 rondas × 6 usuarios):

```
60 peticiones concurrentes, 6 identidades distintas x 10 rondas.
TODO OK: ninguna petición recibió la identidad de otro usuario.
```

Ninguna de las 60 respuestas cruzó identidad con otro usuario — la resolución por
petición sostiene bajo concurrencia real.

## Nota de QA, no bloqueante: solo 4 de los 6 roles son sembrados localmente

`DataSeeder.cs` siembra `IdentidadPortalId` para 4 de los 6 roles de Nexo
(CoordinadorFletes, LiderProceso, ServiciosLogisticos, Admin) — **`Comercial` y
`AdminTarifas` no tienen ningún usuario sembrado con `IdentidadPortalId`**. Como todo
`[Authorize(Roles=...)]` del proyecto incluye `Admin` como comodín, esto no bloquea
ninguna prueba funcional (usé Admin donde hacía falta), pero si en el futuro alguien
necesita probar el comportamiento NEGATIVO específico de esos 2 roles contra la app
real (no solo contra `ApiFixture`), no hay con qué autenticarse localmente sin
insertar una fila a mano. Vale la pena sembrar 1 usuario de cada uno para la próxima
HU que toque esos roles — no es un defecto de esta PR, es una recomendación de cara
al trabajo que sigue.

**Actualización 2026-09-18 (S4-HU-08.5, issue #187):** sembrado un usuario `Comercial`
(`IdentidadPortalId` 1007, Mariana Ospina) — motivado por los comandos de escritura de
S3-HU-02 (plan de cargue), que en su mayoría exigen ese rol. `AdminTarifas` sigue sin
usuario sembrado; la recomendación de arriba sigue vigente para ese rol.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores |
| Regresión | ✅ 108+113+94 = 315 pruebas, sin ningún filtro, cero fallos — incluidas las 8 nuevas que el autor no pudo correr |
| Integración | ✅ Mecanismo real completo contra la app viva: 401/403/200 en los 8 escenarios de arriba, retiro genuino de `X-Rol`, autoría real verificada contra la base |
| Carga y estrés | ✅ 60 peticiones concurrentes de 6 identidades distintas, sin ningún cruce — la resolución sin caché sostiene bajo concurrencia |

## Conclusión

Confirmado exactamente lo que el autor pidió verificar (`SesionTests.cs` contra el
arnés real) y, más allá de eso: el mecanismo completo probado en vivo — incluida la
confirmación explícita de que el viejo `X-Rol` que usé en toda la sesión ya no
funciona, la autoría real contra la base, y que la resolución sin caché no se rompe
bajo concurrencia. Los 3 pendientes (P1/P2/P3) están declarados honestamente en
`PENDIENTES.md`, no ocultos. Sin objeciones para mergear.
