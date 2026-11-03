# Flujo de costeo — diseño acordado ("debería ser")

_Fecha: 2026-10-21. Es el diseño acordado con Sara, no necesariamente lo que el código hace hoy en cada detalle. Corregido tras aplicarlo al ejemplo de línea 1 (ver `docs/pruebas/ejemplos-spot-puntual/`)._

1. **¿Regla "Solo SICE-TAC" en la ruta?**
   Sí y hay piso → flete principal = piso, tal cual (no mira plaza ni histórico). Sí pero no hay piso → pendiente. No tiene la regla → sigue al punto 2.

2. **Mayor entre los candidatos que existan:** piso SICE-TAC, plaza vigente, histórico de plaza (últimos 3 meses). No hace falta que existan los tres.
   - Piso válido solo si el período es el mes y año actuales (sin mes de gracia). Si una ruta/vehículo tiene varias filas (vías alternas), se usa la marcada como vía estándar.
   - Si hay al menos un candidato válido → ese es el flete principal (el mayor). Si no hay ninguno → pendiente.

3. **¿Ningún candidato, o el único es una plaza vencida?** → pendiente (una vencida no cuenta como candidato, solo como referencia).

4. **¿Retorna contenedor o levante en ciudad diferente?** Mismo tratamiento para ambas: piso de viaje vacío siempre de SICE-TAC (nunca plaza/histórico), 1h+1h.
   - Se encuentra → se SUMA al flete principal.
   - No se encuentra → la línea COMPLETA queda pendiente (ni siquiera se guarda el flete principal ya calculado).

5. Una línea autocosteada se puede editar después, salvo que su ruta tenga regla "Solo SICE-TAC" (esas no se editan).

6. Un flete guardado nunca queda por debajo del piso SICE-TAC, si el piso se conoce ese día.

7. **Horas para recalcular el piso:** si la línea declara sus propias horas de cargue/descargue, esas mandan. Si no declara nada, default: 4h+4h en el tramo cargado (1h+1h si la línea de negocio es Devolución de Contenedor). El tramo vacío (retorno/levante) siempre es 1h+1h, sin importar lo que declare la línea para el tramo cargado.

Si 1 de 10 líneas cae en una excepción (puntos 1, 3 o 4), las otras 9 se autocostean igual, cada una con su flete.
