using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Resultado de una llamada al servidor.</summary>
public class Resultado
{
    public bool exito;
    public bool sinConexion;   // no se pudo ni contactar el servidor
    public long codigo;        // código HTTP (200, 401, 409, ...)
    public string mensaje;     // mensaje listo para mostrar al jugador
}

[Serializable]
public class EntradaRanking
{
    public string username;
    public double tostadasTotales;
}

/// <summary>
/// Cliente HTTP del backend del juego (FastAPI). Todas las llamadas son asíncronas
/// (corrutinas) y terminan llamando a un callback con el Resultado.
///
/// Rutas (relativas a /api):
///   POST /auth/registro   POST /auth/login   POST /auth/logout
///   GET  /partida         PUT  /partida
///   GET  /ranking
///
/// En el build Web el juego y la API están en el mismo dominio (Nginx hace de proxy),
/// así que se usa /api del mismo origen y el navegador envía la cookie de sesión solo.
/// En el Editor se usa urlBaseEditor.
/// </summary>
public class ServicioAPI : MonoBehaviour
{
    [Tooltip("URL del backend al probar en el Editor. En el build Web se usa /api del mismo dominio.")]
    [SerializeField] private string urlBaseEditor = "http://localhost:8000/api";
    [SerializeField] private int timeoutSegundos = 8;

    // ---------- DTOs (formato JSON que espera el servidor) ----------
    [Serializable] private class Credenciales { public string username; public string password; }
    [Serializable] private class ErrorAPI { public string detail; }
    [Serializable] private class RespuestaRanking { public EntradaRanking[] jugadores; }

    private string UrlBase
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new Uri(new Uri(Application.absoluteURL), "/api").ToString().TrimEnd('/');
#else
            return urlBaseEditor.TrimEnd('/');
#endif
        }
    }

    // ---------- Autenticación ----------

    public void Registrar(string usuario, string clave, Action<Resultado> alTerminar)
    {
        string cuerpo = JsonUtility.ToJson(new Credenciales { username = usuario, password = clave });
        StartCoroutine(Enviar("POST", "/auth/registro", cuerpo, (r, _) => alTerminar(r)));
    }

    public void IniciarSesion(string usuario, string clave, Action<Resultado> alTerminar)
    {
        string cuerpo = JsonUtility.ToJson(new Credenciales { username = usuario, password = clave });
        StartCoroutine(Enviar("POST", "/auth/login", cuerpo, (r, _) => alTerminar(r)));
    }

    public void CerrarSesion(Action<Resultado> alTerminar)
    {
        StartCoroutine(Enviar("POST", "/auth/logout", null, (r, _) => alTerminar?.Invoke(r)));
    }

    // ---------- Partida ----------

    /// <summary>Carga la partida guardada. Si el usuario es nuevo (404), devuelve un estado vacío.</summary>
    public void CargarPartida(Action<Resultado, EstadoJuego> alTerminar)
    {
        StartCoroutine(Enviar("GET", "/partida", null, (r, texto) =>
        {
            EstadoJuego estado = null;
            if (r.exito)
            {
                estado = EstadoJuego.DesdeJson(texto);
            }
            else if (r.codigo == 404)
            {
                estado = new EstadoJuego();
                r.exito = true;
            }
            alTerminar(r, estado);
        }));
    }

    public void GuardarPartida(EstadoJuego estado, Action<Resultado> alTerminar)
    {
        StartCoroutine(Enviar("PUT", "/partida", estado.AJson(), (r, _) => alTerminar?.Invoke(r)));
    }

    // ---------- Ranking ----------

    public void ObtenerRanking(Action<Resultado, EntradaRanking[]> alTerminar)
    {
        StartCoroutine(Enviar("GET", "/ranking", null, (r, texto) =>
        {
            EntradaRanking[] lista = Array.Empty<EntradaRanking>();
            if (r.exito)
            {
                RespuestaRanking respuesta = JsonUtility.FromJson<RespuestaRanking>(texto);
                if (respuesta != null && respuesta.jugadores != null) lista = respuesta.jugadores;
            }
            alTerminar(r, lista);
        }));
    }

    // ---------- Núcleo HTTP ----------

    private IEnumerator Enviar(string metodo, string ruta, string cuerpoJson, Action<Resultado, string> alTerminar)
    {
        using (UnityWebRequest req = new UnityWebRequest(UrlBase + ruta, metodo))
        {
            if (cuerpoJson != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(cuerpoJson));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = timeoutSegundos;

            yield return req.SendWebRequest();

            Resultado r = new Resultado { codigo = req.responseCode };
            string texto = req.downloadHandler != null ? req.downloadHandler.text : "";

            if (req.result == UnityWebRequest.Result.Success)
            {
                r.exito = true;
            }
            else if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                r.sinConexion = true;
                r.mensaje = "No se pudo conectar con el servidor.";
            }
            else
            {
                r.mensaje = ExtraerMensaje(texto, req.responseCode);
            }

            alTerminar(r, texto);
        }
    }

    /// <summary>Convierte la respuesta de error del servidor en un mensaje para el jugador.</summary>
    private static string ExtraerMensaje(string texto, long codigo)
    {
        // FastAPI responde {"detail": "mensaje"} en los errores que lanzamos nosotros.
        try
        {
            ErrorAPI error = JsonUtility.FromJson<ErrorAPI>(texto);
            if (error != null && !string.IsNullOrEmpty(error.detail)) return error.detail;
        }
        catch (ArgumentException)
        {
            // No era JSON (o "detail" era una lista de validación): usamos el mensaje genérico.
        }

        switch (codigo)
        {
            case 401: return "Usuario o contraseña incorrectos.";
            case 409: return "Ese nombre de usuario ya existe.";
            case 422: return "Datos inválidos.";
            case 429: return "Demasiados intentos. Espera un momento.";
            default: return $"Error del servidor ({codigo}).";
        }
    }
}
