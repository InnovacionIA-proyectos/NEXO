# Copias legibles — SOLO para compartir y entender

Estos 3 CSV son copias de `tiposMercancia.csv`, `relacionVehiculosCarroceria.csv`
y `reglasIncompatibilidad.csv` (carpeta de arriba), con columnas extra que
resuelven en texto los `Id` de categoría/carrocería/vehículo, para que se
puedan leer sin tener que buscar qué significa cada UUID.

**No se cargan a la base de datos.** `ejecutar_schema_y_seeders.py` nunca
lee esta carpeta — carga directamente los CSV de `seeder/`, que coinciden
exacto con las columnas reales de cada tabla. Si alguien intenta cargar
estos archivos con una herramienta genérica (BULK INSERT, el asistente de
importación de SSMS, etc.), va a fallar, porque tienen columnas de más que
no existen en ninguna tabla.

Se regeneran junto con los de `seeder/`, corriendo
`seeder/_generar_seeders.py` — no se editan a mano.
