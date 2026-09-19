# ============================================================================
# CabinaFletes (Clean Architecture) — API ASP.NET Core (.NET 10)
# Build multi-stage: restore -> publish -> imagen runtime mínima.
# Contexto de build esperado: la RAÍZ de NEXO-Clean (por las referencias
# entre proyectos Domain / Application / Infrastructure / Api).
#   docker build -f src/CabinaFletes.Api/Dockerfile -t cabinafletes-clean-api .
# ============================================================================

# ---- Etapa de build --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia primero los .csproj (las 4 capas) para aprovechar la cache de capas
# de Docker en el restore: si solo cambia código, no se repite el restore.
COPY ["src/CabinaFletes.Domain/CabinaFletes.Domain.csproj", "src/CabinaFletes.Domain/"]
COPY ["src/CabinaFletes.Mensajes/CabinaFletes.Mensajes.csproj", "src/CabinaFletes.Mensajes/"]
COPY ["src/CabinaFletes.Application/CabinaFletes.Application.csproj", "src/CabinaFletes.Application/"]
COPY ["src/CabinaFletes.Infrastructure/CabinaFletes.Infrastructure.csproj", "src/CabinaFletes.Infrastructure/"]
COPY ["src/CabinaFletes.Api/CabinaFletes.Api.csproj", "src/CabinaFletes.Api/"]
RUN dotnet restore "src/CabinaFletes.Api/CabinaFletes.Api.csproj"

# Copia el resto del código y publica en Release.
COPY src/ src/
RUN dotnet publish "src/CabinaFletes.Api/CabinaFletes.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Etapa de runtime -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl: usado por el healthcheck de docker-compose contra /health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# ---------------------------------------------------------------------------
# TLS heredado para el Data Warehouse corporativo (SQL Server 2008 R2).
#
# El DW solo negocia TLS 1.0 con cifradores que las bases Debian endurecidas
# rechazan de fábrica (CipherString=DEFAULT:@SECLEVEL=2, MinProtocol=TLSv1.2).
# Y SQL Server cifra SIEMPRE el paquete de login, así que `Encrypt=False` en
# la cadena de conexión NO evita el handshake: sin esto, toda consulta al DW
# (Biblioteca / SICE-TAC / histórico SAP) falla con
#   "an error occurred during the pre-login handshake
#    (provider: SSL Provider, error: 31 - Encryption(ssl/tls) handshake failed)"
#
# Se usa un archivo PROPIO apuntado con OPENSSL_CONF en vez de parchear
# /etc/ssl/openssl.cnf: es idempotente y no depende del formato ni del
# estado del config de la imagen base. Aplica SOLO dentro de este
# contenedor, no al servidor anfitrión.
# ---------------------------------------------------------------------------
RUN printf '%s\n' \
        'openssl_conf = openssl_init' \
        '' \
        '[openssl_init]' \
        'ssl_conf = ssl_sect' \
        '' \
        '[ssl_sect]' \
        'system_default = system_default_sect' \
        '' \
        '[system_default_sect]' \
        'CipherString = DEFAULT:@SECLEVEL=0' \
        'MinProtocol = TLSv1' \
        'Options = UnsafeLegacyRenegotiation' \
        > /etc/ssl/openssl-legacy-tls.cnf
ENV OPENSSL_CONF=/etc/ssl/openssl-legacy-tls.cnf

# Ejecuta como usuario sin privilegios (incluido en la imagen base).
USER $APP_UID

# La imagen aspnet:10.0 escucha en 8080 por defecto (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CabinaFletes.Api.dll"]
