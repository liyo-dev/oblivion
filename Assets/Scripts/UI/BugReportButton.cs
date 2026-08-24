using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// OBSOLETO desde el 24 ago 2026 — ya no lo usa MainMenuPatchNotesBugReportBuilder.cs, que ahora
/// elimina este GameObject de la escena si lo encuentra (de una ejecución anterior) y añade
/// BugReportFlyoutPanel.cs en su lugar. Se deja este archivo como referencia histórica (y por si
/// algún día hiciera falta volver al enfoque de "abrir el navegador" como alternativa de bajo
/// esfuerzo), pero no se monta en ningún sitio actualmente.
///
/// Motivo del cambio: Raúl quería que Reportar un Fallo se sintiera "dentro del juego" en vez de
/// abrir Chrome — BugReportFlyoutPanel.cs construye el mismo formulario como UI nativa del menú y
/// manda los datos directamente al Google Form por HTTP al pulsar Enviar.
///
/// Comentario original: Botón "REPORTAR UN FALLO" del MainMenu: abre en el navegador del sistema
/// (vía <see cref="Application.OpenURL"/>) el formulario externo de reporte de bugs — mismo patrón
/// que usan otros juegos en pre-alpha (formulario de Google Form) para recoger severidad, número de
/// build, descripción y pasos de reproducción sin tener que construir esa UI dentro del propio
/// juego. NO crea UI propia. Solo se engancha al botón ya existente en MainMenu.unity — mismo
/// patrón de auto-detección con reintento que CreditsFlyoutPanel/PatchNotesFlyoutPanel.
/// </summary>
[DisallowMultipleComponent]
public class BugReportButton : MonoBehaviour
{
    [Header("Botón que dispara la acción")]
    [Tooltip("Si se deja vacío, se busca en toda la escena un Button cuyo nombre o texto contenga 'bug' o 'fallo'.")]
    [SerializeField] Button bugReportButtonOverride;

    [Header("Formulario externo")]
    [Tooltip("URL del Google Form (u otro formulario externo) de reporte de bugs. Déjalo vacío hasta " +
             "que exista de verdad — el botón no falla si está vacío, solo avisa por consola y no abre nada.")]
    [SerializeField] string bugReportFormUrl = "";

    Button _button;

    void Start()
    {
        StartCoroutine(WireButtonWithRetry());
    }

    IEnumerator WireButtonWithRetry()
    {
        const float maxWaitSeconds = 2f;
        float deadline = Time.unscaledTime + maxWaitSeconds;

        while (Time.unscaledTime < deadline)
        {
            if (TryWireButton())
                yield break;

            yield return null;
        }

        if (!TryWireButton())
        {
            Debug.LogWarning("[BugReportButton] No se encontró el botón REPORTAR UN FALLO automáticamente " +
                              $"tras reintentar durante {2f:0.#}s. Asigna 'Bug Report Button Override' a mano en el Inspector.");
        }
    }

    bool TryWireButton()
    {
        if (_button != null)
            return true;

        _button = bugReportButtonOverride != null ? bugReportButtonOverride : FindBugReportButton();

        if (_button == null)
            return false;

        _button.onClick.AddListener(OnClickBugReport);
        return true;
    }

    void OnClickBugReport()
    {
        if (string.IsNullOrWhiteSpace(bugReportFormUrl))
        {
            Debug.LogWarning("[BugReportButton] 'Bug Report Form Url' está vacío — pega aquí la URL real " +
                              "del Google Form en el Inspector de este componente.");
            return;
        }

        AudioService.Instance?.PlaySFX("UI_Submit");
        Application.OpenURL(bugReportFormUrl);
    }

    Button FindBugReportButton()
    {
        var all = FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (var b in all)
        {
            if (Matches(b.gameObject.name)) return b;

            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && Matches(label.text)) return b;

            var legacyLabel = b.GetComponentInChildren<Text>(true);
            if (legacyLabel != null && Matches(legacyLabel.text)) return b;
        }
        return null;
    }

    static bool Matches(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var n = StripAccents(s.ToLowerInvariant());
        return n.Contains("bug") || n.Contains("fallo");
    }

    static string StripAccents(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case 'á': sb.Append('a'); break;
                case 'é': sb.Append('e'); break;
                case 'í': sb.Append('i'); break;
                case 'ó': sb.Append('o'); break;
                case 'ú': sb.Append('u'); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
