using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Construye TODA la interfaz desde código (no hay que armar nada en el editor):
///   - HUD: contador de tostadas, producción por segundo, barra de carga y botón Tienda.
///   - Pantalla de tienda: una fila por mejora con nivel, efecto y botón de compra.
/// Cada frame lee el EstadoJuego y refresca los textos.
/// </summary>
public class InterfazJuego : MonoBehaviour
{
    [SerializeField] private ControladorJuego controlador;
    [SerializeField] private PalancaTostadora palanca;

    // Paleta
    private static readonly Color ColorPanel = new Color(0.22f, 0.14f, 0.07f, 0.97f);
    private static readonly Color ColorFila = new Color(0.33f, 0.22f, 0.12f, 1f);
    private static readonly Color ColorBoton = new Color(0.95f, 0.70f, 0.25f, 1f);
    private static readonly Color ColorTexto = new Color(1f, 0.96f, 0.88f, 1f);
    private static readonly Color ColorTextoSuave = new Color(0.92f, 0.82f, 0.65f, 1f);
    private static readonly Color ColorTextoBoton = new Color(0.20f, 0.12f, 0.05f, 1f);

    private class FilaMejora
    {
        public TipoMejora tipo;
        public Text titulo;
        public Text efecto;
        public Button boton;
        public Text textoBoton;
    }

    private Font fuente;
    private Text textoTostadas;
    private Text textoProduccion;
    private RectTransform rellenoCarga;
    private GameObject panelTienda;
    private Text textoTostadasTienda;
    private FilaMejora[] filas;

    private void Start()
    {
        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        AsegurarEventSystem();

        Transform canvas = CrearCanvas();
        CrearHUD(canvas);
        CrearTienda(canvas);
        panelTienda.SetActive(false);
    }

    private void Update()
    {
        EstadoJuego e = controlador.Estado;

        textoTostadas.text = $"Tostadas: {Formatear(e.tostadas)}";
        double produccion = e.ProduccionPorSegundo();
        textoProduccion.text = produccion > 0 ? $"+{produccion:0.#} por segundo" : "";

        rellenoCarga.anchorMax = new Vector2(palanca.CargaNormalizada, 1f);

        if (panelTienda.activeSelf != controlador.TiendaAbierta)
            panelTienda.SetActive(controlador.TiendaAbierta);

        if (controlador.TiendaAbierta) ActualizarTienda(e);
    }

    // ===================== HUD =====================

    private void CrearHUD(Transform canvas)
    {
        textoTostadas = CrearTexto("Tostadas", canvas, "", 40, TextAnchor.UpperLeft, ColorTexto);
        Colocar(textoTostadas.rectTransform, new Vector2(0, 1), new Vector2(24, -20), new Vector2(600, 56));
        textoTostadas.fontStyle = FontStyle.Bold;

        textoProduccion = CrearTexto("Produccion", canvas, "", 24, TextAnchor.UpperLeft, ColorTextoSuave);
        Colocar(textoProduccion.rectTransform, new Vector2(0, 1), new Vector2(24, -76), new Vector2(600, 36));

        Button botonTienda = CrearBoton("BotonTienda", canvas, "Tienda (T)", () => controlador.AbrirTienda(true));
        Colocar((RectTransform)botonTienda.transform, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(220, 64));

        // Barra de carga del resorte
        Text etiqueta = CrearTexto("EtiquetaCarga", canvas, "Mantén ESPACIO para cargar", 22, TextAnchor.MiddleCenter, ColorTexto);
        Colocar(etiqueta.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(500, 30));

        Image fondo = CrearImagen("BarraCarga", canvas, new Color(0, 0, 0, 0.6f));
        Colocar(fondo.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(420, 26));

        Image relleno = CrearImagen("Relleno", fondo.transform, ColorBoton);
        rellenoCarga = relleno.rectTransform;
        Estirar(rellenoCarga, Vector2.zero, new Vector2(0, 1));
    }

    // ===================== TIENDA =====================

    private void CrearTienda(Transform canvas)
    {
        // Fondo oscuro que cubre la pantalla y bloquea clics al juego.
        Image overlay = CrearImagen("Tienda", canvas, new Color(0, 0, 0, 0.7f));
        Estirar(overlay.rectTransform, Vector2.zero, Vector2.one);
        panelTienda = overlay.gameObject;

        Image caja = CrearImagen("Caja", overlay.transform, ColorPanel);
        Colocar(caja.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 600));

        Text titulo = CrearTexto("Titulo", caja.transform, "TIENDA", 46, TextAnchor.MiddleCenter, ColorTexto);
        titulo.fontStyle = FontStyle.Bold;
        Colocar(titulo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(700, 60));

        textoTostadasTienda = CrearTexto("Saldo", caja.transform, "", 26, TextAnchor.MiddleCenter, ColorTextoSuave);
        Colocar(textoTostadasTienda.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(700, 36));

        int cantidad = EstadoJuego.Mejoras.Length;
        filas = new FilaMejora[cantidad];
        for (int i = 0; i < cantidad; i++)
            filas[i] = CrearFila(caja.transform, (TipoMejora)i, -128 - i * 100);

        Button volver = CrearBoton("Volver", caja.transform, "Volver (T)", () => controlador.AbrirTienda(false));
        Colocar((RectTransform)volver.transform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(240, 60));
    }

    private FilaMejora CrearFila(Transform padre, TipoMejora tipo, float y)
    {
        Image fondo = CrearImagen("Fila_" + tipo, padre, ColorFila);
        Colocar(fondo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(720, 88));

        FilaMejora fila = new FilaMejora { tipo = tipo };

        fila.titulo = CrearTexto("Nombre", fondo.transform, "", 28, TextAnchor.UpperLeft, ColorTexto);
        fila.titulo.fontStyle = FontStyle.Bold;
        Colocar(fila.titulo.rectTransform, new Vector2(0, 1), new Vector2(16, -10), new Vector2(450, 36));

        fila.efecto = CrearTexto("Efecto", fondo.transform, "", 21, TextAnchor.UpperLeft, ColorTextoSuave);
        Colocar(fila.efecto.rectTransform, new Vector2(0, 1), new Vector2(16, -50), new Vector2(450, 30));

        fila.boton = CrearBoton("Comprar", fondo.transform, "", () => controlador.ComprarMejora(tipo));
        Colocar((RectTransform)fila.boton.transform, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(220, 60));
        fila.textoBoton = fila.boton.GetComponentInChildren<Text>();

        return fila;
    }

    private void ActualizarTienda(EstadoJuego e)
    {
        textoTostadasTienda.text = $"Tienes {Formatear(e.tostadas)} tostadas";

        foreach (FilaMejora fila in filas)
        {
            InfoMejora info = EstadoJuego.Mejoras[(int)fila.tipo];
            fila.titulo.text = $"{info.nombre}   Nv. {e.Nivel(fila.tipo)}";
            fila.efecto.text = e.DescribirEfecto(fila.tipo);

            if (e.EstaAlMaximo(fila.tipo))
            {
                fila.textoBoton.text = "MÁXIMO";
                fila.boton.interactable = false;
            }
            else
            {
                fila.textoBoton.text = $"Comprar: {Formatear(e.Costo(fila.tipo))}";
                fila.boton.interactable = e.PuedeComprar(fila.tipo);
            }
        }
    }

    // ===================== Utilidades =====================

    private static string Formatear(double valor)
    {
        valor = Math.Floor(valor);
        if (valor < 1_000) return valor.ToString("0");
        if (valor < 1_000_000) return (valor / 1_000).ToString("0.##") + "K";
        return (valor / 1_000_000).ToString("0.##") + "M";
    }

    private static void AsegurarEventSystem()
    {
        // Necesario para que los botones reciban clics (con el Input System nuevo).
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private Transform CrearCanvas()
    {
        GameObject go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                       typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler escalador = go.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1280, 720);
        escalador.matchWidthOrHeight = 0.5f;

        return go.transform;
    }

    private static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private Text CrearTexto(string nombre, Transform padre, string contenido, int tamano,
                            TextAnchor alineacion, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        Text t = go.GetComponent<Text>();
        t.font = fuente;
        t.text = contenido;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private Button CrearBoton(string nombre, Transform padre, string contenido, Action alHacerClic)
    {
        Image img = CrearImagen(nombre, padre, ColorBoton);
        Button boton = img.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.onClick.AddListener(() => alHacerClic());

        Text t = CrearTexto("Texto", img.transform, contenido, 24, TextAnchor.MiddleCenter, ColorTextoBoton);
        t.fontStyle = FontStyle.Bold;
        Estirar(t.rectTransform, Vector2.zero, Vector2.one);
        return boton;
    }

    /// <summary>Ancla y pivote en el mismo punto (esquina, borde o centro), con posición y tamaño fijos.</summary>
    private static void Colocar(RectTransform rt, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    /// <summary>Estira el elemento entre dos anclas relativas al padre, sin márgenes.</summary>
    private static void Estirar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax)
    {
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
