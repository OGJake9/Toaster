"""Punto de entrada: arranca Uvicorn con la configuración del .env."""
import uvicorn

from . import config

if __name__ == "__main__":
    uvicorn.run(
        "app.main:app",
        host=config.BIND_HOST,
        port=config.BIND_PORT,
        proxy_headers=True,
        forwarded_allow_ips=config.FORWARDED_ALLOW_IPS,
        server_header=False,   # no revelar "uvicorn" en las respuestas
        access_log=True,
    )
