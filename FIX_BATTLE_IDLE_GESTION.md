# ✅ FIX: Gestión Correcta de Battle Idle en Combate

**Fecha:** 2025-12-26  
**Estado:** Implementado

---

## 🚨 **PROBLEMA IDENTIFICADO**

Durante el combate, tanto NPCs como el Player deben usar **Battle Idle** en lugar del idle normal, pero esto no estaba gestionándose correctamente:

### NPCs:
❌ Solo cambiaban a Battle Idle cuando se detenían después de moverse  
❌ Si estaban quietos esperando cooldowns → usaban idle normal  
❌ Spam potencial de `PlayBattleIdle()` sin protección  

### Player:
❌ **No tenía sistema de Battle Mode** implementado  
❌ Siempre usaba idle normal, incluso en combate  
❌ No había detección de enemigos cercanos  

---

## ✅ **SOLUCIONES IMPLEMENTADAS**

### 1. **NPC Battle Idle - Siempre Activo en Combate**

#### A) Protección Anti-Spam en `PlayBattleIdle()`

```csharp
public void PlayBattleIdle()
{
    if (_isInBattle && !string.IsNullOrEmpty(idleBattleState))
    {
        // ✅ Solo crossfade si NO está ya en este estado
        int targetHash = Animator.StringToHash(idleBattleState);
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        
        if (currentState.shortNameHash != targetHash)
        {
            CrossFadeToState(idleBattleState, 0.2f);
        }
    }
}
```

**Beneficio:** Evita reiniciar constantemente la animación de Battle Idle.

#### B) `StopAndIdle()` Siempre Usa Battle Idle

```csharp
void StopAndIdle()
{
    NavMeshAgentUtility.SafeSetStopped(_agent, true);
    _animator?.ResetMovement();
    
    // Re-sincronizar con NavAgent
    npcAnimator.syncWithNavAgent = true;
    
    // ✅ SIEMPRE reproducir Battle Idle cuando se detiene en combate
    _animator?.PlayBattleIdle();
    _wasMovingLastFrame = false;
}
```

**Antes:** Solo llamaba a `PlayBattleIdle()` si `_wasMovingLastFrame == true`  
**Ahora:** SIEMPRE llama a `PlayBattleIdle()` cuando se detiene en combate  

**Protegido contra spam** por la verificación en `PlayBattleIdle()`.

---

### 2. **Player Battle Mode - Sistema Completo Nuevo** ⭐

He creado un nuevo componente: **`PlayerBattleModeController.cs`**

#### Funcionalidad:

```csharp
[Detección Automática de Enemigos]
    ↓
[¿Enemigos en radio de 15m?]
    ↓ SÍ
[Activar Battle Mode]
    ↓
[Player quieto? → Battle Idle]
    ↓
[Sin enemigos por 3s? → Idle Normal]
```

#### Características:

✅ **Detección automática** de NPCs enemigos en un radio configurable (15m default)  
✅ **Battle Idle automático** cuando el player está quieto cerca de enemigos  
✅ **Transición suave** a idle normal cuando no hay enemigos (delay de 3s)  
✅ **Protección anti-spam** (solo cambia si no está ya en el estado)  
✅ **Debug mode** con Gizmos visuales para verificar el radio de detección  
✅ **Auto-configuración** de referencias (Animator, vThirdPersonController)  

#### Parámetros Configurables:

```csharp
battleIdleStateName = "Idle_Battle"     // Nombre del estado en Animator
normalIdleStateName = "Idle"            // Nombre del idle normal
enemyDetectionRadius = 15f              // Radio de detección (metros)
enemyLayer = LayerMask "Enemy"          // Capa de enemigos
exitBattleDelay = 3f                    // Segundos sin enemigos para salir
debugMode = false                       // Activar logs y Gizmos
```

---

## 🔧 **IMPLEMENTACIÓN TÉCNICA**

### NPCs:

**Flujo Correcto:**
```
1. CombatState.OnEnter()
   └─ SetBattleMode(true)  ← Activa Battle Mode
      └─ CrossFade a Battle Idle (si está quieto)

2. Durante combate:
   ├─ Si se mueve: Locomotion Battle
   └─ Si se detiene: StopAndIdle()
      └─ PlayBattleIdle()  ← SIEMPRE (protegido anti-spam)

3. CombatState.OnExit()
   └─ SetBattleMode(false)
      └─ TransitionToIdle() (idle normal)
```

### Player:

**Flujo Correcto:**
```
1. PlayerBattleModeController.Update()
   └─ DetectEnemiesNearby()
      ├─ OverlapSphere(15m, EnemyLayer)
      └─ Verificar si están en CombatState

2. Si enemigos detectados:
   ├─ EnterBattleMode()
   └─ Si player quieto (input < 0.1f):
      └─ EnsureBattleIdle()  ← Protegido anti-spam

3. Si NO hay enemigos por 3s:
   └─ ExitBattleMode()
      └─ CrossFade a Idle Normal (si está quieto)
```

---

## 📊 **COMPARACIÓN**

### Antes:

```
NPC en combate, esperando cooldown:
🧍 Idle Normal ❌ (postura casual)

Player cerca de NPC enemigo:
🧍 Idle Normal ❌ (postura casual)
```

### Ahora:

```
NPC en combate, esperando cooldown:
🗡️ Battle Idle ✅ (postura de guardia)

Player cerca de NPC enemigo:
🗡️ Battle Idle ✅ (postura de guardia)
```

---

## 🎮 **COMPORTAMIENTO ESPERADO**

### Escenario 1: Inicio de Combate

```
Player se acerca a NPC enemigo
    ↓
NPC: Detecta player → CombatState
    ↓
NPC: SetBattleMode(true) → 🗡️ Battle Idle
    ↓
Player: PlayerBattleModeController detecta enemigo (15m)
    ↓
Player: Si quieto → 🗡️ Battle Idle
```

### Escenario 2: Durante Combate

```
NPC: Dispara hechizo → PostAttack hold (0.4s) → 🗡️ Battle Idle
    ↓
NPC: Sin magia → Escudo/Cobertura → 🗡️ Battle Idle
    ↓
Player: Quieto recargando → 🗡️ Battle Idle
    ↓
Player: Se mueve → Locomotion (automático por Invector)
```

### Escenario 3: Fin de Combate

```
NPC: Derrotado → ExitCombatState
    ↓
NPC: SetBattleMode(false) → 🧍 Idle Normal
    ↓
Player: Sin enemigos por 3s
    ↓
Player: ExitBattleMode() → 🧍 Idle Normal
```

---

## 🔧 **ARCHIVOS MODIFICADOS/CREADOS**

### Modificados:

1. ✅ **`NPCSimpleAnimator.cs`**
   - Agregada protección anti-spam en `PlayBattleIdle()`

2. ✅ **`NPCCombatBrain.cs`**
   - `StopAndIdle()` ahora SIEMPRE llama a `PlayBattleIdle()`

### Creados:

3. ✅ **`PlayerBattleModeController.cs`** (NUEVO)
   - Sistema completo de Battle Mode para el player
   - Detección automática de enemigos
   - Gestión de Battle Idle/Normal Idle

---

## 📋 **INSTRUCCIONES DE USO**

### Para NPCs:
✅ **Ya funciona automáticamente** - No requiere configuración adicional.

### Para Player:

1. **Agregar el componente** al GameObject del player:
   ```
   Player GameObject → Add Component → PlayerBattleModeController
   ```

2. **Configurar el Animator:**
   - Crear/verificar estado `Idle_Battle` en el Animator del player
   - Opcional: Ajustar nombre en Inspector si es diferente

3. **Configurar Layer:**
   - Asegurar que los NPCs enemigos estén en layer `Enemy`
   - Ajustar `Enemy Layer` mask en Inspector si es necesario

4. **Verificar referencias:**
   - El componente auto-encuentra `Animator` y `vThirdPersonController`
   - Si no las encuentra, asignarlas manualmente en Inspector

5. **Debug (opcional):**
   - Activar `Debug Mode` en Inspector
   - Ver Gizmos rojos/verdes mostrando radio de detección
   - Ver logs en Console

---

## ✅ **VERIFICACIÓN**

### Test NPC:
1. Iniciar combate con un NPC
2. **Observar:** NPC usa Battle Idle cuando está quieto ✅
3. **Observar:** NPC usa Battle Idle esperando cooldowns ✅
4. **Observar:** Animación NO se reinicia constantemente ✅

### Test Player:
1. Acercarse a un NPC enemigo (< 15m)
2. **Observar:** Player cambia a Battle Idle si está quieto ✅
3. Moverse → **Observar:** Locomotion normal ✅
4. Detenerse → **Observar:** Vuelve a Battle Idle ✅
5. Alejar enemigos → Esperar 3s → **Observar:** Idle normal ✅

### Logs Esperados:

```
[PlayerBattleMode] 🗡️ ENTRANDO en Battle Mode
[PlayerBattleMode] ✅ Cambiado a Battle Idle
[PlayerBattleMode] Enemigo detectado: Erika
[PlayerBattleMode] 🏡 SALIENDO de Battle Mode
```

---

## 🎯 **BENEFICIOS**

### Inmersión:
✅ **Coherencia visual** - Ambos personajes en postura de combate  
✅ **Lenguaje corporal** - Comunicación visual del estado de batalla  
✅ **Tensión mantenida** - Postura de guardia incluso cuando están quietos  

### Gameplay:
✅ **Feedback claro** - Player sabe cuándo está en "zona de combate"  
✅ **Transiciones naturales** - No hay saltos bruscos de animación  
✅ **Performance** - Protección anti-spam evita cambios innecesarios  

### Técnico:
✅ **Modular** - Componente independiente para el player  
✅ **Configurable** - Todos los parámetros ajustables en Inspector  
✅ **Debuggable** - Debug mode para verificar funcionamiento  

---

**Estado:** ✅ IMPLEMENTADO - Battle Idle gestionado correctamente para NPCs y Player

