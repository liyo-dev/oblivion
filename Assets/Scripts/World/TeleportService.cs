using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class TeleportService : MonoBehaviour
{
    static TeleportService _inst;

    [Header("Refs")]
    public ScreenFader fader; // opcional

    [Header("Fade Durations")]
    [Min(0f)] public float fadeOut = 0.45f;
    [Min(0f)] public float holdBlack = 0.05f;
    [Min(0f)] public float fadeIn = 0.45f;

    void Awake()
    {
        _inst = this;
        if (!fader) fader = FindFirstObjectByType<ScreenFader>();
    }

    public static void PlaceAtAnchor(GameObject player, string anchorId, bool immediate = false)
    {
        var anchor = SpawnAnchor.FindById(anchorId);
        if (!anchor) { Debug.LogWarning($"SpawnAnchor '{anchorId}' no encontrado"); return; }

        var rot = anchor.facing.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(anchor.facing, Vector3.up)
            : anchor.transform.rotation;

        _inst?.Place(player, anchor.transform.position, rot, immediate);
        // OJO: no guardamos aquí
        SpawnManager.SetCurrentAnchor(anchorId);
    }

    public static void TeleportToAnchor(GameObject player, string anchorId)
    {
        var anchor = SpawnAnchor.FindById(anchorId);
        if (!anchor) { Debug.LogWarning($"Teleport: anchor '{anchorId}' no existe"); return; }
        _inst?.StartCoroutine(_inst.Co_Teleport(player, anchor));
    }

    IEnumerator Co_Teleport(GameObject player, SpawnAnchor anchor)
    {
        if (fader) yield return fader.Co_FadeOut(fadeOut);
        if (holdBlack > 0f) yield return new WaitForSecondsRealtime(holdBlack);

        var rot = anchor.facing.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(anchor.facing, Vector3.up)
            : anchor.transform.rotation;

        Place(player, anchor.transform.position, rot, immediate: true);

        if (fader) yield return fader.Co_FadeIn(fadeIn);

        SpawnManager.SetCurrentAnchor(anchor.anchorId);
        // NO se guarda aquí
    }

    void Place(GameObject player, Vector3 pos, Quaternion rot, bool immediate)
    {
        if (!player) return;

        var cc    = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
        var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>() ?? player.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        var rb    = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>(true);

        if (cc)    cc.enabled = false;
        if (agent) agent.enabled = false;

        player.transform.SetPositionAndRotation(pos, rot);

        if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (agent) agent.enabled = true;
        if (cc)    cc.enabled = true;
    }
}
