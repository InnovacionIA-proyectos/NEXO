# Documentación de NEXO

Punto de entrada a `docs/`. Para qué es el proyecto y cómo se levanta, ver el [`README.md`](../README.md) de la raíz; para el
despliegue en Dokploy, [`DOKPLOY.md`](../DOKPLOY.md).

## Cómo funciona NEXO

| Documento | Qué contiene |
|---|---|
| [`funcionamiento/FUNCIONAMIENTO-NEXO.md`](funcionamiento/FUNCIONAMIENTO-NEXO.md) | NEXO de punta a punta: roles y permisos, pantallas, captura, priorización, costeo, cotización, vehículos, Biblioteca, cubicación, asistente, métricas y notificaciones |
| [`funcionamiento/`](funcionamiento/) | También el documento de entendimiento de negocio (Word) |
| [`decisiones/DECISIONES-NEXO.md`](decisiones/DECISIONES-NEXO.md) | Las decisiones técnicas ya tomadas, cada una con su problema, decisión, verificación y cuándo revisarla |

## API e integraciones

| Documento | Qué contiene |
|---|---|
| [`CONTRATO.md`](../CONTRATO.md) | Referencia endpoint por endpoint: identidad, roles, parámetros, respuestas y errores. Su §0 es la guía de consumo para el front |
| [`contrato-integracion/`](contrato-integracion/) | Contrato con el portal: identidad sin token, roles, trabajos pesados, avisos en vivo, errores, `openapi.yaml` y escenarios |
| [`integraciones/`](integraciones/) | Integración de Cotifly con NEXO y con el Cubicador |
| [`mensajeria/`](mensajeria/) | Topología de RabbitMQ y contrato de mensajes con el Cubicador |

## Base de datos

| Documento | Qué contiene |
|---|---|
| [`../database/`](../database/) | Esquema (`esquema/ESQUEMA_NEXO.sql`), datos iniciales (`seeder/`) y el script que crea la base |
| [`database/`](database/) | Diccionario de datos y origen de los datos iniciales |

## Despliegue y pruebas

| Documento | Qué contiene |
|---|---|
| [`despliegue/CHECKLIST_DESPLIEGUE.md`](despliegue/CHECKLIST_DESPLIEGUE.md) | Qué configurar antes de abrir un ambiente nuevo (producción) |
| [`despliegue/ROTACION-LLAVE-ANTHROPIC.md`](despliegue/ROTACION-LLAVE-ANTHROPIC.md) | Configuración, verificación y rotación de la llave de Anthropic |
| [`despliegue/CI_CD_RUTAS_COMPARTIDAS.md`](despliegue/CI_CD_RUTAS_COMPARTIDAS.md) | Qué cambios disparan cada pipeline de CI/CD |
| [`../tests/README.md`](../tests/README.md) | Cómo correr las pruebas contra la base real y cómo se manejan los datos de prueba |

## Fuera de `docs/`

| Documento | Qué contiene |
|---|---|
| [`../cubicador/README.md`](../cubicador/README.md) | El Cubicador: qué hace, cómo se levanta y su documentación |
| [`../evaluacion-ia/cubicador/`](../evaluacion-ia/cubicador/) | Evaluación de la IA en el Cubicador: estudio, scripts e informes |
