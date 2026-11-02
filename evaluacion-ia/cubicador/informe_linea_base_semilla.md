# Linea base del motor actual (seleccion_flota)

- Fecha: 2026-10-30 · casos: 22 · repeticiones por caso: 50
- Dataset: huella `78884e7c8037` · 22 sintetico
- Catalogo: QA calcado (5 vehiculos, 9 tipos de mercancia)

| Metrica | Resultado |
|---|---|
| Decision (asignar / no asignar) | 100.0 % (22/22) |
| Motivo cuando no asigna | 100.0 % (2/2) |
| Acierto del vehiculo (flota exacta) | 100.0 % (18/18) |
| Acierto de la configuracion (sin mirar carroceria) | 100.0 % (18/18) |
| Flota minima (cantidad exacta) | 100.0 % (20/20) |
| Exceso medio de vehiculos | 0.0 |
| Respeta el peso | 100.0 % (20/20) |
| Respeta el volumen | 100.0 % (20/20) |
| Conserva las unidades | 100.0 % (20/20) |
| Latencia p50 / p95 / max | 1.595 / 25.713 / 390.028 ms |
| Costo por cubicacion | 0 USD (tokens de entrada/salida medios: 0.0/0.0) |
