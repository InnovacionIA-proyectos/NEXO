# Evaluacion de la IA del modo sombra (claude-sonnet-5-5, esfuerzo high, prompt v1)

- Fecha: 2026-10-30 · casos: 22 · repeticiones por caso: 1
- Dataset: huella `78884e7c8037` · 22 sintetico
- Catalogo: QA calcado (5 vehiculos, 9 tipos de mercancia)

| Metrica | Resultado |
|---|---|
| Decision (asignar / no asignar) | 100.0 % (22/22) |
| Motivo cuando no asigna | 100.0 % (2/2) |
| Acierto del vehiculo (flota exacta) | 100.0 % (18/18) |
| Acierto de la configuracion (sin mirar carroceria) | 100.0 % (18/18) |
| Flota minima (cantidad exacta) | 95.0 % (19/20) |
| Exceso medio de vehiculos | -0.05 |
| Respeta el peso | 100.0 % (20/20) |
| Respeta el volumen | 100.0 % (20/20) |
| Conserva las unidades | 100.0 % (20/20) |
| Latencia p50 / p95 / max | 5147 / 13045 / 13639 ms |
| Costo por cubicacion | 0.011015 USD (tokens de entrada/salida medios: 2712.5/559.0) |

## Para revisar

- **Defecto**: caso S-19, 1 vehiculo(s) de menos

## Corrida

- Llamadas al modelo: 22 · tokens de entrada/salida: 59676/12297 · costo aproximado: US$0.2423
- Precio de referencia: US$2.0/US$10.0 por millon de tokens de entrada/salida.
