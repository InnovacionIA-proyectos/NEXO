"""Composition root de la API - equivalente a Program.cs de NEXO.

Sin middleware CORS a proposito: ningun navegador llama a esta API
directo desde que se movio toda escritura y lectura del plan de cargue a
traves de NEXO. Esta API sigue existiendo para consumidores server-to-server
(worker propio, scripts de CI/smoke) - esos nunca necesitaron CORS. Si algun
dia vuelve a hacer falta un frontend hablandole directo a este servicio, es
una decision nueva que hay que tomar a proposito, no reabrir la variable vieja."""

from __future__ import annotations

from contextlib import asynccontextmanager

import logging

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from sqlalchemy.exc import SQLAlchemyError

from api.dependencias import cerrar_publisher, iniciar_publisher
from api.routers import catalogo, planes_cargue, recomendaciones
from application.excepciones import AppException, ConflictException, NotFoundException, ServiceUnavailableException, ValidationFailedException
from infrastructure.persistence.db import crear_tablas

_STATUS_POR_EXCEPCION = {
    NotFoundException: 404,
    ValidationFailedException: 400,
    ConflictException: 409,
    ServiceUnavailableException: 503,
}


@asynccontextmanager
async def lifespan(app: FastAPI):
    await crear_tablas()
    await iniciar_publisher()
    try:
        yield
    finally:
        await cerrar_publisher()


app = FastAPI(
    title="Cubicador",
    version="0.1.0",
    description=(
        "Microservicio de cubicacion: seleccion de flota (POST /recomendaciones) "
        "y motor de bin-packing 3D (Plan de Cargue). Autenticacion: Bearer "
        "estatico servicio-a-servicio, una llave POR SISTEMA para el contrato "
        "de recomendaciones (boton Authorize en Swagger). Contrato e "
        "integracion completos en CONTRATO.md."
    ),
    lifespan=lifespan,
)

app.include_router(recomendaciones.router)
app.include_router(catalogo.router)
app.include_router(planes_cargue.router)


@app.exception_handler(AppException)
async def manejar_app_exception(request: Request, exc: AppException) -> JSONResponse:
    status = 500
    for tipo, codigo_http in _STATUS_POR_EXCEPCION.items():
        if isinstance(exc, tipo):
            status = codigo_http
            break
    return JSONResponse(status_code=status, content={"codigo": exc.codigo, "mensaje": exc.mensaje})


@app.exception_handler(SQLAlchemyError)
async def manejar_fallo_de_base(request: Request, exc: SQLAlchemyError) -> JSONResponse:
    """Un fallo de base es 503 con el formato de error del contrato, no un 500
    pelado.

    Sin esto, una base caida o una tabla que falta se le aparece al consumidor
    como el 500 por defecto de FastAPI, con un cuerpo que no tiene `codigo` ni
    `mensaje`: NEXO lo traduce a su `clsApiError` y Cotifly a su
    `clsCodigosError`, y los dos esperan esa forma. Tampoco distinguirian
    "el servicio esta mal" de "mi peticion esta mal".

    La causa real va al log, no a la respuesta: el detalle de un error de
    SQLAlchemy trae el SQL y los nombres de las tablas.
    """
    logging.getLogger("cubicador").exception("Fallo de base de datos en %s", request.url.path)
    return JSONResponse(status_code=503, content={
        "codigo": "CUBICADOR_NO_DISPONIBLE",
        "mensaje": "La base de datos del Cubicador no esta disponible. Reintentar mas tarde.",
    })


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
