using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor que añade un botón "Volver" visible al panel de Ajustes del menú
/// principal (MainMenu.unity). El panel ya se podía cerrar con el mapeo compartido de Cancelar
/// (Esc en teclado, botón B/East en mando — ver SettingsMenuController.WasCancelPressedThisFrame),
/// pero jugando con ratón no había ningún botón en pantalla para volver, solo ese atajo de
/// teclado/mando — incómodo si se está navegando todo el menú con el propio ratón.
///
/// Clona el botón de idioma "Español" (siempre presente, con el mismo estilo visual —
/// fondo/fuente/tamaño— que el resto de controles del panel) para no inventar un estilo nuevo,
/// lo re-etiqueta con la clave de localización "Settings_Back" (ya existente en
/// ui_es.json/ui_en.json como "Volver"/"Back" desde antes, pero sin usar todavía en ningún
/// sitio) y lo cablea al campo SettingsMenuController.backButton (que llama a Close() al
/// pulsarlo — ver SettingsMenuController.cs).
///
/// Uso: menú "El Sendero → Controles → Añadir Botón Volver a Ajustes".
/// Requiere que SettingsMenuController.cs (con el campo 'backButton' ya añadido) compile sin
/// errores antes de ejecutar esto.
///
/// Idempotente: si 'BotonVolver' ya existe bajo el panel de Ajustes, se reutiliza (reposición y
/// recableado) en vez de duplicarlo.
/// </summary>
public static class SettingsBackButtonBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string ButtonName = "BotonVolver";
    const string LocalizationKey = "Settings_Back";

    [MenuItem("El Sendero/Controles/Añadir Botón Volver a Ajustes")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[SettingsBackButtonBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        // No pisar trabajo sin guardar: este proceso cambia la escena activa a MainMenu.unity.
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[SettingsBackButtonBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[SettingsBackButtonBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        try
        {
            BuildBackButton();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SettingsBackButtonBuilder] ✅ Listo. Botón 'Volver' añadido y cableado en el panel de Ajustes de MainMenu.unity. " +
                      "Revisa con 'git diff' y ajusta color/tamaño/posición a mano si quieres afinar el estilo.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SettingsBackButtonBuilder] Error durante la construcción (la escena NO se ha guardado): {e}");
        }
    }

    static void BuildBackButton()
    {
        var settingsMenu = UnityEngine.Object.FindAnyObjectByType<SettingsMenuController>(FindObjectsInactive.Include);
        if (settingsMenu == null)
            throw new Exception("No se encontró SettingsMenuController en MainMenu.unity.");

        var settingsSo = new SerializedObject(settingsMenu);

        var rootProp = settingsSo.FindProperty("root");
        var panelGo = rootProp != null && rootProp.objectReferenceValue != null
            ? rootProp.objectReferenceValue as GameObject
            : settingsMenu.gameObject;

        if (panelGo == null)
            throw new Exception("El panel de Ajustes (campo 'root') no está asignado — no se puede colocar el botón.");

        // Botón de referencia para clonar estilo: el de idioma Español, siempre presente en el panel
        // y no atado a la lógica de pares Sí/No (que recolorea según el estado activo).
        var spanishProp = settingsSo.FindProperty("spanishButton");
        var referenceButton = spanishProp != null ? spanishProp.objectReferenceValue as Button : null;
        if (referenceButton == null)
            throw new Exception("No se encontró 'spanishButton' en SettingsMenuController — no hay ningún botón de referencia del que clonar el estilo.");

        var existing = FindDeepChild(panelGo.transform, ButtonName);
        Button backButton;
        GameObject backButtonGo;

        if (existing != null && existing.GetComponent<Button>() != null)
        {
            Debug.Log($"[SettingsBackButtonBuilder] Ya existe '{ButtonName}' — se reutiliza (reposición y recableado, sin duplicar).");
            backButtonGo = existing.gameObject;
            backButton = existing.GetComponent<Button>();
        }
        else
        {
            var clonedGo = UnityEngine.Object.Instantiate(referenceButton.gameObject, panelGo.transform);
            clonedGo.name = ButtonName;
            backButtonGo = clonedGo;
            backButton = clonedGo.GetComponent<Button>();
            backButton.onClick = new Button.ButtonClickedEvent(); // limpiar el listener heredado del clon (SetLanguage("es"))
            Debug.Log($"[SettingsBackButtonBuilder] Creado '{ButtonName}' (clonado del botón de idioma Español) en el panel de Ajustes.");
        }

        // Posición: esquina inferior derecha del panel, simétrica a la etiqueta de versión
        // ("v0.1.3 (dev)") que ya vive en la esquina inferior izquierda — hueco libre en el layout
        // actual (columnas Language / Camera-Controls / General terminan bastante más arriba).
        var rt = backButtonGo.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(220f, 64f);
            rt.anchoredPosition = new Vector2(-40f, 40f);
        }
        // Última posición en la jerarquía a propósito: si 'firstSelection' no está asignado a mano
        // en el Inspector, SettingsMenuController.ResolveInitialSelection() cae al primer Selectable
        // interactable en orden de jerarquía — no queremos que Volver robe esa selección por defecto
        // a los controles de idioma/audio.
        backButtonGo.transform.SetAsLastSibling();

        // El botón de idioma clonado puede llegar deshabilitado si Español ya era el idioma activo
        // en la escena (spanishButton.interactable = false cuando coincide con el idioma actual) —
        // Volver debe estar SIEMPRE disponible.
        backButton.interactable = true;

        // Texto: reapunta la clave de localización ya existente en ui_es.json/ui_en.json
        // ("Settings_Back" -> "Volver"/"Back"), preparada de antes pero sin usar en ningún sitio.
        // NOTA (24 ago 2026): la v1 de esta herramienta escribía el texto vía la propiedad
        // TMP_Text.text/Text.text directamente y el cambio no sobrevivía al guardado de la escena
        // (el botón salía bien clonado y posicionado, pero con el literal "Español" heredado del
        // botón de idioma) — comprobado además que 'BotonEspanol' no tiene ningún LocalizedText
        // propio (solo TextMeshProUGUI + MenuTextHighlight), así que no era un Refresh() pisando el
        // valor. Ahora se escribe el campo serializado (m_text) vía SerializedObject, la vía fiable
        // para que un valor puesto por un script de Editor sobreviva al guardado.
        var localized = backButtonGo.GetComponentInChildren<LocalizedText>(true);
        if (localized != null)
        {
            localized.key = LocalizationKey;
            EditorUtility.SetDirty(localized);
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SettingsBackButtonBuilder] '{ButtonName}' no tiene LocalizedText (el botón de idioma clonado tampoco lo tenía) — se escribe 'Volver' como texto suelto.");
#endif
        }

        bool wroteLabel = false;
        var tmpLabel = backButtonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null)
        {
            var tmpSo = new SerializedObject(tmpLabel);
            tmpSo.FindProperty("m_text").stringValue = "Volver";
            tmpSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tmpLabel);
            wroteLabel = true;
        }
        var legacyLabel = backButtonGo.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            var legacySo = new SerializedObject(legacyLabel);
            legacySo.FindProperty("m_Text").stringValue = "Volver";
            legacySo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(legacyLabel);
            wroteLabel = true;
        }
        if (!wroteLabel)
            Debug.LogWarning($"[SettingsBackButtonBuilder] '{ButtonName}' no tiene ningún componente de texto (TMP ni Legacy) — revisa el botón de idioma de referencia.");

        settingsSo.FindProperty("backButton").objectReferenceValue = backButton;
        settingsSo.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[SettingsBackButtonBuilder] SettingsMenuController.backButton cableado.");
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
