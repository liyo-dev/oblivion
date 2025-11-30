using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida simple que se coloca sobre un NPC en combate.
/// Sigue un objetivo en el mundo y actualiza colores según el ratio de salud.
/// </summary>
public class NPCBattleHealthBar : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Damageable target;
    [SerializeField] private Transform worldTarget;
    [SerializeField] private Image fill;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Ajustes visuales")]
    [SerializeField] private Vector3 worldOffset = new(0f, 2.4f, 0f);
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    [Range(0f, 1f)] [SerializeField] private float warningThreshold = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float criticalThreshold = 0.25f;
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private float followLerp = 12f;

    Camera _camera;

    void Awake()
    {
        _camera = Camera.main;
    }

    void OnEnable()
    {
        Subscribe();
        RefreshImmediate();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    public void Bind(Damageable damageable, Transform targetOverride = null)
    {
        Unsubscribe();
        target = damageable;
        worldTarget = targetOverride ? targetOverride : damageable ? damageable.transform : worldTarget;
        Subscribe();
        RefreshImmediate();
    }

    public void SetOffset(Vector3 offset) => worldOffset = offset;

    public void SetColors(Color healthy, Color warning, Color critical, float warningT, float criticalT)
    {
        healthyColor = healthy;
        warningColor = warning;
        criticalColor = critical;
        warningThreshold = Mathf.Clamp01(warningT);
        criticalThreshold = Mathf.Clamp01(criticalT);
        RefreshImmediate();
    }

    void Subscribe()
    {
        if (target == null) return;
        target.OnDamaged -= HandleDamaged;
        target.OnDamaged += HandleDamaged;
        target.OnDied -= HandleDied;
        target.OnDied += HandleDied;
    }

    void Unsubscribe()
    {
        if (target == null) return;
        target.OnDamaged -= HandleDamaged;
        target.OnDied -= HandleDied;
    }

    void Update()
    {
        if (!worldTarget || _camera == null)
        {
            if (_camera == null)
                _camera = Camera.main;
            return;
        }

        Vector3 targetPos = worldTarget.position + worldOffset;
        Vector3 screenPos = _camera.WorldToScreenPoint(targetPos);
        if (screenPos.z < 0f)
            return;

        transform.position = Vector3.Lerp(transform.position, screenPos, Time.unscaledDeltaTime * followLerp);
    }

    void HandleDamaged(float amount)
    {
        RefreshImmediate();
    }

    void HandleDied()
    {
        RefreshImmediate();
    }

    void RefreshImmediate()
    {
        if (target == null)
            return;

        float ratio = Mathf.Clamp01(target.Current / Mathf.Max(1f, target.Max));
        if (fill != null)
        {
            fill.fillAmount = ratio;
            fill.color = GetColorForRatio(ratio);
        }

        if (canvasGroup != null && hideWhenFull)
            canvasGroup.alpha = ratio >= 0.999f ? 0f : 1f;
    }

    Color GetColorForRatio(float ratio)
    {
        if (ratio <= criticalThreshold)
            return criticalColor;
        if (ratio <= warningThreshold)
            return warningColor;
        return healthyColor;
    }
}
