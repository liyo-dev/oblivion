using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Utilidad de editor para unificar el look Quibli entre escenas.
///
/// Diagnóstico (ver auditoría): el pipeline de render (<c>Quibli URP Config</c>) ya es el mismo para TODO
/// el proyecto (Project Settings → Graphics), y los materiales de personajes/mundo ya usan el shader
/// tooning de Quibli (<c>StylizedLit</c>). Lo que falta en los menús y secuencias (MainMenu, CharacterCreator,
/// Credits, LoadingScreen, SplashScreen) es el Global Volume: MainWorld tiene uno apuntando a
/// "Assets/Scenes/Worlds/MainWorld/Volumen Profile.asset" (bloom/color grading/tonemapping), y esas otras
/// escenas no tienen ningún Volume — por eso se ven "planas" en comparación.
///
/// Este menú añade a la escena ACTIVA un Global Volume que apunta al mismo perfil que usa MainWorld, para
/// que compartan el mismo grading. No toca el pipeline ni ningún shader — solo añade lo que faltaba.
///
/// Uso: abrir la escena (p. ej. MainMenu.unity), y ejecutar
/// "El Sendero/Render/Aplicar look de MainWorld a la escena activa". Revisar el resultado en Play/Scene view
/// y guardar la escena si convence. Si el look no encaja igual de bien en una pantalla 100% UI (sin geometría
/// 3D detrás), lo normal es que solo haga falta bajar el "weight" del Volume o desactivar overrides
/// concretos (p. ej. Vignette) para esa escena — se puede hacer luego a mano en el Inspector, este script
/// solo deja el punto de partida ya enlazado en vez de tener que crearlo desde cero.
/// </summary>
static class AplicarLookQuibliAEscena
{
    const string RutaPerfilMainWorld = "Assets/Scenes/Worlds/MainWorld/Volumen Profile.asset";
    const string NombreGameObject = "Global Volume (Look Quibli)";

    [MenuItem("El Sendero/Render/Aplicar look de MainWorld a la escena activa")]
    static void Aplicar()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaPerfilMainWorld);
        if (perfil == null)
        {
            EditorUtility.DisplayDialog(
                "Look Quibli",
                $"No se encontró el perfil de post-proceso en:\n{RutaPerfilMainWorld}\n\n" +
                "¿Se ha movido o renombrado el asset? Ajusta la constante RutaPerfilMainWorld en " +
                "AplicarLookQuibliAEscena.cs si es así.",
                "Vale");
            return;
        }

        var existente = GameObject.Find(NombreGameObject);
        GameObject go = existente != null ? existente : new GameObject(NombreGameObject);
        if (existente == null) Undo.RegisterCreatedObjectUndo(go, "Aplicar look Quibli a la escena");

        var volume = go.GetComponent<Volume>();
        if (volume == null) volume = Undo.AddComponent<Volume>(go);

        Undo.RecordObject(volume, "Aplicar look Quibli a la escena");
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.sharedProfile = perfil;
        EditorUtility.SetDirty(volume);

        EditorGUIUtility.PingObject(go);
        Selection.activeGameObject = go;

        Debug.Log(
            $"[AplicarLookQuibliAEscena] Global Volume '{NombreGameObject}' listo en " +
            $"'{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}', usando el perfil de MainWorld. " +
            "Recuerda guardar la escena (Ctrl+S) si el resultado te convence.");
    }
}
