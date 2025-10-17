using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Script de prueba para testear el sistema de salud del jugador
/// Compatible con el nuevo Input System
/// </summary>
public class PlayerHealthTester : MonoBehaviour
{
    [Header("Testing")]
    [SerializeField] private Key damageKey = Key.X;
    [SerializeField] private Key healKey = Key.Z;
    [SerializeField] private Key killKey = Key.K;
    [SerializeField] private Key reviveKey = Key.R;
    [SerializeField] private Key godModeKey = Key.G;
    [SerializeField] private float testDamageAmount = 20f;
    [SerializeField] private float testHealAmount = 15f;
    
    [Header("Referencias (opcional)")]
    [SerializeField] private PlayerHealthSystem playerHealthSystem;
    
    // Cache para el Keyboard
    private Keyboard _keyboard;
    
    void Start()
    {
        // Auto-encontrar si no se asignó
        if (playerHealthSystem == null)
        {
            playerHealthSystem = FindFirstObjectByType<PlayerHealthSystem>();
        }

        if (playerHealthSystem == null)
        {
            Debug.LogWarning("[HealthTester] No se encontró PlayerHealthSystem en la escena");
        }

        // Obtener referencia al teclado
        _keyboard = Keyboard.current;

        if (_keyboard == null)
        {
            Debug.LogWarning("[HealthTester] No se encontró teclado. Asegúrate de que el Input System esté configurado correctamente.");
        }
    }
    
    void Update()
    {
        if (playerHealthSystem == null || _keyboard == null) return;
        
        // Verificar inputs usando el nuevo Input System
        if (_keyboard[damageKey].wasPressedThisFrame)
        {
            playerHealthSystem.TakeDamage(testDamageAmount);
            Debug.Log($"[HealthTester] Aplicando {testDamageAmount} de daño de prueba");
        }
        
        if (_keyboard[healKey].wasPressedThisFrame)
        {
            playerHealthSystem.Heal(testHealAmount);
            Debug.Log($"[HealthTester] Aplicando {testHealAmount} de curación de prueba");
        }
        
        if (_keyboard[killKey].wasPressedThisFrame)
        {
            playerHealthSystem.Kill();
            Debug.Log("[HealthTester] Matando jugador de prueba");
        }
        
        if (_keyboard[reviveKey].wasPressedThisFrame)
        {
            playerHealthSystem.Revive(); // Revivir con valor por defecto (100%)
            Debug.Log("[HealthTester] Reviviendo jugador con vida completa");
        }
        
        if (_keyboard[godModeKey].wasPressedThisFrame)
        {
            bool newGodMode = !playerHealthSystem.IsGodModeActive;
            playerHealthSystem.SetGodMode(newGodMode);
            Debug.Log($"[HealthTester] God Mode {(newGodMode ? "activado" : "desactivado")}");
        }
    }
    
    void OnGUI()
    {
        if (playerHealthSystem == null) return;

        // Calcular posición en la izquierda, centrado verticalmente
        float guiWidth = 350f;
        float guiHeight = 180f;
        float margin = 10f;

        // Posicionar pegado al borde izquierdo y centrado vertical
        float x = margin;
        float y = (Screen.height - guiHeight) * 0.5f;

        Rect guiRect = new Rect(x, y, guiWidth, guiHeight);

        GUILayout.BeginArea(guiRect);
        GUILayout.Label("=== PLAYER HEALTH TESTER ===");
        GUILayout.Label($"Salud: {playerHealthSystem.CurrentHealth:0}/{playerHealthSystem.MaxHealth:0} ({playerHealthSystem.HealthPercentage:P1})");
        GUILayout.Label($"Estado: {(playerHealthSystem.IsAlive ? "VIVO" : "MUERTO")}");
        GUILayout.Label($"Invulnerable: {(playerHealthSystem.IsInvulnerable ? "SÍ" : "NO")}");
        GUILayout.Label($"God Mode: {(playerHealthSystem.IsGodModeActive ? "ACTIVADO" : "DESACTIVADO")}");
        GUILayout.Space(5);
        GUILayout.Label($"Controles (Input System):");
        GUILayout.Label($"'{damageKey}' - Hacer daño ({testDamageAmount})");
        GUILayout.Label($"'{healKey}' - Curar ({testHealAmount})");
        GUILayout.Label($"'{killKey}' - Matar jugador");
        GUILayout.Label($"'{reviveKey}' - Revivir jugador");
        GUILayout.Label($"'{godModeKey}' - Toggle God Mode");
        GUILayout.EndArea();
    }
}
