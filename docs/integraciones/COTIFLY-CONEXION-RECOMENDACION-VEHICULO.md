# Qué le falta a Cotifly para conectarse al motor de recomendación de vehículo del Cubicador

Basado en la revisión real del código de `Cotifly/backend-dotnet` y en el
diseño del nuevo motor descrito en
`NEXO/cubicador/docs/PLAN-IMPLEMENTACION-MOTOR-SELECCION-VEHICULOS.md`.

---

## 1. Lo que ya existe hoy (contexto, no hay que construirlo de nuevo)

Cotifly **no llama al Cubicador directamente** — todo pasa por un proxy
síncrono de Nexo. El camino real hoy es:

```
Cotifly → Nexo (POST /api/cubicacion) → Cubicador
```

Piezas ya construidas y en producción:
- `clsCubicacionController.cs` (`POST /web/cubicacion`) — el endpoint propio de Cotifly.
- `clsCubicacionNexo.cs` — el cliente HTTP que llama a `{Nexo:UrlBase}/api/cubicacion`.
- `clsItemCubicacion` (`Descripcion, Cantidad, LargoCm, AnchoCm, AltoCm, PesoKg`) —
  el DTO de bulto que ya se manda, dimensiones en cm, peso en kg.
- Fallback `clsCubicacionNoDisponible` si `Nexo:UrlBase` no está configurada.

Esto es la cubicación **rápida** (sugerencia de un vehículo + flota), no la
recomendación nueva (varios tipos de vehículo, heterogénea, con
`recomendacionVehiculo`/`recomendacionVehiculoMercancia`). Para esa nueva
funcionalidad, hay que decidir y construir lo de abajo.

---

## 2. Decisión tomada: Cotifly llama al Cubicador directo

**Confirmado: Cotifly va a llamar al Cubicador directamente**, sin pasar
por el proxy de Nexo (a diferencia de la cubicación rápida actual, que sí
pasa por `Nexo → Cubicador`). Esto es distinto al resto de los 5 módulos
existentes (Clientes, Municipios, CatalogosFlete, Cubicación, Costeo), que
hoy pasan todos por Nexo — esta es la primera integración de Cotifly que
habla con el Cubicador sin intermediario.

Lo que esto implica construir, que con el proxy no haría falta:

1. **Configuración nueva.** Hoy no existe ninguna clave `Cubicador:UrlBase`
   en ningún lado de Cotifly (solo existe `Nexo:UrlBase`). Hay que agregar
   `clsOpcionesCubicador` (análogo a `clsOpcionesNexo.cs`) con su propia
   `UrlBase`, y la variable de entorno correspondiente
   (`Cubicador__UrlBase`).
2. **Autenticación servicio-a-servicio, no identidad de comercial.** El
   Cubicador no tiene concepto de usuario/comercial — es un servicio
   interno. Pero **sí tiene ya un mecanismo de API key**:
   `docker-compose.yml` del Cubicador define `CUBICADOR_API_KEY` como
   variable de entorno obligatoria. Cotifly necesita esa misma key
   configurada (`Cubicador:ApiKey` o similar) y mandarla en cada request
   (header, a confirmar cuál usa el Cubicador del lado del servidor). A
   diferencia de las llamadas a Nexo, **no hace falta resolver ningún
   `X-Portal-clsUsuario-Id`** — el Cubicador no lo necesita para calcular
   la recomendación.
3. **Registrar el `HttpClient` nuevo** en `clsDependenciasInfraestructura.cs`
   — no puede reutilizar el `HttpClient` compartido `"Nexo"` (apunta a otra
   base URL), necesita su propio nombre (ej. `"Cubicador"`) con su propia
   tubería de resiliencia.

---

## 3. Qué necesita ENVIAR Cotifly (ya tiene casi todo listo)

La buena noticia: el DTO que Cotifly ya usa para las posiciones de carga
(`clsPosicionCarga`, en `clsSolicitudCosteo.cs`/`clsLineaSolicitudCosteo.cs`)
tiene **exactamente la misma forma** que necesita el motor de recomendación:
`Descripcion, Cantidad, LargoCm, AnchoCm, AltoCm, PesoKg` — es literalmente
el mismo shape que `clsItemCubicacion`, que ya se usa para cubicación
rápida.

**Lo que SÍ falta:**
- **`TipoMercancia` por posición.** Hoy `clsItemCubicacion` tiene el campo
  pero Cotifly siempre manda `null` (`clsCubicacionNexo.cs:77`, a
  propósito). El nuevo motor de recomendación SÍ necesita saber el tipo de
  mercancía (para la Tabla 3 `tiposMercancia`: compatibilidad, carrocería
  principal/secundaria, si es condicionada) — este campo no puede seguir
  yendo en `null`. Hay que agregar un selector de tipo de mercancía en el
  formulario de captura de Cotifly (hoy no existe ningún campo así en
  `clsPosicionCarga`).
- **`OrigenSistema`.** El diseño del Cubicador espera saber si la línea
  viene de NEXO o de Cotifly (campo nuevo en `recomendacionVehiculo`) —
  Cotifly tiene que mandar ese dato explícito en cada request, no asumir
  que el Cubicador lo infiere solo.
- **El código de línea de solicitud** (`idLineaSolicitud` que pide el
  Cubicador) — hay que decidir qué campo de Cotifly se usa como ese código
  (¿el folio de la cotización? ¿un id de línea interno?) y documentarlo,
  para que `GET /recomendaciones?idLineaSolicitud=...&origenSistema=COTIFLY`
  funcione.

---

## 4. Qué necesita RECIBIR Cotifly, y dónde usarlo

La respuesta de `/recomendaciones` trae: tipo de vehículo, carrocería,
cuántos vehículos, por mercancía cuánto peso/volumen/cantidad le tocó, % de
llenado, y si quedó "sin selección automática".

**Dónde engancharlo (punto concreto en el código real):**
`clsEnvioCosteoService.EnviarAsync` (línea 78), **antes** de
`entrada.Solicitud.Validar()` — ahí es donde hoy se arma
`clsSolicitudCosteo`/`clsLineaSolicitudCosteo` a partir de `Posiciones`. El
flujo nuevo sería:

1. Si el comercial no especificó `TipoVehiculo`/`Carroceria` a mano, llamar
   a la recomendación con las `Posiciones` ya capturadas.
2. Si el motor devuelve un resultado `ASIGNADO`, rellenar
   `TipoVehiculo`/`Carroceria` automáticamente con lo que devolvió, antes de
   validar.
3. Si devuelve `SIN_SELECCION_AUTOMATICA`, dejar los campos vacíos como hoy
   (el comercial los llena a mano) y mostrar el motivo que trajo el motor.
4. **Caso heterogéneo (varias mercancías en un mismo vehículo):** como
   `clsSolicitudCosteo` hoy modela UNA línea con UN `TipoVehiculo`/UNA
   `Carroceria` para todas sus `Posiciones` juntas, si el motor devuelve
   varios vehículos (`recomendacionVehiculo` con varias filas) para la
   misma solicitud, Cotifly tiene que **partir esa solicitud en varias
   líneas** (`clsLineaSolicitudCosteo`, que ya soporta N líneas desde
   HU-011/ADR-017) — una línea por cada vehículo recomendado, no una sola
   línea con dos tipos de vehículo adentro (eso hoy no tiene dónde ir en el
   modelo).

---

## 5. Desplegables de tipo de vehículo y carrocería — hoy apuntan a otro catálogo

Esto es un hallazgo importante: **el dropdown de tipo de vehículo/carrocería
que ya existe en Cotifly NO viene del Cubicador.** Viene de
`GET /api/biblioteca-sap-historico/filtros` en Nexo (la "biblioteca SAP TM
histórico"), vía `clsCatalogosFleteNexo.cs`. `TipoVehiculo` y `Carroceria`
en todo el dominio de Cotifly son `string?` libres, validados solo por
longitud (100 caracteres), nunca contra una lista cerrada local.

**Lo que hay que decidir:** ¿el dropdown debe seguir mostrando los códigos
de SAP TM histórico (como hoy), o debe mostrar los códigos del nuevo
catálogo `vehiculos`/`tiposCarroceria` del Cubicador? Si son **catálogos
distintos con códigos distintos** (hay que verificar esto — el Cubicador
usa `2L2`, `2S2`, `3S3`, etc.; no sabemos todavía si SAP TM histórico usa
exactamente los mismos códigos), hace falta:

1. Verificar si los códigos coinciden 1:1 entre los dos catálogos.
2. Si no coinciden, decidir cuál es la fuente de verdad para el dropdown
   que ve el comercial — probablemente debería pasar a ser el catálogo del
   Cubicador, ya que es el que el motor de recomendación realmente entiende
   y puede validar.
3. Construir un cliente nuevo (mismo patrón que `clsCatalogosFleteNexo.cs`,
   pero sobre el cliente `"Cubicador"` del punto 2, no sobre `"Nexo"`)
   apuntando a `GET /tipos-carroceria` y `GET /vehiculos` directo en el
   Cubicador, en vez de o además de `biblioteca-sap-historico/filtros`.

---

## 6. Patrón técnico a seguir (ya existe 3 veces en el código, clonarlo)

El cliente nuevo hacia el Cubicador debe seguir el mismo patrón estructural
que ya usan `clsCubicacionNexo.cs`, `clsCatalogosFleteNexo.cs` y
`clsCosteoNexo.cs` — aunque apunte a una base URL y un `HttpClient`
distintos (`"Cubicador"`, no `"Nexo"`):

- Opciones validadas (`IOptions<T>` con `UrlBase` opcional) — en este caso
  `clsOpcionesCubicador`, nueva.
- Una Fábrica (`clsFabricaRecomendacionVehiculo.Crear` o similar) que elige
  implementación real o "NoDisponible" según si `Cubicador:UrlBase` está
  configurada — mismo criterio que los demás módulos.
- DTOs "wire" privados, separados de los DTOs públicos de Aplicación.
- Nunca lanza excepciones hacia arriba — todo vía `clsResultado<T>`/
  `clsErrorDominio`; no-2xx/timeout/circuito abierto → código
  `RECOMENDACION_VEHICULO_NO_DISPONIBLE` (o el que se decida).
- En vez del header `X-Portal-clsUsuario-Id` (eso es específico de Nexo),
  manda la API key del Cubicador (punto 2.2).
- Para la recomendación (corre un algoritmo completo, puede ser más pesada
  que un catálogo simple): un `HttpClient` propio sin reintento de POST,
  igual que `clsCosteoNexo.cs` ("NexoCosteo", 30s, nunca reintenta POST
  porque no es idempotente) — aquí sería algo como `"Cubicador"` con el
  mismo criterio de no reintentar el `POST /recomendaciones`.

---

## 7. Vista unificada de la solicitud partida en varias líneas

Sigue directo del punto 4: cuando una carga heterogénea produce varios
vehículos recomendados, Cotifly parte la solicitud en varias
`clsLineaSolicitudCosteo`. El problema: si el comercial ve esas N líneas
como filas sueltas y desconectadas, pierde de vista que **todas vienen de
la misma solicitud** — mismo origen, mismo destino, misma línea de negocio.

**La vista debe agrupar por lo que es compartido, y desglosar por vehículo
lo que es propio de cada línea resultante:**

```
Línea de negocio: [X]   Origen: [Y]   Destino: [Z]
├── Vehículo 1: TipoVehiculo A · Carrocería A — mercancías: ..., peso: ..., % llenado: ...
├── Vehículo 2: TipoVehiculo B · Carrocería B — mercancías: ..., peso: ..., % llenado: ...
└── Vehículo 3: TipoVehiculo C · Carrocería C — mercancías: ..., peso: ..., % llenado: ...
```

- **Encabezado (una sola vez por grupo):** Línea de negocio, Origen,
  Destino — los 3 campos que son idénticos en las N líneas partidas, porque
  vienen de la misma carga original.
- **Sub-filas (una por cada línea resultante del split):** Tipo de
  vehículo + Carrocería (lo que decidió el motor de recomendación para esa
  porción de la carga), más el detalle de qué mercancías/peso/% de llenado
  le tocó (viene de `recomendacionVehiculoMercancia`, Tabla 8 del
  Cubicador).

**Dónde encaja:** es la contraparte visual de lo que ya documentamos en el
punto 4 — en cuanto el motor devuelve varios vehículos para una misma
solicitud y Cotifly arma las N líneas, esta vista es la que le muestra al
comercial el resultado consolidado, en vez de N líneas sueltas sin relación
visible entre sí. Para armarla, Cotifly necesita agrupar en memoria (o en la
consulta) todas las `clsLineaSolicitudCosteo` que nacieron de la misma
solicitud original (mismo folio/solicitud padre) antes de pintarlas.

---

## 8. Checklist resumen

- [ ] Agregar `clsOpcionesCubicador` + variable de entorno `Cubicador__UrlBase`.
- [ ] Configurar la `CUBICADOR_API_KEY` también del lado de Cotifly y confirmar en qué header la espera el Cubicador.
- [ ] Registrar el `HttpClient` nuevo `"Cubicador"` en `clsDependenciasInfraestructura.cs` (no reutiliza el de `"Nexo"`).
- [ ] Agregar campo de tipo de mercancía al formulario de captura de posiciones en Cotifly (hoy no existe, siempre se manda `null`).
- [ ] Definir qué campo de Cotifly se usa como `idLineaSolicitud`, y mandar siempre `OrigenSistema=COTIFLY`.
- [ ] Construir el cliente HTTP nuevo hacia el Cubicador (clonando la estructura de `clsCubicacionNexo.cs`, apuntando a la URL/API key propias).
- [ ] Enganchar en `clsEnvioCosteoService.EnviarAsync`, antes de `Validar()`.
- [ ] Resolver el caso de solicitud heterogénea → partir en varias `clsLineaSolicitudCosteo`.
- [ ] Construir la vista unificada (§7): agrupar por Línea de negocio/Origen/Destino, con cada vehículo+carrocería como sub-fila.
- [ ] Verificar si los códigos de TipoVehiculo/Carroceria de SAP TM histórico coinciden con los del Cubicador.
- [ ] Si no coinciden, migrar el dropdown de catálogos a consultar `GET /tipos-carroceria`/`GET /vehiculos` directo en el Cubicador.
