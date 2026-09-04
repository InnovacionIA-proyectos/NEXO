"""Composition root de la API - equivalente a Program.cs de NEXO."""

from __future__ import annotations

import os
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
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

# Origenes autorizados a consumir la API directamente (un frontend que hable
# REST con este servicio en vez de pasar por NEXO). Vacio por defecto: sin
# CUBICADOR_CORS_ORIGENES configurada, no se agrega el middleware y el
# comportamiento es el de antes (solo consumidores server-to-server, que no
# necesitan CORS). Mismo criterio que Cors__OrigenesPermitidos en NEXO.
_ORIGENES_CORS = [
    o.strip() for o in os.environ.get("CUBICADOR_CORS_ORIGENES", "").split(",") if o.strip()
]


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
    servers=[
        # {"url": "http://localhost:8081", "description": "Docker Compose local (host)"},
        # {"url": "http://localhost:8000", "description": "Fuera de Docker (uvicorn --reload)"},
    ],
)

if _ORIGENES_CORS:
    app.add_middleware(
        CORSMiddleware,
        allow_origins=_ORIGENES_CORS,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
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
