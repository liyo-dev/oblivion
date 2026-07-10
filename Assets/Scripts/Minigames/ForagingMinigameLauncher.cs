using UnityEngine;

/// <summary>
/// Trigger en la entrada del reino. Si la misión requerida está activa,
/// al cruzarlo lanza el minijuego de recogida (ForagingMinigameController).
/// Se desactiva tras el primer uso exitoso.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class ForagingMinigameLauncher : MonoBehaviour
{
    [Header("Minijuego")]
    [Tooltip("Controlador del minijuego a lanzar.")]
    [SerializeField] private ForagingMinigameController controller;

    [Header("Requisito de misión")]
    [Tooltip("El trigger solo funciona si esta misión está activa.")]
    [SerializeField] private QuestRequirement questRequirement;

    [Header("Comportamiento")]
    [Tooltip("Si true, el trigger se desactiva tras el primer uso exitoso.")]
    [SerializeField] private bool disableAfterUse = true;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!questRequirement.IsSatisfied()) return;
        if (controller == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ForagingMinigameLauncher:{name}] Sin referencia a ForagingMinigameController.");
#endif
            return;
        }

        if (disableAfterUse)
            gameObject.SetActive(false);

        controller.StartMinigame();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
#endif
}
