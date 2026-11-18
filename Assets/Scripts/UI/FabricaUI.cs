using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Utilidades compartidas para construir interfaz desde código (HUD, tienda, menú).
/// Centraliza colores, fuente y la creación de textos, botones y campos de texto,
/// para no duplicar código entre pantallas.
/// </summary>
public static class FabricaUI
{
    // ---------- Paleta ----------
    public static readonly Color ColorFondo = new Color(0.16f, 0.10f, 0.05f, 1f);
    public static readonly Color ColorPanel = new Color(0.22f, 0.14f, 0.07f, 0.97f);
    public static readonly Color ColorFila = new Color(0.33f, 0.22f, 0.12f, 1f);
    public static readonly Color ColorBoton = new Color(0.95f, 0.70f, 0.25f, 1f);
    public static readonly Color ColorTexto = new Color(1f, 0.96f, 0.88f, 1f);
    public static readonly Color ColorTextoSuave = new Color(0.92f, 0.82f, 0.65f, 1f);
    public static readonly Color ColorTextoBoton = new Color(0.20f, 0.12f, 0.05f, 1f);
    public static readonly Color ColorError = new Color(1f, 0.55f, 0.45f, 1f);
    public static readonly Color ColorExito = new Color(0.65f, 0.95f, 0.60f, 1f);

    private static Font fuente;
    public static Font Fuente
    {
        get
        {
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fuente;
        }
    }

    // ---------- Estructura ----------

    /// <summary>Crea el EventSystem si no existe (necesario para clics y teclado en la UI).</summary>
    public static void AsegurarEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static Transform CrearCanvas(Transform padre, string nombre, int orden)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Canvas),
                                       typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(padre, false);

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;

        CanvasScaler escalador = go.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1280, 720);
        escalador.matchWidthOrHeight = 0.5f;

        return go.transform;
    }

    /// <summary>Contenedor vacío (sin imagen) que ocupa todo el padre.</summary>
    public static RectTransform CrearContenedor(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        Estirar(rt, Vector2.zero, Vector2.one);
        return rt;
    }

    // ---------- Elementos ----------

    public static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text CrearTexto(string nombre, Transform padre, string contenido, int tamano,
                                  TextAnchor alineacion, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        Text t = go.GetComponent<Text>();
        t.font = Fuente;
        t.text = contenido;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public static Button CrearBoton(string nombre, Transform padre, string contenido, Action alHacerClic)
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

    /// <summary>Campo de texto de una línea. Si esClave, oculta lo que se escribe.</summary>
    public static InputField CrearCampo(string nombre, Transform padre, string placeholder,
                                        bool esClave, int limite)
    {
        Image fondo = CrearImagen(nombre, padre, new Color(1f, 0.98f, 0.94f, 1f));
        InputField campo = fondo.gameObject.AddComponent<InputField>();

        Text texto = CrearTexto("Texto", fondo.transform, "", 24, TextAnchor.MiddleLeft, ColorTextoBoton);
        texto.supportRichText = false;
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        texto.verticalOverflow = VerticalWrapMode.Truncate;
        Estirar(texto.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 4), new Vector2(-14, -4));

        Text marcador = CrearTexto("Placeholder", fondo.transform, placeholder, 24, TextAnchor.MiddleLeft,
                                   new Color(0.45f, 0.40f, 0.35f, 1f));
        marcador.fontStyle = FontStyle.Italic;
        Estirar(marcador.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 4), new Vector2(-14, -4));

        campo.targetGraphic = fondo;
        campo.textComponent = texto;
        campo.placeholder = marcador;
        campo.lineType = InputField.LineType.SingleLine;
        campo.contentType = esClave ? InputField.ContentType.Password : InputField.ContentType.Standard;
        campo.characterLimit = limite;
        return campo;
    }

    // ---------- Posicionamiento ----------

    /// <summary>Ancla y pivote en el mismo punto (esquina, borde o centro), con posición y tamaño fijos.</summary>
    public static void Colocar(RectTransform rt, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    /// <summary>Estira el elemento entre dos anclas relativas al padre, con márgenes opcionales.</summary>
    public static void Estirar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax,
                               Vector2 margenMin = default, Vector2 margenMax = default)
    {
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.offsetMin = margenMin;
        rt.offsetMax = margenMax;
    }

    public static string Formatear(double valor)
    {
        valor = Math.Floor(valor);
        if (valor < 1_000) return valor.ToString("0");
        if (valor < 1_000_000) return (valor / 1_000).ToString("0.##") + "K";
        return (valor / 1_000_000).ToString("0.##") + "M";
    }
}
