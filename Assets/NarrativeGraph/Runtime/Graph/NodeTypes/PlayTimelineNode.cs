// PlayTimelineNode.cs
using System;
using UnityEngine;
using UnityEngine.Playables;

[Obsolete("PlayTimelineNode está obsoleto. Usa PlayTimeline via Timeline/PlayableDirector directamente o nodos alternativos.")]
[Serializable]
public sealed class PlayTimelineNode : NarrativeNode
{
    // No referenciamos PlayableDirector por ExposedReference para evitar drag&drop de escena.
    [Tooltip("Nombre del PlayableDirector (GameObject) a buscar en escena. Si está vacío, se usará el primer PlayableDirector encontrado.")]
    public string directorName = "";

    Action _done;
    PlayableDirector _dir;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Buscar director por nombre o usar el primero disponible
        if (!string.IsNullOrEmpty(directorName))
        {
            try
            {
                var gos = UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var pd in gos)
                {
                    try { if (pd.gameObject.name == directorName) { _dir = pd; break; } }
                    catch (Exception ex) { Debug.LogWarning($"[PlayTimelineNode] Error checking director name: {ex.Message}"); }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayTimelineNode] Error while searching PlayableDirectors: {ex.Message}");
            }
        }

        if (_dir == null)
        {
            // Fallback: primer PlayableDirector en escena
            try { _dir = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>(FindObjectsInactive.Include); }
            catch { _dir = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>(); }
        }

        if (!_dir) { onReadyToAdvance?.Invoke(); return; }

        _done = () => { _dir.stopped -= OnStopped; onReadyToAdvance?.Invoke(); };
        _dir.stopped += OnStopped;
        _dir.Play();
    }

    void OnStopped(PlayableDirector d) => _done?.Invoke();

    public override void Exit(NarrativeContext ctx)
    {
        if (_dir) _dir.stopped -= OnStopped;
        _done = null;
    }
}