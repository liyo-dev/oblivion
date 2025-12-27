# 🐛 Diagnóstico: NPC No Ataca - Solo se Mueve

**Fecha:** 2025-12-26  
**Estado:** En investigación - Logging agregado

---

## 🔍 Problema Reportado

Al interactuar con un NPC de combate (Erika), la batalla se inicia correctamente:
- ✅ CombatLoop se activa
- ✅ El NPC entra en modo batalla
- ✅ El NPC se mueve (acercándose y retrocediendo)
- ❌ **El NPC NUNCA dispara ni ejecuta ataques**

---

## 📊 Análisis de Logs

### Logs Observados:

```
[NPCCombatBrain] ✅ Iniciando CombatLoop()
[NPCCombatBrain] ⚔️ Cambiando a estado AGRESIVO (jugador cerca)
[NPCCombatBrain] 🏃 MOVIENDO - Retrocediendo (repetido cientos de veces)
[NPCCombatBrain] 🏃 MOVIENDO - Acercándose (repetido cientos de veces)
```

### Logs AUSENTES (nunca aparecen):

```
❌ [NPCCombatBrain] ⚔️ PARADO - Atacando
❌ [NPCCombatBrain] ⚔️ Ejecutando spell cast LEFT/RIGHT/SPECIAL
❌ [NPCCombatBrain] 🔄 LEFT/RIGHT/SPECIAL cooldown: X.XXs
❌ [NPCCombatBrain] ✅ ATACANDO - Mirando al player correctamente
```

---

## 🔬 Análisis de Código

### Flujo de Combate (CombatLoop):

El loop de combate tiene 3 prioridades:

```csharp
// PRIORIDAD 1: Si puede atacar → PARADO y atacar
if (hasAttackReady && clearLos && _attackLockTimer <= 0f && !_isWindup && inAttackRange)
{
    StopAndIdle();
    FacePlayer();
    TryExecuteAttack();  // ❌ NUNCA SE EJECUTA
}
// PRIORIDAD 2: Si está en windup → PARADO
else if (_isWindup || _postAttackHoldTimer > 0f)
{
    StopAndIdle();
}
// PRIORIDAD 3: Reposicionarse → MOVERSE
else
{
    // ✅ SIEMPRE entra aquí
    if (tooClose) { Retroceder(); }
    else if (tooFar) { Acercarse(); }
    else { Circular(); }
}
```

### Condiciones para Atacar:

Para que el NPC ataque, **TODAS** estas condiciones deben cumplirse:

1. ✅ `hasAttackReady` - Al menos un ataque con cooldown listo
2. ✅ `clearLos` - Línea de visión despejada (o no requerida)
3. ✅ `_attackLockTimer <= 0f` - No está en cooldown de ataque
4. ✅ `!_isWindup` - No está en wind-up de ataque previo
5. ❓ `inAttackRange` - **Distancia entre minDistance y maxDistance**

---

## 🎯 Hipótesis Principal

**El NPC probablemente NO está entrando en `inAttackRange`**

### Definición de `inAttackRange`:

```csharp
bool inAttackRange = distanceToPlayer >= _settings.minDistance && 
                     distanceToPlayer <= _settings.maxDistance;
```

### Comportamiento Observado:

```
if (tooClose) → Retrocediendo  // distancia < minDistance
if (tooFar) → Acercándose      // distancia > maxDistance
```

**Problema:** El NPC oscila constantemente entre `tooClose` y `tooFar`, pero **nunca se queda en el rango de ataque** el tiempo suficiente para disparar.

---

## 🔧 Posibles Causas

### 1. Rango de Ataque Demasiado Estrecho

Si `minDistance` y `maxDistance` están muy cerca:
```
minDistance: 5.0
maxDistance: 6.0  // Solo 1 metro de ventana
```

El NPC puede estar "oscilando" alrededor del límite sin poder estabilizarse en el rango de ataque.

### 2. NavMesh Repath Interval Demasiado Corto

```csharp
if (repathTimer <= 0f)
{
    NavMeshAgentUtility.SetDestination(_agent, targetPos, 0.5f);
    repathTimer = _settings.repathInterval;  // Si es muy corto, re-calcula constantemente
}
```

### 3. Cooldowns Iniciales NO Establecidos

```csharp
// Al iniciar BeginCombat(), los cooldowns están en 0f por defecto
_leftAttackCooldown = 0f;  // ← ¿Esto está correcto?
_rightAttackCooldown = 0f;
_specialAttackCooldown = 0f;
```

**¿Se están inicializando correctamente en `BeginCombat()`?**

### 4. Line of Sight Siempre Fallando

Si `requireLineOfSight = true` y hay algo bloqueando la visión (geometría, otros NPCs), el NPC nunca atacará.

---

## 🛠️ Solución Implementada

### Paso 1: Logging Detallado

He agregado logging exhaustivo para diagnosticar el problema:

```csharp
// Cada 60 frames (para no spam), mostrar:
Debug.Log($"[NPCCombatBrain] 🔍 DIAGNÓSTICO ATAQUE:" +
    $"\n  hasAttackReady: {hasAttackReady}" +
    $"\n  clearLos: {clearLos}" +
    $"\n  attackLockTimer: {_attackLockTimer:F2}" +
    $"\n  isWindup: {_isWindup}" +
    $"\n  inAttackRange: {inAttackRange}" +
    $"\n  distance: {distanceToPlayer:F2} (min:{_settings.minDistance:F2}, max:{_settings.maxDistance:F2})" +
    $"\n  Cooldowns - LEFT:{_leftAttackCooldown:F2} RIGHT:{_rightAttackCooldown:F2} SPECIAL:{_specialAttackCooldown:F2}");
```

### Paso 2: Logging de Configuración Inicial

```csharp
Debug.Log($"[NPCCombatBrain] 🎮 CONFIGURACIÓN INICIAL:" +
    $"\n  minDistance: {_settings.minDistance:F2}" +
    $"\n  maxDistance: {_settings.maxDistance:F2}" +
    $"\n  requireLineOfSight: {_settings.requireLineOfSight}" +
    $"\n  Ataques configurados:" +
    $"\n    LEFT: {leftAttack.animationState} - cooldown: {leftAttack.cooldown:F2}s" +
    $"\n    RIGHT: {rightAttack.animationState} - cooldown: {rightAttack.cooldown:F2}s" +
    $"\n    SPECIAL: {specialAttack.animationState} - cooldown: {specialAttack.cooldown:F2}s" +
    $"\n  Cooldowns iniciales - LEFT:{_leftAttackCooldown:F2} RIGHT:{_rightAttackCooldown:F2} SPECIAL:{_specialAttackCooldown:F2}");
```

---

## 📋 Próximos Pasos

### 1. Ejecutar en Unity y Revisar Logs

Después de compilar, interactuar con Erika nuevamente y buscar en la consola:

```
🔍 DIAGNÓSTICO ATAQUE
```

Este log mostrará **exactamente** qué condición está fallando.

### 2. Verificar Configuración del NPCCombatConfig

Revisar el ScriptableObject `NPC_Combat_Config_Erika`:

- ✅ `minDistance` y `maxDistance` tienen un rango razonable (ej: 5-10 metros)
- ✅ `leftAttack`, `rightAttack`, `specialAttack` tienen:
  - `animationState` configurado (no vacío)
  - `cooldown` > 0
- ✅ `requireLineOfSight` está en `false` (para testing)

### 3. Posibles Soluciones Según el Diagnóstico

#### Si `inAttackRange = false`:
```csharp
// Aumentar el rango de ataque
minDistance: 3.0 → 4.0
maxDistance: 8.0 → 10.0
```

#### Si `hasAttackReady = false`:
```csharp
// Verificar que los cooldowns se inicializan en 0 al comenzar combate
// O establecerlos explícitamente en BeginCombat():
_leftAttackCooldown = 0f;
_rightAttackCooldown = 0f;
_specialAttackCooldown = 0f;
```

#### Si `clearLos = false`:
```csharp
// Temporalmente desactivar requirement de LoS
requireLineOfSight: false
```

#### Si `_attackLockTimer > 0`:
```csharp
// Ver por qué el timer no decrementa o se queda bloqueado
// Verificar que cdStep está correcto en el loop
```

---

## 🔍 Información Adicional Necesaria

Para continuar el diagnóstico, necesito que proporciones:

1. **Los logs completos** después de ejecutar con el nuevo logging
2. **Captura del Inspector** del `NPCCombatConfig` de Erika mostrando:
   - minDistance y maxDistance
   - leftAttack, rightAttack, specialAttack configuration
   - requireLineOfSight
3. **Distancia aproximada** a la que te colocas del NPC al iniciar combate

---

## 📝 Archivos Modificados

- ✅ `NPCCombatBrain.cs` - Logging detallado agregado

---

**Estado:** ✅ PROBLEMA IDENTIFICADO Y SOLUCIONADO

---

## 🚨 **CAUSA RAÍZ ENCONTRADA**

### ❌ EL PROBLEMA:

```
minDistance: 2,00
maxDistance: 2,00   ← IGUALES - Rango de ataque = 0 metros
```

**El rango de ataque es de CERO metros** - El NPC necesita estar EXACTAMENTE a 2.00 metros para atacar, lo cual es imposible de mantener.

### 📊 Por Qué No Funciona:

```csharp
bool tooClose = distanceToPlayer < 2.0;      // < 2.0 → Retrocede
bool tooFar = distanceToPlayer > 2.0;        // > 2.0 → Se acerca  
bool inAttackRange = distanceToPlayer >= 2.0 && 
                     distanceToPlayer <= 2.0; // == 2.0 EXACTAMENTE (imposible mantener)
```

**Resultado:** El NPC oscila constantemente entre "muy cerca" (< 2.0) y "muy lejos" (> 2.0), pero NUNCA está en rango de ataque el tiempo suficiente para disparar.

---

## ✅ **SOLUCIÓN**

### Paso 1: Ajustar el NPCCombatConfig

1. Abrir Unity
2. Buscar: `Assets\_NPCs\Combat\NPC_Combat_Config_Erika.asset`
3. **Cambiar:**
   ```
   Min Distance: 4.0  (el NPC retrocede si estás más cerca)
   Max Distance: 10.0 (el NPC se acerca si estás más lejos)
   ```

Esto le da una **ventana de 6 metros** (de 4m a 10m) donde puede atacar libremente.

### Paso 2: (Opcional) Desactivar Line of Sight para testing

```
Require Line Of Sight: false  (temporalmente, para testing)
```

El config actual tiene `requireLineOfSight: True`, lo cual puede causar problemas adicionales si hay geometría bloqueando la visión.

---

## 🎯 **Valores Recomendados**

### Para Mago/Ranged (como Erika):
```
minDistance: 4.0-5.0   (mantener distancia del jugador)
maxDistance: 8.0-12.0  (rango de hechizos)
requireLineOfSight: false (o true si quieres que requiera ver al jugador)
```

### Para Melee/Guerrero:
```
minDistance: 1.5-2.0   (pelear cuerpo a cuerpo)
maxDistance: 3.0-5.0   (alcance de espada)
```

### Para Híbrido:
```
minDistance: 3.0       (versátil)
maxDistance: 7.0       (rango medio)
```

---

## 📈 **Verificación Post-Fix**

Después de cambiar los valores, deberías ver en los logs:

```
[NPCCombatBrain] 🎮 CONFIGURACIÓN INICIAL:
  minDistance: 4,00    ✅
  maxDistance: 10,00   ✅
  Cooldowns iniciales - LEFT:0,00 RIGHT:0,00 SPECIAL:0,00  ✅
```

Y cada 60 frames (1 segundo):

```
[NPCCombatBrain] 🔍 DIAGNÓSTICO ATAQUE:
  hasAttackReady: True
  inAttackRange: True   ✅ ← Ahora debería ser true frecuentemente
  [NPCCombatBrain] ⚔️ PARADO - Atacando  ✅
  [NPCCombatBrain] ⚔️ Ejecutando spell cast LEFT  ✅
```

---

**Estado:** ✅ RESUELTO - Ajustar minDistance y maxDistance en el config

