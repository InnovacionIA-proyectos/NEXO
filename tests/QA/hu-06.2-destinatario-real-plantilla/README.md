# Verificación QA — S2-HU-06.2 (issue #22, PR #78)

Rama: `22-s2-hu-062-resolución-del-destinatario-y-plantilla-de-la-notificación`
(ivasco-dev). Verificación hecha antes de aprobar/mergear. Sin conflictos de merge.

## Qué trae el PR

- El destinatario de la notificación de entrega (S2-HU-06.1) pasa de un "comercial
  responsable" resuelto por NIT del cliente contra `DwCubosSap`, a
  **`Solicitud.Creador`** — el usuario real de Nexo que dio de alta la consulta,
  resuelto contra la sesión real del portal (S2-HU-03).
- Se retira `IComercialResponsablePort`/`ComercialResponsableSapAdapter`/
  `DwCubosSapDbContext` por completo, ya sin uso.
- Plantilla ajustada al nuevo destinatario (saludo, asunto) — redacción pendiente de
  aprobación final de negocio, declarado explícitamente por el autor.
- **Salvaguarda doble**: de comportamiento (prueba que el destinatario nunca sale del
  cliente) y **estructural** (`Cliente` no tiene ninguna columna de correo — un test
  de reflexión lo confirma, así que aunque el handler tuviera un bug mañana, no hay
  de dónde sacar una dirección de cliente).

## Verificación — reproducida de forma independiente, no solo leída

### Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 108/108 |
| `Application.Tests` | **114/114** (coincide con lo que afirma el PR) |
| `Api.Tests` | 94/94 (+2 saltadas por diseño de otra HU) |

Build `--no-cache`: 0 errores.

### Flujo real de punta a punta — la primera notificación que se envía con éxito en toda la sesión

Todos los intentos anteriores de esta sesión (HU-06.1) fallaban en "no había a quién
escribirle" porque dependían del DW corporativo (`DwCubosSap`), no disponible en este
entorno. Como esta HU cambia el destinatario a `Solicitud.Creador` —que sí existe
localmente, sembrado por HU-03— **es la primera vez que se puede completar el envío
real**. Con [`verify-destinatario-real.mjs`](verify-destinatario-real.mjs): creé una
solicitud completa como Carlos Herrera (`X-Portal-Usuario-Id: 1001`,
`CoordinadorFletes` real) hasta la cotización entregada:

```
solicitud creada por Carlos Herrera (1001): d78c048b-...
entregar -> 200: { estadoNotificacion: 'encolada', ... }

Correo recibido -> To: cherrera@boterosoto.com.co | Subject: Tu cotización para
  Almacenes Éxito S.A. quedó entregada — ¤1,500,000
TODO OK: el correo llegó al creador real de la solicitud, no a un "comercial
  responsable" del DW.
```

**Confirmado exactamente lo que promete la HU**: el correo llegó a
`cherrera@boterosoto.com.co` — el email real de Carlos Herrera en la tabla
`Usuarios`, sincronizado desde la sesión del portal — no a ningún dato del cliente
ni a un responsable comercial externo.

### Carga y estrés — nuevo: varios creadores concurrentes, sin cruces

El riesgo específico de resolver el destinatario desde `Solicitud.Creador` bajo
carga es que dos flujos concurrentes de creadores distintos crucen sus
notificaciones. [`verify-notificaciones-carga.mjs`](verify-notificaciones-carga.mjs):
**4 flujos completos concurrentes** (crear solicitud → cargar línea → cotizar →
entregar), cada uno con un creador real distinto:

```
4 flujos completados en 243 ms.
4 correos recibidos (esperados 4).
Esperados: [ablanco@..., cherrera@..., prestrepo@..., yzapata@...]
Recibidos: [ablanco@..., cherrera@..., prestrepo@..., yzapata@...]
TODO OK: cada notificación llegó exactamente a su propio creador, sin cruces bajo concurrencia.
```

(Nota de la propia construcción de esta prueba: al incluir inicialmente un
`LiderProceso` como creador, la petición de creación falló con `403` — confirma, de
paso, que la autorización real del endpoint de creación de solicitudes sigue
restringida a `CoordinadorFletes`/`Admin`, tal como declara
`SolicitudesController.cs`.)

## ⚠️ Hallazgo — no bloqueante, no es un bug de esta PR

El correo real (asunto y cuerpo) muestra el total con el símbolo **`¤`** en vez de
`$`: `Tu cotización para Almacenes Éxito S.A. quedó entregada — ¤1,500,000`. Viene de
`{c.Total:C0}` en `PlantillaDeEntrega.cs`, que depende de la cultura del sistema —
en este contenedor es "invariante", sin símbolo de moneda real configurado.

**No es un bug de esta PR**: ese código con `:C0` no cambió aquí, viene sin
modificaciones desde el PR #71 (HU-06.1). Es la primera vez que se ve un correo real
completo en esta sesión precisamente porque esta PR es la que por fin logra que la
notificación se envíe con éxito — antes de esto, ningún intento llegaba a
completarse para poder verlo. Reportado a Kevin, quien decidió: documentarlo aquí
sin bloquear esta PR ni tocar código; el equipo decide cuándo corregirlo (arreglo
simple: un `$` literal o especificar una cultura como `es-CO` al formatear el
total).

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores |
| Regresión | ✅ 108+114+94 = 316 pruebas, sin ningún filtro, cero fallos |
| Integración | ✅ Flujo real de punta a punta, primera notificación exitosa de toda la sesión, destinatario y contenido verificados contra un capturador SMTP real |
| Carga y estrés | ✅ 4 flujos completos concurrentes con creadores distintos, sin cruces de destinatario |

## Conclusión

El destinatario real (`Solicitud.Creador`) queda verificado de punta a punta, con
datos y ejecuciones propias, incluida la primera notificación que se completa con
éxito en toda esta sesión de QA. Se detectó y documentó (sin bloquear) un defecto
cosmético pre-existente del PR #71 en el formato de moneda, visible por primera vez
gracias a esta PR. Sin objeciones para mergear.
