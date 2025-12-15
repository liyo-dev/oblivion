using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Script de diagnóstico para problemas de UI.
/// Adjúntalo a cualquier GameObject en la escena del menú.
/// </summary>
public class UIDebugHelper : MonoBehaviour
{
    void Start()
    {
        Debug.Log("===== UI DIAGNOSTICS =====");
        
        // Verificar EventSystem
        EventSystem es = ServiceLocator.Get<EventSystem>(false);
        if (es == null)
        {
            Debug.LogError("[UIDebug] ❌ NO HAY EventSystem en la escena!");
        }
        else
        {
            Debug.Log($"[UIDebug] ✓ EventSystem encontrado: {es.gameObject.name}");
            Debug.Log($"[UIDebug]   - Active: {es.gameObject.activeInHierarchy}");
            Debug.Log($"[UIDebug]   - Enabled: {es.enabled}");
            var inputModule = es.GetComponent<BaseInputModule>();
            if (inputModule == null)
            {
                Debug.LogError("[UIDebug] ❌ EventSystem no tiene Input Module!");
            }
            else
            {
                Debug.Log($"[UIDebug] ✓ Input Module: {inputModule.GetType().Name}");
                Debug.Log($"[UIDebug]   - Enabled: {inputModule.enabled}");
            }
        }

        // Verificar Canvas
        var canvas = ServiceLocator.Get<Canvas>(false);
        if (canvas != null)
        {
            Debug.Log($"[UIDebug]   - {canvas.gameObject.name}");
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Debug.LogError($"[UIDebug] ❌ Canvas '{canvas.gameObject.name}' NO TIENE GraphicRaycaster!");
            }
            else
            {
                Debug.Log($"[UIDebug]   ✓ Tiene GraphicRaycaster (enabled: {raycaster.enabled})");
            }
        }

        // Verificar botones
        var button = ServiceLocator.Get<Button>(false);
        if (button != null)
        {
            Debug.Log($"[UIDebug]   - {button.gameObject.name}");
        }
        {
            Debug.Log($"[UIDebug]   - {button.gameObject.name}");
            Debug.Log($"[UIDebug]     · Active: {button.gameObject.activeInHierarchy}");
            Debug.Log($"[UIDebug]     · Interactable: {button.interactable}");
            Debug.Log($"[UIDebug]     · Raycast Target: {button.GetComponent<Image>()?.raycastTarget}");
            Debug.Log($"[UIDebug]     · OnClick listeners (Persistent): {button.onClick.GetPersistentEventCount()}");
            Debug.Log($"[UIDebug]     · OnClick listeners (Runtime): {button.onClick.GetPersistentEventCount()}");
            
            // Mostrar detalles de cada listener persistente
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                var target = button.onClick.GetPersistentTarget(i);
                var methodName = button.onClick.GetPersistentMethodName(i);
                Debug.Log($"[UIDebug]       → Listener {i}: {target?.GetType().Name}.{methodName}");
            }
        }
        
        Debug.Log("===== END DIAGNOSTICS =====");
    }
    
    void Update()
    {
        // Mostrar información del EventSystem cada 2 segundos
        if (Time.frameCount % 120 == 0)
        {
            EventSystem es = EventSystem.current;
            if (es != null)
            {
                GameObject selected = es.currentSelectedGameObject;
                Debug.Log($"[UIDebug] Selected: {(selected != null ? selected.name : "NONE")}");
            }
        }
    }
}
