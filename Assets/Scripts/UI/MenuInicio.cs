using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menú de inicio construido desde código. Pantallas:
///   - Principal: Iniciar sesión, Crear cuenta, Ranking, Jugar sin conexión.
///   - Formulario: login o registro (usuario, contraseña y confirmación).
///   - Ranking: top 10 por tostadas totales.
/// Se muestra mientras ControladorJuego.EnMenu es true.
/// </summary>
public class MenuInicio : MonoBehaviour
{
    [SerializeField] private ControladorJuego controlador;
    [SerializeField] private ServicioAPI api;

    private enum Pantalla { Principal, Login, Registro, Ranking }

    // Mismas reglas que validará el servidor (la validación del cliente es solo comodidad).
    private static readonly Regex PatronUsuario = new Regex("^[A-Za-z0-9_]{3,20}$");
    private const int ClaveMinima = 8;
    private const int ClaveMaxima = 64;
    private const int FilasRanking = 10;

    private GameObject raiz;
    private GameObject panelPrincipal;
    private GameObject panelFormulario;
    private GameObject panelRanking;

    private Text mensajePrincipal;

    private Text tituloFormulario;
    private Text textoBotonEnviar;
    private Text mensajeFormulario;
    private InputField campoUsuario;
    private InputField campoClave;
    private InputField campoConfirmar;
    private GameObject grupoConfirmar;

    private Text[] nombresRanking;
    private Text[] puntajesRanking;
    private Text mensajeRanking;

    private Pantalla pantallaActual;
    private bool ocupado;

    private void Start()
    {
        FabricaUI.AsegurarEventSystem();
        Transform canvas = FabricaUI.CrearCanvas(transform, "CanvasMenu", 20);

        Image fondo = FabricaUI.CrearImagen("FondoMenu", canvas, FabricaUI.ColorFondo);
        FabricaUI.Estirar(fondo.rectTransform, Vector2.zero, Vector2.one);
        raiz = fondo.gameObject;

        CrearPrincipal(fondo.transform);
        CrearFormulario(fondo.transform);
        CrearRanking(fondo.transform);

        Mostrar(Pantalla.Principal);
    }

    private void Update()
    {
        // Mostrar u ocultar el menú según el estado del juego.
        if (raiz.activeSelf != controlador.EnMenu)
        {
            raiz.SetActive(controlador.EnMenu);
            if (controlador.EnMenu) Mostrar(Pantalla.Principal);
        }

        // Enter envía el formulario.
        bool enFormulario = pantallaActual == Pantalla.Login || pantallaActual == Pantalla.Registro;
        if (controlador.EnMenu && enFormulario && Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            Enviar();
        }
    }

    // ===================== Construcción =====================

    private void CrearPrincipal(Transform padre)
    {
        RectTransform panel = FabricaUI.CrearContenedor("Principal", padre);
        panelPrincipal = panel.gameObject;

        Text titulo = FabricaUI.CrearTexto("Titulo", panel, "TOSTADORA INCREMENTAL", 68,
                                           TextAnchor.MiddleCenter, FabricaUI.ColorBoton);
        titulo.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(titulo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1200, 90));

        Text subtitulo = FabricaUI.CrearTexto("Subtitulo", panel, "Lanza tostadas, pégalas en la pared y mejora tu cocina.",
                                              26, TextAnchor.MiddleCenter, FabricaUI.ColorTextoSuave);
        FabricaUI.Colocar(subtitulo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1200, 40));

        CrearBotonMenu(panel, "Iniciar sesión", 90, () => Mostrar(Pantalla.Login));
        CrearBotonMenu(panel, "Crear cuenta", 10, () => Mostrar(Pantalla.Registro));
        CrearBotonMenu(panel, "Ranking", -70, () => Mostrar(Pantalla.Ranking));
        CrearBotonMenu(panel, "Jugar sin conexión", -150, JugarSinConexion);

        mensajePrincipal = FabricaUI.CrearTexto("Mensaje", panel, "", 24, TextAnchor.MiddleCenter, FabricaUI.ColorExito);
        FabricaUI.Colocar(mensajePrincipal.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(1100, 40));
    }

    private static void CrearBotonMenu(Transform padre, string texto, float y, System.Action accion)
    {
        Button b = FabricaUI.CrearBoton("Boton_" + texto, padre, texto, accion);
        FabricaUI.Colocar((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 64));
    }

    private void CrearFormulario(Transform padre)
    {
        Image caja = FabricaUI.CrearImagen("Formulario", padre, FabricaUI.ColorPanel);
        FabricaUI.Colocar(caja.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 560));
        panelFormulario = caja.gameObject;

        tituloFormulario = FabricaUI.CrearTexto("Titulo", caja.transform, "", 40, TextAnchor.MiddleCenter, FabricaUI.ColorTexto);
        tituloFormulario.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(tituloFormulario.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(520, 56));

        campoUsuario = CrearCampoConEtiqueta(caja.transform, "Usuario", "3 a 20 letras, números o _", false, 20, -96).campo;
        campoClave = CrearCampoConEtiqueta(caja.transform, "Contraseña", "Mínimo 8 caracteres", true, ClaveMaxima, -196).campo;

        (GameObject grupo, InputField campo) confirmar =
            CrearCampoConEtiqueta(caja.transform, "Confirmar contraseña", "Repite la contraseña", true, ClaveMaxima, -296);
        grupoConfirmar = confirmar.grupo;
        campoConfirmar = confirmar.campo;

        mensajeFormulario = FabricaUI.CrearTexto("Mensaje", caja.transform, "", 22, TextAnchor.MiddleCenter, FabricaUI.ColorError);
        mensajeFormulario.horizontalOverflow = HorizontalWrapMode.Wrap;
        FabricaUI.Colocar(mensajeFormulario.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 96), new Vector2(520, 56));

        Button enviar = FabricaUI.CrearBoton("Enviar", caja.transform, "", Enviar);
        FabricaUI.Colocar((RectTransform)enviar.transform, new Vector2(0.5f, 0), new Vector2(-125, 24), new Vector2(230, 60));
        textoBotonEnviar = enviar.GetComponentInChildren<Text>();

        Button volver = FabricaUI.CrearBoton("Volver", caja.transform, "Volver", () => Mostrar(Pantalla.Principal));
        FabricaUI.Colocar((RectTransform)volver.transform, new Vector2(0.5f, 0), new Vector2(125, 24), new Vector2(230, 60));
    }

    private static (GameObject grupo, InputField campo) CrearCampoConEtiqueta(
        Transform padre, string etiqueta, string placeholder, bool esClave, int limite, float y)
    {
        RectTransform grupo = FabricaUI.CrearContenedor("Grupo_" + etiqueta, padre);

        Text t = FabricaUI.CrearTexto("Etiqueta", grupo, etiqueta, 22, TextAnchor.MiddleLeft, FabricaUI.ColorTextoSuave);
        FabricaUI.Colocar(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(480, 30));

        InputField campo = FabricaUI.CrearCampo("Campo", grupo, placeholder, esClave, limite);
        FabricaUI.Colocar((RectTransform)campo.transform, new Vector2(0.5f, 1), new Vector2(0, y - 34), new Vector2(480, 56));

        return (grupo.gameObject, campo);
    }

    private void CrearRanking(Transform padre)
    {
        Image caja = FabricaUI.CrearImagen("Ranking", padre, FabricaUI.ColorPanel);
        FabricaUI.Colocar(caja.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 640));
        panelRanking = caja.gameObject;

        Text titulo = FabricaUI.CrearTexto("Titulo", caja.transform, "RANKING", 40, TextAnchor.MiddleCenter, FabricaUI.ColorTexto);
        titulo.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(titulo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(520, 56));

        nombresRanking = new Text[FilasRanking];
        puntajesRanking = new Text[FilasRanking];
        for (int i = 0; i < FilasRanking; i++)
        {
            float y = -96 - i * 42;
            nombresRanking[i] = FabricaUI.CrearTexto("Nombre" + i, caja.transform, "", 24, TextAnchor.MiddleLeft, FabricaUI.ColorTexto);
            FabricaUI.Colocar(nombresRanking[i].rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(480, 38));

            puntajesRanking[i] = FabricaUI.CrearTexto("Puntaje" + i, caja.transform, "", 24, TextAnchor.MiddleRight, FabricaUI.ColorBoton);
            FabricaUI.Colocar(puntajesRanking[i].rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(480, 38));
        }

        mensajeRanking = FabricaUI.CrearTexto("Mensaje", caja.transform, "", 22, TextAnchor.MiddleCenter, FabricaUI.ColorTextoSuave);
        FabricaUI.Colocar(mensajeRanking.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(520, 40));

        Button volver = FabricaUI.CrearBoton("Volver", caja.transform, "Volver", () => Mostrar(Pantalla.Principal));
        FabricaUI.Colocar((RectTransform)volver.transform, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(230, 60));
    }

    // ===================== Navegación =====================

    private void Mostrar(Pantalla pantalla)
    {
        if (ocupado) return;
        pantallaActual = pantalla;

        panelPrincipal.SetActive(pantalla == Pantalla.Principal);
        panelFormulario.SetActive(pantalla == Pantalla.Login || pantalla == Pantalla.Registro);
        panelRanking.SetActive(pantalla == Pantalla.Ranking);

        switch (pantalla)
        {
            case Pantalla.Login:
                PrepararFormulario("INICIAR SESIÓN", "Entrar", false);
                break;
            case Pantalla.Registro:
                PrepararFormulario("CREAR CUENTA", "Crear cuenta", true);
                break;
            case Pantalla.Ranking:
                CargarRanking();
                break;
        }
    }

    private void PrepararFormulario(string titulo, string textoBoton, bool conConfirmacion)
    {
        tituloFormulario.text = titulo;
        textoBotonEnviar.text = textoBoton;
        grupoConfirmar.SetActive(conConfirmacion);
        campoClave.text = "";
        campoConfirmar.text = "";
        MostrarMensaje("", false);
        campoUsuario.Select();
    }

    private void MostrarMensaje(string texto, bool esExito)
    {
        mensajeFormulario.text = texto;
        mensajeFormulario.color = esExito ? FabricaUI.ColorExito : FabricaUI.ColorError;
    }

    // ===================== Acciones =====================

    private void JugarSinConexion()
    {
        Sesion.IniciarInvitado();
        mensajePrincipal.text = "";
        controlador.IniciarPartida(new EstadoJuego());
    }

    private void Enviar()
    {
        if (ocupado) return;

        string usuario = campoUsuario.text.Trim();
        string clave = campoClave.text;
        bool esRegistro = pantallaActual == Pantalla.Registro;

        if (!PatronUsuario.IsMatch(usuario))
        {
            MostrarMensaje("El usuario debe tener de 3 a 20 letras, números o _.", false);
            return;
        }
        if (clave.Length < ClaveMinima)
        {
            MostrarMensaje($"La contraseña debe tener al menos {ClaveMinima} caracteres.", false);
            return;
        }
        if (esRegistro && clave != campoConfirmar.text)
        {
            MostrarMensaje("Las contraseñas no coinciden.", false);
            return;
        }

        ocupado = true;
        MostrarMensaje("Conectando...", true);

        if (esRegistro)
        {
            api.Registrar(usuario, clave, r =>
            {
                if (!r.exito) { Fallo(r); return; }
                IniciarSesion(usuario, clave);   // cuenta creada: entra directamente
            });
        }
        else
        {
            IniciarSesion(usuario, clave);
        }
    }

    private void IniciarSesion(string usuario, string clave)
    {
        api.IniciarSesion(usuario, clave, r =>
        {
            if (!r.exito) { Fallo(r); return; }

            Sesion.IniciarEnLinea(usuario);
            api.CargarPartida((r2, estado) =>
            {
                if (!r2.exito) { Fallo(r2); return; }

                ocupado = false;
                campoClave.text = "";
                campoConfirmar.text = "";
                mensajePrincipal.text = "";
                controlador.IniciarPartida(estado);
            });
        });
    }

    private void Fallo(Resultado r)
    {
        ocupado = false;
        campoClave.text = "";
        campoConfirmar.text = "";
        string extra = r.sinConexion ? " Puedes jugar sin conexión desde el menú." : "";
        MostrarMensaje(r.mensaje + extra, false);
    }

    private void CargarRanking()
    {
        for (int i = 0; i < FilasRanking; i++)
        {
            nombresRanking[i].text = "";
            puntajesRanking[i].text = "";
        }
        mensajeRanking.text = "Cargando...";

        api.ObtenerRanking((r, lista) =>
        {
            if (!r.exito)
            {
                mensajeRanking.text = r.mensaje;
                return;
            }

            mensajeRanking.text = lista.Length == 0 ? "Todavía no hay puntuaciones." : "";
            for (int i = 0; i < FilasRanking && i < lista.Length; i++)
            {
                nombresRanking[i].text = $"{i + 1}.  {lista[i].username}";
                puntajesRanking[i].text = FabricaUI.Formatear(lista[i].tostadasTotales);
            }
        });
    }

    /// <summary>El controlador llama esto al volver al menú desde la partida.</summary>
    public void AvisarSalida(string texto)
    {
        mensajePrincipal.text = texto;
    }
}
