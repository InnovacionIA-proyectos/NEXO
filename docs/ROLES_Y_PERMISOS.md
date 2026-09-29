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
| **CoordinadorFletes** | Da de alta y gestiona el flujo operativo de las solicitudes: crea, carga líneas, recalcula el score de priorización y decide si se rechazan. | `crearSolicitud`, `cargarLineas`, `recalcularScore`, `rechazarSolicitud`, `gestionarCotizacion` |
| **AdminTarifas** | Administra el catálogo de tarifas (Biblioteca, vigencia, reglas Solo-SICETAC) y los servicios conexos. | `verBiblioteca`, `ajustarVigencia`, `editarReglasSoloSicetac`, `gestionarCotizacion`, `editarServiciosConexos` |
| **ServiciosLogisticos** | Mantiene el catálogo de servicios conexos (cargues, descargues, etc.). | `editarServiciosConexos` |
| **LiderProceso** | Supervisa el proceso: solo consulta métricas de cumplimiento. | `verMetricas` |
| **Admin** | Control total sobre Nexo, incluida la priorización (único rol con acceso). | `*` (todas) |

*(Capacidades tomadas de `CapacidadesPorRol` en [`clsSesionController.cs`](../src/Nexo.Api/Sesion/clsSesionController.cs) — las usa el frontend para decidir qué pinta; la autorización real vive en el backend, en la tabla siguiente. Ojo: las capacidades no cubren todo lo que el backend permite — p. ej. Comercial puede cubicar y crear planes de cargue sin tener una capacidad para eso.)*

## Qué puede hacer cada uno (además de consultar)

Excepción a "todos consultan": los `GET` de captura de solicitudes (clientes,
aprovisionamiento), reglas Solo-SICETAC, métricas y priorización también están
restringidos por rol, igual que sus escrituras.

### Comercial
- Analizar y capturar solicitudes desde Excel, buscar clientes y estimar aprovisionamiento (`/api/captura-solicitudes*`)
- Resolver catálogos de líneas (`POST /api/solicitudes/resolver-catalogos`)
- Crear cotización, aplicar propuestas de tarifa, agregar conexos y marcar resultado (ganada/perdida)
- Rechazar una solicitud
- Cubicación rápida (`POST /api/cubicacion`)
- Crear, editar, optimizar y borrar planes de cargue; alta de vehículos del catálogo

### CoordinadorFletes
- Crear solicitudes, resolver catálogos y cargar líneas en bloque
- Rechazar solicitudes y recalcular el score manualmente
- Todo lo de cotizaciones (crear, aplicar propuestas, agregar línea de flete, agregar conexos, entregar al comercial, marcar resultado)
- Ajustar vigencia de tarifas de plaza (individual y masivo)
- Importar el Excel SICE-TAC
- Cubicación rápida; crear, editar, optimizar y borrar planes de cargue; alta de vehículos del catálogo

### AdminTarifas
- Ajustar vigencia de tarifas de plaza (individual y masivo)
- Ver, crear y eliminar reglas Solo-SICETAC; importarlas desde Excel
- Crear y editar servicios conexos; importarlos desde Excel
- Crear cotización, aplicar propuestas, agregar línea de flete, entregar y marcar resultado (**no** agrega conexos)
- Ver métricas de cumplimiento y KPIs del piloto

### ServiciosLogisticos
- Crear y editar servicios conexos; importarlos desde Excel

### LiderProceso
- Ver métricas de cumplimiento y KPIs del piloto (único permiso además de consulta general)

### Admin
- Todo lo anterior, sin excepción
- Único rol con acceso a Priorización (`GET`/`PUT /api/priorizacion/parametros`)
- Único rol que puede eliminar vehículos del catálogo (`DELETE /api/vehiculos/{id}`)

## Referencia rápida por endpoint

Matriz rol × endpoint en [`CONTRATO.md` §5](../CONTRATO.md#5-matriz-de-permisos);
detalle ruta por ruta, con request/response y errores, en el resto de
[`CONTRATO.md`](../CONTRATO.md). Fuente de la verdad real: los atributos
`[Authorize(Roles=...)]` en `src/Nexo.Api/*/cls*Controller.cs`.
