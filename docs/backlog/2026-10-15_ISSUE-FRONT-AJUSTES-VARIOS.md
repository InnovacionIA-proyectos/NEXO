### [S5-HU-XX.X] Ajustes de pantalla: botones, filtros, encabezados fijos y horas de devolución (solo frontend)
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task · Prioridad: por definir · Esfuerzo: por definir
Asignado a: por definir (Frontend)
```
Todo se resuelve en el frontend. No hay cambios de backend.

CONTEXTO
Cuatro ajustes pequeños de pantalla pedidos por Sara y un ajuste de horas por defecto. Cada uno se puede cerrar por separado.

1. Comercial no marca ganada/perdida desde Nexo
- Ocultar (o quitar) los botones «Ganada» y «Perdida» para el rol Comercial. No se quita el permiso `marcarGanadaPerdida`.
- Los botones están en la Mesa de Costeo (mesa-costeo-page.component.html, líneas 35-37) y en el resumen de cotización (puedeRegistrarResultado, cotizacion-resumen-page.component.ts línea 67).
- La columna «Resultado» y el filtro de resultado de la Bandeja NO cambian.
- El backend no cambia: Cotifly sigue marcando el resultado por API.

2. Biblioteca, matriz SICE-TAC: quitar tres filtros
- Quitar los selectores de tipo de carga, condición y vía estándar (biblioteca-page.component.html, líneas 40, 52 y 57).
- Las columnas de la tabla se quedan como están.

3. Encabezados fijos al desplazarse
- En todas las vistas con tabla, los encabezados con los nombres de las columnas deben quedar inmóviles al bajar.
- Hoy solo son fijos el encabezado de la página y la vista previa del Excel de captura.
- Tablas por revisar: Bandeja (1), Biblioteca (5), Captura (1), Cubicador (2), Reglas Solo-SICE (1) y Tarifas Plaza (2).

4. Devolución de contenedor: horas por defecto 1 h + 1 h
- Para la línea de negocio «Oferta de Expedición Devolución Contenedor», mostrar las horas de cargue y descargue con 1 y 1 por defecto, y enviarlas.
- Hoy el front las oculta y no las manda, y el backend usa 4 + 4 (captura-page.component.ts: mostrarHoras, ajustarHorasPorDefecto, constante LINEA_DEVOLUCION_CONTENEDOR).
- El backend ya respeta las horas de la línea cuando llegan las dos.
- Límite: solo aplica a lo que se captura en pantalla. Lo que llegue por la API de Cotifly sigue con 4 + 4 mientras el backend no cambie.

CHECKLIST
- [ ] 1. Botones Ganada/Perdida ocultos para Comercial, sin quitar el permiso
- [ ] 2. Tres filtros fuera de la matriz SICE-TAC
- [ ] 3. Encabezados fijos en todas las tablas
- [ ] 4. Devolución de contenedor con 1 h + 1 h por defecto, visibles y enviadas

PRUEBAS
- [ ] Comercial: no ve Ganada ni Perdida en la Mesa ni en el resumen; sigue viendo la columna y el filtro de Resultado en la Bandeja
- [ ] Admin (rol que conserva el permiso): sigue marcando ganada/perdida
- [ ] Matriz SICE-TAC: los tres filtros no aparecen y la búsqueda sigue funcionando
- [ ] Cada tabla: al bajar, los encabezados quedan fijos, también en pantalla pequeña
- [ ] Captura, línea Devolución de contenedor: salen 1 y 1, se pueden editar y el backend las guarda
- [ ] Captura por Excel con línea de devolución: lleva 1 y 1
- [ ] El piso SICE-TAC de esa línea se recalcula con 1 + 1

CRITERIOS DE ACEPTACION
- Comercial no puede marcar ganada ni perdida desde las pantallas de Nexo
- La matriz SICE-TAC no muestra los tres filtros
- Todas las tablas mantienen fijos sus encabezados
- La línea de devolución de contenedor sale con 1 h + 1 h por defecto
```
