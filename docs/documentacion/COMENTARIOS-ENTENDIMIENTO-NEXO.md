# Comentarios al Documento de Entendimiento de Nexo

Sobre la versión «visión desde desarrollo». Revisado contra el código el 13 de octubre de 2026. Este documento no modifica el Entendimiento: solo reúne comentarios.

**Leyenda:** ✔ ya existe · ◐ existe en parte · ⏳ planificado (se hará).

## 1. Qué cambiar, por sección

| Sección | Comentario | Estado |
|---|---|---|
| 1.1 Alcance | La Biblioteca tarifaria también administra los servicios logísticos y adicionales (conexos). | ✔ |
| 1.3 KPI de tiempo | Reemplazar «300 líneas» por el **tiempo de finalización de la solicitud** (resolución − ingreso a la cola, incluye espera), con filtro por tamaño: 1–10, 11–100 y más de 100 líneas. Meta < 10 min. | ◐ |
| 1.3 Costos desde Nexo | Nexo cuenta las cotizaciones hechas en Nexo. El total que llega a la empresa se digita a mano o se calcula fuera. | ◐ |
| 1.3 Valores bajo piso | «No se mide en esta fase». Se previene con el bloqueo del valor bajo piso. | ⏳ |
| 1.3 Tiempo por línea | De acuerdo. Eliminar «Tiempo AdminTarifas»: queda cubierto por el KPI de tiempo. | ◐ |
| 3. RN-13 | «Cubicación rápida de mercancías para Comercial y AdminTarifas.» | ⏳ |
| 3. RN-14 | «Por ahora el orden de la cola se basa solo en urgencia» (se logra con los otros tres pesos en 0). | ✔ |
| 3. Reglas nuevas | **RN-16:** administración de tarifas plaza y vigencia, servicios conexos y reglas de uso de tarifas (AdminTarifas). **RN-17:** medición de métricas clave (Nexo; consultan AdminTarifas y Admin). | ✔ |
| 4.1 y 4.3 | Comercial y AdminTarifas: «cubicación rápida». AdminTarifas también gestiona reglas Solo-SICE, tarifas plaza y servicios conexos. | ✔ |
| 5. Flujos | Crear una imagen de flujo por cada flujo (principal, alternos y «Retorno vacío con criterio 1 + 1»). Las horas del retorno son 1 h de cargue + 1 h de descargue. | — |
| 10. Integraciones | Agregar a Cotifly: envía solicitudes a Nexo por API y gestiona la cotización al cliente y el resultado. | — |
| 11. No funcionales | Ajustar «Respuesta» al nuevo KPI de tiempo. | — |

### Reglas de negocio sugeridas (se suman a RN-16 y RN-17)

| ID | Regla | Condición / excepción | Responsable | Estado |
|---|---|---|---|---|
| RN-18 | La plaza solo gobierna si está vigente y supera el piso. | Debe ser **estrictamente mayor** que el piso SICE-TAC; si es igual o menor se hace la consulta completa. Una plaza vencida no se propone, solo sirve de referencia. | Nexo | ⏳ |
| RN-19 | Una línea sin vehículo no se autocostea. | Aplica a Cupo; queda pendiente con el motivo visible. No aplica a contenedor. | Nexo | ⏳ |
| RN-20 | Entregar exige todas las líneas costeadas. | Mientras tanto Comercial **ve** las líneas ya disponibles. El autocosteo no pasa por el botón Entregar. | Nexo | ◐ |
| RN-21 | Los conexos se pueden agregar y quitar antes y después de la entrega. | Solo se bloquean si la cotización ya está Ganada o Perdida. Si otra persona los cambia tras la entrega, se avisa al comercial. | Comercial / AdminTarifas | ⏳ |
| RN-22 | Tiempos logísticos por línea. | Por defecto 4 h de cargue + 4 h de descargue, editables por línea; el piso SICE-TAC se recalcula con ellas. El retorno vacío usa 1 + 1 (RN-12). | Nexo | ✔ |

## 2. Pantallas que faltan en 6.1

- **Tarifas Plaza:** mantenimiento con vigencia y carga por Excel (AdminTarifas / Admin).
- **Reglas Solo-SICE:** rutas que se cotizan solo con el piso (AdminTarifas / Admin).
- **Ficha de costos:** líneas disponibles y pendientes; Excel y PDF.

Retirar lo que hoy no existe: la pantalla de **Configuración** (solo hay API) y una **bandeja de Comercial** aparte (es la misma Bandeja filtrada por rol).

## 3. Colores (7.1)

La lógica de colores coincide; los tonos no. Hoy se usa: verde 117A3E, ámbar B45309, rojo C62828 (coincide), azul 1E6FB5 y gris 586573. El morado «En gestión» no existe.

## 4. Avisos (8)

| Aviso | Cómo queda | Estado |
|---|---|---|
| Líneas autocosteadas | Aviso dentro de Nexo; correo por decidir. | ◐ |
| Líneas disponibles (antes «entrega parcial») | Aviso dentro de Nexo, sin correo por línea. | ⏳ |
| Entrega formal | Correo al comercial y aviso en Nexo. | ◐ |
| Conexos cambiados tras la entrega | Correo si lo cambia otra persona. | ⏳ |
| Valor bajo piso | Mensaje y bloqueo en pantalla. | ⏳ |
| Plaza vigente al guardar | Confirmación en pantalla. | ⏳ |
| Tarifa vencida descartada | Correo de urgencia alta. Falta en el documento. | ✔ |

## 5. Tabla de KPIs (1.3) como quedaría

| Indicador | Cómo se mide | Meta |
|---|---|---|
| Tiempo de finalización de la solicitud de costeo | Resolución − ingreso a la cola, incluye espera; global y por tamaño (1–10, 11–100, +100 líneas) | < 10 min |
| Líneas autocosteadas | Automáticas / procesadas | ≥ 80 % |
| Costos desde Nexo | Cotizaciones en Nexo / total que llegan a la empresa (total digitado o calculado fuera) | 100 % |
| Valores bajo piso | No se mide en esta fase | 0 |
| Tiempo por línea | Entrega − entrada a Nexo | Sin límite inicial |

## 6. Sugerencias de los puntos 9 a 12

**10. Integraciones y dependencias**
- Agregar a Cotifly, que envía solicitudes a Nexo por API y gestiona la cotización al cliente y el resultado.

**11. Requisitos no funcionales**
- Ajustar «Respuesta» a la nueva definición del KPI de tiempo (< 10 min, con filtro por tamaño).
