using System.Collections.Generic;
using UnityEngine;

public enum TipoSuperficie { Pared, Suelo }

/// <summary>
/// Caja de colisión propia, alineada a los ejes (AABB). Reemplaza a BoxCollider2D.
/// No usa el motor de física de Unity: el tamaño se calcula a partir de la escala
/// del objeto y la detección/resolución la hace cada tostada con Interseccion().
///
/// Nota: asume que el objeto NO está rotado (paredes y suelo rectos).
/// </summary>
public class ColisionAABB : MonoBehaviour
{
    /// <summary>Registro de todas las superficies activas en la escena.</summary>
    public static readonly List<ColisionAABB> Todas = new List<ColisionAABB>();

    [SerializeField] private TipoSuperficie tipo = TipoSuperficie.Pared;
    [Tooltip("Tamaño del sprite con escala 1. El Square de Unity mide 1x1 unidades.")]
    [SerializeField] private Vector2 tamanoBase = Vector2.one;

    public TipoSuperficie Tipo => tipo;
    public Vector2 Centro => transform.position;
    public Vector2 MitadTamano => Vector2.Scale(tamanoBase, (Vector2)transform.lossyScale) * 0.5f;

    private void OnEnable() => Todas.Add(this);
    private void OnDisable() => Todas.Remove(this);

    /// <summary>
    /// Prueba de solapamiento entre dos AABB (A = objeto móvil, B = superficie).
    /// Si se solapan, devuelve la normal de colisión (apuntando de B hacia A) y la
    /// penetración por el eje de MENOR solapamiento, que es por donde hay que sacar a A.
    /// </summary>
    public static bool Interseccion(Vector2 centroA, Vector2 mitadA,
                                    Vector2 centroB, Vector2 mitadB,
                                    out Vector2 normal, out float penetracion)
    {
        Vector2 d = centroA - centroB;
        float solapeX = (mitadA.x + mitadB.x) - Mathf.Abs(d.x);
        float solapeY = (mitadA.y + mitadB.y) - Mathf.Abs(d.y);

        normal = Vector2.zero;
        penetracion = 0f;
        if (solapeX <= 0f || solapeY <= 0f) return false;   // separadas en algún eje

        if (solapeX < solapeY)
        {
            normal = new Vector2(Mathf.Sign(d.x), 0f);   // choque lateral
            penetracion = solapeX;
        }
        else
        {
            normal = new Vector2(0f, Mathf.Sign(d.y));   // choque por arriba / abajo
            penetracion = solapeY;
        }
        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = tipo == TipoSuperficie.Pared ? Color.red : Color.green;
        Gizmos.DrawWireCube(Centro, MitadTamano * 2f);
    }
}
