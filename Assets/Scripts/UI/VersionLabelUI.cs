using UnityEngine;
using TMPro;

/// <summary>
/// Muestra el número de versión del juego (PlayerSettings > Other Settings > Version, leído vía
/// <see cref="Application.version"/>) en una esquina de la pantalla de título.
///
/// Motivo (INC-083 del Tracker de Incidencias): facilita a testers/QA saber qué build están
/// probando, agiliza el soporte postlanzamiento cuando un jugador reporta un bug (ya no hay que
/// explicarle cómo mirar la versión en menús), confirma visualmente que una actualización se
/// instaló bien, y ayuda a la comunidad/creadores de contenido a referenciar qué versión están
/// jugando.
///
/// Este componente solo formatea y pinta el texto; la colocación/estilo visual del GameObject vive
/// en la escena (creado por MainMenuVersionLabelBuilder, ver Assets/Scripts/Editor/).
/// </summary>
[DisallowMultipleComponent]
public class VersionLabelUI : MonoBehaviour
{
    [Tooltip("Prefijo delante del número de versión.")]
    [SerializeField] private string prefix = "v";

    [Tooltip("Sufijo añadido solo en el Editor o en builds de desarrollo, para no confundir una " +
             "build de prueba con la build final que llega a jugadores.")]
    [SerializeField] private string devSuffix = " (dev)";

    [Tooltip("Si se deja vacío, se busca un TMP_Text en este mismo GameObject.")]
    [SerializeField] private TMP_Text label;

    void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        Refresh();
    }

    void Refresh()
    {
        if (!label)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[VersionLabelUI] No se encontró un TMP_Text para mostrar la versión.");
#endif
            return;
        }

        string text = prefix + Application.version;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        text += devSuffix;
#endif

        label.text = text;
    }
}
