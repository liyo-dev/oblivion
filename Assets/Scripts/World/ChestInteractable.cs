using UnityEngine;
using DG.Tweening;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public class ChestInteractable : MonoBehaviour
{
    [Tooltip("If true, collection will happen when the Interactable.OnFinished is invoked (after dialogue). Otherwise it happens immediately on OnInteract.")]
    [SerializeField] private bool collectOnFinished = false;

    [Header("Lid Animation (optional)")]
    [Tooltip("Transform that represents the lid/top of the chest. Rotation is applied in localEulerAngles.")]
    [SerializeField] private Transform lidTransform;
    [Tooltip("Local Euler angles to rotate the lid to when opened (e.g.  -60 on X to tilt back)")]
    [SerializeField] private Vector3 lidOpenEuler = new Vector3(-60f, 0f, 0f);
    [SerializeField] private float openDuration = 0.4f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [Tooltip("If true, play the open animation before applying the pickup. If false, collect immediately and still play the animation.")]
    [SerializeField] private bool waitForAnimationBeforeCollect = true;

    [Header("SFX (AudioGraphProfile)")]
    [Tooltip("Clave de SFX en el AudioGraphProfile para el sonido de abrir el cofre (ej. 'Chest'). Se reproduce una vez, al iniciar la apertura.")]
    [SerializeField] private string openSfxKey = "Chest";
    [Tooltip("Clave de SFX en el AudioGraphProfile para el sonido de coger el tesoro (ej. 'Coin'). Se reproduce al aplicar el pickup. Si el WorldPickup asociado ya tiene su propio 'Pickup Sfx Key' configurado, deja este campo vacío para no duplicar el sonido.")]
    [SerializeField] private string collectSfxKey = "Coin";

    Interactable _interactable;
    WorldPickup _pickup;
    bool _isAnimating;
    bool _collectedAlready;
    
    [Header("Coins Visuals (optional)")]
    [Tooltip("Container with coin GameObjects that will be shown when the chest opens and animated away when collected.")]
    [SerializeField] private Transform coinsContainer;
    [Tooltip("Particle or VFX prefab to spawn when coins disappear.")]
    [SerializeField] private GameObject coinVfxPrefab;
    [SerializeField] private float coinAppearDuration = 0.25f;
    [SerializeField] private float coinDisappearDuration = 0.28f;
    [SerializeField] private float coinStagger = 0.04f;
    [SerializeField] private Ease coinEase = Ease.OutBack;
    [SerializeField] private bool coinsInitiallyActive = false;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
        _pickup = GetComponent<WorldPickup>() ?? GetComponentInChildren<WorldPickup>();
        if (coinsContainer != null)
        {
            coinsContainer.gameObject.SetActive(coinsInitiallyActive);
            if (!coinsInitiallyActive)
            {
                // ensure children start scaled to zero if initially inactive
                foreach (Transform c in coinsContainer)
                {
                    c.localScale = Vector3.zero;
                }
            }
        }
    }

    IEnumerator Start()
    {
        // Esperar un frame para que WorldPickup aplique su estado persistido en Awake.
        yield return null;
        ApplyPersistedStateIfNeeded();
    }

    void OnEnable()
    {
        if (_interactable == null) _interactable = GetComponent<Interactable>();
        if (_interactable == null) return;

        if (collectOnFinished)
            _interactable.OnFinished.AddListener(OnFinished);
        else
            _interactable.OnInteract.AddListener(OnInteract);

        PlayerPresetService.OnPresetApplied += ApplyPersistedStateIfNeeded;
        if (PlayerPresetService.HasAppliedPreset)
            ApplyPersistedStateIfNeeded();
    }

    void OnDisable()
    {
        PlayerPresetService.OnPresetApplied -= ApplyPersistedStateIfNeeded;

        if (_interactable == null) return;
        if (collectOnFinished)
            _interactable.OnFinished.RemoveListener(OnFinished);
        else
            _interactable.OnInteract.RemoveListener(OnInteract);
    }

    void OnInteract(GameObject interactor)
    {
        if (_collectedAlready) return;
        if (_pickup == null) return;

        var collector = ResolveCollectorFrom(interactor);
        if (collector == null && PlayerService.TryGetComponent(out PlayerPickupCollector cached))
            collector = cached;

        if (lidTransform != null)
        {
            PlayOpenAnimation(() => {
                if (!_collectedAlready && collector != null)
                {
                    _pickup.Collect(collector);
                    _collectedAlready = true;
                    _interactable?.EnableInteraction(false);
                    PlayCollectSfx();
                    AnimateCoinsDisappear();
                }
            }, collectImmediately: !waitForAnimationBeforeCollect);
        }
        else
        {
            if (collector != null)
            {
                PlayOpenSfx();
                _pickup.Collect(collector);
                _collectedAlready = true;
                _interactable?.EnableInteraction(false);
                PlayCollectSfx();
            }
        }
    }

    void OnFinished()
    {
        if (_collectedAlready) return;
        if (_pickup == null) return;

        PlayerPickupCollector cached = null;
        if (PlayerService.TryGetComponent(out PlayerPickupCollector c)) cached = c;

        if (lidTransform != null)
        {
            PlayOpenAnimation(() => {
                if (!_collectedAlready && cached != null)
                {
                    _pickup.Collect(cached);
                    _collectedAlready = true;
                    _interactable?.EnableInteraction(false);
                    PlayCollectSfx();
                    AnimateCoinsDisappear();
                }
            }, collectImmediately: !waitForAnimationBeforeCollect);
        }
        else
        {
            if (cached != null)
            {
                PlayOpenSfx();
                _pickup.Collect(cached);
                _collectedAlready = true;
                _interactable?.EnableInteraction(false);
                PlayCollectSfx();
            }
        }
    }

    void AnimateCoinsDisappear()
    {
        if (coinsContainer == null) return;
        int i = 0;
        Debug.Log("[ChestInteractable] AnimateCoinsDisappear called");
        foreach (Transform c in coinsContainer)
        {
            if (c == null) continue;
            c.gameObject.SetActive(true);
            c.DOKill();
            // optional vfx at coin position
            if (coinVfxPrefab != null)
            {
                var v = Instantiate(coinVfxPrefab, c.position, Quaternion.identity);
                Destroy(v, 2f);
            }
            c.DOScale(Vector3.zero, coinDisappearDuration).SetEase(Ease.InBack).SetDelay(i * coinStagger).OnComplete(() => {
                c.gameObject.SetActive(false);
            });
            i++;
        }
        // Optionally deactivate container after all tweens
        float total = coinDisappearDuration + (Mathf.Max(0, i - 1) * coinStagger) + 0.05f;
        DOVirtual.DelayedCall(total, () => { if (coinsContainer) coinsContainer.gameObject.SetActive(false); });
    }

    void PlayOpenAnimation(System.Action onComplete, bool collectImmediately)
    {
        if (_isAnimating) return;
        if (lidTransform == null)
        {
            onComplete?.Invoke();
            return;
        }

        _isAnimating = true;
        PlayOpenSfx();

        // If we should collect immediately, invoke before animation
        if (collectImmediately)
        {
            onComplete?.Invoke();
        }

        // Animate local rotation to target Euler
        Quaternion start = lidTransform.localRotation;
        Quaternion target = Quaternion.Euler(lidOpenEuler);
        lidTransform.DOLocalRotate(lidOpenEuler, openDuration).SetEase(openEase).OnComplete(() => {
            // Show coins (if any) when lid finished opening
            if (coinsContainer != null)
            {
                Debug.Log("[ChestInteractable] Showing coinsContainer and animating children");
                coinsContainer.gameObject.SetActive(true);
                // animate children appearing
                int i = 0;
                foreach (Transform c in coinsContainer)
                {
                    if (c == null) continue;
                    c.gameObject.SetActive(true);
                    c.DOKill();
                    c.localScale = Vector3.zero;
                    c.DOScale(Vector3.one, coinAppearDuration).SetEase(coinEase).SetDelay(i * coinStagger);
                    i++;
                }

                _isAnimating = false;

                // If we are not collecting immediately, wait until the appear tweens finish
                // so the player can actually see the coins before they disappear.
                if (!collectImmediately)
                {
                    float totalAppearTime = coinAppearDuration + (Mathf.Max(0, i - 1) * coinStagger) + 0.05f;
                    DOVirtual.DelayedCall(totalAppearTime, () => onComplete?.Invoke());
                    return;
                }
            }

            _isAnimating = false;
            if (!collectImmediately)
            {
                onComplete?.Invoke();
            }
        });
    }

    void PlayOpenSfx()
    {
        if (string.IsNullOrEmpty(openSfxKey)) return;
        AudioService.Instance?.PlaySFX(openSfxKey, 1f, transform.position);
    }

    void PlayCollectSfx()
    {
        if (string.IsNullOrEmpty(collectSfxKey)) return;
        AudioService.Instance?.PlaySFX(collectSfxKey, 1f, transform.position);
    }

    PlayerPickupCollector ResolveCollectorFrom(GameObject other)
    {
        if (other == null) return null;

        var c = other.GetComponent<PlayerPickupCollector>();
        if (c != null) return c;

        c = other.GetComponentInParent<PlayerPickupCollector>();
        if (c != null) return c;

        c = other.GetComponentInChildren<PlayerPickupCollector>();
        if (c != null) return c;

        return null;
    }

    void ApplyPersistedStateIfNeeded()
    {
        if (_pickup == null || !_pickup.HasBeenCollected)
            return;

        if (!_collectedAlready)
            _collectedAlready = true;

        if (_interactable != null)
            _interactable.EnableInteraction(false);

        if (lidTransform != null)
        {
            lidTransform.DOKill();
            lidTransform.localRotation = Quaternion.Euler(lidOpenEuler);
        }

        if (coinsContainer != null)
        {
            coinsContainer.gameObject.SetActive(false);
            foreach (Transform c in coinsContainer)
            {
                if (c == null) continue;
                c.DOKill();
                c.localScale = Vector3.zero;
                c.gameObject.SetActive(false);
            }
        }
    }
}
