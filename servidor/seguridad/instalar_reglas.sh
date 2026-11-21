#!/bin/bash
# Instala en el manager de Wazuh (sg-vargas-security) el decodificador y las reglas
# propias de Tostadora Incremental, las valida con wazuh-logtest y reinicia el manager.
# Uso (en sg-vargas-security): bash instalar_reglas.sh
set -euo pipefail

DEC=/var/ossec/etc/decoders/tostadora_decoders.xml
REG=/var/ossec/etc/rules/tostadora_rules.xml

echo "== Decodificador: líneas 'evento=... usuario=... ip=...' de la API =="
sudo tee "$DEC" > /dev/null <<'XML'
<!--
  Tostadora Incremental - decodificador del log de la API (/var/log/tostadora/backend.log)
  Ejemplo: Nov 19 10:00:00 sg-vargas-backend tostadora-backend: WARNING evento=login_fallido usuario=ana ip=1.2.3.4
  El pre-decodificador syslog extrae el programa (tostadora-backend); los hijos comparten
  nombre ("decodificadores hermanos") para acumular campos.
-->
<decoder name="tostadora">
  <program_name>^tostadora-backend$</program_name>
</decoder>

<decoder name="tostadora-campo">
  <parent>tostadora</parent>
  <regex>evento=(\w+)</regex>
  <order>evento</order>
</decoder>

<decoder name="tostadora-campo">
  <parent>tostadora</parent>
  <regex>usuario=(\S+)</regex>
  <order>dstuser</order>
</decoder>

<decoder name="tostadora-campo">
  <parent>tostadora</parent>
  <regex>ip=(\S+)</regex>
  <order>srcip</order>
</decoder>

<decoder name="tostadora-campo">
  <parent>tostadora</parent>
  <regex>capa=(\w+)</regex>
  <order>capa</order>
</decoder>

<decoder name="tostadora-campo">
  <parent>tostadora</parent>
  <regex>ganancia=(\S+)</regex>
  <order>ganancia</order>
</decoder>
XML

echo "== Reglas propias (IDs 100100-100199) =="
sudo tee "$REG" > /dev/null <<'XML'
<!-- Tostadora Incremental - reglas propias. Niveles: 3 informativo, 5-7 aviso, 10+ ataque. -->
<group name="tostadora,">

  <!-- Base: cualquier evento del log de la API -->
  <rule id="100100" level="0">
    <decoded_as>tostadora</decoded_as>
    <description>Tostadora: evento de la API</description>
  </rule>

  <rule id="100101" level="3">
    <if_sid>100100</if_sid>
    <field name="evento">^login_ok$</field>
    <description>Tostadora: inicio de sesión correcto de $(dstuser) desde $(srcip)</description>
    <group>authentication_success,</group>
  </rule>

  <rule id="100102" level="5">
    <if_sid>100100</if_sid>
    <field name="evento">^login_fallido$</field>
    <description>Tostadora: inicio de sesión fallido para $(dstuser) desde $(srcip)</description>
    <group>authentication_failed,</group>
  </rule>

  <!-- 5 fallidos desde la misma IP en 2 minutos = fuerza bruta -->
  <rule id="100103" level="10" frequency="5" timeframe="120">
    <if_matched_sid>100102</if_matched_sid>
    <same_srcip />
    <description>Tostadora: posible fuerza bruta (5 logins fallidos en 2 min) desde $(srcip)</description>
    <mitre><id>T1110</id></mitre>
    <group>authentication_failures,attack,</group>
  </rule>

  <rule id="100104" level="10">
    <if_sid>100100</if_sid>
    <field name="evento">^login_bloqueado$</field>
    <description>Tostadora: login bloqueado por límite de intentos para $(dstuser) desde $(srcip)</description>
    <mitre><id>T1110</id></mitre>
    <group>authentication_failures,attack,</group>
  </rule>

  <rule id="100105" level="3">
    <if_sid>100100</if_sid>
    <field name="evento">^registro$</field>
    <description>Tostadora: cuenta nueva $(dstuser) desde $(srcip)</description>
  </rule>

  <rule id="100106" level="7">
    <if_sid>100100</if_sid>
    <field name="evento">^registro_limitado$</field>
    <description>Tostadora: demasiados registros desde $(srcip) (posible creación masiva de cuentas)</description>
    <group>attack,</group>
  </rule>

  <rule id="100107" level="12">
    <if_sid>100100</if_sid>
    <field name="evento">^trampa$</field>
    <description>Tostadora: TRAMPA bloqueada - $(dstuser) intentó guardar una ganancia imposible ($(ganancia)) desde $(srcip)</description>
    <mitre><id>T1565.001</id></mitre>
    <group>attack,anti_cheat,</group>
  </rule>

  <rule id="100108" level="10">
    <if_sid>100100</if_sid>
    <field name="evento">^prompt_injection$</field>
    <field name="capa">^entrada$</field>
    <description>Tostadora: inyección de prompt bloqueada en la ENTRADA (OWASP LLM01) - $(dstuser) desde $(srcip)</description>
    <group>attack,llm,</group>
  </rule>

  <rule id="100109" level="12">
    <if_sid>100100</if_sid>
    <field name="evento">^prompt_injection$</field>
    <field name="capa">^salida$</field>
    <description>Tostadora: el modelo intentó filtrar sus instrucciones y se bloqueó en la SALIDA (OWASP LLM07) - $(dstuser) desde $(srcip)</description>
    <group>attack,llm,</group>
  </rule>

  <rule id="100110" level="7">
    <if_sid>100100</if_sid>
    <field name="evento">^tosti_sin_ia$</field>
    <description>Tostadora: el backend no pudo contactar al modelo de IA (nodo sg-vargas-ai)</description>
    <group>availability,</group>
  </rule>

  <rule id="100111" level="10">
    <if_sid>100100</if_sid>
    <field name="evento">^error_bd$</field>
    <description>Tostadora: error de conexión con la base de datos</description>
    <group>availability,</group>
  </rule>

  <rule id="100112" level="3">
    <if_sid>100100</if_sid>
    <field name="evento">^inicio_api$</field>
    <description>Tostadora: la API se inició</description>
  </rule>

  <!-- Backups cifrados de MySQL en sg-vargas-db (logger -t backup-toaster).
       Padre: cualquier mensaje del script (nivel 10); hijo: "Backup OK" baja a informativo. -->
  <rule id="100120" level="10">
    <program_name>^backup-toaster$</program_name>
    <description>Tostadora: el backup de la base de datos reportó un problema</description>
    <group>backup,</group>
  </rule>

  <rule id="100121" level="3">
    <if_sid>100120</if_sid>
    <match>Backup OK</match>
    <description>Tostadora: backup cifrado de la base de datos completado</description>
    <group>backup,</group>
  </rule>

</group>
XML

sudo chown wazuh:wazuh "$DEC" "$REG"
sudo chmod 660 "$DEC" "$REG"

echo "== Reiniciando el manager =="
sudo systemctl restart wazuh-manager
sleep 5
systemctl is-active wazuh-manager

# wazuh-logtest usa las reglas cargadas en el manager: se valida DESPUÉS de reiniciar
echo "== Validando con wazuh-logtest =="
for linea in \
  "Nov 21 10:00:00 sg-vargas-backend tostadora-backend: WARNING evento=trampa usuario=jhon_prueba ip=190.25.164.91 ganancia=999995 segundos=0" \
  "Nov 21 10:00:01 sg-vargas-backend tostadora-backend: WARNING evento=prompt_injection usuario=jhon_prueba ip=190.25.164.91 capa=entrada" \
  "Nov 21 10:00:02 sg-vargas-backend tostadora-backend: WARNING evento=login_fallido usuario=ana ip=1.2.3.4"
do
  echo "--- $linea"
  echo "$linea" | sudo /var/ossec/bin/wazuh-logtest 2>&1 | grep -E "name:|evento:|srcip:|id:|level:|description:" || true
done
echo "REGLAS LISTAS"
