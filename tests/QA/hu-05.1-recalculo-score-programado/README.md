# Verificación QA — S2-HU-05.1 (issue #19, PR #66)

Rama: `19-s2-hu-051-recalculo-score-programado` (se apila sobre HU-01.1 → HU-04.5).
Verificación hecha situándose en esa rama, antes de aprobar/mergear el PR #66.
Sin conflictos de merge (`mergeable: true` desde el inicio, confirmado con merge de prueba).

## Qué trae el PR
- El recálculo periódico del score deja de vivir como `PeriodicTimer` dentro del proceso del API y pasa a una **saga de priorización** (`CorrelationId` fijo, una sola instancia posible) que se reprograma sola vía el planificador de retardo de RabbitMQ.
- **`GET`/`PUT /api/priorizacion/parametros`** (solo Admin) — antes `ParametrosScore` se declaraba editable y no existía ningún endpoint para editarla.
- El refresco de scores baja a Application y pagina por identificador (no `Skip/Take`, porque escribe sobre las mismas filas que recorre).
- **`build/e2e/recorrido.py`**: script de punta a punta contra la app real (SQL Server, RabbitMQ con plugin de retardo, saga persistida, SignalR con cliente real), solo biblioteca estándar de Python.

## Tres hallazgos del propio autor — verificados, no solo leídos

1. **El disparador por cambio de parámetros no tenía quién lo disparara** — corregido con el endpoint `PUT`. Verificado: `PUT /api/priorizacion/parametros` → 200, y un `PUT` con pesos que no suman 1.0 → 400 sin tocar la fila (ambos en el recorrido E2E).
2. **Solo `horizonteHoras` mueve el puntaje** — los 4 pesos son "configuración dormida" por decisión de negocio (el total es exclusivamente la urgencia por deadline). **Verificado en vivo, con la app real:** cambié `horizonteHoras` de 240 a 4000 sobre una solicitud con deadline a 720 horas → puntaje pasó a **82** (`100*(1-720/4000)`, exacto). Cambié después los pesos → **el puntaje siguió en 82**. Coincide con lo que afirma el PR.
3. **El arranque publicaba al Outbox sin `SaveChanges`** (invisible para cualquier test, porque ninguno publica a través del Outbox real) — cerrado por el propio recorrido E2E, que si esto siguiera roto habría fallado en el primer paso de priorización.

## ⚠️ Corrección a mi propia verificación anterior (PR #64)

En `tests/QA/hu-04.5-signalr-avisos-respaldo/README.md` yo había escrito que los avisos llegaban con **"secuencia estrictamente creciente"**. Es impreciso — mi corrida tuvo solo 10 avisos y le tocó un orden favorable. El `recorrido.py` de este PR, contra la misma app real, reprodujo avisos **desordenados y repetidos de verdad**:

```
secuencias tal como llegaron: [1, 1, 8, 8, 8, 8, 9, 8, 9, 9, 10, 11, 12, 13, 14, 15, 16]
desordenados=1 repetidos=7
```

Lo que el contrato promete **no es entrega ordenada** — es que el cliente descarta cualquier aviso con secuencia menor o igual a la última que pintó. Aplicando esa regla sobre la misma secuencia desordenada de arriba:

```
con la regla del contrato la barra nunca retrocede: [1, 8, 9, 10, 11, 12, 13, 14, 15, 16]
```

Ya corregí la nota en el documento del PR #64 (no reescribo el historial, dejo la corrección ahí mismo con fecha y referencia a este documento). La garantía real del canal **sí se sostiene** — lo que estaba mal era mi descripción de cómo se sostiene, no el canal en sí.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ `Build succeeded`, 0 errores, 1 advertencia conocida |
| Regresión | `Domain.Tests` **69/69** (subió de 61). `Application.Tests` **89/89** (subió de 75). `Api.Tests` sin filtro **66/66** (subió de 54). **Total 224/224**, coincide exacto con lo que afirma el PR |
| Integración | 21 migraciones aplicadas por el camino real de arranque (contenedor recreado desde volúmenes limpios, no solo dentro de un test). `/health` con detalle. Autorización de `/api/priorizacion/parametros`: 401 sin identidad, 403 con rol incorrecto, 200 con Admin |
| Carga y estrés | **Dos reinicios de la API dejan exactamente 1 fila** en `cadena_priorizacion` — verificado por consulta directa a la BD tras 2 `docker restart` reales. Confirma que el candado (CorrelationId fijo) funciona: no se duplica el reloj entre reinicios |

## Recorrido de punta a punta — corrido completo, sin fallos

```
docker compose up -d --build --wait   # desde volúmenes limpios
docker compose --profile e2e run --rm e2e
```

**Resultado: `RECORRIDO COMPLETO SIN FALLOS`.** Atraviesa las HU-04.2 a HU-05.1 en un solo flujo: salud del sistema, autorización de parámetros, creación de solicitud, carga masiva (202+id), avisos en vivo por SignalR con verificación de la regla de secuencia, consulta de respaldo, flujo completo de cotización (flete→conexos→entregar→resultado, el fix del 409 del PR #64 sigue sano), el hallazgo de `horizonteHoras` vs. pesos, y la bandeja ordenada por score.

## Conclusión
Los 3 hallazgos del autor están verificados con evidencia propia, no solo leídos. Mi propia verificación anterior tenía una imprecisión (secuencia "estrictamente creciente") que este PR corrigió y que yo mismo dejé anotada donde corresponde. El candado de instancia única de la saga se sostiene bajo reinicios reales. Sin objeciones para mergear.
