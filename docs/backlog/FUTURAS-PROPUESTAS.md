# Futuras propuestas — NEXO

Ideas de mejora que surgen durante la revisión paso a paso, pero que **no
son bugs** ni pendientes de decisión inmediata — son trabajo más grande,
para evaluar más adelante. Para los hallazgos reales del día a día, ver
`REVISION-NEXO-PASO-A-PASO.md`.

## 1. Mesa de Costeo — permitir editar un flete manual ya guardado

**✅ Ya no es una propuesta futura — en implementación (2026-10-12).**
Sara pidió avanzar con esto directamente. Backend implementado y
compilado (nuevo `clsEditarLineaCotizacionCommand` +
`PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}`), frontend
relayado al peer de sandbox, pendiente de confirmar. Ver **HU-B10**
(`HU-BACKEND.md`) y **HU-15** (`HU-FRONTEND.md`), ambos en Mesa de
Costeo, para el detalle completo — lo que sigue abajo queda como
registro histórico de las preguntas que originaron la decisión.

**Situación hoy:** una vez una línea tiene un flete guardado (manual o por
"Aplicar propuestas"), no hay ninguna forma de corregirlo desde la
pantalla — el formulario de edición se oculta apenas existe un flete para
esa línea (`mesa-costeo-page.component.html:255-271`,
`*ngIf="fleteDeLinea(entry.linea.lineaId) as flete; else editorFlete"`).

**Por qué no es solo "agregar un botón de editar":** el backend no tiene
ningún comando de actualización — `clsAgregarLineaCotizacionCommand.cs`
**siempre agrega una fila nueva** (`c.Lineas.Add(linea)`), nunca revisa si
ya existe un flete para esa línea ni lo reemplaza. Si se habilitara
"editar" en el frontend sin arreglar esto primero, el resultado sería una
**fila duplicada** por línea (el flete viejo y el nuevo conviven), e
inflaría el total de la cotización en silencio
(`clsCotizacionCalculos.Recalcular` sumaría ambas).

**Lo que haría falta para hacerlo bien:**
1. Backend: un comando real de actualizar/reemplazar
   (`clsEditarLineaCotizacionCommand` o similar) que busque si ya existe
   un `clsCotizacionLinea` para esa `LineaSolicitudId` y lo actualice en
   vez de agregar uno nuevo — con su propio registro de auditoría
   (`auditoria.Registrar(...)`, ya existe el patrón) para dejar rastro de
   que se corrigió un valor.
2. Frontend: mostrar el formulario editable también cuando ya hay flete
   (no solo la tarjeta de solo lectura), llamando al comando nuevo en vez
   de `agregarFlete()`.
3. Decidir el alcance: ¿cualquiera con permiso `agregarFlete` puede
   editar lo que ya se guardó, o solo mientras la cotización siga en
   `Borrador` (igual que hoy aplica para agregar)? ¿Se permite editar un
   flete que vino de "Aplicar propuestas" (automático), o solo los que se
   cargaron manual?

**Alternativa más barata mientras tanto (ya recomendada, no implementada
todavía):** en vez de habilitar edición, agregar una **advertencia clara
antes de guardar** ("verifique el valor, no podrá editarlo después") en el
formulario de `agregarFlete`, para reducir el error de captura sin tocar
el backend.
