using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(PlayerPresetSO))]
public class PlayerPresetSOEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayerPresetSO preset = (PlayerPresetSO)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Herramientas de Apariencia", EditorStyles.boldLabel);

        if (GUILayout.Button("Capturar apariencia del jugador en escena"))
        {
            CaptureAppearanceFromScene(preset);
        }

        EditorGUILayout.HelpBox(
            "Usa el botón 'Capturar apariencia del jugador en escena' para copiar la configuración " +
            "visual actual del jugador en la escena a este preset. El jugador debe tener un componente " +
            "ModularAutoBuilder activo en la escena.",
            MessageType.Info
        );
    }

    private void CaptureAppearanceFromScene(PlayerPresetSO preset)
    {
        // Buscar el jugador en la escena
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró un GameObject con tag 'Player' en la escena.", "OK");
            return;
        }

        // Buscar ModularAutoBuilder
        ModularAutoBuilder builder = playerGO.GetComponentInChildren<ModularAutoBuilder>(true);
        if (builder == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró ModularAutoBuilder en el jugador.", "OK");
            return;
        }

        // Obtener la selección actual
        var selection = builder.GetSelection();
        if (selection == null || selection.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "El ModularAutoBuilder no tiene ninguna selección activa.", "OK");
            return;
        }

        // Limpiar y llenar la lista de apariencia
        Undo.RecordObject(preset, "Capturar apariencia del jugador");

        if (preset.appearance == null)
            preset.appearance = new List<AppearanceEntry>();
        else
            preset.appearance.Clear();

        foreach (var kv in selection)
        {
            // Solo agregar si hay una parte seleccionada (no null)
            if (!string.IsNullOrEmpty(kv.Value))
            {
                preset.appearance.Add(new AppearanceEntry
                {
                    category = kv.Key,
                    partName = kv.Value
                });
            }
        }

        EditorUtility.SetDirty(preset);
        Debug.Log($"[PlayerPresetSOEditor] Apariencia capturada: {preset.appearance.Count} partes guardadas en '{preset.name}'");

        // Mostrar resumen
        string summary = "Partes capturadas:\n";
        foreach (var entry in preset.appearance)
        {
            summary += $"  • {entry.category}: {entry.partName}\n";
        }
        EditorUtility.DisplayDialog("Apariencia Capturada", summary, "OK");
    }
}
