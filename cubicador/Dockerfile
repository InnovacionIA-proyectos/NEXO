FROM python:3.14-slim

WORKDIR /app

# Driver ODBC de Microsoft (S5-HU-17.1). Lo exigen `aioodbc` y `pyodbc`: sin el,
# el dialecto mssql+aioodbc falla al conectar con "Can't open lib ... file not
# found", que no dice nada util. `unixodbc-dev` ademas hace falta para COMPILAR
# pyodbc, por eso no se purga despues.
#
# Se instala con `packages-microsoft-prod.deb` y NO agregando el repo a mano con
# la clave microsoft.asc, que es lo que dice casi toda la documentacion: la
# imagen base es Debian 13 (trixie), que verifica firmas con `sqv` y rechaza ese
# repo con "Missing key EE4D7792F748182B" -> "E: Unable to locate package
# msodbcsql18". microsoft.asc no trae la clave con la que firman trixie; el .deb
# instala el llavero correcto junto con la configuracion del repo.
#
# La version de Debian se detecta en vez de fijarla, para que el dia que cambie
# la imagen base de python esto no se rompa en silencio.
#
# `libgssapi-krb5-2` va EXPLICITO y el purge de curl va SIN `--auto-remove`:
# con --auto-remove, apt se lleva Kerberos de arrastre y el driver queda a
# medias. El sintoma engania, porque unixODBC culpa al archivo equivocado:
# "Can't open lib '/opt/microsoft/msodbcsql18/lib64/libmsodbcsql-18.7.so.1.1':
# file not found" cuando ese .so SI esta - lo que falta es libgssapi_krb5.so.2,
# una dependencia suya. Se ve con `ldd` sobre el .so.
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends curl ca-certificates; \
    . /etc/os-release; \
    curl -fsSL -o /tmp/ms-prod.deb \
        "https://packages.microsoft.com/config/debian/$VERSION_ID/packages-microsoft-prod.deb"; \
    dpkg -i /tmp/ms-prod.deb; \
    rm /tmp/ms-prod.deb; \
    apt-get update; \
    ACCEPT_EULA=Y apt-get install -y --no-install-recommends \
        msodbcsql18 unixodbc-dev libgssapi-krb5-2; \
    apt-get purge -y curl; \
    rm -rf /var/lib/apt/lists/*; \
    if ldd /opt/microsoft/msodbcsql18/lib64/libmsodbcsql-*.so.* | grep "not found"; then \
        echo "FALTAN dependencias del driver ODBC (ver arriba)"; exit 1; \
    fi; \
    echo "driver ODBC completo"

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 8000

HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=5 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/health')" || exit 1

CMD ["uvicorn", "api.main:app", "--host", "0.0.0.0", "--port", "8000"]
