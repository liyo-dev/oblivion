using UnityEngine;

[DisallowMultipleComponent]
public class AnchorEnvironment : MonoBehaviour
{
    [Header("Tipo de entorno")]
    public bool isInterior = true;

    [Header("Cámara en interior")]
    public bool useSolidColorBackground = true;
    public Color interiorBgColor = new Color(0.05f, 0.05f, 0.06f);
    public Material interiorSkyboxOverride; // opcional

    [Header("Luces al entrar (activar)")]
    public Light[] lightsEnableOnEnter;
    [Header("Luces al entrar (desactivar)")]
    public Light[] lightsDisableOnEnter;
}