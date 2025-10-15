using UnityEngine;

public class SessionRouter : MonoBehaviour, IInteractionSession
{
    [SerializeField] private TargetMode mode = TargetMode.Specific;
    [SerializeField] private MonoBehaviour specific; // must implement IInteractionSession
    [SerializeField] private string componentName;   // e.g., "QuestNpcSession"

    public void BeginSession(GameObject interactor, System.Action onFinish)
    {
        IInteractionSession target = null;

        if (mode == TargetMode.Specific && specific is IInteractionSession s1)
            target = s1;
        else if (mode == TargetMode.ByName)
        {
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb == this) continue;
                if (mb.GetType().Name == componentName && mb is IInteractionSession s2) { target = s2; break; }
            }
        }
        else // FirstFound
        {
            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb != this && mb is IInteractionSession s3) { target = s3; break; }
        }

        if (target == null)
        {
            Debug.LogWarning($"[SessionRouter] No se encontró sesión válida en {name}.");
            onFinish?.Invoke();
            return;
        }

        target.BeginSession(interactor, onFinish);
    }
}
