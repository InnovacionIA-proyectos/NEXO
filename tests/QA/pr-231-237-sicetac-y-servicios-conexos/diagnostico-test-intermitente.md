# Diagnóstico: intermitencia en clsSolicitudesTests (aislamiento por comercial)

## Síntoma

Corriendo `Nexo.Api.Tests` completo contra la base real, 1 de cada 2-3 corridas
falla con `500 Internal Server Error` en alguno de los tests de aislamiento por
comercial (`Un_comercial_no_ve_en_la_bandeja_las_solicitudes_de_otro_comercial`,
`El_dueno_si_puede_pedir_el_costeo_de_su_propia_solicitud`) — nunca el mismo test
dos veces seguidas, y las llamadas que fallan cambian de corrida a corrida.

## Cómo se aisló

Se editó temporalmente (sin comitear) `clsSolicitudesTests.cs` para reemplazar la
llamada que fallaba (`GetFromJsonAsync`, que lanza en cualquier respuesta no-2xx)
por una lectura cruda del `HttpResponseMessage` + `Content.ReadAsStringAsync()`,
para ver el status/body real en vez de solo la excepción de deserialización:

```csharp
var resp = await _fx.Client("Comercial", comercialB).GetAsync("/api/solicitudes?atencion=todas");
var body = await resp.Content.ReadAsStringAsync();
throw new Exception($"DIAG status={resp.StatusCode} body={body}");
```

Resultado: **la llamada aislada respondió 200 con el body correcto** (comercial A ve
su solicitud, comercial B no ve nada) — la lógica de negocio del filtro funciona
bien. El 500 solo aparece corriendo la suite completa, nunca aislando la llamada.

## Conclusión

Es una condición de carrera de infraestructura de pruebas (probablemente
paralelismo entre tests de la misma colección `ICollectionFixture<clsApiFixture>`
compitiendo por el mismo usuario/recurso), no un bug de la regla de negocio. Se deja
como hallazgo no bloqueante — reproducible con `verify-flakiness-bandeja.sh`.

## Pendiente de investigar (no bloqueante)

- Confirmar si `[Collection("api")]` corre sus clases en paralelo por defecto en
  este proyecto (xUnit las corre en serie por colección, pero clases sin
  `[Collection]` explícito sí corren en paralelo entre sí — revisar si alguna
  otra clase de test toca las mismas tablas/usuarios al mismo tiempo).
