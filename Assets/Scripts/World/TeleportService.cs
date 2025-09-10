using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class TeleportService : MonoBehaviour
{
    static TeleportService _inst;
    public ScreenFader fader;

    void Awake(){ _inst = this; if(!fader) fader = FindFirstObjectByType<ScreenFader>(); }

    public static void PlaceAtAnchor(GameObject player, string anchorId, bool immediate=false)
    {
        var anchor = SpawnAnchor.FindById(anchorId);
        if (!anchor){ Debug.LogWarning($"SpawnAnchor '{anchorId}' no encontrado"); return; }
        _inst?.Place(player, anchor.transform.position, anchor.facing.sqrMagnitude>0.001f? Quaternion.LookRotation(anchor.facing, Vector3.up) : anchor.transform.rotation, immediate);
        SpawnManager.SetCurrentAnchor(anchorId);
    }

    public static void TeleportToAnchor(GameObject player, string anchorId, bool saveAfter=true)
    {
        var anchor = SpawnAnchor.FindById(anchorId);
        if (!anchor){ Debug.LogWarning($"Teleport: anchor '{anchorId}' no existe"); return; }
        _inst?.StartCoroutine(_inst.Co_Teleport(player, anchor, saveAfter));
    }

    IEnumerator Co_Teleport(GameObject player, SpawnAnchor anchor, bool saveAfter)
    {
        if (fader) yield return fader.Co_FadeOut(0.2f);
        Place(player, anchor.transform.position, anchor.transform.rotation, immediate:true);
        if (fader) yield return fader.Co_FadeIn(0.2f);
        SpawnManager.SetCurrentAnchor(anchor.anchorId);
        if (saveAfter) FindFirstObjectByType<SpawnManager>()?.SaveNow();
    }

    void Place(GameObject player, Vector3 pos, Quaternion rot, bool immediate)
    {
        if (!player) return;
        var cc = player.GetComponent<CharacterController>();
        var rb = player.GetComponent<Rigidbody>();
        var agent = player.GetComponent<NavMeshAgent>();

        if (cc) cc.enabled = false;
        if (agent) agent.enabled = false;

        player.transform.SetPositionAndRotation(pos, rot);

        if (rb){ rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (agent) agent.enabled = true;
        if (cc) cc.enabled = true;

        // refrescar cámara si usas Cinemachine: CinemachineBrain?.ManualUpdate();
    }
}
