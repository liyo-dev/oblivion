# ✅ FIX CRÍTICO: NPC Se Queda en Bucle de Animación Después de Recibir Daño

**Fecha:** 2025-12-26  
**Estado:** Implementado y Corregido

---

## 🚨 **PROBLEMA CRÍTICO RESUELTO**

### Síntoma:
Cuando un NPC recibe daño:
1. ✅ Reproduce correctamente la animación de `TakeDamage`
2. ❌ **Después se queda en bucle andando en el sitio** (animación de caminar pero sin moverse)

---

## 🔬 **ANÁLISIS DE CAUSA RAÍZ**

### Flujo del Bug:

```
1. NPC recibe daño
   └─ NPCCombatLifecycleHandler.HandleNPCDamaged()
      └─ DamageStunSequence() inicia

2. Durante el stun:
   ├─ NavMeshAgent.isStopped = true  ✅ (se detiene)
   ├─ Reproduce animación TakeDamage  ✅
   └─ Espera damageStunDuration (ej: 0.3s)

3. Después del stun:
   ├─ NavMeshAgent.isStopped = false  ✅ (se reactiva)
   └─ ❌ PROBLEMA: El Animator se queda con SetMovementSpeed(1.0) activo

4. NPCCombatBrain sigue su loop:
   ├─ StartMoving() ya había desactivado syncWithNavAgent
   ├─ El Animator muestra animación de caminar
   └─ PERO el NavAgent no se está moviendo realmente
   
RESULTADO: Loop de animación de caminar en el sitio
```

### ¿Por Qué Sucedía?

El **`NPCCombatBrain`** y el **`NPCCombatLifecycleHandler`** trabajaban de forma independiente:

1. **CombatBrain** controla el loop de combate y movimiento
2. **LifecycleHandler** maneja el stun de daño

Cuando el NPC recibía daño:
- El LifecycleHandler paraba el agent y reproducía la animación
- PERO el CombatBrain seguía ejecutándose en paralelo
- Al terminar el stun, el agent se reactivaba pero el Animator estaba en un estado inconsistente

---

## ✅ **SOLUCIÓN IMPLEMENTADA**

### 1. **Reset del Animator Después del Stun**

Se agregó un paso en `DamageStunSequence()` para resetear completamente el animator:

```csharp
// 6. Reactivar movimiento
_isStunned = false;
if (wasAgentActive && _navAgent != null && _navAgent.enabled)
{
    _navAgent.isStopped = false;
}

// ✅ 6.5. RESETEAR ANIMATOR para evitar que se quede en loop de caminar
if (_animator != null)
{
    // Resetear velocidad de movimiento a 0
    _animator.ResetMovement();
    
    // Re-sincronizar con NavAgent
    var npcAnimator = _animator as NPCSimpleAnimator;
    if (npcAnimator != null)
    {
        npcAnimator.syncWithNavAgent = true;
    }
    
    Debug.Log($"[NPCCombatLifecycleHandler:{name}] 🔄 Animator reseteado después de stun");
}
```

**Esto asegura que:**
- La velocidad de movimiento del animator vuelve a 0
- El sync con NavAgent se reactiva
- El animator vuelve a un estado limpio

### 2. **Skip del Frame Durante Stun en CombatBrain**

Se agregó una verificación al inicio del `CombatLoop()` para que NO ejecute lógica de combate mientras el NPC está stunneado:

```csharp
while (_ctx != null && _manager != null && _manager.isActiveAndEnabled && _player != null)
{
    // ✅ SKIP FRAME SI ESTÁ STUNNEADO (recibiendo daño)
    var lifecycleHandler = _manager.GetComponent<Modules.NPCCombatLifecycleHandler>();
    if (lifecycleHandler != null && lifecycleHandler.IsStunned)
    {
        // Durante el stun, no ejecutar lógica de combate
        yield return null;
        continue;
    }
    
    // ...resto del loop
}
```

**Esto previene que:**
- El CombatBrain intente mover al NPC mientras está stunneado
- Se ejecuten ataques durante el stun
- Se modifique el animator mientras recibe daño

### 3. **Propiedad Pública `IsStunned`**

Se expuso el estado de stun como propiedad pública:

```csharp
/// <summary>
/// Indica si el NPC está actualmente stunneado (recibiendo daño)
/// </summary>
public bool IsStunned => _isStunned;
```

Esto permite que otros sistemas (como CombatBrain) consulten el estado del NPC.

---

## 🔧 **Archivos Modificados**

### 1. `NPCCombatLifecycleHandler.cs`
- ✅ Agregado reset completo del animator después del stun
- ✅ Expuesta propiedad `IsStunned`

### 2. `NPCCombatBrain.cs`
- ✅ Agregada verificación de stun al inicio del CombatLoop

---

## 📊 **Flujo Correcto Después del Fix**

```
1. NPC recibe daño
   └─ NPCCombatLifecycleHandler.HandleNPCDamaged()
      └─ DamageStunSequence() inicia
         ├─ _isStunned = true  ✅

2. Durante el stun:
   ├─ CombatBrain detecta IsStunned = true
   │  └─ Skip del frame (yield return null; continue)  ✅
   ├─ NavMeshAgent parado  ✅
   └─ Animación TakeDamage reproduciéndose  ✅

3. Después del stun:
   ├─ NavMeshAgent reactivado  ✅
   ├─ Animator.ResetMovement()  ✅
   ├─ syncWithNavAgent = true  ✅
   ├─ _isStunned = false  ✅
   └─ CombatBrain reanuda el loop normalmente  ✅

RESULTADO: ✅ Animaciones correctas, movimiento fluido
```

---

## 🎮 **Comportamiento Esperado**

### Antes del Fix:
```
NPC recibe daño → TakeDamage ✅ → 🐛 Loop de caminar en el sitio ❌
```

### Después del Fix:
```
NPC recibe daño → TakeDamage ✅ → Vuelve a idle/battle idle ✅ → Reanuda combate ✅
```

---

## ✅ **Verificación**

Para confirmar que el fix funciona:

1. **Entra en combate con un NPC**
2. **Ataca al NPC** y observa:
   - ✅ Reproduce animación de TakeDamage
   - ✅ Se queda parado durante el stun (~0.3s)
   - ✅ Después vuelve a idle de batalla o reanuda movimiento
   - ✅ NO se queda en loop de caminar en el sitio

3. **Busca en los logs:**
```
[NPCCombatLifecycleHandler:NombreNPC] 🔄 Animator reseteado después de stun
```

---

## 🎯 **Mejoras Adicionales Implementadas**

- **Sincronización entre sistemas**: CombatBrain y LifecycleHandler ahora se comunican correctamente
- **Estado consistente**: El animator siempre vuelve a un estado limpio después del daño
- **Prevención de edge cases**: El skip del frame evita que comandos de movimiento se ejecuten durante el stun

---

**Estado:** ✅ RESUELTO - Bug crítico de animación corregido

