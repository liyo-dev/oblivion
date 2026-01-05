using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Inventory))]
public class PlayerPickupCollector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHealthSystem healthSystem;
    [SerializeField] private ManaPool manaPool;
    [SerializeField] private SpecialChargeMeter specialChargeMeter;
    [SerializeField] private Animator animator;

    [Header("Animation")]
    [SerializeField] private string drinkPotionAnimationName = "DrinkPotion_NoWeapon";

    [Header("Settings")]
    [SerializeField] private bool logWarnings;

    [Header("Events")]
    [SerializeField] private UnityEvent<PickupEffectType> onEffectApplied;
    [SerializeField] private UnityEvent onAnyPickup;

    void Awake()
    {
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!healthSystem) healthSystem = GetComponentInChildren<PlayerHealthSystem>(true);
        if (!manaPool) manaPool = GetComponentInChildren<ManaPool>(true);
        if (!specialChargeMeter) specialChargeMeter = GetComponentInChildren<SpecialChargeMeter>(true);
        if (!animator) animator = GetComponentInChildren<Animator>(true);

        PlayerService.RegisterComponent(this);
    }

    /// <summary>
    /// Applies a pickup effect. Returns true when the effect modified the player state.
    /// The caller can decide whether the pickup should be consumed when false is returned.
    /// </summary>
    public bool TryCollect(PickupEffect effect, out bool shouldConsume)
    {
        bool changed = effect.effectType switch
        {
            PickupEffectType.Currency => ApplyCurrency(effect),
            PickupEffectType.ManaRestore => ApplyMana(effect),
            PickupEffectType.HealthRestore => ApplyHealth(effect),
            PickupEffectType.SpecialCharge => ApplySpecialCharge(effect),
            PickupEffectType.AddToInventory => ApplyAddToInventory(effect),
            _ => false
        };

        shouldConsume = changed || effect.consumeEvenIfNoChange;

        if (changed)
        {
            onEffectApplied?.Invoke(effect.effectType);
            onAnyPickup?.Invoke();
        }

        return changed;
    }

    private bool ApplyCurrency(PickupEffect effect)
    {
        if (!inventory)
        {
            LogMissingComponent(nameof(Inventory));
            return false;
        }

        if (!effect.item)
        {
            if (logWarnings) Debug.LogWarning("[PlayerPickupCollector] Currency pickup has no ItemData assigned.");
            return false;
        }

        int quantity = effect.GetQuantityOrDefault();
        if (quantity <= 0) return false;

        inventory.Add(effect.item, quantity);
        return true;
    }

    private bool ApplyMana(PickupEffect effect)
    {
        if (!manaPool)
        {
            LogMissingComponent(nameof(ManaPool));
            return false;
        }

        float amount = effect.GetAmountOrFallback();
        if (amount <= 0f) return false;

        float before = manaPool.Current;
        manaPool.Refill(amount);
        bool restored = manaPool.Current > before + Mathf.Epsilon;
        
        // Reproducir animación de beber poción si se restauró maná
        if (restored)
            PlayDrinkPotionAnimation();
        
        return restored;
    }

    private bool ApplyHealth(PickupEffect effect)
    {
        if (!healthSystem)
        {
            LogMissingComponent(nameof(PlayerHealthSystem));
            return false;
        }

        float amount = effect.GetAmountOrFallback();
        if (amount <= 0f) return false;

        bool healed = healthSystem.Heal(amount);
        
        // Reproducir animación de beber poción si se curó
        if (healed)
            PlayDrinkPotionAnimation();
        
        return healed;
    }

    private bool ApplySpecialCharge(PickupEffect effect)
    {
        if (!specialChargeMeter)
        {
            LogMissingComponent(nameof(SpecialChargeMeter));
            return false;
        }

        float amount = effect.GetAmountOrFallback();
        if (amount <= 0f) return false;

        return specialChargeMeter.AddCharge(amount);
    }

    private bool ApplyAddToInventory(PickupEffect effect)
    {
        if (!inventory)
        {
            LogMissingComponent(nameof(Inventory));
            return false;
        }

        if (!effect.item)
        {
            if (logWarnings) Debug.LogWarning("[PlayerPickupCollector] AddToInventory pickup has no ItemData assigned.");
            return false;
        }

        int quantity = effect.GetQuantityOrDefault();
        if (quantity <= 0) return false;

        // Añadir el item al inventario (para items de historia/quest como botas, llaves, etc.)
        inventory.Add(effect.item, quantity);
        Debug.Log($"[PlayerPickupCollector] ✅ Item añadido al inventario: {effect.item.displayName} x{quantity}");
        return true;
    }

    private void LogMissingComponent(string componentName)
    {
        if (!logWarnings) return;
        Debug.LogWarning($"[PlayerPickupCollector] Missing required component '{componentName}' on '{name}'.");
    }

    private void PlayDrinkPotionAnimation()
    {
        if (animator == null)
        {
            if (logWarnings)
                Debug.LogWarning("[PlayerPickupCollector] No se puede reproducir animación de poción: Animator no encontrado.");
            return;
        }

        if (string.IsNullOrEmpty(drinkPotionAnimationName))
        {
            if (logWarnings)
                Debug.LogWarning("[PlayerPickupCollector] No se puede reproducir animación de poción: nombre de animación vacío.");
            return;
        }

        // Usar corrutina para dar tiempo al Animator Controller de procesar cambios
        StartCoroutine(PlayDrinkPotionAnimationCoroutine());
    }

    private System.Collections.IEnumerator PlayDrinkPotionAnimationCoroutine()
    {
        // Resetear TODOS los parámetros del animator a valores por defecto
        foreach (var param in animator.parameters)
        {
            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(param.name, 0f);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(param.name, 0);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(param.name, false);
                    break;
            }
        }

        // Esperar 1 frame para que el Animator Controller procese los cambios
        yield return null;

        // Ahora reproducir la animación - el animator ya debería estar en idle limpio
        animator.PlayInFixedTime(drinkPotionAnimationName, 0, 0f);
        
        Debug.Log($"[PlayerPickupCollector] Animación reproducida: {drinkPotionAnimationName} (con delay de 1 frame)");
    }
}

