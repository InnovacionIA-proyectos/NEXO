# Diagnóstico y fix — el matching normal de SICE-TAC descarta rutas que sí existen

**Estado:** implementado, pendiente verificación con datos reales · **Fecha:** 2026-09-18
**Issues:** #175 (diagnóstico), #176 (fix), #177 (remedición) — S4-HU-07

## El síntoma (dado por la HU)

Auto-costeo real: 58 de 451 líneas (12,9%), muy por debajo de la meta de 80%. Caso
reproducible: la ruta Medellín-Guarne no recibe flete propuesto automáticamente, pero
en cuanto el Administrador la marca "Solo SICE-TAC" a mano, el piso aparece al
instante — es decir, la ruta siempre fue encontrable.

## Causa raíz

`PropuestaTarifaAdapter.ProponerLineaAsync`
(`src/CabinaFletes.Infrastructure/Biblioteca/Adaptadores/PropuestaTarifaAdapter.cs`)
tiene dos caminos completamente distintos para resolver el piso SICE-TAC de una línea,
y el que corre automáticamente (cuando hay `ClaseViaje` capturado y hay conexión SAP —
o sea, casi siempre) es mucho más estricto que el que corre cuando alguien fuerza
"Solo SICE-TAC" a mano:

| | Camino normal (automático) | Camino "Solo SICE-TAC" (manual) |
|---|---|---|
| Método | `TarifarioSapAdapter.SicetacOficialAsync` (líneas 350-401) | `PropuestaTarifaAdapter.CandidatoSicetacAsync` + `SeleccionarSice` (líneas 318-377) |
| Configuración | Igualdad exacta, sin fallback | Se intenta exacta; si no hay filas, se reintenta SIN filtrar por configuración |
| Tipo de carga | Igualdad exacta contra un **parámetro global SAP** (`ParametroAsync(457)`, normalmente `'General'`) — no depende de la `Tipologia` de la línea | Se deriva de `linea.Tipologia` (`MapearTipoCargaSice`), con fallback a `'General'` si no hay filas de ese tipo |
| Unidad de transporte | Igualdad exacta contra otro parámetro global SAP (458) | No es parte del filtro; se usa después de tener candidatas |
| Vía estándar | `= 'SI'` como **filtro duro** — descarta cualquier fila con `'NO'` | Solo criterio de **orden** (se prefiere `'SI'`, nunca se descarta `'NO'`) |
| Aplica solo a | `ClaseViaje ∈ {Contenedor, Cupo}` — **cualquier línea `CargaSuelta` o `Extradimensionado` queda excluida de entrada**, sin construir ninguna consulta | Cualquier `ClaseViaje` |

Ambos construyen el mismo patrón `LIKE '%origen%-%destino%'` sobre `NombreRuta` con la
misma función `Normalizar` — el texto de la ruta en sí casi nunca es el problema. El
problema son los 5 filtros adicionales exclusivos del camino "oficial", cualquiera de
los cuales puede descartar una fila que en realidad existe y tiene tarifa vigente.

Medellín-Guarne es un corredor corto e intrarregional — el perfil típico de una ruta
sin fila `VIAESTANDAR='SI'` en SICE-TAC (verificación con datos reales pendiente, ver
`tests/QA/s4-hu-07-matching-sicetac-oficial/verify-medellin-guarne.sql`).

**No es un problema de datos/negocio** (como concluía el reporte de arquitectura
citado como habilitador): la ruta siempre estuvo en la tabla, con tarifa vigente — el
código automático simplemente nunca la buscaba de la forma correcta.

## Por qué el fix NO reemplaza el camino oficial completo

`tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/README.md` verificó con un
pliego real (15 líneas, comparado 1:1 contra un PDF de cotización real) que el camino
oficial SÍ funciona correctamente para 8 de 12 líneas cotizadas — su lógica de
resolución de `Configuracion` (por rango de peso para Contenedor, por código de
vehículo para Cupo) está probada contra datos reales y no se toca. Reemplazarla por
completo arriesgaba romper esos casos ya verificados.

## Fix (issue #176)

`PropuestaTarifaAdapter.ProponerLineaAsync`: si el camino oficial no encuentra piso
(`oficial.SicetacOficial is null` — ya sea por el gate de `ClaseViaje`, por el filtro
duro de `ViaEstandar`, o por cualquiera de las otras 3 igualdades exactas), se
reintenta con el MISMO selector tolerante que ya usa la marca manual
(`CandidatoSicetacAsync`) antes de rendirse. El camino oficial se prueba primero, sin
modificar su lógica interna:

```csharp
var sicetac = oficial?.SicetacOficial;
if (sicetac is null && dw is not null)
    sicetac = await CandidatoSicetacAsync(dw, origen, destino, vehiculo, linea.Tipologia, linea.ConfiguracionVehiculo?.Carroceria, ct);
```

Efecto: el camino normal ahora encuentra, como mínimo, todo lo que encuentra "Solo
SICE-TAC" (mismo código, mismo resultado garantizado) — y en los casos donde el camino
oficial ya funcionaba, el comportamiento no cambia en absoluto (el fallback nunca se
ejecuta cuando `sicetac` ya tiene valor).

`ReglaDeCosteo` (Domain, RN-01) no se tocó: la regla en sí siempre estuvo bien
implementada, solo nunca recibía un `sicetac` no-nulo para los casos afectados.

## Limitación de la verificación

No existe proyecto de tests para `CabinaFletes.Infrastructure` (los adaptadores hablan
con SQL Server real vía EF, sin mocks — limitación preexistente, no de este fix). No se
pudo verificar contra datos reales desde el entorno de desarrollo (sin conectividad a
las bases corporativas desde el sandbox). Verificación pendiente, con las queries en
`tests/QA/s4-hu-07-matching-sicetac-oficial/`:

1. Confirmar con `verify-medellin-guarne.sql` cuál de los 5 filtros descartaba
   exactamente la ruta.
2. Confirmar con `verify-otros-2-casos-cargasuelta.sql` que hay líneas reales
   `CargaSuelta`/`Extradimensionado` (casos adicionales del issue #175).
3. Desplegar el fix y repetir el pliego de prueba contra `GET /api/metricas/kpis-piloto`
   (issue #177) — ver plantilla abajo.

## Remedición (issue #177) — plantilla para completar con el resultado real

| | Antes del fix | Después del fix |
|---|---|---|
| Líneas auto-costeadas | 58 | ? |
| Total de líneas | 451 | ? (mismo pliego) |
| Porcentaje | 12,9% | ? |

Si el porcentaje sigue lejos del 80%: documentar aquí cuánto se cerró la brecha y qué
queda pendiente. Candidatos ya identificados en este diagnóstico que NO resuelve este
fix (y podrían explicar una brecha remanente):

- Rutas capturadas con formato de ciudad inconsistente (ej. `"Medellín (ANT)"` en el
  catálogo propio vs `"MEDELLIN ANTIOQUIA"` en SICE-TAC) — el `LIKE` no calza en
  ningún camino, ni el normal ni el tolerante, cuando el sufijo de departamento
  difiere tanto. Fuera de alcance de #176 (es un problema de captura/normalización de
  texto, no de qué selector se usa). Candidato a issue aparte si la brecha remanente
  es grande.
- Líneas sin plaza SAP vigente NI piso SICE-TAC para su ruta real (dato faltante, no
  bug de matching) — estas correctamente requieren atención manual (RN inviolable 6).
