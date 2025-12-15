using UnityEngine;

/// <summary>
/// Script de prueba para testear el sistema de salud del jugador
/// Compatible con el nuevo Input System
/// </summary>
public class PlayerHealthTester : MonoBehaviour
{
    [Header("Testing")]
    [SerializeField] private float testDamageAmount = 20f;
    [SerializeField] private float testHealAmount = 15f;
    
    [Header("Referencias (opcional)")]
    [SerializeField] private PlayerHealthSystem playerHealthSystem;
    
    void Start()
    {
        // Auto-encontrar si no se asignó
        if (playerHealthSystem == null)
        {
            playerHealthSystem = ServiceLocator.Get<PlayerHealthSystem>(false);
        }

        if (playerHealthSystem == null)
        {
            Debug.LogWarning("[HealthTester] No se encontró PlayerHealthSystem en la escena");
        }
    }

    public void ApplyTestDamage()
    {
        if (playerHealthSystem == null) return;

        playerHealthSystem.TakeDamage(testDamageAmount);
        Debug.Log($"[HealthTester] Aplicando {testDamageAmount} de daño de prueba");
    }

    public void ApplyTestHeal()
    {
        if (playerHealthSystem == null) return;

        playerHealthSystem.Heal(testHealAmount);
        Debug.Log($"[HealthTester] Aplicando {testHealAmount} de curación de prueba");
    }

    public void KillPlayer()
    {
        if (playerHealthSystem == null) return;

        playerHealthSystem.Kill();
        Debug.Log("[HealthTester] Matando jugador de prueba");
    }

    public void RevivePlayer()
    {
        if (playerHealthSystem == null) return;

        playerHealthSystem.Revive();
        Debug.Log("[HealthTester] Reviviendo jugador con vida completa");
    }

    public void ToggleGodMode()
    {
        if (playerHealthSystem == null) return;

        bool newGodMode = !playerHealthSystem.IsGodModeActive;
        playerHealthSystem.SetGodMode(newGodMode);
        Debug.Log($"[HealthTester] God Mode {(newGodMode ? "activado" : "desactivado")}");
    }
}
