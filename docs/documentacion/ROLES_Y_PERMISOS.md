# Roles y permisos — NEXO (Nexo)

Nexo no tiene login propio: la identidad y el rol llegan resueltos desde el portal
de la Administración en cada petición (encabezado `X-Portal-Usuario-Id` según el
contrato; **hoy el código espera `X-Portal-clsUsuario-Id`**, ver
[`CONTRATO.md` §1.1](../CONTRATO.md#11-autenticación-de-usuarios-identidad-del-portal)),
sin caché de sesión — ver [`docs/contrato-integracion/ADR-001-identidad-sin-token.md`](contrato-integracion/ADR-001-identidad-sin-token.md).
Un usuario tiene **exactamente uno** de los 6 roles siguientes (`RolUsuario`,
[`src/Nexo.Domain/Enums/Enums.cs`](../src/Nexo.Domain/Enums/Enums.cs)); sin un rol
válido, la petición se rechaza (403 `SIN_PERMISO_CABINA_FLETES`) aunque la
identidad sea válida.

Cualquiera de los 6 roles puede **consultar** (listados, detalles, exportar a
Excel) casi todo el sistema — la mayoría de los `GET` solo exige `[Authorize]`
sin rol específico. Lo que diferencia a los roles es quién puede **escribir**
(crear, editar, aprobar). Eso es lo que detalla la tabla de cada rol abajo.

## Resumen

| Rol | Descripción | Capacidades (para pintar UI) |
|---|---|---|
| **Comercial** | Vende y cotiza: captura solicitudes desde Excel, gestiona la cotización y agrega servicios conexos. | `capturarSolicitud`, `gestionarCotizacion`, `agregarConexos` |
| **AdminBiblioteca** | Administra la Biblioteca: Tarifas Plaza, rangos de peso, reglas Solo-SICETAC y vigencia de la plaza. **No** entra a Bandeja, Mesa de Costeo ni cotizaciones (S6-HU-25.1/#307). | `verBiblioteca`, `ajustarVigencia`, `editarReglasSoloSicetac` |
| **AdminTarifas** | Administra el catálogo de tarifas (Biblioteca, vigencia, reglas Solo-SICETAC) y los servicios conexos. | `verBiblioteca`, `ajustarVigencia`, `editarReglasSoloSicetac`, `gestionarCotizacion`, `editarServiciosConexos` |
| **ServiciosLogisticos** | Mantiene el catálogo de servicios conexos (cargues, descargues, etc.). | `editarServiciosConexos` |
| **LiderProceso** | Supervisa el proceso: solo consulta métricas de cumplimiento. | `verMetricas` |
| **Admin** | Control total sobre Nexo, incluida la priorización (único rol con acceso) y, desde #307, crear solicitudes, cargar líneas en bloque y recalcular el puntaje. | `*` (todas) |

*(Capacidades tomadas de `CapacidadesPorRol` en [`clsSesionController.cs`](../src/Nexo.Api/Sesion/clsSesionController.cs) — las usa el frontend para decidir qué pinta; la autorización real vive en el backend, en la tabla siguiente. Ojo: las capacidades no cubren todo lo que el backend permite — p. ej. Comercial puede cubicar y crear planes de cargue sin tener una capacidad para eso.)*

## Qué puede hacer cada uno (además de consultar)

Excepción a "todos consultan": los `GET` de captura de solicitudes (clientes,
aprovisionamiento), reglas Solo-SICETAC, métricas y priorización también están
restringidos por rol, igual que sus escrituras. Los catálogos de ciudades (`GET /api/captura-solicitudes/ciudades`, `departamentos`, `municipios`) 
son la excepción a la excepción (S5-HU-22.1/#288): los ve cualquier rol válido, porque los usan formularios de 
Biblioteca que no son de Comercial. Capturar y analizar un Excel siguen siendo solo de Comercial y Admin.

**#307 — AdminBiblioteca queda fuera de Bandeja, Mesa y cotizaciones, también de sus lecturas.** `GET /api/solicitudes`, `GET /api/solicitudes/{id}`, `GET .../propuesta-tarifa`, el canal `/hubs/bandeja` y todo `/api/cotizaciones` exigen uno de `Comercial`, `AdminTarifas`, `ServiciosLogisticos`, `LiderProceso` o `Admin`: un `[Authorize(Roles)]` de clase se SUMA (AND) al de cada método.

### Comercial
- Analizar y capturar solicitudes desde Excel, buscar clientes y estimar aprovisionamiento (`/api/captura-solicitudes*`)
- Resolver catálogos de líneas (`POST /api/solicitudes/resolver-catalogos`)
- Crear cotización, aplicar propuestas de tarifa, agregar conexos y marcar resultado (ganada/perdida)
- Rechazar una solicitud
- Cubicación rápida (`POST /api/cubicacion`)
- Crear, editar, optimizar y borrar planes de cargue; alta de vehículos del catálogo

### AdminBiblioteca
Antes de #307 este rol operaba el flujo de solicitudes y cotizaciones; ahora es el rol de la **Biblioteca**.
- Consultar la Biblioteca (SICE-TAC, histórico y plaza de SAP, propuesta)
- Tarifas Plaza completas (listar, crear, editar, eliminar, importar y exportar Excel) y rangos de peso (`/api/pesos`)
- Reglas Solo-SICETAC (ver, crear, eliminar, importar y exportar)
- Ajustar vigencia de tarifas de plaza (individual y masivo)
- Cubicación rápida (`POST` y `GET /api/cubicacion`) — conserva este permiso en el backend; el front no le muestra el Cubicador
- Consultar servicios conexos
- **No puede**: Bandeja, Mesa de Costeo, solicitudes, cotizaciones ni planes de cargue (`403`)
- Recibe la alerta de tarifa de plaza vencida

### AdminTarifas
- Ajustar vigencia de tarifas de plaza (individual y masivo)
- Ver, crear y eliminar reglas Solo-SICETAC; importarlas desde Excel
- Crear y editar servicios conexos; importarlos desde Excel
- Crear cotización, aplicar propuestas, agregar línea de flete, editar un flete manual (`PUT .../lineas/{lineaSolicitudId}`), agregar y quitar servicios conexos (también tras entregar, mientras no esté Ganada/Perdida; S5-HU-20.1/#286), entregar y marcar resultado
- Ver métricas de cumplimiento y KPIs del piloto
- Cubicación rápida (`POST` y `GET /api/cubicacion`) — #300
- **Cubicador completo (#310)**: crear, optimizar y editar planes de cargue (`POST /api/planes-cargue`, `/demo`, `/desde-linea/{id}`, `/{id}/optimizar`, `/{id}/items`, `/{id}/items/importar`; `DELETE /{id}/items/{itemId}` y `DELETE /{id}`), mover colocaciones (`PATCH /{id}/colocaciones/{id}`) y crear vehículos (`POST /api/vehiculos`). `DELETE /api/vehiculos/{id}` sigue solo para Admin
- Editar el flete de una línea de **cualquier fuente** salvo con regla Solo-SICETAC (`409 FLETE_SOLO_SICETAC`); al guardar, la fuente pasa a Manual (S6-HU-26.1/#30)
- Recibe la alerta de "línea que no pudo costearse" (#307) y la de tarifa de plaza vencida
- **Rechazar una línea con motivo** y deshacerlo (`POST`/`DELETE /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/rechazo`) — S6-HU-24.3/#320
- **Cambiar el vehículo de una línea** (`PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/vehiculo`) — S6-HU-27.3/#322
- **Agregar alias** al diccionario (`POST /api/catalogos/alias`) y trabajar la **bandeja de normalización** de vehículos en cuarentena (`/api/catalogos/vehiculos-cuarentena`: listar, mapear, descartar) — S6-HU-27.2/#321 y S6-HU-27.3/#322. Leer el diccionario y "resolver" un texto lo puede cualquier rol

### ServiciosLogisticos
- Crear y editar servicios conexos; importarlos desde Excel

### LiderProceso
- Ver métricas de cumplimiento y KPIs del piloto (único permiso además de consulta general)

### Admin
- Todo lo anterior, sin excepción
- Único rol con acceso a Priorización (`GET`/`PUT /api/priorizacion/parametros`)
- Único rol que puede eliminar vehículos del catálogo (`DELETE /api/vehiculos/{id}`)
- Único rol (con el `*`) que crea solicitudes (`POST /api/solicitudes`), carga líneas en bloque (`/lineas:bulk`) y recalcula el puntaje (`/recalcular-score`) — #307, decisión D5

## Referencia rápida por endpoint

Matriz rol × endpoint en [`CONTRATO.md` §5](../CONTRATO.md#5-matriz-de-permisos);
detalle ruta por ruta, con request/response y errores, en el resto de
[`CONTRATO.md`](../CONTRATO.md). Fuente de la verdad real: los atributos
`[Authorize(Roles=...)]` en `src/Nexo.Api/*/cls*Controller.cs`.
