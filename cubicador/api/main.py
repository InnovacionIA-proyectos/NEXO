"""Composition root de la API - equivalente a Program.cs de NEXO."""

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


app = FastAPI(title="Cubicador", version="0.1.0", lifespan=lifespan)
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
