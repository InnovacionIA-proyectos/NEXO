# Nexo (Clean Architecture) — API ASP.NET Core (.NET 10). Build multi-stage: restore -> publish -> runtime mínimo.
# Contexto de build: la RAÍZ del repositorio (referencias entre proyectos).
#   docker build -f src/Nexo.Api/Dockerfile -t nexo-clean-api .

# ---- Etapa de build --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia primero los .csproj (las 4 capas) para aprovechar la cache de capas
# de Docker en el restore: si solo cambia código, no se repite el restore.
COPY ["src/Nexo.Domain/Nexo.Domain.csproj", "src/Nexo.Domain/"]
COPY ["src/Nexo.Mensajes/Nexo.Mensajes.csproj", "src/Nexo.Mensajes/"]
COPY ["src/Nexo.Application/Nexo.Application.csproj", "src/Nexo.Application/"]
COPY ["src/Nexo.Infrastructure/Nexo.Infrastructure.csproj", "src/Nexo.Infrastructure/"]
COPY ["src/Nexo.Api/Nexo.Api.csproj", "src/Nexo.Api/"]
RUN dotnet restore "src/Nexo.Api/Nexo.Api.csproj"

# Copia el resto del código y publica en Release.
COPY src/ src/
RUN dotnet publish "src/Nexo.Api/Nexo.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Etapa de runtime -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl: usado por el healthcheck de docker-compose contra /health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# TLS heredado para el Data Warehouse corporativo (SQL Server 2008 R2): solo negocia TLS 1.0, que las
# bases Debian endurecidas rechazan (SECLEVEL=2, MinProtocol=TLSv1.2). SQL Server cifra SIEMPRE el login,
# así que `Encrypt=False` no evita el handshake y sin esto toda consulta al DW falla con
# "pre-login handshake ... Encryption(ssl/tls) handshake failed".
# Se usa un archivo PROPIO apuntado con OPENSSL_CONF (idempotente, no depende del openssl.cnf de la
# base) que aplica solo dentro de este contenedor.
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
ENTRYPOINT ["dotnet", "Nexo.Api.dll"]
