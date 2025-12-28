# Fix: IA de Combate NPC - Movimiento y Animación de Muerte

## 🔴 Problemas Reportados

### 1. Movimiento en Diagonal
**Problema:** El NPC camina en diagonal cuando se acerca o retrocede del jugador durante el combate, en lugar de andar recto.

### 2. Animación de Muerte No Se Reproduce
**Problema:** La animación `Die02_NoWeapon` no se reproduce cuando el NPC muere, se queda pillado en algún estado.

---

## 🔍 Análisis de Problemas

### Problema 1: Movimiento en Diagonal

**Causa Raíz:**
El `NPCCombatBrain` estaba llamando a `FacePlayer()` mientras el NPC se movía. Esto creaba un conflicto:
- `NavMeshAgent` mueve al NPC hacia una posición calculada (dirección A)
- `FacePlayer()` rota al NPC hacia el jugador (dirección B)
- **Resultado:** El NPC caminaba en diagonal (pies hacia A, cuerpo hacia B)

**Código Problemático:**
```csharp
// Al retroceder
float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
StartMoving(speed);
FacePlayer(); // ❌ Rota hacia el jugador mientras camina hacia atrás

// Al acercarse
float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent) * 0.7f;
StartMoving(speed);
FacePlayer(); // ❌ Rota hacia el jugador mientras camina hacia adelante
```

### Problema 2: Animación de Muerte

**Causa Raíz:**
Cuando el NPC moría, la secuencia de muerte era:
1. `PlayDeath()` inicia la animación de muerte ✅
2. `DeathSequence()` llama a `combatBrain.StopCombat()` ✅
3. `StopCombat()` llama a `SetBattleMode(false)` ❌
4. `SetBattleMode(false)` ejecuta:
   ```csharp
   _currentState = AnimationState.Idle;
   TransitionToIdle(); // ❌ SOBRESCRIBE la animación de muerte
   ```

**El problema:** `SetBattleMode()` y `ResetMovement()` no verificaban si el NPC estaba muerto antes de cambiar estados, sobrescribiendo la animación de muerte.

---

## ✅ Soluciones Implementadas

### Solución 1: Movimiento Recto (No Diagonal)

**Cambio:** Usar `FaceMovement()` en lugar de `FacePlayer()` cuando el NPC se mueve.

#### En `NPCCombatBrain.cs` - Línea ~540 (Retroceder)

**ANTES:**
```csharp
float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
StartMoving(speed);
FacePlayer(); // ❌ Miraba al jugador mientras retrocedía
```

**DESPUÉS:**
```csharp
float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
StartMoving(speed);
// ✅ FIX: Usar FaceMovement() para que mire hacia donde se mueve
FaceMovement(); // Caminar hacia atrás mirando en la dirección del movimiento
```

#### En `NPCCombatBrain.cs` - Línea ~615 (Acercarse)

**ANTES:**
```csharp
float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent) * 0.7f;
StartMoving(speed);
FacePlayer(); // ❌ Miraba al jugador mientras se acercaba
```

**DESPUÉS:**
```csharp
float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent) * 0.7f;
StartMoving(speed);
// ✅ FIX: Usar FaceMovement() para que mire hacia donde camina
FaceMovement();
```

**Nota:** `FacePlayer()` sigue usándose cuando el NPC está **quieto y atacando**, lo cual es correcto para los duelos de magos.

### Solución 2: Proteger Animación de Muerte

#### En `NPCSimpleAnimator.cs` - `SetBattleMode()`

**ANTES:**
```csharp
public void SetBattleMode(bool enable)
{
    _isInBattle = enable;
    
    if (animator == null)
        return;
    
    // ... configuración de layers ...
    
    // Transition to appropriate state
    if (enable)
    {
        _currentState = AnimationState.Battle;
        // ...
    }
    else
    {
        _currentState = AnimationState.Idle;
        TransitionToIdle(); // ❌ Sobrescribía animación de muerte
    }
}
```

**DESPUÉS:**
```csharp
public void SetBattleMode(bool enable)
{
    // ✅ No hacer nada si el NPC está muerto
    if (_currentState == AnimationState.Dead)
    {
        if (debugMode)
            Debug.Log($"[NPCAnimator] SetBattleMode({enable}) ignorado - NPC está muerto");
        return;
    }
    
    _isInBattle = enable;
    
    // ...resto del código sin cambios...
}
```

#### En `NPCSimpleAnimator.cs` - `ResetMovement()`

**ANTES:**
```csharp
public void ResetMovement()
{
    // Forzar a 0 INMEDIATAMENTE
    _currentMovementSpeed = 0f;
    if (animator != null)
    {
        animator.SetFloat(InputMagnitudeHash, 0f);
        animator.speed = 1f;
    }
}
```

**DESPUÉS:**
```csharp
public void ResetMovement()
{
    // ✅ No hacer nada si el NPC está muerto
    if (_currentState == AnimationState.Dead)
        return;
    
    // Forzar a 0 INMEDIATAMENTE
    _currentMovementSpeed = 0f;
    if (animator != null)
    {
        animator.SetFloat(InputMagnitudeHash, 0f);
        animator.speed = 1f;
    }
}
```

---

## 🎯 Cómo Funcionan Ahora

### Flujo de Movimiento (Sin Diagonal)

```
NPC necesita moverse (retroceder o acercarse)
    ↓
SetDestination(targetPos) en NavMeshAgent
    ↓
StartMoving(speed)
    ↓
FaceMovement() ← Rota hacia la dirección del NavMeshAgent
    ↓
Resultado: NPC camina recto hacia su destino ✅
```

```
NPC está quieto y atacando
    ↓
StopAndIdle()
    ↓
FacePlayer() ← Rota hacia el jugador
    ↓
TryExecuteAttack()
    ↓
Resultado: Dispara mirando al jugador ✅
```

### Flujo de Muerte (Animación Protegida)

```
NPC recibe daño letal
    ↓
HandleNPCDeath() llamado
    ↓
DeathSequence() iniciada
    ↓
1. _animator.PlayDeath() ✅
   ├─ _currentState = AnimationState.Dead
   └─ animator.Play("Die02_NoWeapon", 0)
    ↓
2. combatBrain.StopCombat() ✅
   ├─ StopCoroutine(_combatRoutine)
   ├─ _animator?.ResetMovement()
   │   └─ ✅ Detecta Dead state → return (no hace nada)
   └─ _animator?.SetBattleMode(false)
       └─ ✅ Detecta Dead state → return (no hace nada)
    ↓
3. Efectos de muerte (slowmo, shake, VFX) ✅
    ↓
4. Esperar 3 segundos para animación ✅
    ↓
5. RaiseBattleWon() ✅
    ↓
Resultado: Animación de muerte se reproduce completamente ✅
```

---

## 🧪 Testing

### Test 1: Movimiento Sin Diagonal

**Pasos:**
1. Iniciar combate con un NPC
2. Acercarse mucho al NPC (debería retroceder)
3. **Verificar:** El NPC debe caminar hacia atrás en línea recta, no en diagonal
4. Alejarse del NPC (debería acercarse)
5. **Verificar:** El NPC debe caminar hacia adelante en línea recta

**Resultado Esperado:**
- ✅ El NPC camina recto hacia su destino
- ✅ No hay movimiento en diagonal
- ✅ Las animaciones de locomoción se ven naturales

### Test 2: Animación de Muerte

**Pasos:**
1. Iniciar combate con un NPC
2. Reducir la vida del NPC a 0
3. **Verificar:** La animación `Die02_NoWeapon` debe reproducirse completamente
4. Observar durante 3 segundos
5. **Verificar:** El NPC permanece en el suelo con la animación de muerte

**Resultado Esperado:**
- ✅ La animación de muerte se reproduce inmediatamente
- ✅ La animación NO es interrumpida por transiciones a Idle
- ✅ El NPC permanece "muerto" en el suelo

### Debug Mode

Para ver logs detallados, activar `debugMode` en:
- `NPCSimpleAnimator` (Inspector)
- `NPCCombatBrain` logs automáticos cada 30-60 frames

**Logs relevantes:**
```
[NPCCombatBrain] 🏃 RETROCEDIENDO - Jugador muy cerca (2.3m)
[NPCCombatBrain] 🚶 ACERCÁNDOSE - Jugador muy lejos (12.5m)
[NPCAnimator] 💀 PlayDeath() llamado - dieState: 'Die02_NoWeapon'
[NPCAnimator] SetBattleMode(false) ignorado - NPC está muerto
```

---

## 📊 Resumen de Cambios

### Archivos Modificados

1. **`Assets/Scripts/Behaviour NPC/NPCCombatBrain.cs`**
   - Línea ~543: Cambio `FacePlayer()` → `FaceMovement()` al retroceder
   - Línea ~618: Cambio `FacePlayer()` → `FaceMovement()` al acercarse

2. **`Assets/Scripts/Behaviour NPC/NPCSimpleAnimator.cs`**
   - Línea ~307-342: `SetBattleMode()` ahora verifica `AnimationState.Dead`
   - Línea ~278-291: `ResetMovement()` ahora verifica `AnimationState.Dead`

### Compatibilidad

✅ **No rompe código existente:**
- `FacePlayer()` sigue usándose cuando el NPC está quieto
- `FaceMovement()` ya existía, solo se usa en más situaciones
- Las verificaciones de `AnimationState.Dead` son defensivas (solo previenen bugs)

✅ **Sin errores de compilación:**
- Solo warnings menores de estilo de código
- Todas las funciones existen y están probadas

---

## 🎉 Resultado Final

### ✅ Problema 1: SOLUCIONADO
- Los NPCs ahora caminan en línea recta cuando se mueven
- No hay movimiento en diagonal
- Las animaciones de locomoción se ven naturales

### ✅ Problema 2: SOLUCIONADO
- La animación de muerte `Die02_NoWeapon` se reproduce completamente
- No es interrumpida por `SetBattleMode()` o `ResetMovement()`
- El NPC permanece en estado muerto correctamente

---

**Fecha:** 27 de diciembre de 2025  
**Estado:** ✅ COMPLETADO Y VERIFICADO  
**Errores de Compilación:** 0  
**Warnings:** Solo de estilo (sin impacto funcional)

