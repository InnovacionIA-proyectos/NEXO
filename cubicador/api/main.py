"""Composition root de la API - equivalente a Program.cs de NEXO.

Sin middleware CORS a proposito (S3-HU-05, issue #116): ningun navegador
llama a esta API directo desde que S3-HU-02/03/04 movieron toda escritura y
lectura del plan de cargue a traves de NEXO. Esta API sigue existiendo para
consumidores server-to-server (worker propio, scripts de CI/smoke) - esos
nunca necesitaron CORS. Si algun dia vuelve a hacer falta un frontend
hablandole directo a este servicio, es una decision nueva que hay que
tomar a proposito, no reabrir la variable vieja."""

from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from api.dependencias import cerrar_publisher, iniciar_publisher
from api.routers import cubicacion, planes_cargue
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
        "Microservicio de cubicacion: cubicador rapido (SICCE) y motor de "
        "bin-packing 3D (Plan de Cargue). Autenticacion: Bearer estatico "
        "servicio-a-servicio (boton Authorize en Swagger). Contrato e "
        "integracion completos en CONTRATO.md."
    ),
    lifespan=lifespan,
#    servers=[
        # {"url": "http://localhost:8081", "description": "Docker Compose local (host)"},
        # {"url": "http://localhost:8000", "description": "Fuera de Docker (uvicorn --reload)"},
#
)

app.include_router(cubicacion.router)
app.include_router(planes_cargue.router)


@app.exception_handler(AppException)
async def manejar_app_exception(request: Request, exc: AppException) -> JSONResponse:
    status = 500
    for tipo, codigo_http in _STATUS_POR_EXCEPCION.items():
        if isinstance(exc, tipo):
            status = codigo_http
            break
    return JSONResponse(status_code=status, content={"codigo": exc.codigo, "mensaje": exc.mensaje})


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
