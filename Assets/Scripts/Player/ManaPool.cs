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

            // FIX: el frame en que se alcanza el máximo tiene que notificar SIEMPRE, aunque el
            // incremento sea menor que manaRegenNotifyEpsilon. Si no, la última notificación que
            // llega a la UI se queda justo por debajo de 1.0 y nunca se avisa del "lleno real":
            // PlayerHUDV2 interpola con Lerp hacia ese fillAmount < 1 (que jamás llega a 1
            // exactamente porque el propio Update() de aquí ya no vuelve a llamar tras
            // "current >= max") y el sprite de la barra se queda recortado justo en la puntita
            // redondeada del extremo derecho, que solo se ve completa con fillAmount == 1.
            bool reachedFull = current >= max && before < max;
            if (current > before && (reachedFull || Mathf.Abs(current - _lastNotifiedMana) >= manaRegenNotifyEpsilon))
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