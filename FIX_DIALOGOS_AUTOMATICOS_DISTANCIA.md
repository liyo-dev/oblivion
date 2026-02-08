# 🔧 FIX: Diálogos Automáticos se Lanzan con NPC Lejano

## 📋 Problema Reportado

**Síntoma**: Al cargar una partida, se lanza un diálogo con un NPC que está en otra zona del mapa, "por la cara".

### Contexto del Bug

Cuando el jugador carga una partida guardada:

1. **Estela se une al party** (restauración desde save)
2. Esto completa automáticamente el **step 1 de ELDRAN_MISSION6** (requiere que Estela esté en el party)
3. El sistema ejecuta la **acción post-step** configurada
4. La acción incluye un **diálogo con Eldran**
5. **El diálogo se lanza INMEDIATAMENTE**, sin importar la distancia al jugador
6. Eldran puede estar a **1600+ metros** de distancia

### Logs del Bug

```
[QuestManager] 👥 🎉 ¡COMPLETANDO STEP 1 de quest 'ELDRAN_MISSION6' por miembro 'NPC_InteractiveNarrative_Config_Estela_b17a2d68'!
...
[DialogueManager] 🎬 Activando sistema cinematográfico para NPC: Eldran
[PlayerParty] ⚡ demasiado lejos (523,0m > 25,0m), teletransportando...
```

El diálogo se abre con Eldran a **523 metros** de distancia, y luego Estela se teletransporta porque está demasiado lejos.

## 🔍 Causa Raíz

### Sistema de Quest Post-Actions

El `NPCQuestActionExecutor` ejecuta acciones automáticamente cuando se completan quests/steps, incluyendo:

- Diálogos (`dialogueBeforeAction`, `QuestActionType.Dialogue`)
- Movimientos (`QuestActionType.Move`)
- Teletransporte (`QuestActionType.Teleport`)
- Combate (`QuestActionType.StartCombat`)

**Problema**: No había **validación de distancia** antes de lanzar diálogos automáticos.

### Flujo del Bug

```
1. Cargar partida
   └─> PlayerParty.OnProfileReady()
       └─> RestoreMembersFromIds([Estela])
           └─> Estela.JoinParty()
               └─> QuestManager.OnPartyMemberJoined(Estela)
                   └─> Detecta que Estela cumple requisito de ELDRAN_MISSION6.step[1]
                       └─> QuestManager.MarkStepDone(ELDRAN_MISSION6, 1)
                           └─> QuestManager.OnQuestCompleted(ELDRAN_MISSION6) ❌
                               └─> NPCQuestActionExecutor.HandleQuestCompleted()
                                   └─> ExecuteActionCoroutine(postAction)
                                       └─> StartDialogue() SIN VALIDAR DISTANCIA ❌
```

## ✅ Solución Implementada

### Archivo: `NPCQuestActionExecutor.cs`

#### Cambio 1: Validación en `ExecuteActionCoroutine`

```csharp
private IEnumerator ExecuteActionCoroutine(QuestPostAction action, int questIndex)
{
    _isExecutingPostAction = true;
    yield return null;

    // ✅ NUEVO: VALIDACIÓN DE DISTANCIA
    const float maxDialogueDistance = 20f; // Distancia máxima para auto-iniciar diálogos
    
    var player = PlayerService.Player;
    if (player != null)
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        
        // Si hay diálogo pre-acción o la acción es un diálogo, validar distancia
        bool hasDialogue = action.dialogueBeforeAction != null 
                        || action.actionType == QuestActionType.Dialogue;
        
        if (hasDialogue && distanceToPlayer > maxDialogueDistance)
        {
            Debug.LogWarning($"[NPCQuestActionExecutor:{name}] ⚠️ Diálogo cancelado - NPC muy lejos del jugador ({distanceToPlayer:F1}m > {maxDialogueDistance}m)");
            _isExecutingPostAction = false;
            yield break; // ✅ Cancelar toda la acción
        }
    }

    // ...resto del código...
}
```

**Beneficio**: Cancela **toda la acción** (incluyendo diálogos pre-acción y movimientos) si el NPC está lejos.

#### Cambio 2: Validación en `ExecuteDialogueAction`

```csharp
private IEnumerator ExecuteDialogueAction(QuestPostAction action)
{
    if (action.dialogueToPlay == null)
    {
        Debug.LogWarning($"[NPCQuestActionExecutor] Dialogue to play no asignado");
        yield break;
    }

    // ✅ NUEVO: VALIDACIÓN DE DISTANCIA
    const float maxDialogueDistance = 20f;
    
    var player = PlayerService.Player;
    if (player != null)
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        
        if (distanceToPlayer > maxDialogueDistance)
        {
            Debug.LogWarning($"[NPCQuestActionExecutor:{name}] ⚠️ Diálogo cancelado - NPC muy lejos del jugador ({distanceToPlayer:F1}m > {maxDialogueDistance}m)");
            yield break; // ✅ Cancelar el diálogo
        }
    }

    // ...resto del código...
}
```

**Beneficio**: Protección adicional para acciones de tipo `QuestActionType.Dialogue`.

### Parámetros de Configuración

| Parámetro | Valor | Descripción |
|-----------|-------|-------------|
| `maxDialogueDistance` | **20 metros** | Distancia máxima para auto-iniciar diálogos |

**Justificación**: 20m es una distancia razonable donde:
- El jugador aún puede ver al NPC en pantalla
- El diálogo tiene sentido contextual
- No es tan restrictivo que rompa la narrativa

### Comportamiento Nuevo

#### Escenario 1: NPC Cerca (≤ 20m)

```
✅ Diálogo se ejecuta normalmente
✅ Cámara cinematográfica se activa
✅ Player se bloquea durante el diálogo
```

#### Escenario 2: NPC Lejos (> 20m)

```
⚠️ Log de advertencia: "Diálogo cancelado - NPC muy lejos del jugador (523.0m > 20m)"
❌ Diálogo NO se ejecuta
❌ Player NO se bloquea
✅ El jugador puede seguir jugando normalmente
```

## 🧪 Cómo Probar el Fix

### Test Case 1: Cargar Partida con Estela en el Party

**Setup**:
1. Guardar una partida con Estela en el party
2. El jugador debe estar **lejos de Eldran** (>20m)

**Resultado Esperado**:
- ✅ Estela se restaura correctamente
- ✅ El step se completa automáticamente
- ⚠️ Log: "Diálogo cancelado - NPC muy lejos..."
- ❌ El diálogo NO se lanza
- ✅ El jugador puede moverse libremente

**Consola**:
```
[PlayerParty] ✅ Reintento exitoso: Estela encontrado y unido al party
[QuestManager] 👥 🎉 ¡COMPLETANDO STEP 1 de quest 'ELDRAN_MISSION6'...
[NPCQuestActionExecutor:Eldran] ⚠️ Diálogo cancelado - NPC muy lejos del jugador (523.0m > 20m)
```

### Test Case 2: Completar Step Cerca del NPC

**Setup**:
1. Estar cerca de Eldran (<20m)
2. Completar el step (ej: reclutar a Estela)

**Resultado Esperado**:
- ✅ El step se completa
- ✅ El diálogo se lanza normalmente
- ✅ Cámara cinematográfica se activa
- ✅ Experiencia narrativa intacta

### Test Case 3: Movimiento Post-Quest con Diálogo Pre-Acción

**Setup**:
1. Quest configurada con:
   - `dialogueBeforeAction`: Un diálogo
   - `actionType`: Move/Teleport

**Resultado Esperado**:
- Si **cerca**: Diálogo → Movimiento
- Si **lejos**: Ni diálogo ni movimiento (toda la acción se cancela)

## 📊 Impacto del Fix

### ✅ Ventajas

1. **Inmersión mejorada**: No más diálogos "telepáticos" desde lejos
2. **Control del jugador**: No se bloquea cuando el NPC está fuera de contexto
3. **Lógica narrativa**: Los diálogos solo ocurren cuando tiene sentido espacialmente
4. **Performance**: No se ejecutan acciones costosas innecesariamente

### ⚠️ Consideraciones

#### Posible Impacto en Quests

Si tienes quests diseñadas para lanzar diálogos automáticos con NPCs lejanos, estas **se cancelarán ahora**.

**Soluciones**:

1. **Rediseñar la quest**: El diálogo debería ocurrir cuando el jugador se acerca al NPC
2. **Usar un evento custom**: En lugar de diálogo automático, usar un trigger cuando el jugador llega a la zona
3. **Aumentar la distancia**: Cambiar `maxDialogueDistance` a un valor mayor si es necesario

#### Quests que Podrían Verse Afectadas

Busca quests con:
- `postAction.dialogueBeforeAction != null`
- `postAction.actionType == QuestActionType.Dialogue`

Y verifica que el NPC esté cerca del jugador cuando se completa el step.

### 🔧 Configuración Avanzada

Si necesitas diferentes distancias para diferentes NPCs, puedes:

**Opción 1**: Hacer `maxDialogueDistance` serializable

```csharp
[Header("Dialogue Distance")]
[Tooltip("Distancia máxima para auto-iniciar diálogos (default: 20m)")]
[SerializeField] private float maxDialogueDistance = 20f;
```

**Opción 2**: Añadir un flag para desactivar la validación

```csharp
[Header("Dialogue Distance")]
[Tooltip("¿Validar distancia antes de diálogos automáticos?")]
[SerializeField] private bool validateDialogueDistance = true;

[SerializeField] private float maxDialogueDistance = 20f;

// En el código:
if (validateDialogueDistance && hasDialogue && distanceToPlayer > maxDialogueDistance)
{
    // Cancelar
}
```

## 🎓 Lecciones Aprendidas

1. **Validar contexto espacial**: Antes de ejecutar acciones que afectan al jugador (diálogos, combates), validar que el NPC esté en rango
2. **Guardado de estado causa eventos**: Al cargar partidas, la restauración de estado puede disparar eventos/callbacks automáticos
3. **Timing de inicialización**: Los sistemas que reaccionan a eventos de restauración deben ser robustos ante NPCs no disponibles/lejanos

## 🔄 Alternativas Consideradas

### Alternativa 1: Postponer el Diálogo

En lugar de cancelar, guardar el diálogo pendiente y lanzarlo cuando el jugador se acerque.

**Descartada**: Más complejo, requiere sistema de "diálogos pendientes" y puede confundir al jugador.

### Alternativa 2: Teletransportar al NPC

Al completar el step, teletransportar automáticamente al NPC cerca del jugador.

**Descartada**: Puede romper la lógica espacial del juego y ser inmersivo-breaking si el jugador ve la teletransportación.

### Alternativa 3: Validar en QuestManager

Validar distancia antes de ejecutar `OnQuestCompleted`.

**Descartada**: `QuestManager` no debería conocer detalles de implementación de NPCs individuales. Es mejor validar en el ejecutor.

## 📝 Resumen de Archivos Modificados

| Archivo | Cambios | Líneas |
|---------|---------|--------|
| `NPCQuestActionExecutor.cs` | Validación de distancia en 2 métodos | +44 |

## ✅ Estado Actual

- ✅ **Fix implementado** en `NPCQuestActionExecutor.cs`
- ✅ **Compilación exitosa** (solo warnings de estilo)
- ✅ **Documentación completa** creada
- ⏳ **Pendiente de testing** en juego

---

**Fecha**: 2026-02-06  
**Prioridad**: 🔴 Alta  
**Categoría**: Bug Fix - Experiencia de Usuario
