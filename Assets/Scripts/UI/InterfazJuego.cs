using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD y tienda, construidos desde código con FabricaUI:
///   - HUD: contador de tostadas, producción por segundo, jugador, barra de carga,
///          botón Tienda y botón Guardar y salir.
///   - Tienda: una fila por mejora con nivel, efecto y botón de compra.
/// Se oculta mientras el menú de inicio está abierto.
/// </summary>
public class InterfazJuego : MonoBehaviour
{
    [SerializeField] private ControladorJuego controlador;
    [SerializeField] private PalancaTostadora palanca;

    private class FilaMejora
    {
        public TipoMejora tipo;
        public Text titulo;
        public Text efecto;
        public Button boton;
        public Text textoBoton;
    }

    private GameObject raizHUD;
    private Text textoTostadas;
    private Text textoProduccion;
    private Text textoJugador;
    private Text textoBotonSalir;
    private Text textoMeta;
    private GameObject panelMeta;
    private Text textoMetaDetalle;
    private RectTransform rellenoCarga;
    private GameObject panelTienda;
    private Text textoTostadasTienda;
    private FilaMejora[] filas;

    private void Start()
    {
        FabricaUI.AsegurarEventSystem();

        Transform canvas = FabricaUI.CrearCanvas(transform, "CanvasJuego", 10);
        raizHUD = canvas.gameObject;

        CrearHUD(canvas);
        CrearTienda(canvas);
        CrearPanelMeta(canvas);
        panelTienda.SetActive(false);
        panelMeta.SetActive(false);
    }

    private void Update()
    {
        bool enJuego = !controlador.EnMenu;
        if (raizHUD.activeSelf != enJuego) raizHUD.SetActive(enJuego);
        if (!enJuego) return;

        EstadoJuego e = controlador.Estado;

        textoTostadas.text = $"Tostadas: {FabricaUI.Formatear(e.tostadas)}";
        double produccion = e.ProduccionPorSegundo();
        textoProduccion.text = produccion > 0 ? $"+{produccion:0.#} por segundo" : "";
        textoJugador.text = Sesion.EnLinea ? $"Jugador: {Sesion.Usuario}" : "Invitado: el progreso no se guarda";
        textoBotonSalir.text = Sesion.EnLinea ? "Guardar y salir" : "Salir";
        textoMeta.text = e.MetaAlcanzada()
            ? "¡Meta cumplida!"
            : $"Meta: {FabricaUI.Formatear(e.tostadasTotales)} / {FabricaUI.Formatear(EstadoJuego.META_TOSTADAS)}";

        if (panelMeta.activeSelf != controlador.MostrandoMeta)
        {
            panelMeta.SetActive(controlador.MostrandoMeta);
            if (controlador.MostrandoMeta)
            {
                int seg = Mathf.FloorToInt(controlador.TiempoPartida);
                textoMetaDetalle.text = $"Llegaste a {EstadoJuego.META_TOSTADAS:0} tostadas en {seg / 60}:{seg % 60:00}.\n" +
                                        $"Lanzamientos: {e.lanzamientos}   Impactos: {e.impactos}";
            }
        }

        rellenoCarga.anchorMax = new Vector2(palanca.CargaNormalizada, 1f);

        if (panelTienda.activeSelf != controlador.TiendaAbierta)
            panelTienda.SetActive(controlador.TiendaAbierta);

        if (controlador.TiendaAbierta) ActualizarTienda(e);
    }

    // ===================== HUD =====================

    private void CrearHUD(Transform canvas)
    {
        textoTostadas = FabricaUI.CrearTexto("Tostadas", canvas, "", 40, TextAnchor.UpperLeft, FabricaUI.ColorTexto);
        textoTostadas.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(textoTostadas.rectTransform, new Vector2(0, 1), new Vector2(24, -20), new Vector2(600, 56));

        textoProduccion = FabricaUI.CrearTexto("Produccion", canvas, "", 24, TextAnchor.UpperLeft, FabricaUI.ColorTextoSuave);
        FabricaUI.Colocar(textoProduccion.rectTransform, new Vector2(0, 1), new Vector2(24, -76), new Vector2(600, 36));

        textoJugador = FabricaUI.CrearTexto("Jugador", canvas, "", 20, TextAnchor.UpperLeft, FabricaUI.ColorTextoSuave);
        FabricaUI.Colocar(textoJugador.rectTransform, new Vector2(0, 1), new Vector2(24, -112), new Vector2(600, 30));

        textoMeta = FabricaUI.CrearTexto("Meta", canvas, "", 24, TextAnchor.UpperLeft, FabricaUI.ColorBoton);
        textoMeta.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(textoMeta.rectTransform, new Vector2(0, 1), new Vector2(24, -144), new Vector2(600, 34));

        Button botonTienda = FabricaUI.CrearBoton("BotonTienda", canvas, "Tienda (T)", () => controlador.AbrirTienda(true));
        FabricaUI.Colocar((RectTransform)botonTienda.transform, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(220, 64));

        Button botonSalir = FabricaUI.CrearBoton("BotonSalir", canvas, "", controlador.GuardarYSalir);
        FabricaUI.Colocar((RectTransform)botonSalir.transform, new Vector2(1, 1), new Vector2(-24, -96), new Vector2(220, 52));
        textoBotonSalir = botonSalir.GetComponentInChildren<Text>();
        textoBotonSalir.fontSize = 20;

        // Barra de carga del resorte
        Text etiqueta = FabricaUI.CrearTexto("EtiquetaCarga", canvas, "Mantén ESPACIO para cargar", 22,
                                             TextAnchor.MiddleCenter, FabricaUI.ColorTexto);
        FabricaUI.Colocar(etiqueta.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(500, 30));

        Image fondo = FabricaUI.CrearImagen("BarraCarga", canvas, new Color(0, 0, 0, 0.6f));
        FabricaUI.Colocar(fondo.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(420, 26));

        Image relleno = FabricaUI.CrearImagen("Relleno", fondo.transform, FabricaUI.ColorBoton);
        rellenoCarga = relleno.rectTransform;
        FabricaUI.Estirar(rellenoCarga, Vector2.zero, new Vector2(0, 1));
    }

    // ===================== META =====================

    private void CrearPanelMeta(Transform canvas)
    {
        Image overlay = FabricaUI.CrearImagen("PanelMeta", canvas, new Color(0, 0, 0, 0.75f));
        FabricaUI.Estirar(overlay.rectTransform, Vector2.zero, Vector2.one);
        panelMeta = overlay.gameObject;

        Image caja = FabricaUI.CrearImagen("Caja", overlay.transform, FabricaUI.ColorPanel);
        FabricaUI.Colocar(caja.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 360));

        Text titulo = FabricaUI.CrearTexto("Titulo", caja.transform, "¡COCINA COMPLETADA!", 52,
                                           TextAnchor.MiddleCenter, FabricaUI.ColorBoton);
        titulo.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(titulo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(660, 70));

        textoMetaDetalle = FabricaUI.CrearTexto("Detalle", caja.transform, "", 26, TextAnchor.MiddleCenter, FabricaUI.ColorTexto);
        FabricaUI.Colocar(textoMetaDetalle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(660, 90));

        Button seguir = FabricaUI.CrearBoton("Seguir", caja.transform, "¡Seguir tostando!", controlador.CerrarMeta);
        FabricaUI.Colocar((RectTransform)seguir.transform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(300, 64));
    }

    // ===================== TIENDA =====================

    private void CrearTienda(Transform canvas)
    {
        // Fondo oscuro que cubre la pantalla y bloquea clics al juego.
        Image overlay = FabricaUI.CrearImagen("Tienda", canvas, new Color(0, 0, 0, 0.7f));
        FabricaUI.Estirar(overlay.rectTransform, Vector2.zero, Vector2.one);
        panelTienda = overlay.gameObject;

        Image caja = FabricaUI.CrearImagen("Caja", overlay.transform, FabricaUI.ColorPanel);
        FabricaUI.Colocar(caja.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 600));

        Text titulo = FabricaUI.CrearTexto("Titulo", caja.transform, "TIENDA", 46, TextAnchor.MiddleCenter, FabricaUI.ColorTexto);
        titulo.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(titulo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(700, 60));

        textoTostadasTienda = FabricaUI.CrearTexto("Saldo", caja.transform, "", 26, TextAnchor.MiddleCenter, FabricaUI.ColorTextoSuave);
        FabricaUI.Colocar(textoTostadasTienda.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(700, 36));

        int cantidad = EstadoJuego.Mejoras.Length;
        filas = new FilaMejora[cantidad];
        for (int i = 0; i < cantidad; i++)
            filas[i] = CrearFila(caja.transform, (TipoMejora)i, -128 - i * 100);

        Button volver = FabricaUI.CrearBoton("Volver", caja.transform, "Volver (T)", () => controlador.AbrirTienda(false));
        FabricaUI.Colocar((RectTransform)volver.transform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(240, 60));
    }

    private FilaMejora CrearFila(Transform padre, TipoMejora tipo, float y)
    {
        Image fondo = FabricaUI.CrearImagen("Fila_" + tipo, padre, FabricaUI.ColorFila);
        FabricaUI.Colocar(fondo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(720, 88));

        FilaMejora fila = new FilaMejora { tipo = tipo };

        fila.titulo = FabricaUI.CrearTexto("Nombre", fondo.transform, "", 28, TextAnchor.UpperLeft, FabricaUI.ColorTexto);
        fila.titulo.fontStyle = FontStyle.Bold;
        FabricaUI.Colocar(fila.titulo.rectTransform, new Vector2(0, 1), new Vector2(16, -10), new Vector2(450, 36));

        fila.efecto = FabricaUI.CrearTexto("Efecto", fondo.transform, "", 21, TextAnchor.UpperLeft, FabricaUI.ColorTextoSuave);
        FabricaUI.Colocar(fila.efecto.rectTransform, new Vector2(0, 1), new Vector2(16, -50), new Vector2(450, 30));

        fila.boton = FabricaUI.CrearBoton("Comprar", fondo.transform, "", () => controlador.ComprarMejora(tipo));
        FabricaUI.Colocar((RectTransform)fila.boton.transform, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(220, 60));
        fila.textoBoton = fila.boton.GetComponentInChildren<Text>();

        return fila;
    }

    private void ActualizarTienda(EstadoJuego e)
    {
        textoTostadasTienda.text = $"Tienes {FabricaUI.Formatear(e.tostadas)} tostadas";

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
                fila.textoBoton.text = $"Comprar: {FabricaUI.Formatear(e.Costo(fila.tipo))}";
                fila.boton.interactable = e.PuedeComprar(fila.tipo);
            }
        }
    }
}
