#!/bin/bash
# Instala o actualiza la configuración de Nginx del frontend.
# Uso (en sg-vargas-frontend, con tostadora.conf en el mismo directorio): bash instalar_nginx.sh
# Volver a ejecutarlo después de cada build nuevo del juego (recalcula la huella de la CSP).
set -euo pipefail
cd "$(dirname "$0")"

WEB=/var/www/tostadora

echo "== Quitando archivos de depuración que no deben publicarse =="
sudo rm -rf "$WEB"/*_BurstDebugInformation_DoNotShip
sudo find "$WEB" -name 'lib_burst_generated.txt' -delete

echo "== Calculando la huella (hash) del script de index.html para la CSP =="
HUELLA=$(python3 - <<'PY'
import base64, hashlib, re
html = open('/var/www/tostadora/index.html', encoding='utf-8').read()
scripts = re.findall(r'<script>(.*?)</script>', html, re.S)
print(' '.join("'sha256-" + base64.b64encode(hashlib.sha256(s.encode()).digest()).decode() + "'" for s in scripts))
PY
)
echo "Huella: $HUELLA"

echo "== Instalando la configuración =="
sudo cp tostadora.conf /etc/nginx/sites-available/tostadora
sudo sed -i "s|__HUELLA__|$HUELLA|" /etc/nginx/sites-available/tostadora
sudo chown root:root /etc/nginx/sites-available/tostadora
sudo chmod 644 /etc/nginx/sites-available/tostadora
sudo ln -sf /etc/nginx/sites-available/tostadora /etc/nginx/sites-enabled/tostadora
sudo rm -f /etc/nginx/sites-enabled/default

grep -q "application/wasm" /etc/nginx/mime.types && echo "Tipo .wasm: OK"

echo "== Probando y recargando Nginx =="
sudo nginx -t
sudo systemctl restart nginx
echo "NGINX LISTO"
