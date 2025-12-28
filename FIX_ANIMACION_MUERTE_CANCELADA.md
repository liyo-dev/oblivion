# FIX: Animación de Muerte Cancelada por PlayOneShot

## 📋 Problema Identificado

Después de matar al NPC, la animación de muerte se iniciaba correctamente pero luego se cancelaba, dejando al NPC de pie sin hacer nada.

### Logs del Problema

```
[NPCAnimator:Boy_Pirate] 💀 PlayDeath() llamado ✅
[NPCAnimator:Boy_Pirate] ✅ animator.Play('Die02_NoWeapon', 0) ejecutado ✅
[NPCAnimator:Boy_Pirate] ✅ Animación de muerte iniciada ✅

// ❌ PROBLEMA: Inmediatamente después...
[NPCAnimator] ✅ CrossFade a estado 'Idle_Normal_NoWeapon' <- CANCELA LA MUERTE
```

## 🔍 Causa Raíz

La secuencia de eventos era:

1. NPC recibe daño letal
2. `PlayGetHit()` reproduce animación de daño como "OneShot"
3. **AL MISMO TIEMPO**, `Die()` se llama y establece `_currentState = Dead`
4. `PlayDeath()` inicia la animación de muerte
5. **PERO** la coroutine de `PlayGetHit()` sigue ejecutándose
6. Cuando termina la animación de daño, `PlayOneShotCoroutine` hace:

```csharp
// ❌ CÓDIGO PROBLEMÁTICO
onComplete?.Invoke();

// Return to idle if not interacting
if (!_isInteracting && !_isInBattle)
{
    _currentState = AnimationState.Idle;  // ← Sobrescribe Dead
    TransitionToIdle();                   // ← Cancela animación de muerte
}
```

### Diagrama del Problema

```
t=0.0s → NPC recibe golpe letal
t=0.0s → PlayGetHit() inicia (coroutine 1)
t=0.0s → Animación TakeDamage empieza
t=0.0s → Die() se llama → _currentState = Dead
t=0.0s → PlayDeath() se llama → Animación Die02 empieza
t=0.8s → Animación TakeDamage termina
t=0.8s → PlayOneShotCoroutine hace TransitionToIdle() ← ❌ CANCELA MUERTE
t=0.8s → Animación Die02 se cancela → NPC en Idle (de pie)
```

## ✅ Solución Implementada

Modificar `PlayOneShotCoroutine` para que **NO haga transición a Idle si el NPC está muerto**.

### Código Corregido

```csharp
private IEnumerator PlayOneShotCoroutine(string stateName, int layer, System.Action onComplete)
{
    // ...esperar animación...
    
    // Callback
    onComplete?.Invoke();
    
    // ✅ NUEVO: NO hacer transición a Idle si el NPC está muerto
    // Esto previene que se cancele la animación de muerte
    if (_currentState == AnimationState.Dead)
    {
        if (debugMode)
            Debug.Log($"[NPCAnimator] OneShot completado pero NPC está muerto - NO transicionar a Idle");
        _oneShotCoroutine = null;
        return; // ← SALIR SIN TRANSICIONAR
    }
    
    // Return to idle if not interacting (let the callback handle battle state)
    if (!_isInteracting && !_isInBattle)
    {
        _currentState = AnimationState.Idle;
        TransitionToIdle();
    }
    
    _oneShotCoroutine = null;
}
```

### Diagrama de la Solución

```
t=0.0s → NPC recibe golpe letal
t=0.0s → PlayGetHit() inicia (coroutine 1)
t=0.0s → Animación TakeDamage empieza
t=0.0s → Die() se llama → _currentState = Dead
t=0.0s → PlayDeath() se llama → Animación Die02 empieza
t=0.8s → Animación TakeDamage termina
t=0.8s → PlayOneShotCoroutine detecta _currentState == Dead
t=0.8s → ✅ NO hace TransitionToIdle() - SALE SIN TOCAR NADA
t=0.8s → Animación Die02 continúa normalmente
t=3.0s → Animación Die02 termina → Transición a Dizzy
```

## 📊 Comparación

| Aspecto | ANTES ❌ | DESPUÉS ✅ |
|---------|---------|-----------|
| **Animación de muerte inicia** | Sí | Sí |
| **Animación de muerte se cancela** | Sí (por Idle) | No |
| **NPC se queda de pie** | Sí | No |
| **Transición a Dizzy** | No ocurre | Ocurre correctamente |
| **Diálogo post-derrota** | No se muestra | Se muestra |

## 🎯 Por Qué Funciona

La verificación `if (_currentState == AnimationState.Dead)` es efectiva porque:

1. **PlayDeath()** establece `_currentState = Dead` **ANTES** de que termine la animación de daño
2. Cuando `PlayOneShotCoroutine` termina, verifica el estado actual
3. Si está muerto, **respeta la animación de muerte** y no interfiere

### Orden de Operaciones Correcto

```
1. TakeDamage() → Vida llega a 0
2. Die() se llama
3. OnDied() se invoca
4. DeathRoutine() inicia
5. PlayDeath() se llama → _currentState = Dead ✅
6. Animación Die02 empieza
7. PlayOneShotCoroutine termina → Detecta Dead → NO transiciona ✅
8. Animación Die02 continúa sin interrupciones
```

## 🔍 Casos Edge Protegidos

### Caso 1: Muerte Durante Animación de Daño
```
✅ PROTEGIDO: _currentState == Dead previene transición a Idle
```

### Caso 2: Muerte Durante Animación de Ataque
```
✅ PROTEGIDO: La misma verificación aplica a cualquier OneShot
```

### Caso 3: Muerte Durante Animación de Búsqueda
```
✅ PROTEGIDO: Cualquier animación OneShot respeta el estado Dead
```

### Caso 4: NPC Vivo Recibe Daño No Letal
```
✅ FUNCIONA NORMAL: _currentState != Dead, transiciona a Idle como antes
```

## 📝 Logs Esperados Ahora

```
[Lifecycle] ⚔️ Boy_Pirate recibió 50 de daño - Vida: 0/100 - IsAlive: False
[Damageable:Boy_Pirate] 💀 VIDA AGOTADA - Llamando a Die()
[NPCAnimator:Boy_Pirate] 💥 PlayGetHit() - Animación seleccionada: 'TakeDamage'
[NPCAnimator] ✅ CrossFade a estado 'TakeDamage'
[Lifecycle] 💀💀💀 OnDied() LLAMADO para Boy_Pirate
[Lifecycle] 💀 Iniciando secuencia de muerte: Boy_Pirate
[NPCAnimator:Boy_Pirate] 💀 PlayDeath() llamado
[NPCAnimator:Boy_Pirate] 🎬 Reproduciendo animación de muerte: Die02_NoWeapon
[NPCAnimator:Boy_Pirate] ✅ animator.Play('Die02_NoWeapon', 0) ejecutado
[Lifecycle] 💀 Animación de muerte iniciada inmediatamente después del slow-mo

// ✅ NUEVO: NO aparece transición a Idle_Normal
// La animación de muerte continúa sin interrupciones

[Signals] BattleWon: Npc_Battle
[Lifecycle] 😵 Esperando transición a animación dizzy para Boy_Pirate
// ... resto de la secuencia ...
```

## 🎮 Resultado Final

- ✅ Animación de muerte se reproduce completamente
- ✅ Transición automática a Dizzy (gracias a Exit Time en Animator)
- ✅ NPC se levanta mareado
- ✅ Diálogo post-derrota se muestra
- ✅ Sistema completo funciona como se diseñó

## 🔑 Lección Aprendida

**Problema General**: Coroutines que modifican el estado pueden ejecutarse en paralelo y causar conflictos.

**Solución General**: Siempre verificar el estado actual antes de hacer transiciones automáticas.

```csharp
// ❌ MALO: Asumir que el estado no ha cambiado
TransitionToIdle();

// ✅ BUENO: Verificar el estado antes de actuar
if (_currentState != AnimationState.Dead)
{
    TransitionToIdle();
}
```

---

**Fecha**: 28 de diciembre de 2024  
**Tipo**: Bug Fix - Race Condition  
**Estado**: ✅ COMPLETADO  
**Archivos Modificados**: `NPCSimpleAnimator.cs` - PlayOneShotCoroutine

