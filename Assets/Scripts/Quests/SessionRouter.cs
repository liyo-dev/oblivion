using UnityEngine;

public class SessionRouter : MonoBehaviour, IInteractionSession
{
    [SerializeField] private TargetMode mode = TargetMode.Specific;
    [SerializeField] private MonoBehaviour specific; // must implement IInteractionSession
    [SerializeField] private string componentName;   // e.g., "QuestNpcSession"

    public void BeginSession(GameObject interactor, System.Action onFinish)
    {
        Debug.Log($"[SessionRouter:{name}] 🔀 BeginSession - mode={mode}");
        
        IInteractionSession target = null;

        if (mode == TargetMode.Specific && specific is IInteractionSession s1)
        {
            target = s1;
            Debug.Log($"[SessionRouter:{name}] ✅ Usando sesión específica: {specific.GetType().Name}");
        }
        else if (mode == TargetMode.ByName)
        {
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb == this) continue;
                if (mb.GetType().Name == componentName && mb is IInteractionSession s2) 
                { 
                    target = s2;
                    Debug.Log($"[SessionRouter:{name}] ✅ Encontrada sesión por nombre: {componentName}");
                    break; 
                }
            }
        }
        else // FirstFound
        {
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb != this && mb is IInteractionSession s3) 
                { 
                    target = s3;
                    Debug.Log($"[SessionRouter:{name}] ✅ Encontrada primera sesión: {s3.GetType().Name}");
                    break; 
                }
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[SessionRouter:{name}] ⚠️ No se encontró sesión válida.");
            onFinish?.Invoke();
            return;
        }

        target.BeginSession(interactor, onFinish);
    }
}
