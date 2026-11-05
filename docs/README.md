# Documentación de NEXO

Punto de entrada a `docs/`. Para qué es el proyecto y cómo se levanta, ver el [`README.md`](../README.md) de la raíz; para el despliegue en Dokploy, [`DOKPLOY.md`](../DOKPLOY.md).

## Contrato de la API

| Documento | Qué contiene |
|---|---|
| [`CONTRATO.md`](../CONTRATO.md) | Referencia endpoint por endpoint: header de identidad, roles, parámetros, respuestas y errores. Su §0 es la guía de consumo para el frontend |
| [`contrato-integracion/`](contrato-integracion/) | Por qué la identidad del portal viaja sin token (ADR-001), trabajos pesados, avisos en vivo, `openapi.yaml` y escenarios |
| [`integraciones/`](integraciones/) | Integración de Cotifly con NEXO y con el Cubicador |

## Negocio y reglas

| Documento | Qué contiene |
|---|---|
| [`documentacion/ROLES_Y_PERMISOS.md`](documentacion/ROLES_Y_PERMISOS.md) | Los seis roles y qué puede hacer cada uno |
| [`documentacion/FLUJO-DECISION-TARIFA.md`](documentacion/FLUJO-DECISION-TARIFA.md) y [`costeo/`](costeo/) | Cómo se decide el flete: Solo SICE-TAC, plaza vigente o proceso completo, con sus escenarios |
| [`documentacion/REGLAS-CAPTURA-POR-LINEA-DE-NEGOCIO.md`](documentacion/REGLAS-CAPTURA-POR-LINEA-DE-NEGOCIO.md) | Qué campos exige la captura según la línea de negocio y la clase de viaje |
| [`documentacion/MAPA-FUNCIONAL-NEXO.md`](documentacion/MAPA-FUNCIONAL-NEXO.md) | Mapa funcional por módulo y rol |
| [`tarifas-plaza-provisionales/`](tarifas-plaza-provisionales/) | Dataset de tarifas de plaza provisionales validado por negocio |

## Datos y base de datos

| Documento | Qué contiene |
|---|---|
| [`Database/`](Database/) | Esquema (`esquema/ESQUEMA_NEXO.sql`), datos iniciales (`seeder/`) y el script que crea la base |
| [`documentacion/database/`](documentacion/database/) | Diccionario de datos y origen de los datos iniciales |

## Mensajería e IA

| Documento | Qué contiene |
|---|---|
| [`mensajeria/`](mensajeria/) | Topología RabbitMQ/MassTransit y el puente con el Cubicador |
| [`ia/`](ia/) | Alcance de la IA en el Cubicador y estudio comparativo |
| [`decisiones/ia-claude-seguridad-y-datos.md`](decisiones/ia-claude-seguridad-y-datos.md) | Llave, datos que salen y modelo para usar Claude |
| [`despliegue/ROTACION-LLAVE-ANTHROPIC.md`](despliegue/ROTACION-LLAVE-ANTHROPIC.md) | Configuración, verificación y rotación de la llave de Anthropic |

## Despliegue y pruebas

| Documento | Qué contiene |
|---|---|
| [`despliegue/`](despliegue/) | Checklist de despliegue y convención de rutas de CI/CD |
| [`pruebas/`](pruebas/) | Pruebas locales contra la base real, datos de prueba y ejemplos de costeo |

## Decisiones de diseño

[`decisiones/`](decisiones/) reúne las decisiones técnicas ya tomadas (paginación, score de prioridad, matching SICE-TAC, plaza SAP, mapeo de vehículos, plan de cargue). Cada archivo explica el problema, la decisión y su motivo.

## Histórico

[`archivo/`](archivo/) y [`304-QUE-DEBEN-HACER-FRONT-Y-BACK.md`](304-QUE-DEBEN-HACER-FRONT-Y-BACK.md) se conservan como referencia de trabajos ya cerrados.
