using System.Collections;
using UnityEngine;

/// Eldran anima a Will durante el combate del despertar:
/// mira hacia Will continuamente y lanza bocadillos con animaciones a intervalos.
/// Añadir al GameObject de Eldran (o a cualquier manager de la escena).
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)] // ejecutar LateUpdate después de NPCSimpleAnimator para ganar la rotación
public class EldranCombatCheerController : MonoBehaviour
{
    [Header("Personajes")]
    [SerializeField] private Transform eldranTransform;
    [SerializeField] private Transform willTransform;

    [Header("Señales narrativas")]
    [SerializeField] private string startSignal = "AWAKEN_DONE";
    [SerializeField] private string stopSignal  = "AWAKEN_COMBAT_END";

    [Header("Bocadillos de ánimo")]
    [SerializeField] private string[] cheerKeys  = { "EVT_ELDRAN_CHEER_01", "EVT_ELDRAN_CHEER_02", "EVT_ELDRAN_CHEER_03" };
    [SerializeField] private string[] cheerAnims = { "Cheer01", "Cheer01", "Cheer01" };
    [SerializeField] private float    bubbleDuration = 2.8f;

    [Header("Timings")]
    [SerializeField] private float firstDelay  = 2.5f;
    [SerializeField] private float minInterval = 6f;
    [SerializeField] private float maxInterval = 14f;

    [Header("Rotación hacia Will")]
    [SerializeField] private float lookAtSpeed = 360f;  // grados/segundo

    private NPCSimpleAnimator _npcAnim;
    private Animator          _animator;
    private Coroutine         _cheerCoroutine;
    private bool              _lookingAtWill;

    void Awake()
    {
        if (eldranTransform != null)
        {
            _npcAnim  = eldranTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _animator = eldranTransform.GetComponentInChildren<Animator>();
        }
    }

    void OnEnable()
    {
        var signals = DefaultNarrativeSignals.EnsureInstance();
        signals.OnCustom(startSignal, StartCheering);
        signals.OnCustom(stopSignal,  StopCheering);
    }

    void OnDisable()
    {
        StopCheering();
        var signals = DefaultNarrativeSignals.Instance;
        if (signals != null)
        {
            signals.OffCustom(startSignal, StartCheering);
            signals.OffCustom(stopSignal,  StopCheering);
        }
    }

    void OnDestroy() => StopCheering();

    // LateUpdate con DefaultExecutionOrder(100) corre después de NPCSimpleAnimator,
    // sobreescribiendo cualquier rotación que el sistema NPC haya aplicado ese frame.
    void LateUpdate()
    {
        if (!_lookingAtWill || eldranTransform == null || willTransform == null) return;

        Vector3 dir = willTransform.position - eldranTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(dir.normalized);
            eldranTransform.rotation = Quaternion.RotateTowards(
                eldranTransform.rotation, target, lookAtSpeed * Time.deltaTime);
        }
    }

    // ── API ───────────────────────────────────────────────────────────────────

    private void StartCheering()
    {
        StopCheering();
        _cheerCoroutine = StartCoroutine(Co_Cheer());

        if (willTransform != null && eldranTransform != null)
        {
            _npcAnim?.DisableAutoRotation();
            _lookingAtWill = true;
        }
    }

    private void StopCheering()
    {
        if (_cheerCoroutine != null) { StopCoroutine(_cheerCoroutine); _cheerCoroutine = null; }
        _lookingAtWill = false;
        _npcAnim?.EnableAutoRotation();
    }

    // ── Corrutinas ────────────────────────────────────────────────────────────

    private IEnumerator Co_Cheer()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            ShowCheer();
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    private void ShowCheer()
    {
        if (cheerKeys == null || cheerKeys.Length == 0) return;

        int    idx  = Random.Range(0, cheerKeys.Length);
        string key  = cheerKeys[idx];
        string anim = idx < cheerAnims.Length ? cheerAnims[idx] : null;

        if (!string.IsNullOrEmpty(anim))
        {
            if (_npcAnim != null)
                _npcAnim.PlaySocialGesture(anim);
            else if (_animator != null)
                _animator.CrossFadeInFixedTime(Animator.StringToHash(anim), 0.1f, 0);
        }

        if (eldranTransform == null || SpeechBubbleUI.Instance == null) return;
        string text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(key, key)
            : key;
        SpeechBubbleUI.Instance.Show(eldranTransform, text, duration: bubbleDuration, speakerName: "Eldran");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular cheer")]
    void SimulateCheer() => ShowCheer();

    [ContextMenu("Iniciar cheers")]
    void EditorStart() => StartCheering();

    [ContextMenu("Detener cheers")]
    void EditorStop() => StopCheering();
#endif
}
