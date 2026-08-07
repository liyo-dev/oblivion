using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        // Quaternion.LookRotation(forward) usa Vector3.up implícito como
        // referencia; con ángulos de cámara casi cenitales (ver
        // SleepTrigger.MoveCameraToSleepAnchor) ese cálculo queda mal
        // condicionado y el icono gira sobre su propio eje ("doblado").
        // Pasar cam.transform.up explícito evita el caso degenerado.
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
    }
}