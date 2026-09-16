-- Base de datos: CabinaFletes (la app)
-- Regla 3: rutas marcadas "solo SICE-TAC" ignoran la tarifa de plaza.
--
-- reglas_solo_sicetac NO tiene columna de "activa": la regla existe (activa)
-- o se borra (hard delete) — cualquier fila devuelta abajo está vigente hoy.

SELECT Id AS ReglaId, Origen, Destino, TipoVehiculo, CreatedAt
FROM dbo.reglas_solo_sicetac;

-- Con cada Origen/Destino de arriba, correr esto (reemplazar los marcadores)
-- para ver si hay una línea real costeada de esa ruta, y confirmar que su
-- Fuente es 'Sicetac' aunque exista tarifa de plaza para esa misma ruta
-- (buscar la tarifa de plaza con la consulta 02 sobre esa misma ruta, para
-- confirmar que sí existía y aun así se ignoró):

SELECT TOP 5
    cl.Id, c.Id AS CotizacionId, r.Origen, r.Destino,
    cl.ValorFlete, cl.Fuente, cl.CreatedAt
FROM dbo.cotizacion_lineas cl
JOIN dbo.cotizaciones c      ON c.Id = cl.CotizacionId
JOIN dbo.lineas_solicitud ls ON ls.Id = cl.LineaSolicitudId
JOIN dbo.rutas r             ON r.Id = ls.RutaId
WHERE r.Origen = '<<ORIGEN_DE_LA_REGLA>>' AND r.Destino = '<<DESTINO_DE_LA_REGLA>>'
ORDER BY cl.CreatedAt DESC;

-- Si la primera consulta no devuelve ninguna fila, esta regla queda "no
-- alcanzada" en el ambiente real (no hay ninguna ruta "solo SICE-TAC"
-- configurada todavía) — se documenta así en el README, no se fuerza un caso.
