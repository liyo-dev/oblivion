using UnityEngine;

/// <summary>
/// Marca un punto del mundo como referencia de foco para la cámara.
/// Otros sistemas (cinemáticas, cutscenes) buscan este componente por nombre
/// para saber dónde apuntar la cámara.
/// </summary>
public class CameraFocusPoint : MonoBehaviour
{
    [Tooltip("ID opcional para que los sistemas de cámara localicen este punto por nombre.")]
    [SerializeField] public string focusId;

    /// <summary>
    /// Posición del punto de foco en el mundo.
    /// </summary>
    public Vector3 WorldPosition => transform.position;
}
