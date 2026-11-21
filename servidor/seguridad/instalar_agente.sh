#!/bin/bash
# Instala el agente de Wazuh en un nodo Ubuntu y lo registra en sg-vargas-security.
# Uso (en el nodo): bash instalar_agente.sh <nombre-del-nodo>
set -euo pipefail

NOMBRE="${1:?Uso: bash instalar_agente.sh <nombre-del-nodo>}"
MANAGER="100.101.84.31"   # IP de Tailscale de sg-vargas-security
VERSION="4.14.10-1"       # misma versión que el manager

echo "== Repositorio oficial de Wazuh (firmado con su llave GPG) =="
curl -fsSL https://packages.wazuh.com/key/GPG-KEY-WAZUH \
  | sudo gpg --batch --yes --dearmor -o /usr/share/keyrings/wazuh.gpg
sudo chmod 644 /usr/share/keyrings/wazuh.gpg
echo "deb [signed-by=/usr/share/keyrings/wazuh.gpg] https://packages.wazuh.com/4.x/apt/ stable main" \
  | sudo tee /etc/apt/sources.list.d/wazuh.list > /dev/null

echo "== Instalando wazuh-agent $VERSION como $NOMBRE =="
sudo apt-get update -qq
sudo WAZUH_MANAGER="$MANAGER" WAZUH_AGENT_NAME="$NOMBRE" \
  apt-get install -y -qq "wazuh-agent=$VERSION"
# Que una actualización automática no lo deje en una versión distinta a la del manager
sudo apt-mark hold wazuh-agent > /dev/null

echo "== Iniciando el agente =="
sudo systemctl daemon-reload
sudo systemctl enable --now wazuh-agent
sleep 10
systemctl is-active wazuh-agent
sudo grep -E "Connected to the server|ERROR" /var/ossec/logs/ossec.log | tail -3 || true
echo "AGENTE LISTO: $NOMBRE -> $MANAGER"
