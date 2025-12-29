# 🐛 Resumen de Problemas Corregidos - 29/12/2024

## ✅ 1. Verificación Automática de Items en Inventario al Iniciar Quests

### 📋 Problema
- **Escenario:** El jugador compra/encuentra una poción **ANTES** de iniciar una quest
- **Quest 5:** "Consígueme una poción, una capa y un trofeo"  
- **Error:** La quest NO detectaba que el jugador ya tenía la poción
- **Esperado:** El paso "Conseguir Poción" debe estar **automáticamente completado**

### 🔧 Solución Implementada

#### Archivos modificados:
1. `Assets/Scripts/Behaviour NPC/Modules/QuestChainEntry.cs`
2. `Assets/Scripts/Quests/QuestManager.cs`

#### Cambios clave:

**1. QuestChainEntry - Soporte para múltiples items:**
```csharp
[Header("Verificación de Inventario - Múltiples Items")]
public ItemRequirement[] requiredItems = System.Array.Empty<ItemRequirement>();

[Serializable]
public class ItemRequirement
{
    public ItemData item;
    [Min(1)] public int amount = 1;
    public string stepConditionId = "";  // ej: "ITEM_Potion"
    public int stepIndex = -1;            // -1 = auto-detectar
}
```

**2. QuestManager - Verificación automática:**
```csharp
public void StartQuest(string questId)
{
    // ...código existente...
    if (rq.State == QuestState.Inactive)
    {
        rq.State = QuestState.Active;
        
        // ✅ NUEVO: Verificar items existentes
        CheckExistingItemsForQuest(rq);
        
        OnQuestStarted?.Invoke(questId);
        OnQuestsChanged?.Invoke();
    }
}

private void CheckExistingItemsForQuest(RuntimeQuest rq)
{
    // Obtiene inventario usando PlayerService (NUNCA FindObjectOfType)
    if (!PlayerService.TryGetComponent(out Inventory inventory, ...))
        return;
    
    // Verifica cada step con conditionId "ITEM_*"
    foreach (var step in rq.Steps)
    {
        if (step.conditionId.StartsWith("ITEM_"))
        {
            string itemId = step.conditionId.Substring(5);
            if (inventory.Count(itemId) > 0)
                step.completed = true; // ✅ Auto-completado
        }
    }
}
```

### 🎯 Resultado
- ✅ Si el jugador ya tiene items requeridos → pasos marcados automáticamente
- ✅ Si todos los items están en inventario → quest completada automáticamente
- ✅ Funciona para **múltiples items** por quest
- ✅ **NO usa FindObjectOfType** (usa PlayerService)

---

## 🚧 2. Otros Problemas Identificados (Pendientes de revisión)

### A. SpawnAnchor - Rotación incorrecta del Player

**Problema:**
- SpawnAnchor con `faceDoor = false` hace que el player mire **hacia** la puerta
- Debería ser al revés: `faceDoor = false` → de **espaldas** a la puerta

**Ubicación:**
- Componente: `SpawnAnchor` (script desconocido)
- Objeto: `House_FrontDoor_Tutorial`

**Logs relevantes:**
```
[NPC:Oliver] [CinematicSequence] Orientado desde SpawnAnchor 'House_FrontDoor_Tutorial' (away)
[NPC:Oliver] [CinematicSequence] SpawnAnchor 'House_FrontDoor_Tutorial': faceDoor=False
[NPC:Oliver] [CinematicSequence] Anchor forward: (-1.00, 0.00, 0.00)
```

**Soluciones sugeridas:**
1. Invertir la lógica de `faceDoor` en el código
2. O renombrar variable a `backToDoor` para claridad

**Archivos a revisar:**
- Buscar clase `SpawnAnchor`
- `Assets/Scripts/Behaviour NPC/States/CinematicState.cs` (ApplySpawnAnchorOrientation)

---

### B. NPC se gira incorrectamente después de llegar a destino

**Problema:**
- NPC llega a su destino y se coloca bien
- Medio segundo después se gira en dirección errónea
- Ocurre con `turnAroundOnArrival = false`

**Logs relevantes:**
```
[NPC:Oliver] [CinematicSequence] Destino alcanzado naturalmente
[NPC:Oliver] [CinematicSequence] Orientado desde SpawnAnchor 'House_FrontDoor_Tutorial' (away)
[NPC:Oliver] [CinematicSequence] ✅ Rotación objetivo del animator sincronizada
// Medio segundo después...
[DialogueManager] ✅ Rotación automática de NPCSimpleAnimator reactivada para 'Oliver'
```

**Posible causa:**
- Conflicto entre `NPCSimpleAnimator` y `DialogueManager` en control de rotación
- `DialogueManager.MaintainNpcRotationAfterDialogue` podría estar sobrescribiendo

**Archivos a revisar:**
- `Assets/Scripts/Dialogue/DialogueManager.cs` (línea ~942-965)
- `Assets/Scripts/Behaviour NPC/NPCSimpleAnimator.cs`
- `Assets/Scripts/Behaviour NPC/States/CinematicState.cs` (ApplySpawnAnchorOrientation)

---

### C. CollectiblePopupQueue se queda en pantalla

**Problema:**
- Los popups de items coleccionados se acumulan en pantalla
- No desaparecen automáticamente

**Logs relevantes:**
```
[CollectiblePopupQueue] Unbound from Inventory
CollectiblePopupQueue:UnbindFromInventory ()
CollectiblePopupQueue:OnDestroy ()
```

**Posible causa:**
- Falta lógica de auto-cierre después de X segundos
- O falta llamada a `Destroy()` del popup después de mostrarse

**Archivos a revisar:**
- `Assets/Scripts/UI/CollectiblePopupQueue.cs`
- `Assets/Scripts/UI/CollectiblePopupPanel.cs` (si existe)

---

### D. Diálogo de alerta de combate se ejecuta dos veces

**Problema:**
- Al iniciar combate con Erika, el diálogo de alerta aparece **2 veces**:
  1. Antes de que inicie la música de combate
  2. Durante la alerta de combate (correcta)

**Logs relevantes:**
```
[NPC:Erika] [IdleState] 👁️ Jugador visto. ¡Alerta!
[NPC:Erika] [AlertState] ⚠️ INICIANDO ALERTA
[DialogueManager] ⚔️ Preparando jugador para diálogo de batalla con 'Erika'
[DialogueManager] 🕐 Diálogo abierto en t=133,355
// Aparece dos veces
```

**Configuración actual:**
```
NPCCombatConfig:
├─ Dialogue On Alert: DLG_GUERRERACHINA_ALERT
├─ Alert Music Event: Npc_Battle_Alert
└─ Wait For Alert Dialogue: [verificar]
```

**Solución esperada:**
- Diálogo debe aparecer **SOLO durante** la alerta con música
- Eliminar diálogo previo

**Archivos a revisar:**
- `Assets/Scripts/Behaviour NPC/States/AlertState.cs`
- `Assets/Scripts/Dialogue/DialogueManager.cs` (PreparePlayerForBattleDialogue)

---

### E. NPC de combate no muere con animación Dizzy

**Problema:**
- NPC configurado con `postDeathBehavior: Get Up Dizzy`
- Al quitarle toda la vida, **no reproduce animación de dizzy**
- Se destruye directamente

**Configuración actual:**
```
Damageable:
├─ destroyOnDeath: false
└─ (debería activar dizzy)

NPCCombatConfig:
└─ Post Death Behavior: Get Up Dizzy
```

**Logs relevantes:**
```
[Damageable:Erika] 💀 VIDA AGOTADA - Llamando a Die()
[Damageable:Erika] OnDied invocado - destroyOnDeath: False
// No hay logs de dizzy state
```

**Posible causa:**
- `Damageable.Die()` no está comunicándose con `NPCCombatBrain`
- Falta transición a estado `Dizzy` en `NPCCombatBrain`

**Archivos a revisar:**
- `Assets/Scripts/Health/Damageable.cs` (Die())
- `Assets/Scripts/Behaviour NPC/NPCCombatBrain.cs`
- `Assets/Scripts/Behaviour NPC/States/CombatState.cs`

---

### F. Condición narrativa incorrecta para Victoria

**Problema:**
- NPC Victoria tiene 2 narrativas condicionales:
  1. `Quest Not Started`: DG_VICTORIA_BEFORE
  2. `Quest Active/Completed`: DG_VICTORIA_PROGRESS
  
- **Error:** Entra por narrativa "Quest Active" cuando la quest **NO ha empezado**

**Configuración actual:**
```
Conditional Narratives:
└─ Element 0:
    ├─ Condition Type: Quest Not Started
    ├─ Target Quest: Q_ELDRAN_MISSION5
    └─ Narrative: DG_VICTORIA_BEFORE ✅ (debería activarse)
```

**Logs relevantes:**
```
[Interactable:Victoria] ✅ Iniciando diálogo: DG_VICTORIA
// Pero muestra: "Asi que te manda Eldran... en qué estará metido ahora..."
// (diálogo de quest iniciada, NO el diálogo "before")
```

**Posible causa:**
- Bug en sistema de evaluación de condiciones narrativas
- `QuestManager.GetState()` devolviendo estado incorrecto

**Archivos a revisar:**
- `Assets/NarrativeGraph/Runtime/Conditions/QuestConditionNode.cs`
- `Assets/Scripts/Quests/QuestManager.cs` (GetState)
- `Assets/Scripts/Behaviour NPC/Modules/NPCInteractiveNarrativeExecutor.cs`

---

## 📝 Próximos Pasos

### Prioridad Alta 🔴
1. ✅ **Verificación de items en inventario** (RESUELTO)
2. 🔧 **Victoria - Condición narrativa incorrecta** (afecta gameplay)
3. 🔧 **NPC combate - No reproduce dizzy al morir** (afecta gameplay)

### Prioridad Media 🟡
4. 🔧 **Diálogo de alerta duplicado** (molesto pero no crítico)
5. 🔧 **SpawnAnchor - Rotación invertida** (confuso pero workaround posible)

### Prioridad Baja 🟢
6. 🔧 **CollectiblePopup no desaparece** (visual, no rompe juego)
7. 🔧 **NPC se gira incorrectamente después de moverse** (estético)

---

## 🛠️ Convenciones del Proyecto

### ❌ NUNCA usar:
```csharp
var inventory = FindObjectOfType<Inventory>();  // ❌ PROHIBIDO
```

### ✅ Siempre usar:
```csharp
if (PlayerService.TryGetComponent(out Inventory inventory, includeInactive: true, allowSceneLookup: true))
{
    // Usar inventory aquí
}
```

### Acceso a componentes del jugador:
- **PlayerService.TryGetComponent<T>(...)** → Para componentes en el player
- **QuestManager.Instance** → Para quests
- **DialogueManager.Instance** → Para diálogos
- **NUNCA FindObjectOfType** → Muy costoso y poco fiable

---

## 🗑️ UPDATE 29/12/2024 - Sistema Antiguo Eliminado

### Limpieza de código obsoleto
Se eliminó el sistema antiguo de verificación de item individual para evitar confusión:

**Eliminado de `QuestChainEntry.cs`:**
- ❌ `requireItemInInventory` (bool)
- ❌ `requiredItem` (ItemData)
- ❌ `requiredAmount` (int)
- ❌ `consumeItemOnComplete` (bool global)

**Reemplazado por sistema unificado:**
- ✅ `requiredItems[]` → Array de `ItemRequirement`
- ✅ Cada item tiene su propio `consumeOnComplete`
- ✅ Soporte nativo para múltiples items
- ✅ Validación mejorada en `NPCQuestConfig.cs`

**Beneficios:**
- 🎯 Un solo sistema para todos los casos
- 📝 Código más limpio y mantenible
- 🔧 Menos confusión en el Inspector
- ⚡ Más flexible (cada item controla si se consume)

**Ver:** `SistemaAntiguoEliminado.md` para más detalles

---

**Última actualización:** 29/12/2024 17:30
**Estado:** Problema #1 resuelto ✅ + Código limpiado 🧹 | Problemas #2-7 documentados 📋


