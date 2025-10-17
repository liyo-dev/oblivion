using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ManaPool : MonoBehaviour
{
    [SerializeField] float max = 0f;
    [SerializeField] float current = 0f;

    [Header("Regeneración de Maná")]
    [Tooltip("Activa la regeneración pasiva de maná")]
    [SerializeField] private bool enableManaRegen = true;
    [Tooltip("Maná por segundo que se regenera")]
    [SerializeField] private float manaRegenPerSecond = 5f;
    [Tooltip("Retraso (segundos) después de gastar maná antes de empezar a regenerar")]
    [SerializeField] private float manaRegenDelayAfterSpend = 1.5f;
    [Tooltip("Evita micro-actualizaciones: margen mínimo de cambio antes de notificar (si algún oyente existiera)")]
    [SerializeField] private float manaRegenNotifyEpsilon = 0.01f;

    [Header("Eventos")]
    [Tooltip("Se dispara con el porcentaje de maná actual (0..1)")]
    public UnityEvent<float> OnManaChanged;

    private float _lastSpendTime = -999f;
    private float _lastNotifiedMana;

    public float Max => max;
    public float Current => current;

    // Llamado por PlayerState al cargar preset/partida
    public void Init(float maxMP, float currentMP)
    {
        Debug.Log($"[ManaPool.Init] On '{gameObject.name}' -> Init(maxMP={maxMP}, currentMP={currentMP}) | prev max={max}, prev current={current}");
        // Mostrar stacktrace para identificar el llamador y el orden de ejecución (temporal, para debugging)
        Debug.Log(System.Environment.StackTrace);
        // opcional: mostrar stacktrace para identificar llamador (descomentar si necesitas más detalle)
        // Debug.Log(Environment.StackTrace);
        max = Mathf.Max(0f, maxMP);
        current = Mathf.Clamp(currentMP, 0f, max);
        _lastNotifiedMana = current;
        NotifyManaChanged();
    }

    // Úsalo desde tu caster de hechizos
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (current < amount) return false;
        current -= amount;
        _lastSpendTime = Time.time;
        NotifyManaChanged();
        return true;
    }

    public void Refill(float amount)
    {
        current = Mathf.Clamp(current + Mathf.Max(0f, amount), 0f, max);
        // No reinicia el retraso; permite seguir regenerando si ya estaba en ello
        NotifyManaChanged();
    }

    void Update()
    {
        if (!enableManaRegen) return;
        if (current >= max) return;

        if (Time.time - _lastSpendTime >= manaRegenDelayAfterSpend)
        {
            float before = current;
            current = Mathf.Min(max, current + Mathf.Max(0f, manaRegenPerSecond) * Time.deltaTime);
            if (current > before && Mathf.Abs(current - _lastNotifiedMana) >= manaRegenNotifyEpsilon)
            {
                NotifyManaChanged();
            }
        }
    }

    private void NotifyManaChanged()
    {
        _lastNotifiedMana = current;
        if (OnManaChanged != null)
        {
            float percent = max > 0f ? (current / max) : 0f;
            OnManaChanged.Invoke(percent);
        }
    }
}