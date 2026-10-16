# Nexo · Cabina de Fletes — Comparación Estado Real vs. Documento de Entendimiento (v3)

**BSSL — Área de Innovación**
Base: `Entendimiento_Nexo.docx.md` (v1.0, 8-sep-2026)
Versiones anteriores: v1 (15-sep-2026, solo backend) · v2 (8-oct-2026, primera actualización)
**Esta versión (v3): 8 de octubre de 2026**

Fuentes:
- Repositorio `NEXO`, rama `DEPLOY` (commit `c503d82`, 7-oct-2026), revisado en solo lectura.
- `NEXO PENDIENTES - Hoja 1.csv` (trabajo nuevo por rol y módulo).
- Confirmaciones del equipo: frontend completo y desplegado; conexión directa a SQL Server 2019 alimentada por SAP; RN-03 implementada; correo en producción confirmado.
- Evidencia adjunta: `cotizacion-falabella-de-colombia-s-a-33c34cae.pdf` y `evidencia-correo-cotizacion-entregada.png` (sección 7).

> **Alcance y confianza.** El backend se verificó contra el código. Lo del frontend (pantallas, PDF, semáforos) se toma de la declaración del equipo y del CSV; el repositorio no incluye el front. La rama `DEPLOY` no contiene aún el trabajo de la **segunda barrida** (p. ej., el motor nuevo de selección de vehículos está solo en documentación en la rama `260-…`), así que ese trabajo se reporta como "en curso".

> **Criterios de conteo de esta versión.** Quedan **fuera del porcentaje**: (1) Azure AD / seguridad de acceso, y (2) la validación de la matriz de roles 6 vs 3 (pendiente de entregables del equipo, no del desarrollo). El porcentaje se calcula en tres bloques: **Primer momento**, **Lo nuevo** y **Total entregado**.

Convenciones: ✅ Implementado · ⚠️ Parcial / en curso · ❌ No implementado · 🆕 Cambio frente a v2

---

## 0. Estadísticas de completado

### 0.1 Bloque A — Primer momento (documento de entendimiento + primera barrida)

Base: 27 requisitos (los 29 originales menos Azure AD y seguridad de acceso, excluidos).

| Dimensión | Ítems | ✅ | ⚠️ | ❌ | % ponderado |
|---|:---:|:---:|:---:|:---:|:---:|
| Alcance (sección 2) | 11 | 11 | 0 | 0 | **100 %** |
| Reglas de negocio RN-01…RN-05 | 5 | 5 | 0 | 0 | **100 %** |
| Notificaciones de negocio | 3 | 3 | 0 | 0 | **100 %** |
| Integraciones (SICE-TAC, SAP/SQL Server, DW) | 3 | 3 | 0 | 0 | **100 %** |
| No funcionales y KPIs del piloto | 5 | 5 | 0 | 0 | **100 %** |
| **Total Bloque A** | **27** | **27** | **0** | **0** | **100 %** |

No quedan parciales. El piloto ya se ejecutó y cumplió la meta de 300 líneas en < 10 min; el piso se verificó (sección 9); la consulta de auditoría por API no fue solicitada y la trazabilidad quedó al 100 % en la primera barrida.

```
Bloque A  [██████████████████████████████]  100 %
```

Evolución del Bloque A: v1 (15-sep) 56,9 % → v2 (8-oct) 82,8 % sobre 29 ítems → **v3 100 %** sobre 27 ítems.

### 0.2 Bloque B — Lo nuevo (CSV de pendientes + segunda barrida)

| Fuente | Ítems | ✅ | ⚠️ En curso | ❌ | % ponderado |
|---|:---:|:---:|:---:|:---:|:---:|
| CSV de pendientes (16 tareas) | 16 | 15 | 1 | 0 | 96,9 % |
| Segunda barrida: ajuste manual de colocaciones de cargue | 1 | 0 | 1 | 0 | 50 % |
| **Total Bloque B** | **17** | **15** | **2** | **0** | **94,1 %** |

Estricto (solo ✅): 88,2 %. En curso: *Recomendación de vehículo + carrocería por tipo de mercancía* y *Ajuste manual de colocaciones de cargue* (este último cerró al 100 % en la primera barrida y se reabre con lo nuevo).

```
Bloque B  [████████████████████████████░░]  94,1 %
```

**CSV por categoría**

| Categoría | Tareas | Realizadas | % |
|---|:---:|:---:|:---:|
| Nuevo (módulo Tarifas Plaza) | 3 | 3 | 100 % |
| Existente (ajustes a módulos construidos) | 13 | 12 | 92,3 % |

**CSV por rol**

| Rol | Tareas | Realizadas | % |
|---|:---:|:---:|:---:|
| AdminTarifas | 11 | 11 | 100 % |
| Comercial | 4 | 4 | 100 % |
| Comercial / AdminTarifas | 1 | 0 (en curso) | 0 % |

**CSV por módulo**

| Módulo | Tareas | Realizadas | % |
|---|:---:|:---:|:---:|
| Tablero de Estado | 1 | 1 | 100 % |
| Reglas Solo-SICE | 2 | 2 | 100 % |
| Tarifas Plaza | 3 | 3 | 100 % |
| Biblioteca | 4 | 4 | 100 % |
| Bandeja Gestión Tarifaria | 1 | 1 | 100 % |
| Nueva Solicitud (Comercial) | 4 | 4 | 100 % |
| General Solicitudes | 1 | 0 | 0 % (en curso) |

### 0.3 Bloque C — Total entregado (A + B)

Aquí se compara **todo lo entregado** contra **lo que se había comprometido originalmente**. Por eso puede superar el 100 %: el proyecto cumplió su línea base y además entregó trabajo adicional.

| Medida | Cálculo | Resultado |
|---|---|:---:|
| Valor entregado (✅ = 1, ⚠️ = 0,5) | A: 27,0 + B: 16,0 | **43,0** |
| Línea base original (Bloque A) | 27 requisitos | 27 |
| **Índice de alcance entregado vs. comprometido** | 43,0 ÷ 27 | **159,3 %** |
| Cumplimiento sobre el total de ítems | 43,0 ÷ 44 (27 + 17) | 97,7 % |

```
Alcance entregado vs. comprometido   [██████████████████████████████████████████████████]  159,3 %
Cumplimiento sobre el total (44)     [█████████████████████████████░]                       97,7 %
```

| Ítems | Total | ✅ | ⚠️ | ❌ |
|---|:---:|:---:|:---:|:---:|
| Bloque A | 27 | 27 | 0 | 0 |
| Bloque B | 17 | 15 | 2 | 0 |
| **A + B** | **44** | **42** | **2** | **0** |

**Cómo leer el 159,3 %:** es un índice de alcance, no un porcentaje de avance. Dice que se entregó 1,6 veces el valor del alcance original. El 97,7 % es el avance real sobre todo lo que hoy está en el tablero; el 2,3 % restante son los 2 ítems en curso del Bloque B.

---

## 1. Objetivo del proyecto

Reemplazar el costeo manual de fletes (SICE-TAC, SAP y Excel cruzados a mano) por un motor que **propone automáticamente el flete por línea** (spot o pliego masivo), respetando el piso legal SICE-TAC y la tarifa de plaza, y que resuelve la cubicación 3D, los servicios conexos y la cotización final. Motivación: **$14.663 millones COP/año** de margen no capturado, riesgo de sanción por cotizar bajo el piso, criterios inconsistentes entre comerciales y pérdida de la memoria de tarifas. Inversión $0; piloto de 2 semanas. Patrocinio: Dirección de Mercadeo (Leonardo Duque); validación: Coordinación de Fletes/Mercadeo (Cristian Jacobo Álvarez); prioridad Alta.

**Lectura actual:** el núcleo (motor de decisión trazable) y todo lo que lo rodea —alertas, KPIs del piloto, fuente de plaza, retorno de contenedor, entrega por correo y PDF— está construido. El piloto ya se ejecutó y cumplió sus metas cuantitativas; lo que queda es cerrar la segunda barrida (Bloque B).

---

## 2. Alcance: qué debía incluir el proyecto vs. qué existe

| # | Ítem del alcance | Estado |
|---|---|:---:|
| 1 | Captura de solicitudes spot | ✅ `POST /api/captura-solicitudes`; buscador de clientes y prospectos; notas comerciales |
| 2 | Captura de pliegos masivos (Excel) | ✅ `analizar-excel`, `lineas:bulk` (con horas por línea) |
| 3 | Propuesta automática de flete | ✅ `ReglaDeCosteo`; matching por DANE y ZC-ZTC; piso con horas reales |
| 4 | Aplicación masiva con resiliencia por línea | ✅ saga de auto-costeo con evidencia QA |
| 5 | Biblioteca consultable | ✅ histórico < 3 meses, sin tarifas ganadas |
| 6 | Catálogo editable de servicios conexos | ✅ por dimensiones logística/adicional; alta individual y por Excel |
| 7 | Tiempos logísticos configurables | ✅ horas de cargue/descargue editables por línea |
| 8 | Retorno de contenedor vacío 1+1 h | ✅ 🆕 ver RN-03 |
| 9 | Cubicador 3D con plan de cargue | ✅ en producción; recomendación de vehículo por peso |
| 10 | Cotización con exportación Excel/PDF | ✅ Excel en backend; PDF (front) adjunto al correo — ver evidencia, sección 7 |
| 11 | Realimentación de tarifas ganadas | ✅ `POST /api/cotizaciones/{id}/resultado` (solo Comercial y Admin) |

**11 de 11.** Lo fuera de alcance (app móvil, sincronización local de SICE-TAC, negociación multinivel, facturación, ruteo de flota, portal de cliente) se respeta.

---

## 3. Problemas que el proyecto debía resolver

| Problema | ¿Se ataca? |
|---|---|
| Costeo manual y lento cruzando 3 fuentes | ✅ |
| Riesgo de multa por cotizar bajo el piso | ✅ RN-02 |
| Pérdida por cotizar sobre el mercado | ✅ RN-01 |
| Criterios inconsistentes entre comerciales | ✅ 🆕 horas por línea y retorno de contenedor normalizados (RN-03) |
| Memoria de tarifas dispersa | ✅ Biblioteca + Tarifas Plaza + tarifas ganadas |
| Margen no capturado | ✅ el instrumento de medición existe (`/api/metricas/kpis-piloto`) y el piloto ya se ejecutó y cumplió |

---

## 4. Reglas de negocio (RN-01 a RN-05)

| ID | Regla | Estado | Evidencia |
|---|---|:---:|---|
| RN-01 | Flete = MAYOR(plaza, piso); sin plaza gobierna SICE-TAC | ✅ | `ReglaDeCosteo.Decidir()` |
| RN-02 | Nunca bajo el piso; marcar vencidas | ✅ | `DecidirConTarifario()` + alerta de tarifa vencida |
| RN-03 | Retorno de contenedor vacío 1+1 h, por ruta | ✅ 🆕 | Ver detalle abajo |
| RN-04 | Cotización ganada crea tarifa histórica | ✅ | módulo `TarifasGanadas` |
| RN-05 | Toda propuesta muestra su fuente | ✅ | `Fuente` obligatorio; el Comercial ve solo la tarifa final |

**Detalle RN-03 (verificado en el código de `DEPLOY`):**
- La línea de solicitud lleva `RetornaContenedor` y `CiudadDevolucionContenedor`; se considera retorno si cualquiera de las dos viene informada (`clsCapturarSolicitudCommand`, línea 119).
- **Por ruta:** el retorno se costea como un candidato de tarifa propio (`RetornoContenedor` en `PropuestaTarifaDtos`), calculado sobre la ruta de devolución, y se guarda como `ValorRetornoContenedor`, que **suma aparte** al total de la línea.
- **Horas 1+1:** `clsResolucionHorasLinea` aplica 2 h totales (1 de cargue + 1 de descargue) cuando la línea retorna contenedor, en vez de las 4+4 por defecto; el Comercial puede sobrescribir ambas horas por línea. Esas horas alimentan el piso SICE-TAC.
- Pruebas asociadas en `Nexo.Api.Tests` (exportador de cotización) y `Nexo.Domain.Tests`.
- Nota técnica menor: las 2 h son una constante del dominio, no una tabla editable. Hoy no es un pendiente porque el valor por ruta sí se resuelve por tarifa; solo habría que cambiarlo si el negocio pide horas distintas por ruta.

---

## 5. Roles y permisos

El sistema aplica 6 roles (`Comercial`, `CoordinadorFletes`, `AdminTarifas`, `ServiciosLogisticos`, `LiderProceso`, `Admin`), documentados en `docs/ROLES_Y_PERMISOS.md`. El documento de entendimiento planteaba 3.

**Fuera del conteo:** la validación de la matriz 6 vs 3 depende de un entregable del equipo de negocio que aún no llega; no afecta el porcentaje.

Cambios de permisos ya aplicados: resultado de cotización solo para Comercial y Admin; la Bandeja aísla las solicitudes por Comercial; `AdminTarifas` administra Tarifas Plaza, rangos de peso y catálogos; `LiderProceso` accede a métricas y KPIs.

---

## 6. Flujos por rol y flujo end-to-end

**Comercial:** captura ✅; tarifa final sin opciones ✅; conexos por línea ✅; horas por línea ✅; datos requeridos por línea de negocio ✅; Excel ✅ y PDF ✅; marcar resultado ✅.
**Administrador de Fletes:** bandeja por score ✅ (4 pesos, recálculo programado, push SignalR); pliego masivo ✅; aplicación masiva resiliente ✅ con alerta por línea fallida.
**Administrador de Reglas (AdminTarifas):** Reglas Solo-SICE ✅ (desplegables, DANE → municipio); **Tarifas Plaza** ✅ (individual o Excel, semáforo de vigencia); Biblioteca ✅; Tablero de Estado ✅.

| Paso del flujo | Estado |
|---|:---:|
| 1. Captura | ✅ |
| 2. Priorización por score | ✅ |
| 3. Consulta a Biblioteca (SICE-TAC, plaza, histórico) | ✅ plaza vía conexión directa a SQL Server 2019 (alimentado por SAP) y Tarifas Plaza |
| 4. Decisión del flete (plaza/piso + retorno 1+1) | ✅ |
| 5. Aplicación masiva resiliente | ✅ |
| 6. Cubicación 3D y plan de cargue | ✅ |
| 7. Servicios conexos y total | ✅ |
| 8. Entrega (Excel/PDF por correo) | ✅ evidencia en sección 7 |
| 9. Realimentación con tarifa ganada | ✅ |
| 10. Auditoría de cada cambio | ✅ se registra en `AuditLog` (primera barrida al 100 %; la consulta por API no fue solicitada) |

---

## 7. Notificaciones, correo y evidencia

| Notificación exigida | Destinatario | Urgencia | Estado |
|---|---|---|:---:|
| Tarifa vencida detectada | Admin. Fletes / Admin. Reglas | Alta | ✅ `clsNotificarTarifaVencidaCommand` + consumer; se publica desde `clsPropuestaTarifaAdapter` |
| Línea que no pudo costearse | Admin. Fletes | Media | ✅ `clsNotificarLineaFalloTecnicoCommand` + consumer |
| Cotización marcada ganada | Admin. Reglas | Baja | ✅ `clsNotificarCotizacionGanadaCommand` + consumer |
| Cotización entregada (adicional) | Quien pidió la cotización | — | ✅ con PDF adjunto |

**Correo en producción: confirmado por el equipo.** Evidencia adjunta:

1. **`cotizacion-falabella-de-colombia-s-a-33c34cae.pdf`** — Cotización N.º 33c34cae del 5-oct-2026: solicitud Spot, ruta ARMENIA → PEREIRA, fletes $885.726, 0 servicios logísticos y adicionales, tiempos 4 h + 4 h, total $885.726. Muestra el PDF final generado por el frontend con la marca Botero Soto.
2. **`evidencia-correo-cotizacion-entregada.png`** — Correo "Cotización entregada" recibido por la persona solicitante, con el mismo total (885.726 COP, en COP explícito), el aviso de que el PDF va adjunto y el pie "Este aviso es interno: nunca se envía al cliente".

Lo que estas dos evidencias demuestran: la cadena **generación de PDF → entrega por correo corporativo** funciona de punta a punta y con cifras coherentes entre ambos artefactos. Las tres alertas de negocio comparten el mismo emisor de correo (`ICorreoPort`) y la misma resolución de destinatarios por rol, de modo que el canal queda validado. Si se quiere evidencia específica de cada alerta, conviene sumar una captura por cada una (tarifa vencida, línea fallida, cotización ganada).

> **Dato sensible:** ambas evidencias contienen el nombre y el NIT de un cliente real. Si el documento sale de BSSL, anonimícelos o use una cotización de prueba.

---

## 8. Integraciones y dependencias

| Integración | Estado |
|---|:---:|
| SICE-TAC en vivo (piso legal) | ✅ fuente única DWSice |
| **SAP / tarifa de plaza** | ✅ 🆕 conexión directa a **SQL Server 2019** alimentado por SAP (tablas `tblSAPTarifas` y `tblSAPTarifasLog`, base `dbSolucionesTI`, descritas en `docs/tarifas-plaza-provisionales/README.md`), más el módulo Tarifas Plaza para mantenimiento manual o por Excel |
| Data Warehouse corporativo | ✅ catálogos SAP y municipios sincronizados |
| ~~Azure AD~~ | ➖ **fuera del conteo** (ADR-001 vigente; decisión de negocio) |

El puerto `ISapTarifaFleteEnVivoPort` sigue con adaptador mock/deshabilitado; ya no es un pendiente porque la plaza se resuelve por la conexión a SQL Server, que es la vía acordada.

---

## 9. Requisitos no funcionales y KPIs

| Requisito / KPI | Estado |
|---|:---:|
| Pliego de 300 líneas en < 10 min | ✅ piloto ejecutado y cumplido (confirmado por el equipo; medido por `kpis-piloto`) |
| ≥ 80 % de líneas con flete automático | ✅ instrumentado |
| 100 % de cotizaciones emitidas desde la app | ✅ instrumentado |
| 0 fletes bajo el piso | ✅ RN-02 lo impide y está verificado por pruebas (ver abajo) |
| Trazabilidad y auditoría | ✅ se registra en `AuditLog`; la consulta por API no fue solicitada |
| CI/CD de .NET | ✅ `ci-cd-api.yml` y flujos por servicio |
| Seguridad y acceso (Azure AD) | ➖ fuera del conteo |
| Disponibilidad (red/VPN/DW) | ➖ infraestructura |

**Verificación local (8-oct-2026, rama `DEPLOY`, commit `c503d82`):**

| Suite | Resultado |
|---|---|
| `Nexo.Domain.Tests` | 147 de 147 superadas |
| `Nexo.Application.Tests` | 252 de 252 superadas |
| `clsPliegoDatasetValidacionTests` (dataset de 320 líneas) | 34 de 34 superadas, 1,3 s en total |

- **0 fletes bajo el piso:** la prueba `Ninguna_propuesta_de_las_320_lineas_queda_por_debajo_del_piso_sicetac_vigente` pasa; ninguna de las 320 líneas del pliego queda bajo el piso SICE-TAC vigente. Las pruebas de regla esperada y de rutas Solo-SICETAC también pasan.
- **Pliego en < 10 min:** localmente solo se midió el cálculo de dominio, que resuelve las 320 líneas en milisegundos. La medición de punta a punta (con Data Warehouse, costeo y persistencia) no es reproducible sin acceso al DW corporativo desde este equipo; ese resultado se apoya en la ejecución del piloto confirmada por el equipo.
- No se ejecutó `Nexo.Api.Tests`, que requiere base de datos.

Además: evidencia QA por historia en `tests/QA/` (más de 50 carpetas).

---

## 10. Criterio de éxito del piloto (2 semanas)

| Meta | ¿Medible hoy? |
|---|:---:|
| Pliego de 300 líneas en < 10 min | ✅ `kpis-piloto` |
| ≥ 80 % de líneas automáticas | ✅ `kpis-piloto` |
| 100 % de cotizaciones desde la app | ✅ `kpis-piloto` |
| 0 fletes bajo el piso | ✅ verificado por pruebas y piloto |
| Confianza del equipo (no volver al Excel) | ✅ fuente visible + alertas operativas; medición cualitativa |

---

## 11. Resumen de brechas (sin Azure AD ni matriz de roles)

| Prioridad | Brecha | Estado |
|---|---|:---:|
| Alta | Adaptador SAP real | ✅ cerrada (conexión directa SQL Server 2019) |
| Alta | RN-03 retorno de vacío 1+1 | ✅ cerrada |
| Alta | Notificaciones de negocio (3) | ✅ cerrada |
| Alta | KPIs del piloto | ✅ cerrada |
| Media | PDF | ✅ cerrada (front) |
| Media | Correo en producción | ✅ cerrada (confirmado, con evidencia) |
| Media | Auditoría / trazabilidad | ✅ cerrada en primera barrida (la consulta por API no fue solicitada) |
| Baja | Ajuste manual de colocaciones de cargue | ✅ cerrada en primera barrida; ⚠️ reabierta en segunda barrida (Bloque B) |
| Baja | CI/CD .NET | ✅ cerrada |

**Cerradas: 9 de 9** en el primer momento.

---

## 12. Trabajo adicional entregado fuera del documento original

| Área | Aporte |
|---|---|
| Módulo **Tarifas Plaza** | CRUD individual y por Excel, semáforo de vigencia, ajuste de vigencia de plaza |
| **Catálogos y rangos de peso** | Catálogos SAP, municipios (DANE, ZC-ZTC), CRUD de rangos de peso |
| **Recomendación de vehículos** | Catálogo unificado, recomendación por peso con reparto equilibrado, una línea por vehículo (🟡 carrocería por tipo de mercancía en curso) |
| **Servicios conexos por línea** | Selección por línea, horas por línea, catálogo logístico/adicional |
| **Clientes y prospectos** | Buscador combinado con NIT |
| **Notas comerciales** | Observaciones en la solicitud |
| **Aislamiento de Bandeja** | Cada Comercial ve solo sus solicitudes |
| **Mensajería y tiempo real** | RabbitMQ con MassTransit/outbox, SignalR, bandeja push |
| **Endurecimiento** | Corte de 3 meses en histórico, piso con horas reales, fuente única SICE-TAC |

---

## 13. Observaciones

1. **Renombramiento:** quedan restos de `CabinaFletes` en documentación (`ROLES_Y_PERMISOS.md` menciona el header `X-Portal-clsUsuario-Id`; el código de error `SIN_PERMISO_CABINA_FLETES`).
2. **Credenciales:** el 22-sep se retiró del repo un CSV con credenciales en texto plano (commit `b339195`). Conviene rotarlas y confirmar que no quedan en el historial de git.
3. **Segunda barrida:** al estar fuera de `DEPLOY`, su avance se vuelve a medir cuando se integre.

---

## 14. Próximos pasos

El primer momento está cerrado al 100 %. Lo que sigue:

1. **Cerrar la segunda barrida:** recomendación de carrocería por tipo de mercancía y ajuste manual de colocaciones.
2. *(Fuera del conteo, a gestionar con negocio)* Ratificar la ausencia de Azure AD (ADR-001) y validar la matriz de roles cuando el equipo entregue su parte.
