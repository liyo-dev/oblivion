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
    
    [Header("Gestión de Zonas Visibles")]
    [Tooltip("GameObject raíz de esta zona/interior. Se activará al entrar aquí.")]
    public GameObject zoneRoot;
    
    [Tooltip("Otras zonas que deben OCULTARSE al entrar aquí. Útil para interiores en la misma escena.")]
    public GameObject[] zonesToHideOnEnter;
    
    [Tooltip("Si true, oculta automáticamente el mundo exterior (busca 'WorldRoot' o similar)")]
    public bool hideExteriorWorld = true;
    
    [Tooltip("Tag o nombre del GameObject raíz del mundo exterior (para ocultarlo automáticamente)")]
    public string exteriorWorldRootName = "ExteriorWorld";
    
    [Header("Culling de Cámara")]
    [Tooltip("Si true, ajusta el far clip plane de la cámara para limitar qué se ve")]
    public bool adjustCameraClipping = false;
    [Tooltip("Far clip plane cuando está en este interior (más pequeño = menos visible)")]
    public float interiorFarClipPlane = 50f;
}