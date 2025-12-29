# Corrección: Sistema de Verificación de Items en Quests

## Problema Identificado

Una misión requería 3 items (poción, capa, trofeo). El jugador ya tenía 1 poción en el inventario ANTES de iniciar la misión. Al iniciar la misión, el contador mostraba 0/3 en lugar de 1/3.

## Arquitectura Correcta (ya existente)

El sistema ya tenía la arquitectura diseñada correctamente:

### 1. QuestChainEntry (ItemRequirement)

```csharp
[Serializable]
public class ItemRequirement
{
    public ItemData item;                    // Item requerido
    [Min(1)] public int amount = 1;          // Cantidad
    public string stepConditionId = "";      // ID de condición (ej: "QUEST_ELDRAN_MISSION5_STEP_01")
    public int stepIndex = -1;               // Índice alternativo
    public bool consumeOnComplete = true;
}
```

### 2. QuestData.Step

```csharp
public class Step
{
    public string descriptionId;    // Localización
    public string description;      // Texto
    public string conditionId;      // ID para matchear (ej: "QUEST_ELDRAN_MISSION5_STEP_01")
}
```

## Flujo Correcto

```
QuestManager.StartQuest()
    ↓
CheckExistingItemsForQuest(rq)
    ↓
1. Obtener Inventory via PlayerService (NO FindObjectOfType)
2. Buscar QuestChainEntry:
   - NPCInteractiveNarrativeRegistry.GetAll() (NO FindObjectsOfType)
   - executor.GetComponent<NPCBehaviourManagerV2>()
   - npcManager.Configuration.questConfig.questChain
3. Para cada ItemRequirement:
   - Verificar inventory.Count(item.itemId) >= amount
   - Buscar step por stepConditionId (matchear con QuestData.Step.conditionId)
   - Fallback: usar stepIndex directo
   - Marcar step.completed = true
4. Notificar cambios con OnQuestsChanged
5. Si AllStepsCompleted() → auto-completar quest
```

## Corrección Aplicada

### Archivo Modificado
- `Assets/Scripts/Quests/QuestManager.cs`

### Cambios

**ANTES:**
- ❌ Código chapucero con FindObjectOfType
- ❌ Logs verbosos innecesarios
- ❌ Lógica confusa

**DESPUÉS:**
- ✅ Usa PlayerService para obtener Inventory
- ✅ Usa NPCInteractiveNarrativeRegistry para buscar NPCs
- ✅ Acceso correcto: executor → NPCBehaviourManagerV2 → Configuration → questConfig
- ✅ Matchea items por stepConditionId (arquitectura correcta)
- ✅ Fallback a stepIndex si no hay conditionId
- ✅ Código limpio y conciso

### Métodos Corregidos

1. **CheckExistingItemsForQuest(RuntimeQuest rq)**
   - Verifica items en inventario al iniciar quest
   - Marca steps completados automáticamente
   - Auto-completa quest si todos los items ya están

2. **FindQuestChainEntry(string questId)**
   - Busca configuración de quest en NPCs registrados
   - Usa NPCInteractiveNarrativeRegistry.GetAll()
   - Acceso correcto: `executor.GetComponent<NPCBehaviourManagerV2>().Configuration.questConfig`

## Configuración de Quest con Items

### Ejemplo: Quest "La Preparación del Mago"

**QuestData (Q_ELDRAN_MISSION5)**
```yaml
questId: "ELDRAN_MISSION5"
steps:
  - descriptionId: "QUEST_ELDRAN_MISSION5_DESC_01"
    description: "Capa de Mago"
    conditionId: "QUEST_ELDRAN_MISSION5_STEP_00"  # <- IMPORTANTE para matching
  
  - descriptionId: "QUEST_ELDRAN_MISSION5_DESC_02"
    description: "Poción de vida"
    conditionId: "QUEST_ELDRAN_MISSION5_STEP_01"  # <- IMPORTANTE para matching
  
  - descriptionId: "QUEST_ELDRAN_MISSION5_DESC_03"
    description: "Poción de magia"
    conditionId: "QUEST_ELDRAN_MISSION5_STEP_02"  # <- IMPORTANTE para matching
```

**QuestChainEntry (Victoria)**
```yaml
questData: Q_ELDRAN_MISSION5
requiredItems:
  - item: IT_CapaMago          # ScriptableObject ItemData
    amount: 1
    stepConditionId: "QUEST_ELDRAN_MISSION5_STEP_00"  # <- Matchea con QuestData.Step[0].conditionId
    stepIndex: 0               # <- Fallback numérico (DEBE configurarse si no hay stepConditionId)
    consumeOnComplete: true
  
  - item: IT_PocionVida
    amount: 1
    stepConditionId: "QUEST_ELDRAN_MISSION5_STEP_01"  # <- Matchea con QuestData.Step[1].conditionId
    stepIndex: 1               # <- Fallback numérico
    consumeOnComplete: true
  
  - item: IT_PocionMagia
    amount: 1
    stepConditionId: "QUEST_ELDRAN_MISSION5_STEP_02"  # <- Matchea con QuestData.Step[2].conditionId
    stepIndex: 2               # <- Fallback numérico
    consumeOnComplete: true
```

### ⚠️ IMPORTANTE: Configuración de stepIndex / stepConditionId

El sistema soporta DOS formas de identificar steps:

1. **Por ID (PREFERIDO):** `stepConditionId` → `QuestData.Step.conditionId`
   - Más robusto (no se rompe si se reordenan steps)
   - Se usa PRIMERO en `CheckExistingItemsForQuest()`
   
2. **Por índice (FALLBACK):** `stepIndex` → posición numérica en array
   - Usado por `MarkStepDone()` y grafos de narrativa
   - Se usa si `stepConditionId` está vacío o no matchea

**DEBES configurar AL MENOS UNO de los dos:**

```csharp
// ✅ CORRECTO - usa stepConditionId
ItemRequirement {
    item: IT_PocionVida,
    amount: 1,
    stepConditionId: "QUEST_ELDRAN_MISSION5_STEP_01",  // <- DEBE matchear con QuestData.Step.conditionId
    stepIndex: -1,                                      // <- -1 = ignorar
    consumeOnComplete: true
}

// ✅ CORRECTO - usa stepIndex
ItemRequirement {
    item: IT_PocionVida,
    amount: 1,
    stepConditionId: "",                                // <- vacío = ignorar
    stepIndex: 1,                                        // <- índice directo
    consumeOnComplete: true
}

// ✅ MEJOR - usa ambos (redundancia)
ItemRequirement {
    item: IT_PocionVida,
    amount: 1,
    stepConditionId: "QUEST_ELDRAN_MISSION5_STEP_01",  // <- intenta primero
    stepIndex: 1,                                        // <- fallback
    consumeOnComplete: true
}

// ❌ INCORRECTO - ninguno configurado
ItemRequirement {
    item: IT_PocionVida,
    amount: 1,
    stepConditionId: "",                                // <- vacío
    stepIndex: -1,                                       // <- sin índice
    consumeOnComplete: true
}
// → CheckExistingItemsForQuest() NO podrá marcar el step como completado
```


## Flujo de Uso

### Escenario 1: Jugador NO tiene items
```
1. Jugador acepta quest
2. CheckExistingItemsForQuest() → No encuentra items
3. UI muestra: 0/3
4. Jugador recoge poción → MarkStepDone() manual
5. UI muestra: 1/3
```

### Escenario 2: Jugador YA tiene 1 poción
```
1. Jugador acepta quest
2. CheckExistingItemsForQuest() → Encuentra IT_PocionVida en inventario
3. Marca step[1].completed = true automáticamente
4. UI muestra: 1/3 ✅
5. Jugador recoge capa → UI muestra: 2/3
```

### Escenario 3: Jugador tiene TODOS los items
```
1. Jugador acepta quest
2. CheckExistingItemsForQuest() → Encuentra todos los items
3. Marca todos los steps completados
4. AllStepsCompleted() = true → Auto-completa quest
5. OnQuestCompleted invocado
6. Quest archivada automáticamente
```

## Notas Importantes

### ❌ NO HACER
- NO usar `FindObjectOfType<Inventory>()`
- NO usar `FindObjectsOfType<NPCInteractiveNarrativeExecutor>()`
- NO asumir prefijos de IDs (ej: "ITEM_")
- NO logs verbosos para flujos normales

### ✅ HACER
- USAR `PlayerService.TryGetComponent<Inventory>()`
- USAR `NPCInteractiveNarrativeRegistry.GetAll()`
- USAR `stepConditionId` para matchear steps
- CONFIGURAR `ItemRequirement[]` en QuestChainEntry del NPC

## Testing

### Caso de Prueba 1: Item Pre-Existente
```
1. Dar al jugador IT_PocionVida (consola, shop, etc.)
2. Iniciar quest ELDRAN_MISSION5
3. Verificar UI muestra 1/3
4. Verificar logs: "[QuestManager] Step X marcado como completado"
```

### Caso de Prueba 2: Múltiples Items Pre-Existentes
```
1. Dar IT_PocionVida + IT_CapaMago
2. Iniciar quest ELDRAN_MISSION5
3. Verificar UI muestra 2/3
```

### Caso de Prueba 3: Todos los Items Pre-Existentes
```
1. Dar IT_PocionVida + IT_CapaMago + IT_PocionMagia
2. Iniciar quest ELDRAN_MISSION5
3. Verificar quest se completa automáticamente
4. Verificar quest aparece como archivada
```

## Conclusión

La arquitectura ya estaba correctamente diseñada con:
- `ItemRequirement.stepConditionId` → `QuestData.Step.conditionId`
- `PlayerService` para referencias
- `NPCInteractiveNarrativeRegistry` para buscar configuraciones

La corrección consistió en:
1. **Eliminar código chapucero** con FindObjectOfType
2. **Usar la arquitectura existente correctamente**
3. **Simplificar la lógica** eliminando logs innecesarios
4. **Respetar los sistemas establecidos** (ServiceLocator, Registry)

---

**Fecha:** 2025-12-29
**Estado:** ✅ Corregido

