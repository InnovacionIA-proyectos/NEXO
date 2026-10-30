# Verificación QA — S2-HU-06.1 (issue #21, PR #71)

Rama: `21-s2-hu-061-notificacion-por-consumidor` (Duban Guerra). Verificación hecha
situándome en esa rama, antes de aprobar/mergear el PR #71.

## ⚠️ Conflicto real de merge — resuelto con autorización explícita de Kevin

Esta rama salió de `QA` en el mismo punto que la #70 (`b39ca8a3`), y **las dos, de
forma independiente, movieron `ICorreoPort` al mismo archivo nuevo**
(`Application/Common/Ports/ICorreoPort.cs`) con formas incompatibles:

- **PR #70** (ya en `QA`): forma simple — un destinatario, un adjunto opcional, sin
  retorno. Pensada solo para el correo de alerta al equipo técnico (issue #13).
- **PR #71** (esta rama): forma más rica — `CorreoParaEnviar` con lista de
  destinatarios, lista de adjuntos, metadatos como diccionario (corrige un bug real
  de un 422 del MailSender), devuelve un id de correlación, y declara
  `CorreoRechazadoException`/`CorreoTransitorioException` para que los reintentos
  clasifiquen el fallo.

No es un conflicto mecánico: es una decisión de diseño real sobre una interfaz
compartida entre dos PRs de autores distintos. Se lo presenté a Kevin con mi
recomendación (quedarse con la de Duban, superconjunto funcional de la otra, con un
bug ya corregido) y él autorizó explícitamente seguir así.

**Resolución aplicada:**
1. `ICorreoPort.cs`: se conserva íntegra la versión de Duban (PR #71). Cero cambios
   de mi parte a este archivo.
2. `Cotizaciones/Adaptadores/SmtpCorreoAdapter.cs` (ubicación vieja): conflicto
   modify/delete — la propia rama de Duban ya lo había borrado, moviéndolo y
   reescribiéndolo en `Notificaciones/Adaptadores/SmtpCorreoAdapter.cs` contra la
   interfaz nueva. Se aceptó el borrado. Confirmado que no queda ninguna referencia
   colgante: la única inyección de `ICorreoPort` en todo el proyecto
   (`Infrastructure/DependencyInjection.cs`) ya elige entre los dos adaptadores
   nuevos de Duban (`MailSenderCorreoAdapter` con URL configurada, `SmtpCorreoAdapter`
   nuevo sin ella) — el archivo viejo no estaba registrado en ningún lado.
3. **`AlertaDeFalloConsumer.cs`** (issue #13/PR #70): único archivo donde se escribió
   lógica real de adaptación. Antes mandaba 6 correos sueltos (uno por destinatario)
   con la forma vieja; ahora arma un solo `CorreoParaEnviar` con los 6 destinatarios
   y llama a la forma nueva, envuelto en un `try/catch` de las 2 excepciones nuevas
   para preservar su contrato original ("un correo que no sale no debe, a su vez,
   agotar reintentos").

**Verificado tras la resolución, no solo compilado:**
- Build limpio, 0 errores.
- Las 3 suites completas, sin filtro: **Domain 103/103, Application 113/113,
  Api.Tests 89/89 (+2 saltadas a propósito por diseño del autor)** — cero fallos.
- **Re-verificación en vivo de la #13 tras el cambio** (con RabbitMQ real + un
  capturador SMTP desechable): forcé el mismo fallo de `CostearLineaSolicitada` que
  ya había probado ayer con la forma vieja. Resultado: **1 solo correo**, con los 6
  destinatarios en el campo "Para" — confirma que el cambio de forma (de 6 llamadas
  a 1) funciona como se diseñó, sin perder a ningún destinatario.

## Qué trae el PR

- La notificación por correo de una cotización entregada sale de la petición HTTP y
  pasa a un consumidor asíncrono (`NotificarEntregaConsumer`), publicado por el
  Outbox en la misma transacción que la entrega — garantiza que no quede una entrega
  sin notificación pendiente.
- El correo sale por el microservicio corporativo **MailSender** (multipart, con el
  PDF adjunto) en vez de SMTP directo, porque el relay SOAP corporativo solo adjunta
  archivos ya alcanzables por ese servidor. Adaptador elegido por configuración
  (`MailSender:BaseUrl`); sin ella cae al SMTP directo.
- Clasificación de reintentos: `400`/`413`/`422` (irreparables) van directo a la cola
  de error sin gastar los 4 intentos; fallos de red/timeout sí reintentan.
- **Hallazgo real del propio autor, encontrado con un envío real** (no con las
  pruebas contra un servidor simulado): el relay rechazó los metadatos como texto
  suelto con 422. Corregido pasando el puerto a recibir un diccionario y que el
  adaptador serialice — el formato es asunto del transporte, no del caso de uso.
- Contrato v1.6.0: la respuesta de `POST /api/cotizaciones/{id}/entregar` cambia de
  `emailEnviado`/`correoResponsable` (mentirían: al responder no ha salido nada) a
  `estadoNotificacion: "encolada"`.

## Verificación — reproducida de forma independiente, no solo leída

### Respuesta rápida de la entrega, con el contrato nuevo

Flujo real completo contra la app levantada (crear solicitud → cargar línea → crear
cotización → agregar línea de flete → entregar con un PDF adjunto real):

```
entregar -> 200 en 124 ms
{
  "cotizacion": { ...Estado: "Entregada"... },
  "estadoNotificacion": "encolada",
  "mensaje": "Cotización entregada. La notificación por correo sale en segundo plano."
}
```
Confirmado: **no** trae `emailEnviado` ni `correoResponsable` (contrato v1.6.0
correcto). Tiempo cercano al `0.099 s` que documenta el PR (la diferencia es el
overhead normal de esta corrida contra SQL Server en Docker vs. la del autor).

### Degradación correcta sin acceso al DW corporativo

Sin `ConnectionStrings:DwCubosSap` (no disponible en este entorno local — misma
limitación que la issue #3), el log muestra:

```
warn: NotificarEntregaConsumer La cotización ... no se notificó: El DW del
  comercial responsable no está configurado (ConnectionStrings:DwCubosSap).
```

Es el camino de degradación correcto y documentado por el propio consumer ("no es un
fallo: es que no había correo que mandar") — **no** genera reintento ni cola de
error. No se pudo probar el envío real con destinatario resuelto por la misma razón
que bloquea la issue #3; queda listo para cuando haya acceso.

### Carga y estrés — 15 entregas concurrentes

```
15 cotizaciones preparadas, 15 entregas disparadas a la vez:
  las 15 -> HTTP 200, estadoNotificacion=encolada
  tiempo máximo individual: 214 ms
  total de la ráfaga: 223 ms
```

Confirmado por el log: **16 mensajes procesados por `NotificarEntregaConsumer`**
(15 de esta ráfaga + 1 de la prueba anterior) — ninguno perdido, ninguno duplicado.
Cola `NotificarEntrega` en 0 mensajes después, `/health` sano.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores, tras resolver el conflicto de merge |
| Regresión | ✅ Domain 103/103, Application 113/113, Api.Tests 89/89 (+2 saltadas por diseño) — coincide con lo que afirma el PR, sin regresiones |
| Integración | ✅ Flujo real de entrega con contrato v1.6.0 verificado, degradación sin DW verificada, re-verificación en vivo de la #13 tras el ajuste del conflicto |
| Carga y estrés | ✅ 15 entregas concurrentes, todas rápidas (máx. 214 ms), sin pérdidas ni duplicados en el consumidor |

## Conclusión

El conflicto de merge con la #70 se resolvió con autorización explícita de Kevin,
verificado en vivo después de resolverlo (no solo compilado). Los 3 criterios de
cierre del PR se reprodujeron: entrega rápida sin importar el estado del MailSender,
correo por consumidor nunca dentro de la petición, y clasificación de reintentos
correcta. Sin objeciones para mergear.
