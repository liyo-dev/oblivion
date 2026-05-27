using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que inicia un combate entre un NPC y el jugador.
/// Prepara al NPC para combate (layer, damageable, combat lifecycle) y
/// opcionalmente espera a que el combate termine emitiendo un evento de derrota.
/// </summary>
[Serializable]
[UnsafeForSave("No guardar durante el combate")]
public sealed class StartCombatNode : NarrativeNode
{
    [Header("NPC")]
    [Tooltip("ID narrativo del NPC que entra en combate.")]
    public string npcId;

    [Header("Combate")]
    [Tooltip("Configuración de combate del NPC (ScriptableObject con stats, drops, etc.)")]
    public Game.NPC.Modules.NPCCombatConfig combatConfig;

    [Header("Evento al derrotar")]
    [Tooltip("Si se marca, se emite un evento custom al derrotar al NPC.")]
    public bool sendEventOnDefeat;

    [Tooltip("Clave del evento a emitir cuando el NPC es derrotado.")]
    public string defeatEventKey;

    [Tooltip("Emitir el evento antes de la animación de muerte (true) o después (false).")]
    public bool sendDefeatEventBeforeDeath;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("[StartCombat] npcId vacío → avanzando");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (combatConfig == null)
        {
            Debug.LogWarning("[StartCombat] combatConfig no asignado → avanzando");
            onReadyToAdvance?.Invoke();
            return;
        }

        ctx.Runner.StartCoroutine(PrepareCombat(ctx, onReadyToAdvance));
    }

    IEnumerator PrepareCombat(NarrativeContext ctx, Action onReadyToAdvance)
    {
        Game.NPC.NPCBehaviourManagerV2 npcManager = null;

        var bridge = Game.NPC.NPCGraphBridgeRegistry.Get(npcId);
        if (bridge != null)
            npcManager = bridge.NpcManager;

        if (npcManager == null && Game.NPC.NPCRegistry.HasInstance)
            npcManager = Game.NPC.NPCRegistry.Instance.GetNPCByID(npcId);

        if (npcManager == null)
        {
            Debug.LogWarning($"[StartCombat] NPC '{npcId}' no encontrado → avanzando");
            onReadyToAdvance?.Invoke();
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[StartCombat:{guid}] Preparando combate con NPC '{npcId}'");
#endif

        // Cambiar a layer de enemigo
        var go = npcManager.gameObject;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            go.layer = enemyLayer;

        // Configurar combate
        npcManager.Configuration.combatConfig = combatConfig;

        if (sendEventOnDefeat && !string.IsNullOrWhiteSpace(defeatEventKey))
        {
            combatConfig.sendEventOnDefeat = true;
            combatConfig.defeatEventKey = defeatEventKey;
            combatConfig.sendDefeatEventBeforeDeath = sendDefeatEventBeforeDeath;
        }

        // Añadir componentes de combate si no existen
        if (go.GetComponent<Damageable>() == null)
        {
            var dmg = go.AddComponent<Damageable>();
            dmg.SetMaxAndCurrent(combatConfig.health, combatConfig.health);
            dmg.SetDestroyOnDeath(false);
        }

        if (go.GetComponent<Game.NPC.Modules.NPCCombatLifecycleHandler>() == null)
        {
            go.AddComponent<Game.NPC.Modules.NPCCombatLifecycleHandler>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[StartCombat:{guid}] NPC '{npcId}' preparado para combate. " +
            "La detección natural del jugador iniciará el combate.");
#endif

        // El nodo avanza inmediatamente - el combate se inicia por detección natural
        // Si se necesita esperar al resultado, usar WaitCustomEventNode con defeatEventKey
        onReadyToAdvance?.Invoke();
    }
}
