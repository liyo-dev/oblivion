using UnityEngine;
using UnityEngine.UI;

public class BillboardUI : MonoBehaviour
{
    private Camera cam;

    // Compartido entre todas las instancias: evita crear un Material por cada
    // icono de interaccion y solo se resuelve una vez por sesion.
    private static Material s_alwaysOnTopMaterial;
    private static bool s_alwaysOnTopMaterialResolved;

    void Awake()
    {
        cam = Camera.main;
        ApplyAlwaysOnTopMaterial();
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

    /// <summary>
    /// El Canvas de este hint es World Space, así que por defecto usa
    /// ZTest [unity_GUIZTestMode], que en World Space respeta el depth
    /// buffer del resto de la escena. Eso hace que el icono se "corte"
    /// (se recorte parcialmente) cada vez que geometría no bloqueante pasa
    /// delante de la cámara (pelo/capucha del personaje, muebles, marcos de
    /// puerta...), y el resultado depende por completo del ángulo de cámara.
    /// InteractionDetector ya comprueba línea de visión antes de mostrar el
    /// hint, así que si el icono está visible no hay ninguna pared real
    /// bloqueando: forzar ZTest Always en un material compartido evita el
    /// recorte cosmético sin afectar a esa lógica de obstrucción.
    /// </summary>
    void ApplyAlwaysOnTopMaterial()
    {
        var material = GetAlwaysOnTopMaterial();
        if (material == null) return;

        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].material = material;
        }
    }

    static Material GetAlwaysOnTopMaterial()
    {
        if (s_alwaysOnTopMaterialResolved) return s_alwaysOnTopMaterial;
        s_alwaysOnTopMaterialResolved = true;

        var shader = Shader.Find("UI/HintAlwaysOnTop");
        if (shader == null)
        {
            Debug.LogWarning("[BillboardUI] Shader 'UI/HintAlwaysOnTop' no encontrado; los iconos de interacción " +
                              "seguirán pudiendo recortarse contra geometría cercana a la cámara.");
            return null;
        }

        s_alwaysOnTopMaterial = new Material(shader) { name = "UI-HintAlwaysOnTop (Shared)" };
        return s_alwaysOnTopMaterial;
    }
}
