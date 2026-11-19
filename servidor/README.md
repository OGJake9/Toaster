# Servidor — Tostadora Incremental (G09)

Código de los nodos del proyecto final de Seguridad Informática.

| Carpeta | Nodo | Qué es |
|---|---|---|
| `backend/` | sg-vargas-backend | API FastAPI en Docker: registro/login (bcrypt + JWT en cookie httpOnly), partida con validación anti-trampa, ranking y Tosti (NPC con IA) con filtros contra prompt injection |

## Seguridad del backend (resumen)
- Contraseñas con bcrypt (costo 12); comparación de tiempo constante aunque el usuario no exista.
- Sesión en cookie `httpOnly` + `Secure` + `SameSite=Strict` (el JavaScript del juego nunca ve el token).
- Límites: 5 logins fallidos por usuario+IP cada 15 min; 5 registros por IP por hora; Tosti 1 mensaje cada 3 s y 20 cada 5 min.
- Anti-trampa: el servidor recalcula la ganancia máxima posible según el tiempo transcurrido y las mejoras; los guardados imposibles se rechazan y se registran en `eventos_sospechosos`.
- Tosti: filtro de entrada, prompt de sistema fijo, filtro de salida y pistas decididas por reglas; Ollama solo es accesible desde el backend.
- MySQL solo por TLS verificando la CA del servidor; usuario con SELECT/INSERT/UPDATE.
- Contenedor: usuario sin privilegios, sistema de archivos de solo lectura, sin capacidades, `no-new-privileges`, límites de memoria y procesos.
- Secretos solo en `.env` en la VM (nunca en el repo). Ver `.env.example`.
- Eventos de seguridad en `/var/log/tostadora/backend.log` con formato `evento=... usuario=... ip=...` para Wazuh.
