using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panel de Tosti (NPC con IA) abajo a la izquierda: burbuja de texto y campo para escribirle.
///
/// - El jugador puede escribirle (Enter o botón Enviar).
/// - El ControladorJuego le avisa eventos de la partida (inicio, primer impacto, compra, meta...)
///   y Tosti comenta solo, como máximo cada `esperaEntreEventos` segundos.
/// - Antes de preguntarle, se guarda la partida: así el servidor elige la pista con el estado actual.
/// - Sin conexión (modo invitado) Tosti no está disponible: la IA vive en el servidor.
/// </summary>
public class PanelTosti : MonoBehaviour
{
    [SerializeField] private ControladorJuego controlador;
    [SerializeField] private ServicioAPI api;
    [Tooltip("Segundos mínimos entre comentarios automáticos de Tosti")]
    [SerializeField] private float esperaEntreEventos = 15f;
    [Tooltip("Imagen de Tosti para el avatar (opcional)")]
    [SerializeField] private Sprite spriteTosti;

    private const float EsperaServidor = 3.5f;   // el servidor exige 3 s entre mensajes

    private GameObject raiz;
    private Text textoBurbuja;
    private GameObject filaEntrada;
    private InputField campo;
    private Button botonEnviar;

    private bool enCurso;
    private float ultimoPedido = -999f;
    private string eventoPendiente;

    /// <summary>true mientras el jugador escribe (para que Espacio y T no afecten el juego).</summary>
    public bool Escribiendo => campo != null && campo.isFocused;

    private void Start()
    {
        FabricaUI.AsegurarEventSystem();
        Transform canvas = FabricaUI.CrearCanvas(transform, "CanvasTosti", 12);
        Construir(canvas);
        raiz.SetActive(false);
    }

    private void Update()
    {
        bool visible = !controlador.EnMenu && !controlador.TiendaAbierta && !controlador.MostrandoMeta;
        if (raiz.activeSelf != visible) raiz.SetActive(visible);
        if (!visible) return;

        if (filaEntrada.activeSelf != Sesion.EnLinea) filaEntrada.SetActive(Sesion.EnLinea);
        botonEnviar.interactable = !enCurso;
    }

    // ===================== Construcción =====================

    private void Construir(Transform canvas)
    {
        Image panel = FabricaUI.CrearImagen("PanelTosti", canvas, FabricaUI.ColorPanel);
        FabricaUI.Colocar(panel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(400, 176));
        raiz = panel.gameObject;

        // "Avatar" provisional hasta tener el sprite de Tosti
        Image avatar = FabricaUI.CrearImagen("Avatar", panel.transform, FabricaUI.ColorBoton);
        FabricaUI.Colocar(avatar.rectTransform, new Vector2(0, 1), new Vector2(12, -12), new Vector2(72, 72));
        Text nombre = FabricaUI.CrearTexto("Nombre", avatar.transform, "TOSTI", 18, TextAnchor.MiddleCenter,
                                           FabricaUI.ColorTextoBoton);
        nombre.fontStyle = FontStyle.Bold;
        FabricaUI.Estirar(nombre.rectTransform, Vector2.zero, Vector2.one);
        if (spriteTosti != null)
        {
            avatar.sprite = spriteTosti;
            avatar.color = Color.white;
            avatar.preserveAspect = true;
            nombre.gameObject.SetActive(false);
        }

        textoBurbuja = FabricaUI.CrearTexto("Burbuja", panel.transform, "", 18, TextAnchor.UpperLeft, FabricaUI.ColorTexto);
        textoBurbuja.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoBurbuja.verticalOverflow = VerticalWrapMode.Truncate;
        FabricaUI.Colocar(textoBurbuja.rectTransform, new Vector2(0, 1), new Vector2(96, -10), new Vector2(292, 104));

        RectTransform fila = FabricaUI.CrearContenedor("FilaEntrada", panel.transform);
        FabricaUI.Estirar(fila, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 54));
        filaEntrada = fila.gameObject;

        campo = FabricaUI.CrearCampo("Campo", fila, "Escríbele a Tosti...", false, 200);
        FabricaUI.Estirar((RectTransform)campo.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-104, 0));
        campo.textComponent.fontSize = 20;
        ((Text)campo.placeholder).fontSize = 20;
        campo.onEndEdit.AddListener(_ => { if (EnterPresionado()) Enviar(); });

        botonEnviar = FabricaUI.CrearBoton("Enviar", fila, "Enviar", Enviar);
        FabricaUI.Estirar((RectTransform)botonEnviar.transform, new Vector2(1, 0), new Vector2(1, 1),
                          new Vector2(-96, 0), Vector2.zero);
    }

    private static bool EnterPresionado()
    {
        return Keyboard.current != null &&
               (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
    }

    // ===================== API pública =====================

    public void Mostrar(string texto)
    {
        if (textoBurbuja != null) textoBurbuja.text = texto;
    }

    /// <summary>Al empezar una partida.</summary>
    public void Reiniciar()
    {
        StopAllCoroutines();
        enCurso = false;
        eventoPendiente = null;
        ultimoPedido = -999f;
        if (campo != null) campo.text = "";
        Mostrar(Sesion.EnLinea
            ? "¡Hola! Soy Tosti. Pregúntame lo que quieras sobre la cocina."
            : "Estás jugando sin conexión. Inicia sesión para charlar conmigo.");
    }

    /// <summary>
    /// Evento de la partida. Los normales respetan la espera entre comentarios; los prioritarios
    /// (inicio, meta) se envían igual y, si hay una petición en curso, quedan en cola.
    /// </summary>
    public void Evento(string evento, bool prioritario = false)
    {
        if (!Sesion.EnLinea) return;
        if (enCurso)
        {
            if (prioritario) eventoPendiente = evento;
            return;
        }
        if (!prioritario && Time.unscaledTime - ultimoPedido < esperaEntreEventos) return;
        Pedir(null, evento);
    }

    // ===================== Interno =====================

    private void Enviar()
    {
        if (enCurso || !Sesion.EnLinea) return;
        string mensaje = campo.text.Trim();
        if (mensaje.Length == 0) return;
        campo.text = "";
        Pedir(mensaje, null);
    }

    private void Pedir(string mensaje, string evento)
    {
        enCurso = true;
        ultimoPedido = Time.unscaledTime;
        Mostrar("Tosti está pensando...");

        // Guardar primero: la pista del servidor sale del estado guardado en MySQL.
        controlador.GuardarAhora(_ => api.HablarConTosti(mensaje, evento, (r, respuesta) =>
        {
            enCurso = false;
            Mostrar(r.exito ? respuesta : r.mensaje);

            if (eventoPendiente != null)
            {
                string siguiente = eventoPendiente;
                eventoPendiente = null;
                StartCoroutine(EnviarDespues(siguiente));
            }
        }));
    }

    private IEnumerator EnviarDespues(string evento)
    {
        yield return new WaitForSecondsRealtime(EsperaServidor);
        Evento(evento, true);
    }
}
