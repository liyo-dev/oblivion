# ✅ MEJORA: Combate en Narrativa Interactiva con NPCCombatConfig

## 📋 Resumen de Cambios

Se ha actualizado el sistema de narrativa interactiva para que cuando una acción de tipo `StartCombat` se ejecute, pueda usar un **`NPCCombatConfig`** completo en lugar de solo un target simple.

---

## 🎯 Problema Anterior

**ANTES**: La sección de Combat en `NarrativeChainEntry` solo tenía:
```csharp
[Header("Combat")]
public Transform combatTarget;  // ❌ Solo el target, sin configuración
```

Esto no tenía sentido porque:
- ❌ No permitía configurar el comportamiento de combate del NPC
- ❌ Solo podías especificar un target pero no HOW combatir
- ❌ El NPC no sabía qué ataques usar, distancias, etc.

---

## ✅ Solución Implementada

**AHORA**: La sección de Combat tiene configuración completa:
```csharp
[Header("Combat")]
[Tooltip("Configuración del combate a iniciar (si actionType = StartCombat)")]
public NPCCombatConfig combatConfig;  // ✅ Config completo

[Tooltip("Target opcional del combate (si no se especifica, usa al jugador)")]
public Transform combatTarget;  // ✅ Opcional, por defecto = jugador
```

---

## 📂 Archivos Modificados

### 1. **NarrativeChainEntry.cs**
**Línea ~81**: Añadido campo `combatConfig`

```csharp
[Header("Combat")]
[Tooltip("Configuración del combate a iniciar (si actionType = StartCombat)")]
public NPCCombatConfig combatConfig;

[Tooltip("Target opcional del combate (si no se especifica, usa al jugador)")]
public Transform combatTarget;
```

**Qué hace**:
- `combatConfig`: Define CÓMO pelea el NPC (ataques, distancias, proyectiles, etc.)
- `combatTarget`: Define CONTRA QUIÉN pelea (opcional, por defecto = jugador)

---

### 2. **NPCInteractiveNarrativeConfig.cs**
**Línea ~183**: Actualizada validación

```csharp
case NarrativeActionType.StartCombat:
    if (entry.combatConfig == null)
    {
        errorMessage = $"Entry {index} tipo StartCombat requiere combatConfig (NPCCombatConfig)";
        return false;
    }
    break;
```

**Qué hace**:
- Valida que el `combatConfig` esté asignado
- Ya NO valida `combatTarget` porque es opcional

---

### 3. **NPCInteractiveNarrativeExecutor.cs**
**Línea ~606**: Actualizado método `ExecuteStartCombat`

```csharp
private IEnumerator ExecuteStartCombat(NarrativeChainEntry entry)
{
    if (entry.combatConfig == null)
    {
        Debug.LogError($"[...] ❌ StartCombat requiere combatConfig");
        yield break;
    }

    Debug.Log($"[...] ⚔️ Iniciando combate con config: {entry.combatConfig.name}");

    // 1. Asignar el combatConfig al NPC
    if (_npcManager.Configuration != null)
    {
        _npcManager.Configuration.combatConfig = entry.combatConfig;
        _npcManager.Configuration.behaviourType |= NPCBehaviourType.Combat;
    }

    // 2. Activar comportamiento de combate
    if (_npcManager.Context != null)
    {
        _npcManager.Context.IsInCombat = true;
        
        // 3. Asignar target (opcional)
        if (entry.combatTarget != null)
        {
            _npcManager.Context.Player = entry.combatTarget;
            Debug.Log($"[...] 🎯 Target: {entry.combatTarget.name}");
        }
        else
        {
            // Si no hay target, usar al jugador
            if (PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
            {
                _npcManager.Context.Player = player.transform;
                Debug.Log($"[...] 🎯 Target: Jugador");
            }
        }
    }

    yield return null;
}
```

**Qué hace**:
1. ✅ **Valida** que `combatConfig` exista
2. ✅ **Asigna** el `combatConfig` al NPC
3. ✅ **Activa** el comportamiento de combate
4. ✅ **Asigna** el target (si existe) o usa al jugador por defecto
5. ✅ El FSM transicionará automáticamente a `CombatState`

---

## 🎮 Cómo Usar

### Configuración en Unity

1. **Crea un NPCCombatConfig** (si no tienes uno):
   - `Create > NPC > Módulos > Combat Config`
   - Configura ataques, proyectiles, distancias, etc.

2. **En tu NPCInteractiveNarrativeConfig**:
   - Añade una narrativa condicional
   - Añade una acción de tipo `StartCombat`
   - **Configura los campos**:

```
┌─────────────────────────────────────────┐
│ Narrative Chain Entry                   │
├─────────────────────────────────────────┤
│ Action Type: StartCombat                │
│                                          │
│ ┌─ Combat ───────────────────────────┐ │
│ │ Combat Config: [Tu CombatConfig]   │ │ ← REQUERIDO
│ │ Combat Target: [Opcional]          │ │ ← Opcional (por defecto = jugador)
│ └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

---

## 📊 Ejemplos de Uso

### Ejemplo 1: Combate Contra el Jugador (Común)

```
Action Type: StartCombat
Combat Config: PirateMeleeConfig
Combat Target: (vacío) ← Usa al jugador automáticamente
```

**Resultado**: El NPC ataca al jugador con la configuración de `PirateMeleeConfig`.

---

### Ejemplo 2: Combate Contra Otro NPC (Avanzado)

```
Action Type: StartCombat
Combat Config: GuardianDefenseConfig
Combat Target: EnemyNPC_Transform ← Especifica otro NPC
```

**Resultado**: El NPC ataca al otro NPC especificado con la configuración de `GuardianDefenseConfig`.

---

### Ejemplo 3: Cadena Narrativa Completa

**Escenario**: NPC te advierte, luego te ataca si no respondes bien.

```
Conditional Narrative:
├─ Condition: Quest "PirateWarning" NOT completed
└─ Narrative Chain:
    ├─ 1. Dialogue: "¡Alto ahí!"
    ├─ 2. Wait: 2 segundos
    ├─ 3. Dialogue: "No deberías estar aquí..."
    ├─ 4. Wait: 1 segundo
    └─ 5. StartCombat:
        ├─ Combat Config: PirateAggressiveConfig
        └─ Combat Target: (vacío = jugador)
```

---

## 🔧 Detalles Técnicos

### Flujo de Ejecución

```
Usuario interactúa con NPC
    ↓
TryExecuteNarrative()
    ↓
ExecuteNarrativeChain()
    ↓
ExecuteAction(entry)
    ↓
ExecuteStartCombat(entry)
    ↓
┌────────────────────────────────────┐
│ 1. Validar combatConfig != null   │
│ 2. Asignar combatConfig al NPC    │
│ 3. Activar behaviourType.Combat   │
│ 4. Marcar IsInCombat = true       │
│ 5. Asignar Player target          │
└────────────────────────────────────┘
    ↓
FSM detecta IsInCombat = true
    ↓
Transición automática a CombatState
    ↓
⚔️ ¡COMBATE INICIADO!
```

---

## 🐛 Logs de Debug

### Cuando se inicia combate:
```
[NPCInteractiveNarrativeExecutor:Pirate_NPC] ▶️ INICIO Acción 4/5: StartCombat
[NPCInteractiveNarrativeExecutor:Pirate_NPC] ⚔️ Iniciando combate con config: PirateMeleeConfig
[NPCInteractiveNarrativeExecutor:Pirate_NPC] ✅ CombatConfig asignado al NPC
[NPCInteractiveNarrativeExecutor:Pirate_NPC] 🎯 Target de combate: Jugador
[NPCInteractiveNarrativeExecutor:Pirate_NPC] 🔄 FSM transicionará a CombatState
[NPCInteractiveNarrativeExecutor:Pirate_NPC] ✅ COMPLETADA Acción 4: StartCombat
```

### Si falta combatConfig:
```
[NPCInteractiveNarrativeExecutor:Pirate_NPC] ❌ StartCombat requiere combatConfig
```

---

## ⚠️ Notas Importantes

### 1. **CombatConfig es Obligatorio**
El `combatConfig` **debe** estar asignado o la validación fallará.

### 2. **CombatTarget es Opcional**
- Si está **vacío**: El NPC atacará al jugador (comportamiento por defecto)
- Si está **asignado**: El NPC atacará al target especificado

### 3. **El NPC Debe Tener Componentes de Combate**
Para que el combate funcione, el NPC debe tener:
- ✅ `NPCBehaviourManagerV2`
- ✅ `NPCCombatBrain` (se añade automáticamente si behaviourType incluye Combat)
- ✅ `Animator` con animaciones de combate
- ✅ `NavMeshAgent`

### 4. **El CombatConfig Define TODO**
El `NPCCombatConfig` define:
- Proyectiles a usar
- Distancias de ataque
- Cooldowns
- Comportamiento táctico
- Diálogos de combate
- ¡Y mucho más!

---

## ✅ Ventajas del Sistema

1. **🎯 Flexible**: Puedes tener múltiples configs de combate para diferentes situaciones
2. **🔄 Reutilizable**: Un mismo CombatConfig puede usarse en múltiples narrativas
3. **🎮 Configurable**: Todo desde el Inspector, sin código
4. **🧩 Modular**: Cada combate puede tener su propia configuración única
5. **📊 Predecible**: El NPC pelea exactamente como configures el CombatConfig

---

## 📚 Casos de Uso Avanzados

### Caso 1: Boss Fight con Fases
```
Fase 1:
└─ StartCombat: BossPhase1Config (75% HP, ataques básicos)

Fase 2 (si HP < 50%):
└─ StartCombat: BossPhase2Config (ataques más agresivos)

Fase 3 (si HP < 25%):
└─ StartCombat: BossPhase3Config (ataques desesperados)
```

### Caso 2: NPC Amistoso que se Vuelve Hostil
```
Conditional Narrative A (Quest NOT completed):
├─ Dialogue: "Hola amigo"
└─ StartQuest: "HelpNPC"

Conditional Narrative B (Quest FAILED):
├─ Dialogue: "¡Me traicionaste!"
└─ StartCombat: BetrayalCombatConfig
```

### Caso 3: Duelo de Honor
```
1. Dialogue: "Te desafío a un duelo"
2. Move: DuelingGround
3. Wait: 2 segundos
4. PlayAnimation: DrawWeapon
5. Wait: 1 segundo
6. StartCombat: DuelCombatConfig (1v1, sin healing)
```

---

## 🎉 Conclusión

El sistema ahora es **mucho más potente y flexible**:
- ✅ Combates configurables desde narrativas
- ✅ Target opcional (jugador por defecto)
- ✅ Configs reutilizables
- ✅ Sin código, todo desde Inspector
- ✅ Validación automática

**¡Ya puedes crear combates épicos directamente desde las narrativas interactivas!** ⚔️

