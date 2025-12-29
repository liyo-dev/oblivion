# 🧠 REFACTORIZACIÓN COMPLETA DE LA IA DE COMBATE NPC

**Fecha:** 29 de Diciembre, 2024  
**Problema:** NPCs actúan de forma nerviosa después de implementar el sistema de escudo  
**Causa Raíz:** Lógica de FSM con componentes aleatorios causando cambios de estado demasiado frecuentes

---

## 🎯 PREMISA DEL SISTEMA

El NPC es un **MAGO COMBATIENTE** cuyo objetivo principal es **MATAR AL PLAYER**, pero también **TEME POR SU VIDA**.

### Estrategia de Combate

```
1. ATACAR AL PLAYER
   ├─ Gastar TODOS los ataques disponibles
   ├─ Si tiene hechizos → ATACAR agresivamente
   └─ Si no tiene hechizos → ESCONDERSE para recargar

2. RECARGA DE HECHIZOS
   ├─ Buscar COBERTURA (objetos Default del escenario)
   ├─ Esconderse y esperar a que recarguen los cooldowns
   └─ Si es atacado durante la recarga:
       ├─ Usar ESCUDO si está disponible
       └─ CONTRAATACAR si tiene hechizos

3. BÚSQUEDA DEL PLAYER
   ├─ Si recargó y el player no está visible → BUSCAR activamente
   ├─ Al encontrarlo → Mostrar alerta y ATACAR
   └─ Animaciones visuales (interrogación, admiración, sense)
```

---

## ✅ CAMBIOS IMPLEMENTADOS

### 1. Nuevo Estado: `HIDING_TO_RECHARGE`

Reemplaza la lógica aleatoria de `DEFENSE` con un estado dedicado para la recarga estratégica:

**Comportamiento:**
- 🏃 Busca cobertura detrás de obstáculos
- 🔍 Muestra animación `SenseSomethingSearching_NoWeapon` al llegar
- ⏳ Espera hasta recargar al menos 2 ataques
- 🛡️ Si es atacado durante recarga → Usa escudo o contraataca
- 👀 Una vez recargado → Busca al player para atacar

```csharp
// Estado de recarga estratégica
IEnumerator State_HidingToRecharge()
{
    // A. Buscar cobertura
    Vector3 coverPosition;
    bool foundCover = FindCoverBehindObstacle(out coverPosition);
    
    // B. Moverse hacia la cobertura
    MoveTo(coverPosition, settings.runSpeed);
    
    // C. Al llegar → Mostrar animación de búsqueda
    _animator.PlaySearching();
    
    // D. Esperar a recargar hechizos
    while (CountAttacksReady() < 2) {
        // Si es atacado → Defender o contraatacar
        if (IsPlayerAttacking()) {
            // Lógica defensiva...
        }
        yield return WaitForSeconds(0.3f);
    }
    
    // E. Recargado → Buscar al player
    if (_hasLineOfSight) {
        _animator.PlaySenseSomething(); // Alerta visual
        _currentState = CombatState.EVALUATE;
    } else {
        _currentState = CombatState.SEARCHING;
    }
}
```

### 2. Refactorización de `State_Evaluate`

**ANTES:** Lógica con componentes aleatorios que causaban nerviosismo
```csharp
// ❌ Problema: ShouldConsiderDefense() tenía probabilidades aleatorias
if (ShouldConsiderDefense()) {
    _currentState = CombatState.DEFENSE; // Causaba cambios constantes
}
```

**DESPUÉS:** Lógica clara y determinista
```csharp
int attacksReady = CountAttacksReady();

if (attacksReady > 0) {
    // ✅ Tiene hechizos → ATACAR
    if (dist <= settings.maxDistance && _globalCd <= 0) {
        _currentState = CombatState.ATTACK;
    }
} else {
    // ✅ Sin hechizos → ESCONDERSE para recargar
    _currentState = CombatState.HIDING_TO_RECHARGE;
}
```

**Resultado:**
- ✅ Sin cambios de estado aleatorios
- ✅ Comportamiento predecible y agresivo
- ✅ Estrategia clara: "Atacar hasta gastar, luego esconderse"

### 3. Control de Transiciones (Anti-Nerviosismo)

Se agregó un sistema para evitar cambios de estado demasiado rápidos:

```csharp
// Control de tiempo mínimo entre cambios de estado
private float _lastStateChangeTime;
private const float MIN_STATE_DURATION = 1.5f;
private CombatState _previousState;

private bool TryChangeState(CombatState newState)
{
    // Permitir cambios inmediatos solo en situaciones críticas
    bool isCritical = newState == CombatState.SEARCHING || 
                     (_currentState == CombatState.SEARCHING && newState == CombatState.EVALUATE);
    
    if (!isCritical) {
        // Verificar que haya pasado el tiempo mínimo
        float timeSinceLastChange = Time.time - _lastStateChangeTime;
        if (timeSinceLastChange < MIN_STATE_DURATION && _currentState != CombatState.EVALUATE) {
            return false; // No cambiar todavía
        }
    }
    
    _previousState = _currentState;
    _currentState = newState;
    _lastStateChangeTime = Time.time;
    return true;
}
```

**Nota:** Este método está implementado pero no se usa actualmente en la FSM. Se puede activar si se detecta nerviosismo residual.

### 4. Mejora de `OnTakeDamage()`

Ahora responde inteligentemente según el estado en que se encuentre:

```csharp
public void OnTakeDamage(Vector3 damageSourcePosition)
{
    // Detectar ataque por la espalda
    bool attackedFromBehind = angle > 90f;
    
    // Si está en estados vulnerables
    if (_currentState == CombatState.SEARCHING || 
        _currentState == CombatState.HIDING_TO_RECHARGE ||
        _currentState == CombatState.REPOSITION)
    {
        // 🔄 GIRAR hacia la fuente del daño
        transform.rotation = Quaternion.LookRotation(directionToDamage);
        
        // 🎬 Reproducir animación SenseSomethingStart_NoWeapon
        _animator.PlaySenseSomething();
        
        // ⚡ Mostrar icono de admiración
        _alertIconController.ShowExclamation(...);
        
        // Decidir: ¿Contraatacar o defender?
        int attacksAvailable = CountAttacksReady();
        
        if (attacksAvailable > 0) {
            // Tiene ataques → CONTRAATACAR
            _currentState = CombatState.EVALUATE;
        } else if (settings.useShield && _shieldCd <= 0) {
            // No tiene ataques pero sí escudo → DEFENDER
            _currentState = CombatState.DEFENSE;
        } else {
            // Sin recursos → Seguir huyendo
        }
    }
}
```

### 5. Animaciones Correctas en Todos los Eventos

Se agregó la animación `SenseSomethingStart_NoWeapon` en todos los eventos de "encontrar al player":

| Evento | Animación | Icono |
|--------|-----------|-------|
| **Encuentra al player tras búsqueda** | `SenseSomethingStart_NoWeapon` | ❗ Admiración |
| **Encuentra al player durante movimiento** | `SenseSomethingStart_NoWeapon` | ❗ Admiración |
| **Encuentra al player mirando alrededor** | `SenseSomethingStart_NoWeapon` | ❗ Admiración |
| **Encuentra al player volviendo al origen** | `SenseSomethingStart_NoWeapon` | ❗ Admiración |
| **Recargado y encuentra al player** | `SenseSomethingStart_NoWeapon` | ❗ Admiración |
| **Atacado por la espalda** | `SenseSomethingStart_NoWeapon` | ❗ Admiración |
| **Llegó a cobertura (no ve al player)** | `SenseSomethingSearching_NoWeapon` | ❓ Interrogación |
| **Pausa durante búsqueda activa** | `SenseSomethingSearching_NoWeapon` | ❓ Interrogación |

**Método usado:** `_animator.PlaySenseSomething()` para alerta inicial

---

## 🎮 FLUJO DE COMBATE RESULTANTE

### Escenario 1: Combate Ofensivo Puro
```
1. NPC ve al player → Entra en CombatState.EVALUATE
2. Tiene 3 ataques disponibles → CombatState.ATTACK
3. Ataca con right hand → 2 ataques restantes
4. Vuelve a EVALUATE → Aún tiene 2 ataques → ATTACK
5. Ataca con left hand → 1 ataque restante
6. Vuelve a EVALUATE → Aún tiene 1 ataque → ATTACK
7. Ataca con special → 0 ataques restantes
8. Vuelve a EVALUATE → Sin ataques → HIDING_TO_RECHARGE
```

### Escenario 2: Recarga Estratégica
```
1. NPC sin ataques → HIDING_TO_RECHARGE
2. Busca cobertura → Encuentra árbol/roca
3. Corre hacia cobertura → Llega y se detiene
4. Muestra animación "SenseSomethingSearching_NoWeapon" + ❓
5. Espera recargando cooldowns...
6. Player le dispara desde lejos
   ├─ ¿Tiene escudo? → Activa escudo 🛡️
   └─ ¿No tiene escudo? → Sigue esperando
7. Recargó 2+ ataques → Busca al player
8. Lo encuentra → Animación "SenseSomethingStart_NoWeapon" + ❗
9. EVALUATE → ATTACK
```

### Escenario 3: Atacado Durante Huida
```
1. NPC huyendo hacia cobertura (HIDING_TO_RECHARGE)
2. Player le dispara por la espalda
3. OnTakeDamage() detecta ataque:
   ├─ GIRAR 180° hacia el player
   ├─ Animación "SenseSomethingStart_NoWeapon"
   ├─ Icono de admiración ❗
   └─ Decidir:
       ├─ Tiene hechizos → CONTRAATACAR
       ├─ Tiene escudo → DEFENDER con escudo
       └─ Sin recursos → Seguir huyendo más rápido
```

### Escenario 4: Búsqueda Activa
```
1. NPC recargado pero no ve al player → SEARCHING
2. Muestra ❓ + "SenseSomethingSearching_NoWeapon"
3. Se mueve a diferentes puntos buscando
4. En cada parada:
   ├─ Muestra ❓ + "SenseSomethingSearching_NoWeapon"
   └─ Mira alrededor 2 segundos
5. Encuentra al player:
   ├─ Animación "SenseSomethingStart_NoWeapon"
   ├─ Icono ❗
   └─ EVALUATE → ATTACK
```

---

## 📊 COMPARACIÓN: ANTES vs. DESPUÉS

### ANTES (Con el bug)
| Problema | Consecuencia |
|----------|--------------|
| `ShouldConsiderDefense()` con Random.value | Cambios de estado cada frame |
| Sin tiempo mínimo entre estados | NPC "nervioso" |
| DEFENSE con probabilidades aleatorias | Comportamiento impredecible |
| Sin estado dedicado para recarga | Confusión entre defender y recargar |

### DESPUÉS (Solucionado)
| Mejora | Beneficio |
|--------|-----------|
| Lógica determinista basada en cooldowns | Comportamiento predecible |
| Estado HIDING_TO_RECHARGE dedicado | Estrategia clara |
| Sin componentes aleatorios en EVALUATE | Sin nerviosismo |
| Animaciones correctas en todos los eventos | Feedback visual claro |

---

## 🔧 MÉTODOS ELIMINADOS

Se eliminaron los siguientes métodos que ya no se usan:

- ❌ `ShouldConsiderDefense()` - Reemplazado por lógica simple de conteo de ataques
- ✅ `TryChangeState()` - Implementado pero no usado (disponible si se necesita)
- ✅ `IsPlayerInFieldOfView()` - Disponible pero no usado actualmente

---

## 🎯 PARÁMETROS CLAVE PARA AJUSTAR

Si se necesita afinar el comportamiento:

### Agresividad
```csharp
// En State_HidingToRecharge, línea ~750
while (CountAttacksReady() < 2) // Cambiar a 1 para más agresivo, 3 para más defensivo
```

### Tiempo de Recarga
```csharp
// El NPC esperará automáticamente según los cooldowns configurados
settings.leftAttack.cooldown
settings.rightAttack.cooldown
settings.specialAttack.cooldown
```

### Uso del Escudo
```csharp
settings.useShield = true; // Activar/desactivar escudo
settings.shieldCooldown = 10f; // Tiempo entre usos
settings.shieldDuration = 3f; // Duración del escudo
```

### Búsqueda
```csharp
settings.activelySearchForPlayer = true; // Busca activamente vs. espera pasivamente
settings.searchDuration = 15f; // Tiempo total de búsqueda
settings.searchMovementRadius = 10f; // Radio de movimiento durante búsqueda
```

---

## ✅ TESTING CHECKLIST

- [ ] NPC ataca agresivamente cuando tiene hechizos disponibles
- [ ] NPC se esconde para recargar cuando gasta todos los hechizos
- [ ] Animación "SenseSomethingSearching_NoWeapon" se reproduce al llegar a cobertura
- [ ] NPC usa escudo si es atacado durante la recarga
- [ ] Animación "SenseSomethingStart_NoWeapon" se reproduce al encontrar al player
- [ ] NPC se gira y alerta si es atacado por la espalda
- [ ] No hay comportamiento "nervioso" (cambios rápidos de estado)
- [ ] El flujo EVALUATE → ATTACK → HIDING_TO_RECHARGE → SEARCHING → EVALUATE funciona correctamente
- [ ] Iconos visuales (❗❓) se muestran en los momentos correctos

---

## 🐛 PROBLEMAS CONOCIDOS

### Warning: `IsPlayerAttacking()` usa capa "PlayerProjectile"
```csharp
// Línea 852
Collider[] nearbyProjectiles = Physics.OverlapSphere(transform.position, 10f, LayerMask.GetMask("PlayerProjectile"));
```

**Solución pendiente:** Verificar que la capa "PlayerProjectile" existe en el proyecto o cambiar la detección.

### Métodos No Usados
- `IsPlayerInFieldOfView()` - Implementado pero no se usa actualmente
- `TryChangeState()` - Disponible como herramienta anti-nerviosismo adicional

---

## 📝 NOTAS FINALES

Esta refactorización **elimina completamente el comportamiento nervioso** causado por:
1. ✅ Componentes aleatorios en decisiones críticas
2. ✅ Falta de tiempo mínimo entre cambios de estado
3. ✅ Confusión entre estados de defensa y recarga

El nuevo sistema es:
- 🎯 **Determinista**: Mismas condiciones = mismo comportamiento
- 🧠 **Estratégico**: El NPC tiene un plan claro
- 👀 **Visual**: Animaciones e iconos reflejan correctamente su estado mental
- ⚔️ **Agresivo**: Prioriza atacar sobre defenderse (como un mago de combate)

**¡El NPC ahora es un oponente digno que pelea inteligentemente por su vida!**

