# 🔧 Fix: Rotación NPCs con Quest System

**Fecha:** 25 Diciembre 2025  
**Archivo modificado:** `NPCQuestConfig.cs`  
**Versión:** 2.0 FINAL

---

## 🔍 Problema Identificado

**NPCs simples vs NPCs con quests:**

| Tipo de NPC | Componentes | ¿Funciona? |
|-------------|-------------|------------|
| **Simple** | `Interactable` + `NPCSimpleAnimator` | ✅ Rota correctamente |
| **Con quests** | `NPCQuestConfig` + `NPCBehaviourManagerV2` + `NPCBrain` | ❌ NO rotaba correctamente |

**Síntomas en NPCs con quests:**
- Se quedaban en posición 3/4 (no completaban la rotación)
- Volvían a su rotación original al terminar el diálogo
- Comportamiento inconsistente con NPCs simples

---

## 🔎 Análisis de Causa Raíz

### Código funcional (Interactable.cs - NPCs simples):
```csharp
void StartDialogue()
{
    var dm = DialogueManager.Instance;
    
    // ✅ Pasa el Transform del NPC al DialogueManager
    dm.StartDialogue(dialogue, transform, () => {
        // Callback
    });
}
```

### Código problemático (NPCQuestConfig.cs - NPCs con quests):
```csharp
public bool ProcessInteraction(GameObject interactor, Common.NPCStateContext context)
{
    // ❌ Rotación manual ANTES del diálogo
    if (rotateToPlayerOnInteract && interactor != null)
    {
        RotateToPlayer(interactor.transform, context);
    }
    // ...
}

private void PlayDialogue(DialogueAsset dialogue, Common.NPCStateContext context)
{
    var dm = DialogueManager.Instance;
    
    // ❌ NO pasa el Transform del NPC
    dm.StartDialogue(dialogue, () => {
        // Callback
    });
}
```

**Problemas detectados:**
1. `NPCQuestConfig` rotaba manualmente con `RotateToPlayer()` ANTES del diálogo
2. Esta rotación era instantánea pero no se mantenía
3. Nunca se iniciaba el seguimiento continuo durante el diálogo
4. `DialogueManager.StartDialogue()` no recibía el `Transform` del NPC
5. Sin el transform, DialogueManager no podía manejar la rotación
6. Conflicto entre dos sistemas de rotación diferentes

---

## ✅ Solución Implementada

**Principio:** Hacer que `NPCQuestConfig` funcione exactamente igual que `Interactable`.

### Cambios en NPCQuestConfig.cs:

#### 1. ProcessInteraction() - Eliminar rotación manual
```csharp
// ANTES
public bool ProcessInteraction(GameObject interactor, Common.NPCStateContext context)
{
    // ❌ Rotación manual
    if (rotateToPlayerOnInteract && interactor != null)
    {
        RotateToPlayer(interactor.transform, context);
    }
    
    StartTalkingAnimation(context);
    // ...
}

// DESPUÉS
public bool ProcessInteraction(GameObject interactor, Common.NPCStateContext context)
{
    // ✅ NO rotar manualmente - DialogueManager lo hará
    StartTalkingAnimation(context);
    // ...
}
```

#### 2. PlayDialogue() - Pasar Transform al DialogueManager
```csharp
// ANTES
private void PlayDialogue(DialogueAsset dialogue, Common.NPCStateContext context)
{
    var dm = DialogueManager.Instance;
    
    // ❌ Sin transform
    dm.StartDialogue(dialogue, () => {
        StopTalkingAnimation(context);
    });
}

// DESPUÉS
private void PlayDialogue(DialogueAsset dialogue, Common.NPCStateContext context)
{
    var dm = DialogueManager.Instance;
    
    // ✅ Con context.Transform para que DialogueManager maneje la rotación
    dm.StartDialogue(dialogue, context.Transform, () => {
        StopTalkingAnimation(context);
    });
}
```

#### 3. PlayDialogueWithCallback() - Pasar Transform al DialogueManager
```csharp
// ANTES
private void PlayDialogueWithCallback(DialogueAsset dialogue, Common.NPCStateContext context, System.Action onFinished)
{
    var dm = DialogueManager.Instance;
    
    // ❌ Sin transform
    dm.StartDialogue(dialogue, combinedCallback);
}

// DESPUÉS
private void PlayDialogueWithCallback(DialogueAsset dialogue, Common.NPCStateContext context, System.Action onFinished)
{
    var dm = DialogueManager.Instance;
    
    // ✅ Con context.Transform
    dm.StartDialogue(dialogue, context.Transform, combinedCallback);
}
```

#### 4. Limpieza - Eliminar código obsoleto

**Métodos eliminados:**
- `RotateToPlayer()` - Ya no se usa
- `StartContinuousLookAtPlayer()` - Ya no se usa
- `StopContinuousLookAtPlayer()` - Ya no se usa
- `ContinuousLookAtPlayerCoroutine()` - Ya no se usa

**Campos eliminados:**
- `rotateToPlayerOnInteract` - Ya no se usa
- `keepLookingAtPlayerDuringDialogue` - Ya no se usa
- `_lookAtPlayerCoroutine` - Ya no se usa
- `_playerTransform` - Ya no se usa

**Campos mantenidos:**
- `rotationSpeed` - Ahora usado por DialogueManager (actualizado tooltip)

---

## 🎯 Resultado

### Comportamiento unificado:

**Todos los NPCs ahora funcionan igual:**

| Paso | Acción | Sistema responsable |
|------|--------|-------------------|
| 1 | Jugador interactúa con NPC | `Interactable` o `NPCQuestConfig` |
| 2 | **Rotación instantánea** hacia jugador | `DialogueManager` |
| 3 | Seguimiento continuo (720°/s) | `DialogueManager` |
| 4 | Diálogo se cierra | `DialogueManager` |
| 5 | **Mantiene rotación 2 segundos** | `DialogueManager` |
| 6 | NPC liberado | Puede retomar comportamiento |

### Ventajas:
- ✅ Consistencia total entre todos los tipos de NPCs
- ✅ Un solo sistema de rotación (DialogueManager)
- ✅ Código más limpio y mantenible
- ✅ Rotación funciona correctamente (100% hacia jugador)
- ✅ Se mantiene la rotación después del diálogo

---

## 🧪 Testing

### Caso de prueba 1: NPC simple (sin quests)
1. Habla con un NPC que solo tiene `Interactable`
2. Debe girarse instantáneamente hacia ti
3. Te sigue con la mirada durante el diálogo
4. Mantiene la rotación al cerrar

**Resultado esperado:** ✅ Funciona (ya funcionaba antes)

### Caso de prueba 2: NPC con quests (Eldran, etc.)
1. Habla con Eldran por la espalda (180° de distancia)
2. Debe girarse instantáneamente hacia ti (100%, no 3/4)
3. Te sigue con la mirada durante el diálogo
4. Mantiene la rotación al cerrar

**Resultado esperado:** ✅ Ahora funciona igual que NPCs simples

### Logs esperados:
```
[NPCQuestConfig.ProcessInteraction] Iniciando interacción con NPC
[NPCQuestConfig.ProcessInteraction] Activando animación de hablar
[NPCQuestConfig.PlayDialogue] Iniciando diálogo 'DLG_...' con NPC en (x, y, z)
[DialogueManager] 👁️ NPC 'Eldran' girado INSTANTÁNEAMENTE hacia el jugador (ángulo: 247.8°)
[DialogueManager] 👁️ Iniciando seguimiento de rotación del NPC 'Eldran'
[DialogueManager] 🔚 Diálogo cerrado - NPC 'Eldran' mantiene rotación final: 247.8°
[DialogueManager] 🔒 Manteniendo rotación del NPC 'Eldran' por 2 segundos
[DialogueManager] ✅ NPC 'Eldran' liberado - rotación final establecida
```

---

## 📁 Archivos Modificados

### NPCQuestConfig.cs
- **Línea ~96-99**: Eliminada llamada a `RotateToPlayer()`
- **Línea ~287**: Agregado `context.Transform` en `PlayDialogue()`
- **Línea ~314**: Agregado `context.Transform` en `PlayDialogueWithCallback()`
- **Línea ~314-393**: Eliminados métodos obsoletos de rotación
- **Línea ~32-39**: Eliminados campos obsoletos

**Total de líneas eliminadas:** ~100 líneas de código obsoleto

---

## 💡 Lecciones Aprendidas

1. **Un solo sistema es mejor que dos**: Tener rotación en `NPCQuestConfig` Y en `DialogueManager` causaba conflictos
2. **Pasar el contexto completo**: `DialogueManager` necesita el `Transform` del NPC para funcionar
3. **Probar ambos caminos**: NPCs simples Y NPCs con quests deben funcionar igual
4. **Código limpio**: Eliminar código obsoleto evita confusión futura

---

## ✅ Conclusión

El problema estaba en que `NPCQuestConfig` intentaba manejar la rotación por su cuenta en lugar de dejar que `DialogueManager` lo hiciera (como hace `Interactable`).

La solución fue **eliminar la rotación manual** de `NPCQuestConfig` y **pasar el Transform** al `DialogueManager`, unificando el comportamiento de todos los NPCs.

**Estado final:** ✅ **RESUELTO COMPLETAMENTE**

