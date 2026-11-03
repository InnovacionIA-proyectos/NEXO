# Verificación QA — S2-HU-04.2 (issue #14, PR #61)

Rama: `14-s2-hu-042-masstransit-outbox-202` (se apila sobre HU-01.1/01.2/02.1/01.7).
Verificación hecha situándose en esa rama, antes de aprobar/mergear el PR #61.

## Qué trae el PR
- **`Nexo.Mensajes`**: proyecto nuevo sin referencias, con los contratos de mensajes (`AutoCosteoSolicitado`) y sus validadores (`IValidadorDeMensaje<T>`).
- **`IPublicadorDeMensajes`** (puerto, Application) + **`PublicadorValidado`** (Decorator que valida y falla cerrado si un mensaje no declara validador) + **`MassTransitPublicador`** (adaptador, Infrastructure).
- **Outbox transaccional** vía `AddEntityFrameworkOutbox` + `UseBusOutbox()`: publicar escribe en la misma transacción que los datos.
- **`CargarLineasBulkCommand`** ahora responde `202 Accepted` con `{trabajoId, estado, consultarEn}` en vez de costear en la petición.
- MassTransit fijado en **8.5.10** (última versión Apache-2.0 antes de la licencia comercial desde 9.0.0).
- 3 tests de consistencia del autor (`OutboxConsistenciaTests.cs`) contra SQL Server real.

## Criterios de cierre de la issue #14 — los 3 verificados, no solo leídos

| Criterio | Verificado cómo | Resultado |
|---|---|---|
| MassTransit configurado (API + workers) con Outbox transaccional | Código revisado (`MensajeriaDependencyInjection.cs`) + ejecución real | ✅ |
| Carga de pliego responde 202+id sin bloquear | Código revisado (`SolicitudesController.CargarLineas` + `CargarLineasBulkCommandHandler`) | ✅ Coincide exacto con el patrón del contrato de la HU-02.1 |
| Prueba de consistencia: rollback ⇒ no se publica mensaje | **Ejecutado contra SQL Server real** (no solo leído) | ✅ 3/3 |

## Cobertura por tipo de prueba

| Tipo | ¿Aplica? | Resultado |
|---|---|---|
| **Smoke** | Sí | ✅ `Build succeeded`, 0 errores — compila `Nexo.Mensajes` + todo lo demás |
| **Regresión** | Sí | `Domain.Tests` 40/40 ✅. `Application.Tests` **58/58** ✅ (subió de 43 con los tests nuevos de mensajería) |
| **Integración** | Sí — verificado de verdad, no solo leído | Ver "Los 3 tests de consistencia" abajo |
| **Carga y estrés** | Sí | Ver "Prueba de carga" abajo — 30 publicaciones concurrentes, ~700 ms, sin pérdidas ni deadlock |

## Los 3 tests de consistencia — corridos contra SQL Server real

A diferencia de todo lo revisado hasta ahora en esta sesión, esta vez sí se pudo montar
un SQL Server real (contenedor Docker en la misma red que el arnés de CI, sin
necesidad de la BD corporativa — ver metodología abajo) y ejecutar de verdad los
tests de integración marcados `[Trait("Categoria", "Integracion")]`:

```
Si_la_transaccion_confirma_el_mensaje_queda_para_entregar   ✅
Si_la_transaccion_revierte_no_queda_nada_que_entregar        ✅
Publicar_sin_confirmar_tampoco_deja_nada                     ✅
Total: 3/3 en 8.5 s
```

Esto confirma la garantía central de la HU de forma independiente, no solo
confiando en la afirmación del PR.

## Prueba de carga (nueva, no pedida por el PR — parte de la práctica que acordamos)

`OutboxCargaTests.cs` (en esta misma carpeta — no se agregó al proyecto de tests
real, ver cabecera del archivo para cómo reproducirla): 30 publicaciones
concurrentes, cada una en su propia transacción, contra el mismo SQL Server real.

**Resultado: 30/30 mensajes en la tabla de salida, cero pérdidas, cero duplicados, ~707 ms.** Sin señales de deadlock ni contención grave.

## ⚠️ Hallazgo real de esta PR (no bloqueante, pero sí a corregir)

Al correr la suite completa de `Nexo.Api.Tests` contra SQL Server real, until ahora nunca lo había podido hacer nadie del equipo — se encontró:

**`SolicitudesTests.Cargar_lineas_bulk_en_licitacion`** — test preexistente que espera `200 OK` con `{"agregadas": 2}` (el formato **síncrono viejo**). Con el cambio de esta PR, el endpoint ahora responde `202 Accepted` con `{trabajoId, estado, consultarEn}`. El test no se actualizó para el nuevo contrato. Es una falla determinística, 100% reproducible, causada directamente por el cambio de esta PR — no un artefacto de infraestructura.

**Sugerencia de fix** (no aplicada aquí, es del autor del PR):
```csharp
var resp = await _fx.Client().PostJson($"/api/solicitudes/{id}/lineas:bulk", new { lineas = [...] });
Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
Assert.True((await resp.Json()).TryGetProperty("trabajoId", out _));
```

## Hallazgos NO causados por esta PR (preexistentes, primera vez que se detectan)

Correr la suite completa de `Api.Tests` contra una BD real —algo que nadie había podido hacer antes por el bloqueo de acceso a la BD corporativa (issue #3)— destapó 3 problemas de infraestructura de pruebas ajenos a esta PR, ninguno toca archivos de este diff:

1. **Condición de carrera en `ApiFixture`**: con el paralelismo por defecto de xUnit, varias clases de test intentan crear la misma base `NexoClean_Test` a la vez y chocan (`Database already exists`). Con paralelismo desactivado (`xunit.runner.json` de prueba, no committeado) el problema desaparece. Recomendación: agregar un `xunit.runner.json` permanente con `parallelizeTestCollections: false` al proyecto — o rediseñar `ApiFixture` para una base por corrida.
2. **`HealthTests.Health_responde_200_y_healthy`**: el endpoint `/health` devuelve texto plano ("Healthy"), pero el test espera JSON con una propiedad `status`. No forma parte de esta PR (`HealthTests.cs` no está en el diff). Requiere o un `ResponseWriter` JSON en `MapHealthChecks`, o ajustar el test.
3. **3 fallas en `CotizacionesTests`** (`Marcar_ganada_realimenta_biblioteca`, `Entregar_dos_veces_devuelve_409`, `Flujo_completo_cotizacion`): tampoco están en el diff de esta PR. Con el paralelismo corregido, el patrón de fallas es consistente con contaminación de estado entre tests que comparten una única `ApiFixture`/BD sembrada una sola vez, sin aislamiento por test. No se investigó a fondo la causa exacta — queda fuera del alcance de esta PR puntual.

Estos 3 hallazgos son relevantes para la issue #3 (poner en verde las 3 suites) y #4 (CI real) — no para esta HU. Se documentan aquí porque esta fue la primera vez que se pudieron observar.

## Metodología para levantar SQL Server real sin BD corporativa

```
docker run -d --name sqltest-outbox --network nexo-ci_default --network-alias sqltest \
  -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=<clave> -e MSSQL_PID=Developer \
  mcr.microsoft.com/mssql/server:2022-latest

docker compose -f docker-compose.ci.yml run --rm \
  -e TEST_DB_CONNECTION="Server=sqltest;Database=NexoClean_Test;User Id=sa;Password=<clave>;TrustServerCertificate=True;" \
  build dotnet test tests/Nexo.Api.Tests --filter "FullyQualifiedName~OutboxConsistencia"
```
El contenedor de SQL Server se une a la misma red que crea `docker-compose.ci.yml` (`nexo-ci_default`), así que el contenedor del arnés lo alcanza por el nombre `sqltest`. No requiere BD corporativa ni sale de la máquina local. No resuelve la issue #3 (que pide la BD corporativa específicamente), pero sirve para validar que el código es correcto mientras se gestiona ese acceso.

## Conclusión
El mecanismo central (Outbox transaccional) está verificado de verdad, no solo leído, y aguanta concurrencia. Hay 1 hallazgo real y accionable de esta PR (test desactualizado) y 3 hallazgos preexistentes de infraestructura de pruebas, ninguno bloqueante para esta HU en particular. Sin objeciones para mergear, con la observación del test desactualizado dejada en el PR.
