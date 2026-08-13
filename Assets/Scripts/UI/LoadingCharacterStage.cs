// Assets/Scripts/UI/LoadingCharacterStage.cs
using UnityEngine;

/// <summary>
/// Orquesta a los 3 personajes decorativos del "escenario" de la pantalla de carga
/// (Will, Estela y Liam): corren en el sitio mientras se carga, y al llegar al 100%
/// se giran hacia cámara y cada uno hace su gesto gracioso. Ver <see cref="LoadingShowcaseCharacter"/>
/// para el comportamiento de cada personaje individual.
///
/// Vive en Assets/Scenes/Systems/LoadingScreen.unity (objeto "CharacterStage"), NO en el
/// prefab LoadingOverlay: los personajes son objetos 3D renderizados por una cámara dedicada
/// a una RenderTexture (ver StageCamera / RT_LoadingCharacterStage), no elementos de UI.
/// LoadingScreenController referencia esta clase para avisar de "empezar a cargar" y
/// "carga terminada".
/// </summary>
public class LoadingCharacterStage : MonoBehaviour
{
    [Tooltip("Los 3 personajes del escenario. No hace falta que estén en ningún orden concreto.")]
    public LoadingShowcaseCharacter[] characters;

    bool _revealed;

    /// <summary>Vuelve a la pose de "corriendo en el sitio". Llamado al mostrar la pantalla de carga.</summary>
    public void ResetToRunning()
    {
        _revealed = false;
        if (characters == null) return;
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i]) characters[i].ResetToRunning();
        }
    }

    /// <summary>Gira a los 3 personajes hacia cámara y dispara su gesto. Solo una vez por ciclo de carga.</summary>
    public void PlayReveal()
    {
        if (_revealed) return;
        _revealed = true;
        if (characters == null) return;
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i]) characters[i].PlayReveal();
        }
    }
}
