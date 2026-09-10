"""S3-HU-10.2 (issue #132): un item sin SKU rompia `guardar_item`.

`GuardarItemRequest.Sku` en NEXO es `string?` — no todos los items tienen SKU,
y es un caso de negocio legitimo. Cuando no lo tiene, NEXO manda la clave con
`null` EXPLICITO, no la omite. `GuardarItemPlanCargueInput.sku` era `str = ""`,
y en Pydantic el default solo aplica si la clave FALTA: un `null` explicito
contra un campo `str` se rechaza.

Consecuencia verificada en vivo (tests/QA/s3-hu-01-02-03-061-cubicacion-por-colas/
README.md bug 2): el item fallaba 3 veces, terminaba en la DLQ y marcaba **todo
el plan** en `error` — no solo ese item.

La distincion "clave ausente" vs "clave presente en null" es el corazon del bug,
asi que los tests cubren las dos por separado: sin la primera no se reproduce
nada, y sin la segunda no se nota la diferencia.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import pytest

from api.dtos.plan_cargue import GuardarItemPlanCargueInput
from application.plan_cargue import items as items_uc
from application.plan_cargue import mapeos
from infrastructure.persistence.db import SessionLocal, crear_tablas
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository


def _payload(**extra) -> dict:
    """La forma exacta que manda NEXO (camelCase, ver `ApiModel`)."""
    base = {
        "descripcion": "Pallet sin codigo", "largoMm": 1200, "anchoMm": 1000,
        "altoMm": 1400, "pesoKg": 800, "cantidad": 2,
    }
    return {**base, **extra}


def test_sku_null_explicito_es_aceptado():
    """El caso real: NEXO manda `"sku": null`. Antes reventaba con ValidationError."""
    dto = GuardarItemPlanCargueInput.model_validate(_payload(sku=None))

    assert dto.sku == ""


def test_sku_ausente_sigue_cayendo_al_default():
    """El comportamiento que ya funcionaba, para no romperlo al arreglar el otro."""
    dto = GuardarItemPlanCargueInput.model_validate(_payload())

    assert dto.sku == ""


def test_sku_con_valor_se_respeta():
    dto = GuardarItemPlanCargueInput.model_validate(_payload(sku="PAL-1200"))

    assert dto.sku == "PAL-1200"


def test_el_item_mapeado_sin_sku_llega_a_la_entidad_con_cadena_vacia():
    """La columna `sku` NO es nullable (`modelos.py`: Mapped[str], default "").
    Aceptar el null en el borde no sirve de nada si despues se propaga hasta la
    base y revienta ahi."""
    dto = GuardarItemPlanCargueInput.model_validate(_payload(sku=None))

    item = mapeos.item_de_input(dto, "11111111-1111-1111-1111-111111111111")

    assert item.sku == ""
    assert item.descripcion == "Pallet sin codigo"


@pytest.mark.parametrize("sku", [None, "AUSENTE", "PAL-1200"])
async def test_guardar_item_sin_sku_persiste_y_no_deja_el_plan_en_error(sku):
    """El desenlace que importa, de punta a punta contra la base: el item se
    guarda y el plan NO queda en `error`."""
    payload = _payload() if sku == "AUSENTE" else _payload(sku=sku)

    await crear_tablas()
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)

        catalogo = await vehiculos.listar_con_siembra()
        plan = await planes.crear("Plan con item sin SKU", catalogo[0].id)

        dto = GuardarItemPlanCargueInput.model_validate(payload)
        await items_uc.guardar_item(planes, plan.id, dto.id, mapeos.item_de_input(dto, plan.id))

        actualizado = await planes.obtener(plan.id)
        assert actualizado is not None
        assert actualizado.estado != "error"
        assert len(actualizado.items) == 1
