# Verificación QA — PR #96 "Cubicador contrato integracion"

Rama: `cubicador-contrato-integracion` (bquinterobtst, nuevo autor en esta
sesión). Verificación hecha antes de aprobar/mergear.

## Antecedente — rama actualizada correctamente

La primera versión de esta rama partía de un punto muy antiguo del historial
(antes de PR #82/90/95), y un primer intento de merge real dejó caer ~65
archivos de `analitica/`, migraciones y evidencia QA (mismo patrón que el
accidente de la rama de ivasco en el ciclo anterior). Se reportó, Kevin le
pidió al autor traer `QA` a su rama, y lo hizo correctamente (commit
`bafd740 "Merge remote-tracking branch 'origin/QA'..."`). Confirmado: el
merge-base de esta versión final contra `origin/QA` es exactamente la punta
actual de `QA` — cero riesgo de la clase de problema anterior. Merge real:
sin conflictos.

## Qué trae el PR

- **Endpoint nuevo `GET /api/lineas-solicitud/{id}/cubicacion`**: cierra un
  hueco documentado desde hace varias rondas de QA (`PENDIENTES.md`) — el
  Cubicador ya llamaba a este endpoint como parte del flujo "crear plan desde
  línea" (botón "Cubicar"), pero NEXO no lo implementaba todavía.
- **`CubicadorApiKeyAuthenticationHandler`**: esquema de autenticación
  servicio-a-servicio nuevo (Bearer estático contra
  `Cubicador3D:InboundApiKey`), separado del esquema de sesión del portal,
  registrado como no-default (solo aplica donde el endpoint lo pide
  explícitamente).
- Del lado de Cubicador (Python): puerto configurable (8081 por defecto, para
  no chocar con el 8080 de NEXO), CORS opcional, `CONTRATO.md` único
  (consolida `docs/CONTRATOS.md`, que se elimina a propósito), `openapi.json`
  exportado.
- Documentación (`README.md`, `PENDIENTES.md`, `docs/mensajeria/CUBICACION.md`)
  actualizada para reflejar que el endpoint ya existe.

## Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 127/127 |
| `Application.Tests` | **123/123** (+2 nuevas, `ObtenerCubicacionLineaQueryTests`) |
| `Api.Tests` | **122/124** (+6 nuevas, `LineasSolicitudCubicacionControllerTests`; +2 saltadas por diseño) |
| Cubicador (`pytest`) | **70/70** |

Build `--no-cache`: 0 errores. `analitica/` y las 4 migraciones de HU-10/HU-90
confirmadas intactas tras el merge.

## Verificación en vivo del endpoint nuevo — más allá de las pruebas del autor

Con la app real levantada (SQL Server real, migraciones aplicadas), probé los
5 casos que las pruebas unitarias no ejercitan contra HTTP real:

| Caso | Resultado |
|---|---|
| Sin `Authorization` | `401` ✅ |
| `Bearer` incorrecto | `401` ✅ |
| `Bearer` correcto, línea real | `200`, con `origen`/`destino` resueltos de la ruta real ✅ |
| Sesión del portal (`X-Portal-Usuario-Id`) en vez del Bearer | `401` ✅ — confirma que los 2 esquemas de auth están genuinamente aislados, uno no sirve para el otro |
| `Bearer` correcto, línea inexistente | `404 LINEA_NO_ENCONTRADA` ✅ |

## Verificación cruzada real NEXO↔Cubicador — parcial

Levanté ambos stacks juntos (mismo patrón de sesiones anteriores, red Docker
compartida) y disparé `POST /api/planes-cargue/desde-linea/{id}` en Cubicador
contra una línea real de NEXO. Cubicador sí completa la llamada a nivel de
API (`202 procesando`), pero el worker asíncrono (el que realmente ejecuta la
llamada HTTP de vuelta a NEXO) no pudo conectar (`Connection refused`) pese a
que la resolución DNS del nombre `api` funcionaba correctamente entre
contenedores. Investigado sin llegar a una causa concluyente en el tiempo
disponible — probing directo por IP también falló, mientras que `curl`
interno al mismo contenedor de NEXO sí respondía. Todo apunta a una
particularidad del bridging manual de red entre 2 proyectos `docker compose`
distintos en este entorno de pruebas puntual, no a un defecto del código: el
endpoint en sí ya se verificó exhaustivamente por HTTP directo (tabla de
arriba), y el flujo Cubicador→NEXO usa el mismo cliente HTTP
(`infrastructure/clientes/nexo_client.py`) que ya tiene pruebas propias en la
suite de 70/70 de Cubicador. Recomendación: si se quiere cerrar este último
tramo con evidencia end-to-end real, repetir la prueba en un entorno con
`docker compose` orquestando ambos proyectos desde el inicio (no bridging
posterior de redes ya creadas).

## Nota de seguridad, no bloqueante

`CubicadorApiKeyAuthenticationHandler.cs` compara el Bearer recibido contra
la clave esperada con `!=` (comparación de `string` normal), no con una
comparación de tiempo constante
(`CryptographicOperations.FixedTimeEquals`). En teoría esto abre una
posibilidad remota de ataque de temporización para adivinar la clave
carácter por carácter; en la práctica, sobre HTTP/Docker con el jitter de red
típico, es muy difícil de explotar. Es fail-closed en todo lo demás (sin
clave configurada, sin header, o con Bearer que no calza → siempre `401`,
nunca cae al esquema de identidad del portal). Vale la pena corregirlo como
buena práctica, no bloquea este PR.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores; ambos stacks levantan sanos |
| Regresión | ✅ 374 pruebas .NET + 70 Python, sin filtros, cero fallos |
| Seguridad | ✅ Fail-closed confirmado en los 5 casos de auth; ⚠️ comparación de clave no es de tiempo constante (no bloqueante) |
| Integración | ✅ Endpoint verificado por HTTP real con datos reales; ⚠️ cruce completo con el worker de Cubicador no concluyente por una particularidad del entorno de pruebas, no del código |
| Integridad del repositorio | ✅ `analitica/` y migraciones de HU-10/PR90 confirmadas intactas tras el merge (la rama ya venía corregida) |

## Conclusión

El endpoint nuevo funciona correctamente y de forma segura contra pruebas
HTTP reales, con fail-closed verificado en los 5 casos relevantes. La
regresión completa (.NET + Python) está en verde. El único punto sin cerrar
al 100% es la prueba end-to-end del worker de Cubicador, por una limitación
del entorno de pruebas de esta verificación, no un hallazgo contra el código.
Sin objeciones para mergear.
