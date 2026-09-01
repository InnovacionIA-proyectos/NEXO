# Verificación QA — S2-HU-02.2 (issue #8, PR #75)

Rama: `8-s2-hu-022-preparar-el-backend-para-servir-al-módulo-del-portal` (ivasco-dev).
Verificación hecha antes de aprobar/mergear el PR #75. Sin conflictos de merge.

## Qué trae el PR

- CORS pasa de un solo origen (`Cors:FrontendOrigin`) a una **lista separada por
  coma** (`Cors:OrigenesPermitidos` / `ORIGENES_PERMITIDOS_CORS`), para cubrir el
  frontend standalone de desarrollo y los dominios del portal por ambiente (QA y
  Producción tienen dominios distintos, sin proxy ni dominio compartido con Nexo).
- Prefijo `/api` y `/health` (ya cumplían el contrato de S2-HU-02.1) quedan
  documentados en el README.
- `.env.example` y `docker-compose.yml` actualizados con la nueva variable.
- `CorsTests.cs` nuevo — el propio autor advirtió en el PR que no pudo correrlo
  contra el arnés Docker (sin Docker Desktop disponible) y pidió confirmarlo en CI
  antes de aprobar.

## Verificación — reproducida de forma independiente, no solo leída

### Lo que el autor pidió confirmar: CorsTests.cs contra el arnés real

```
docker compose -f docker-compose.ci.yml run --rm pruebas \
  dotnet test tests/CabinaFletes.Api.Tests --filter FullyQualifiedName~CorsTests
```

```
Passed CabinaFletes.Api.Tests.CorsTests.Un_origen_fuera_de_la_lista_no_recibe_el_encabezado_de_CORS [204 ms]
Passed CabinaFletes.Api.Tests.CorsTests.Un_origen_de_la_lista_recibe_el_encabezado_de_CORS [23 ms]
```

**Confirmado: sí pasan contra el arnés Docker real.**

### Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 103/103 |
| `Application.Tests` | 113/113 |
| `Api.Tests` | **91/91** (89 + 2 nuevas de `CorsTests`, +2 saltadas por diseño de otra HU) |

Build `--no-cache`: 0 errores. Coincide con lo que el autor ya había corrido
localmente (Domain/Application), y confirma lo que no había podido correr
(`Api.Tests`/`CorsTests`).

### Lo que ni el PR ni `CorsTests.cs` cubrían: el mecanismo real de lista multi-origen

`CorsTests.cs` prueba contra `ApiFixture`, que usa el **origen único por defecto**
(`http://localhost:5173`) — nunca ejercita el `Split(',', ...)` con más de un origen
real, ni el `TrimEntries` que el propio código declara para tolerar espacios, ni un
preflight `OPTIONS` real (`HttpClient` en las pruebas no dispara preflight; un
navegador sí). Levanté la app real con
`ORIGENES_PERMITIDOS_CORS=http://localhost:5173, https://portal-qa.boterosoto.com.co,https://portal.boterosoto.com.co`
(nótese el espacio antes del segundo origen, a propósito) y corrí
[`verify-cors-multiorigen.mjs`](verify-cors-multiorigen.mjs):

```
OK: http://localhost:5173 -> Access-Control-Allow-Origin: http://localhost:5173
OK: https://portal-qa.boterosoto.com.co -> Access-Control-Allow-Origin: https://portal-qa.boterosoto.com.co
OK: https://portal.boterosoto.com.co -> Access-Control-Allow-Origin: https://portal.boterosoto.com.co
OK: https://origen-malicioso.example.com -> sin Access-Control-Allow-Origin (correcto, el navegador bloquearía la lectura)
OK: preflight OPTIONS real -> 204, Origin=https://portal.boterosoto.com.co, Methods=POST, Headers=content-type,x-rol
```

Confirma, con la app real y no con un `HttpClient` de prueba:
- Los 3 orígenes de la lista quedan permitidos, **incluido el que tenía un espacio
  delante en la variable de entorno** (el `TrimEntries` funciona de verdad, no solo
  en teoría).
- Un origen fuera de la lista no recibe el encabezado (el navegador bloquearía la
  lectura de la respuesta, aunque el servidor sí responda `200`).
- Un preflight `OPTIONS` real —lo que manda un navegador antes de un `POST` con
  `Content-Type`/`X-Rol`, que es exactamente como el módulo del portal va a llamar
  a esta API— responde `204` con los 3 encabezados de CORS correctos.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores |
| Regresión | ✅ 103+113+91 = 307 pruebas, sin ningún filtro, cero fallos — incluye las 2 nuevas que el autor no pudo correr |
| Integración | ✅ Multi-origen real (con espacios), origen no autorizado, preflight `OPTIONS` real — contra la app levantada, no un `HttpClient` de prueba |
| Carga y estrés | No aplica: CORS es una decisión de encabezados sin estado ni I/O adicional por petición — no hay nada que cargar que no esté ya cubierto por las pruebas de carga de otras HU sobre los mismos endpoints |

## Conclusión

Confirmado exactamente lo que el autor pidió verificar antes de aprobar
(`CorsTests.cs` contra el arnés real) y, además, el mecanismo real de lista
multi-origen que ninguna prueba existente ejercitaba — incluido el caso borde de
espacios en la variable de entorno y un preflight real. Sin objeciones para
mergear.
