#!/bin/bash
# Instala cloudflared y deja el túnel como servicio. Muestra la URL pública (https://...trycloudflare.com).
# Uso (en sg-vargas-frontend, con cloudflared-tunel.service en el mismo directorio): bash instalar_tunel.sh
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v cloudflared > /dev/null; then
    echo "== Descargando cloudflared =="
    curl -fsSL -o /tmp/cloudflared.deb \
        https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
    sudo dpkg -i /tmp/cloudflared.deb
    rm -f /tmp/cloudflared.deb
fi
cloudflared --version

echo "== Instalando el servicio del túnel =="
sudo cp cloudflared-tunel.service /etc/systemd/system/cloudflared-tunel.service
sudo chown root:root /etc/systemd/system/cloudflared-tunel.service
sudo systemctl daemon-reload
sudo systemctl enable cloudflared-tunel > /dev/null
sudo systemctl restart cloudflared-tunel

echo "== Esperando la URL pública =="
for i in $(seq 1 30); do
    URL=$(sudo journalctl -u cloudflared-tunel --since "-2 min" --no-pager 2>/dev/null \
          | grep -o 'https://[a-z0-9-]*\.trycloudflare\.com' | tail -1 || true)
    if [ -n "$URL" ]; then
        echo ""
        echo "=========================================================="
        echo "  JUEGO PUBLICADO EN:  $URL"
        echo "=========================================================="
        exit 0
    fi
    sleep 2
done
echo "No apareció la URL. Revisa: sudo journalctl -u cloudflared-tunel -n 30"
exit 1
