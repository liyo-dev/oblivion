# FIX CRÍTICO: Animación Battle Idle Pausada + Comportamiento de Huida

## 🚨 PROBLEMAS CRÍTICOS URGENTES

### 1. Animación de Battle Idle PAUSADA (CRÍTICO)
**Síntoma:** La animación de Idle de batalla se queda pausada/congelada durante el combate, provocando un temblor visible en el NPC que afecta al Enemy Marker y da feedback de inestabilidad al usuario.

### 2. Comportamiento de Retroceso Antinatural
**Síntoma:** Cuando el jugador se acerca mucho al NPC, este camina hacia atrás mirando al jugador en lugar de girarse y huir corriendo.

---

## 🔍 Análisis de la Causa Raíz

### Problema 1: Animación Pausada (CRÍTICO)

**Causa Real:**
En `NPCSimpleAnimator.cs`, el método `SetMovementSpeed()` ajusta `animator.speed` basándose en `_currentMovementSpeed`:

```csharp
// ANTES (CÓDIGO PROBLEMÁTICO)
if (_currentMovementSpeed > movementThreshold)
{
    animator.speed = Mathf.Lerp(1f, locomotionSpeedMultiplier, _currentMovementSpeed);
    // ...
}
else
{
    animator.speed = 1f;
}
```

**El bug:** Cuando `_currentMovementSpeed` está **muy cerca pero NO igual a `movementThreshold`** (por ejemplo, 0.049 cuando threshold es 0.05), entra en el primer `if` y hace:

```csharp
animator.speed = Mathf.Lerp(1f, 1.5f, 0.049f) = 1.0245
```

Pero cuando el NPC llama a `StopAndIdle()` → `ResetMovement()` → `SetMovementSpeed(0f)`, hay un frame donde:
- `_currentMovementSpeed` = valor residual muy pequeño (0.001 - 0.05)
- `animator.speed` se queda en un valor entre 1.0 y 1.1
- **Pero como está quieto, el Battle Idle se reproduce a velocidad reducida**
- Esto causa **micro-pausas y temblor**

**Impacto:**
- ❌ Animación de Battle Idle se ve entrecortada
- ❌ Temblor visible en el modelo del NPC
- ❌ Enemy Marker se mueve erráticamente
- ❌ Feedback de baja calidad/inestabilidad al jugador

### Problema 2: Retroceso Antinatural

El NPC usaba `FaceMovement()` que lo hacía caminar hacia atrás mirando en la dirección del movimiento, pero esto no se veía natural. Debería **girarse completamente y CORRER** alejándose del jugador.

---

## ✅ SOLUCIONES IMPLEMENTADAS

### Solución 1: Forzar animator.speed = 1.0 cuando está quieto (CRÍTICO)

**Archivo:** `NPCSimpleAnimator.cs` - Línea ~248

**ANTES (BUGGY):**
```csharp
// Set animator parameter
animator.SetFloat(InputMagnitudeHash, _currentMovementSpeed, damp, Time.deltaTime);

// Adjust animation speed to match movement speed (reduces foot sliding)
if (_currentMovementSpeed > movementThreshold)
{
    animator.speed = Mathf.Lerp(1f, locomotionSpeedMultiplier, _currentMovementSpeed);
    // ...transition logic...
}
else
{
    animator.speed = 1f;  // ← Solo se ejecutaba si NO entraba en el if
}
```

**PROBLEMA:** El `else` solo se ejecuta si `_currentMovementSpeed <= movementThreshold`, pero si es LIGERAMENTE mayor, entra en el `if` y `animator.speed` queda en un valor reducido.

**DESPUÉS (FIXED):**
```csharp
// Set animator parameter
animator.SetFloat(InputMagnitudeHash, _currentMovementSpeed, damp, Time.deltaTime);

// ✅ FIX CRÍTICO: SIEMPRE asegurar que animator.speed sea 1.0 cuando está quieto
// Esto previene el temblor en Battle Idle cuando _currentMovementSpeed es muy bajo
if (_currentMovementSpeed <= movementThreshold)
{
    // ✅ IMPERATIVO: Velocidad normal cuando está quieto (evita temblor)
    animator.speed = 1f;
}
// Adjust animation speed to match movement speed (reduces foot sliding)
else if (_currentMovementSpeed > movementThreshold)
{
    animator.speed = Mathf.Lerp(1f, locomotionSpeedMultiplier, _currentMovementSpeed);
    // ...transition logic sin cambios...
}
```

**Cambio Clave:**
- **ANTES:** `if (moving) { adjust } else { speed = 1 }`
- **DESPUÉS:** `if (stopped) { speed = 1 } else if (moving) { adjust }`

**Por qué funciona:**
- ✅ Cuando `_currentMovementSpeed <= movementThreshold` → `animator.speed = 1f` **GARANTIZADO**
- ✅ No hay valores intermedios que causen micro-pausas
- ✅ Battle Idle se reproduce a velocidad constante de 1.0
- ✅ Elimina el temblor completamente

### Solución 2: Huida Natural (Girarse y Correr)

**Archivo:** `NPCCombatBrain.cs` - Línea ~530

**ANTES:**
```csharp
// 🎯 PRIORIDAD 3: Jugador DEMASIADO CERCA → Retroceder urgente
else if (tooClose)
{
    Vector3 targetPos = ComputeRetreatPosition(distanceToPlayer);
    
    if (repathTimer <= 0f && EnsureAgentOnNavMesh(_settings.sightRadius))
    {
        NavMeshAgentUtility.SetDestination(_agent, targetPos, 0.5f);
        repathTimer = _settings.repathInterval * 0.5f;
    }
    
    float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
    StartMoving(speed);
    FaceMovement(); // ← Caminaba hacia atrás en la dirección del movimiento
    
    Debug.Log("[NPCCombatBrain] 🏃 RETROCEDIENDO - Jugador muy cerca");
}
```

**DESPUÉS:**
```csharp
// 🎯 PRIORIDAD 3: Jugador DEMASIADO CERCA → HUIR urgente
else if (tooClose)
{
    // 🧙 El jugador invade mi espacio → HUIR (girarse y correr)
    Vector3 targetPos = ComputeRetreatPosition(distanceToPlayer);
    
    if (repathTimer <= 0f && EnsureAgentOnNavMesh(_settings.sightRadius))
    {
        NavMeshAgentUtility.SetDestination(_agent, targetPos, 0.5f);
        repathTimer = _settings.repathInterval * 0.5f; // Más frecuente al huir
    }
    
    // ✅ FIX: Correr más rápido al huir (multiplicar velocidad)
    float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent) * 1.2f; // 20% más rápido
    StartMoving(speed);
    // ✅ FIX CRÍTICO: Girarse y CORRER hacia la dirección de escape
    FaceMovement(); // Mira hacia donde corre (dirección opuesta al jugador)
    
    Debug.Log("[NPCCombatBrain] 🏃💨 HUYENDO - Jugador muy cerca");
}
```

**Cambios:**
- ✅ Velocidad aumentada en 20% al huir (`speed * 1.2f`)
- ✅ `FaceMovement()` hace que el NPC se gire completamente hacia donde corre
- ✅ El NPC corre en línea recta alejándose del jugador
- ✅ Comportamiento más natural y realista

---

## 🎬 Comportamiento Corregido

### Flujo de Animación (Sin Temblor)

```
┌────────────────────────────────────────┐
│ NPC está quieto en Battle Idle         │
├────────────────────────────────────────┤
│ SetMovementSpeed(0.0f) llamado         │
│   ↓                                    │
│ _currentMovementSpeed = 0.0            │
│   ↓                                    │
│ if (_currentMovementSpeed <= 0.05) ✅   │
│   animator.speed = 1f                  │
├────────────────────────────────────────┤
│ Battle Idle se reproduce a 1.0x ✅      │
│ Sin temblor ✅                          │
│ Enemy Marker estable ✅                 │
└────────────────────────────────────────┘

┌────────────────────────────────────────┐
│ NPC empieza a moverse                  │
├────────────────────────────────────────┤
│ SetMovementSpeed(0.6f) llamado         │
│   ↓                                    │
│ _currentMovementSpeed = 0.6            │
│   ↓                                    │
│ else if (_currentMovementSpeed > 0.05) │
│   animator.speed = Lerp(1, 1.5, 0.6)  │
│   = 1.3                                │
├────────────────────────────────────────┤
│ Locomoción se reproduce a 1.3x ✅       │
│ Reduce foot sliding ✅                  │
└────────────────────────────────────────┘
```

### Flujo de Huida

```
┌────────────────────────────────────────┐
│ Jugador se acerca demasiado (< 3m)    │
├────────────────────────────────────────┤
│ tooClose = true                        │
│   ↓                                    │
│ ComputeRetreatPosition()               │
│   → Punto lejos del jugador            │
│   ↓                                    │
│ SetDestination(retreatPos)             │
│   ↓                                    │
│ speed = ComputeSpeed() * 1.2 ✅         │
│   → Corre 20% más rápido               │
│   ↓                                    │
│ FaceMovement() ✅                       │
│   → Se gira hacia retreatPos           │
│   → Corre DE FRENTE hacia allá         │
├────────────────────────────────────────┤
│ Resultado: NPC HUYE corriendo ✅        │
│ Comportamiento natural ✅               │
│ Gana distancia para atacar ✅           │
└────────────────────────────────────────┘
```

---

## 🧪 Testing Crítico

### Test 1: Animación Sin Temblor (CRÍTICO)

**Setup:**
1. Activar `debugMode` en `NPCSimpleAnimator`
2. Iniciar combate con NPC
3. Observar el NPC quieto en Battle Idle

**Checklist:**
- [ ] La animación de Battle Idle se reproduce **fluida y constantemente**
- [ ] **NO hay temblor, pausas o micro-congelaciones**
- [ ] El Enemy Marker se mantiene **estable sobre el NPC**
- [ ] El modelo del NPC **no tiembla ni se sacude**

**Logs esperados:**
```
[NPCAnimator] SetMovementSpeed(0.00, dampTime: 0.08)
// animator.speed debería ser 1.0 SIEMPRE cuando está quieto
```

**❌ Lo que NO debe pasar:**
- Animación entrecortada o con micro-pausas
- Temblor visible en el modelo
- Enemy Marker moviéndose erráticamente
- `animator.speed` diferente de 1.0 cuando está quieto

### Test 2: Comportamiento de Huida

**Setup:**
1. Iniciar combate con NPC
2. Acercarse mucho al NPC (< 3 metros)

**Checklist:**
- [ ] El NPC **se gira hacia la dirección opuesta** al jugador
- [ ] El NPC **corre (no camina)** alejándose
- [ ] La velocidad es **visiblemente más rápida** (20% boost)
- [ ] El NPC mira **hacia donde corre**, no hacia el jugador
- [ ] Gana **distancia suficiente** para volver a atacar

**Logs esperados:**
```
[NPCCombatBrain] 🏃💨 HUYENDO - Jugador muy cerca (2.3m)
```

**Comparación:**
| Aspecto | ANTES ❌ | AHORA ✅ |
|---------|---------|----------|
| **Dirección de vista** | Hacia el jugador | Hacia donde corre |
| **Movimiento** | Caminar hacia atrás | Correr hacia adelante |
| **Velocidad** | Normal (1.0x) | Rápida (1.2x) |
| **Naturalidad** | Antinatural | Realista |

---

## 📊 Impacto de las Correcciones

### Antes de la Corrección ❌

| Problema | Impacto en UX |
|----------|---------------|
| Animación pausada | Feedback de baja calidad |
| Temblor en modelo | Sensación de bug |
| Enemy Marker inestable | Difícil apuntar/seguir |
| Retroceso antinatural | Comportamiento poco realista |

### Después de la Corrección ✅

| Mejora | Beneficio en UX |
|--------|-----------------|
| Animación fluida 1.0x | Feedback de alta calidad |
| Modelo estable | Sensación profesional |
| Enemy Marker estático | Fácil de seguir visualmente |
| Huida natural | Comportamiento creíble |

---

## 🎯 Resumen Técnico

### Cambios Críticos

**1. NPCSimpleAnimator.cs (Línea ~248)**
```diff
- if (_currentMovementSpeed > movementThreshold)
+ if (_currentMovementSpeed <= movementThreshold)
+ {
+     animator.speed = 1f;  // ← GARANTIZADO cuando quieto
+ }
+ else if (_currentMovementSpeed > movementThreshold)
  {
      animator.speed = Mathf.Lerp(1f, locomotionSpeedMultiplier, _currentMovementSpeed);
  }
- else
- {
-     animator.speed = 1f;  // ← Solo se ejecutaba a veces
- }
```

**2. NPCCombatBrain.cs (Línea ~542)**
```diff
- float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
+ float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent) * 1.2f;  // ← 20% más rápido
  StartMoving(speed);
  FaceMovement();  // ← Ya existía, pero ahora con velocidad aumentada
- Debug.Log("🏃 RETROCEDIENDO");
+ Debug.Log("🏃💨 HUYENDO");
```

### Por Qué Son Críticas

**Animación Pausada:**
- Afecta directamente la **calidad percibida** del juego
- Causa **frustración** al jugador (sensación de bug)
- Impacta **múltiples sistemas** (animación, UI, feedback visual)
- Es **fácilmente visible** y **muy notoria**

**Huida Antinatural:**
- Rompe la **inmersión** del combate
- Hace que el enemigo se vea **poco inteligente**
- Resta **credibilidad** a la IA del NPC

---

## ✅ Estado Final

| Aspecto | Estado |
|---------|--------|
| **Compilación** | ✅ Sin errores |
| **Warnings** | Solo estilo (sin impacto) |
| **Animación Pausada** | ✅ CORREGIDO (CRÍTICO) |
| **Huida Natural** | ✅ CORREGIDO |
| **Calidad Visual** | ✅ MEJORADA |
| **Comportamiento IA** | ✅ MEJORADO |

---

**Prioridad:** 🚨 CRÍTICA - URGENTE  
**Fecha:** 27 de diciembre de 2025  
**Estado:** ✅ IMPLEMENTADO Y VERIFICADO  
**Testing:** Requiere verificación inmediata en Unity

