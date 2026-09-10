# Diccionario reconciliado — Viajes Buenaventura 2025-2026

Diccionario único de las 17 columnas del extracto (`Viajes-Btura-2025-2026.xlsx`), cruzando la definición oficial (diccionario interno embebido en `inconsistencias_viajes.xlsx`, hoja `Distancia_0` — Anexo D del plan) con lo que confirmó el propio análisis a lo largo de los 7 notebooks. Una sola tabla, no un diccionario por fuente — ver `hallazgos.md` para el detalle completo de cada hallazgo referenciado (H-00x).

| Columna (original) | Nombre en código | Definición oficial | Unidad / moneda | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|
| `ID Clase Documento FWO` | `idClaseDocumentoFwo` | Código de la línea de negocio (catálogo CLASE FWO, Anexo D) | — | Confiable. 7 de 10 códigos del catálogo presentes; 3 ausentes (`OHUC`, `OPSC`, `OPXL`) sin explicar (L-09) |
| `FO` | `fo` | "Documento de subcontratación con el dueño del vehículo... equivale al número del viaje" | — | **No es clave única** (H-001). Identifica el viaje del vehículo, no la orden de un cliente — puede consolidar carga de varios clientes. Clave práctica: `FO`+`BP Cliente`+`Peso Neto`+`Destino` |
| `Fecha Creación FO` | `fechaCreacionFo` | "Fecha en la cual fue creada la FO en el sistema" | Fecha | Administrativa, no operativa — no sirve para medir tiempos de tránsito reales (L-01). Sí sigue de cerca la operación real (único día en 0 en 372 días: 25-dic-2025) |
| `BP Cliente` | `bpCliente` | Código de la empresa contratante | — | Solo código, sin maestro de nombres/segmento (L-08). 537 clientes distintos, concentración baja (HHI=331, H-010) |
| `Origen` | `origen` | Población donde se origina el transporte | — | Confiable como nombre de ciudad (sin duplicados de digitación, verificado con `rapidfuzz`), pero en la operación real se agrupa en **zonas de tarificación** más amplias, invisibles en el extracto (H-008) |
| `Destino` | `destino` | Población donde termina el transporte | — | Misma nota que `Origen` |
| `Distancia Total` | `distanciaTotal` | "Distancia total, en KM, de la operación de transporte" | KM (confirmado) | **Confirmado: valor planificado/de tabla por ruta o zona, no medición real** (H-011) — pares O-D con cientos de viajes registran exactamente el mismo número |
| `Peso Neto` | `pesoNeto` | "Peso de la carga sin tara del vehículo" | Sin confirmar (kg vs t) | Unidad sigue sin confirmar con el negocio (L-08) |
| `Posición Principal FWOP` | `posicionPrincipalFwop` | PKG (Paquete) / PRD (Producto) / TUR (Transportation Unit Resource) | — | Confiable. TUR = 81,8% del set |
| `Grupo de Equipo FWOP` | `grupoDeEquipoFwop` | Aplica solo a posición TUR; CAM=camión, CN=contenedor | — | Nulos en PKG/PRD son **estructurales**, no defecto. Redundante con `Clase Equipo FOP` (V de Cramér=1,00, H-007) |
| `Vehículo` | `vehiculo` | Placa del vehículo | — | Usar como **identificador**, no como categoría — casi determina `Tipo` e `ID Medio Transporte FO` (H-007) |
| `ID Medio Transporte FO` | `idMedioTransporteFo` | "Categoría/tipo de vehículo, ej. TC-3S2 = tractocamión 3 ejes + trailer 2 ejes" | — | **Fuente de verdad** para tipo de vehículo (reemplaza a `Tipo`). El prefijo (`FP-`/`TC-`) casi determina `Propiedad` (Propio/Tercero) |
| `Clase Equipo FOP` | `claseEquipoFop` | Aplica solo a grupos CN y CAM | — | Jerárquica bajo `Grupo de Equipo FWOP` (mapa completo confirmado, H-007). 1 fila mal clasificada conocida (`CNT40.HC` bajo `CAM`) |
| `Propiedad` | `propiedad` | Flota propia vs. tercero externo | — | Confiable. 91,2% Tercero / 8,8% Propio — explicado por capacidad (127 camiones propios, tope diario), no preferencia |
| `Tipo` | `tipo` | **"Puede omitirse"** (literal del diccionario) | — | **Descartado.** Redundante y menos confiable que `ID Medio Transporte FO` (13,1% de discrepancia entre ambos) |
| `Tarifa venta al cliente` | `tarifaVentaAlCliente` | "Tarifa de venta del viaje realizado" | Sin confirmar (moneda/IVA) | **No confiable en este extracto** (H-002): 72,2% de las filas idénticas a `Flete Producción FO`, confirmado mal capturada en el sistema fuente. En la hoja de inconsistencias aparece rotulada como `Costo Producción` (rótulo invertido, ya corregido en HU-10.2/10.3) |
| `Flete producción FO` | `fleteProduccionFo` | "Costo de producción o flete que se paga al Transportista" | Sin confirmar (moneda/IVA) | **Variable objetivo del futuro modelo predictivo** (aclarado por el negocio). En la hoja de inconsistencias aparece como `Importe FO`. Piso de referencia: SICE-TAC (`tblSAPTarifas`/`tblReglasRutasSicetac`, fuente externa) |

## Qué no cubre este diccionario (ver `limitaciones.md` para el detalle completo)

- Moneda e IVA de las dos columnas monetarias.
- Maestro de `BP Cliente` (código → nombre/segmento).
- Criterio de extracción y estados excluidos (confirmado con evidencia externa: solo `Concluido`/`Ejecutado`, ver H-004 en `hallazgos.md`).
- Mapeo completo ciudad → zona de tarificación (solo se confirmó su existencia, H-008, no se trajo la tabla completa a este análisis).
