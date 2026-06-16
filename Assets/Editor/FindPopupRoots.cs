using UnityEditor;
using UnityEngine;

public static class FindPopupRoots
{
    [MenuItem("El Sendero/Debug/Find AbilityPopup popupRoot Assignments")]
    public static void Find()
    {
        var popups = ServiceLocator.GetAll<AbilityUnlockPopupUI>();
        Debug.Log($"[FindPopupRoots] Found {popups.Count} AbilityUnlockPopupUI instances:");
        foreach (var p in popups)
        {
            var f = p.GetType().GetField("popupRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var val = f != null ? f.GetValue(p) as GameObject : null;
            Debug.Log($"- {p.gameObject.name} -> popupRoot={(val!=null?val.name:"null")}");
        }
    }
}
