using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class ActiveStateDebugger : MonoBehaviour
{
    void OnEnable()
    {
        Debug.Log($"[ActiveStateDebugger] OnEnable on '{gameObject.name}' (activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy})");
    }

    void OnDisable()
    {
        Debug.LogWarning($"[ActiveStateDebugger] OnDisable on '{gameObject.name}' (activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy})\nStack:\n" + new StackTrace(true).ToString());
    }
}
