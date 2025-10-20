using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class AutoSaveTrigger : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Si se indica, fija este anchor como último antes de guardar; si se deja vacío conserva el actual.")]
    public string anchorIdToSet;

    [Tooltip("Curar al jugador al guardar")] public bool healOnSave = true;

    [Header("Teletransporte opcional")] public bool teleportAfterSave;
    public string teleportAnchorId;

    [Header("One-shot")] public string oneShotFlag;
    public bool disableAfterUse = true;

    [Header("Eventos")] public UnityEvent<GameObject> onSaved;

    private bool _pendingSave;
    private GameObject _pendingPlayer;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void Awake()
    {
        if (onSaved == null) onSaved = new UnityEvent<GameObject>();
    }

    void OnEnable() { GameBootService.OnProfileReady += HandleProfileReady; }
    void OnDisable() { GameBootService.OnProfileReady -= HandleProfileReady; }

    private void HandleProfileReady()
    {
        if (_pendingSave && _pendingPlayer)
        {
            DoSave(_pendingPlayer);
            _pendingSave = false;
            _pendingPlayer = null;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameBootService.IsAvailable)
        {
            DoSave(other.gameObject);
        }
        else
        {
            _pendingSave = true;
            _pendingPlayer = other.gameObject;
            Debug.Log("[AutoSaveTrigger] Perfil no listo. Guardado diferido hasta OnProfileReady");
        }
    }

    private void DoSave(GameObject playerGo)
    {
        // One-shot via flag
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (!string.IsNullOrEmpty(oneShotFlag) && preset != null && preset.flags != null && preset.flags.Contains(oneShotFlag))
        {
            if (disableAfterUse) gameObject.SetActive(false);
            return;
        }

        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
            Debug.LogError("[AutoSaveTrigger] GameBootProfile no disponible");
            return;
        }

        if (!string.IsNullOrEmpty(anchorIdToSet))
            SpawnManager.SetCurrentAnchor(anchorIdToSet);

        if (healOnSave && playerGo)
        {
            var playerHealth = playerGo.GetComponent<PlayerHealthSystem>() ?? playerGo.GetComponentInParent<PlayerHealthSystem>();
            if (playerHealth != null) playerHealth.SetCurrentHealth(playerHealth.MaxHealth);

            var manaPool = playerGo.GetComponent<ManaPool>() ?? playerGo.GetComponentInParent<ManaPool>();
            if (manaPool != null) manaPool.Init(manaPool.Max, manaPool.Max);
        }

        // Ejecutar animación LevelUp_NoWeapon directamente
        var animator = playerGo.GetComponent<Animator>() ?? playerGo.GetComponentInParent<Animator>();
        if (animator != null)
        {
            animator.Play("LevelUp_NoWeapon");
        }

        var saveSystem = FindFirstObjectByType<SaveSystem>();
        if (saveSystem != null)
        {
            bool ok = bootProfile.SaveCurrentGameState(saveSystem, SaveRequestContext.Auto);
            if (!ok)
            {
                if (!bootProfile.allowAutoSaves)
                {
                    Debug.Log("[AutoSaveTrigger] Auto-guardado omitido (allowAutoSaves = false).");
                }
                else
                {
                    Debug.LogError("[AutoSaveTrigger] Error al guardar");
                }
            }
            else
            {
                // add one-shot flag after success
                if (!string.IsNullOrEmpty(oneShotFlag) && preset != null)
                {
                    if (preset.flags == null) preset.flags = new System.Collections.Generic.List<string>();
                    if (!preset.flags.Contains(oneShotFlag)) preset.flags.Add(oneShotFlag);
                }
                Debug.Log("[AutoSaveTrigger] Guardado automático completado");
                onSaved?.Invoke(playerGo);
            }
        }
        else
        {
            Debug.LogError("[AutoSaveTrigger] No se encontró SaveSystem en escena");
        }

        if (teleportAfterSave && !string.IsNullOrEmpty(teleportAnchorId) && playerGo)
        {
            SpawnManager.TeleportTo(teleportAnchorId, true);
        }

        if (disableAfterUse) gameObject.SetActive(false);
    }
}
