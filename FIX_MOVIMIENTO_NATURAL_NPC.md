# FIX: Movimiento Natural del NPC en Combate

## 🔴 Problema Reportado

**Descripción del usuario:**
> "El movimiento del NPC es muy máquina de los 60. Cuando el NPC va a moverse que mire al punto al que se dirige. Solo mira al jugador cuando necesite enfocarse en él: cuando le voy a atacar, o cuando ha salido huyendo y quiere ver si aún sigue detrás o está lo bastante lejos como para pararse y atacarle de nuevo."

**Análisis:**
El NPC estaba llamando constantemente a `FacePlayer()` o `FaceMovement()` en cada frame, lo que creaba un comportamiento rígido y antinatural, como un robot de los años 60. Faltaba inteligencia y contexto en las decisiones de rotación.

---

## 🎯 Solución Implementada

### Concepto: Rotación Contextual Inteligente

En lugar de forzar la rotación manualmente en cada frame, ahora el NPC usa **rotación automática del NavMeshAgent cuando se mueve** y **control manual solo cuando es necesario**.

### Reglas de Rotación

#### 🟢 NavMeshAgent Rota Automáticamente (Natural)
**Cuándo:** El NPC está **moviéndose** (huyendo, acercándose)
- ✅ `agent.updateRotation = true`
- ✅ El NPC mira hacia su **destino** (donde va)
- ✅ Rotación suave y natural del NavMeshAgent
- ✅ **No hay llamadas manuales a rotación**

**Casos:**
- Huyendo del jugador → Mira hacia donde corre (punto de escape)
- Acercándose al jugador → Mira hacia donde camina (posición del jugador)

#### 🔴 Control Manual de Rotación (FacePlayer)
**Cuándo:** El NPC necesita **enfocar al jugador** específicamente
- ✅ `agent.updateRotation = false`
- ✅ Llamadas explícitas a `FacePlayer()`

**Casos:**
1. **Atacando** → Necesita apuntar el hechizo hacia el jugador
2. **En guardia** → Observa al jugador mientras espera cooldowns
3. **Activando escudo** → Escudo debe estar orientado hacia el jugador
4. **Evaluando después de huir** → Mira hacia atrás para calcular distancia

---

## 🔧 Cambios Implementados

### Archivo: `NPCCombatBrain.cs`

#### 1. Configuración Inicial del NavMeshAgent (Línea ~287)

**ANTES:**
```csharp
_agent.angularSpeed = 180f;
// No había control de updateRotation
```

**DESPUÉS:**
```csharp
_agent.angularSpeed = 240f; // Rotación más rápida y natural
_agent.updateRotation = true; // ✅ Permitir rotación automática al inicio
```

#### 2. Función `StopAndIdle()` (Línea ~1343)

**ANTES:**
```csharp
void StopAndIdle()
{
    NavMeshAgentUtility.SafeSetStopped(_agent, true);
    _animator?.ResetMovement();
    // ...
}
```

**DESPUÉS:**
```csharp
void StopAndIdle()
{
    NavMeshAgentUtility.SafeSetStopped(_agent, true);
    
    // ✅ Desactivar rotación automática del NavMeshAgent cuando se detiene
    // Esto permite que FacePlayer() tome control manual
    if (_agent != null)
    {
        _agent.updateRotation = false;
    }
    
    _animator?.ResetMovement();
    // ...
}
```

**Razón:** Cuando el NPC se detiene, queremos control manual para que mire al jugador (evaluar, atacar, esperar).

#### 3. Función `StartMoving()` (Línea ~1373)

**ANTES:**
```csharp
void StartMoving(float speed)
{
    // Solo configuraba animaciones
    _animator?.SetMovementSpeed(speed, 0.08f);
}
```

**DESPUÉS:**
```csharp
void StartMoving(float speed)
{
    // ✅ Reactivar rotación automática del NavMeshAgent cuando se mueve
    // Esto hace que rote naturalmente hacia su destino
    if (_agent != null)
    {
        _agent.updateRotation = true;
        NavMeshAgentUtility.SafeSetStopped(_agent, false);
    }
    
    _animator?.SetMovementSpeed(speed, 0.08f);
    // ...
}
```

**Razón:** Cuando el NPC se mueve, el NavMeshAgent debe rotar automáticamente hacia su destino (natural).

#### 4. Lógica de Combat Loop (Línea ~509-632)

**Eliminadas llamadas innecesarias:**

**ANTES:**
```csharp
// Al huir
StartMoving(speed);
FaceMovement(); // ❌ Llamada manual redundante

// Al acercarse
StartMoving(speed);
FaceMovement(); // ❌ Llamada manual redundante
```

**DESPUÉS:**
```csharp
// Al huir
StartMoving(speed);
// ✅ NavMeshAgent rota automáticamente hacia el destino de escape

// Al acercarse
StartMoving(speed);
// ✅ NavMeshAgent rota automáticamente hacia el jugador
```

**Mantenidas llamadas necesarias:**

```csharp
// Al atacar
StopAndIdle();
FacePlayer(); // ✅ Necesita mirar al jugador para apuntar

// En guardia
StopAndIdle();
FacePlayer(); // ✅ Observa al jugador mientras espera

// Activando escudo
StopAndIdle();
FacePlayer(); // ✅ Escudo orientado hacia el jugador
```

---

## 🎬 Comportamiento Resultante

### Secuencia: Huida

```
Jugador se acerca (< 3m)
    ↓
NPC: "¡Demasiado cerca!"
    ↓
ComputeRetreatPosition() → Punto lejos
    ↓
SetDestination(retreatPoint)
    ↓
StartMoving() → updateRotation = true
    ↓
🏃 NavMeshAgent rota AUTOMÁTICAMENTE hacia retreatPoint
🏃 NPC corre mirando hacia donde va (NATURAL)
🏃 NO mira constantemente al jugador (REALISTA)
    ↓
Llegó a distancia segura
    ↓
StopAndIdle() → updateRotation = false
FacePlayer() → Mira hacia atrás
    ↓
😤 "¿Aún me sigues? Déjame evaluar..."
```

### Secuencia: Ataque

```
NPC en rango de ataque (4-8m)
    ↓
StopAndIdle() → updateRotation = false
    ↓
FacePlayer() → Rota hacia el jugador manualmente
    ↓
🎯 Apunta cuidadosamente (rotación controlada)
    ↓
TryExecuteAttack()
    ↓
⚡ Dispara hechizo (perfectamente apuntado)
```

### Secuencia: Acercamiento

```
Jugador muy lejos (> 12m)
    ↓
NPC: "Demasiado lejos para atacar"
    ↓
ComputeApproachPosition() → Posición del jugador
    ↓
SetDestination(playerPosition)
    ↓
StartMoving() → updateRotation = true
    ↓
🚶 NavMeshAgent rota AUTOMÁTICAMENTE hacia el jugador
🚶 NPC camina mirando hacia donde va (NATURAL)
    ↓
Llegó a rango de ataque
    ↓
StopAndIdle() → updateRotation = false
FacePlayer() → Enfrenta al jugador
    ↓
⚔️ Listo para atacar
```

---

## 📊 Comparación: Antes vs Después

| Situación | ANTES ❌ | DESPUÉS ✅ |
|-----------|---------|-----------|
| **Huyendo** | Llamaba `FaceMovement()` cada frame | NavMeshAgent rota automáticamente |
| **Acercándose** | Llamaba `FaceMovement()` cada frame | NavMeshAgent rota automáticamente |
| **Atacando** | Llamaba `FacePlayer()` cada frame | Llama `FacePlayer()` solo al detenerse |
| **En guardia** | Llamaba `FacePlayer()` cada frame | Llama `FacePlayer()` solo al detenerse |
| **Sensación** | Robótico, "máquina de los 60" | Natural, inteligente, realista |
| **Control rotación** | Manual constante | Automático (movimiento) / Manual (enfoque) |

---

## 🎯 Criterios de Rotación

### ¿Cuándo el NPC mira al jugador?

✅ **SÍ - Necesita enfocarse:**
1. Va a atacar (apuntar hechizo)
2. Está en guardia (observar mientras espera)
3. Activa escudo (orientar defensa)
4. Después de huir (evaluar distancia)

❌ **NO - No es necesario:**
1. Está corriendo (mira hacia donde va)
2. Está caminando (mira hacia donde va)
3. Está moviéndose en general (NavMeshAgent controla)

### ¿Cuándo el NavMeshAgent controla la rotación?

✅ **SÍ - Movimiento activo:**
- `StartMoving()` llamado
- `agent.updateRotation = true`
- Rota hacia `agent.destination`

❌ **NO - Quieto/Enfocado:**
- `StopAndIdle()` llamado
- `agent.updateRotation = false`
- Rotación manual con `FacePlayer()`

---

## 🧪 Testing

### Test 1: Huida Natural

**Pasos:**
1. Acercarse mucho al NPC (< 3m)
2. Observar que el NPC huye

**Verificar:**
- [ ] El NPC se gira hacia su punto de escape
- [ ] Corre mirando hacia donde va (no hacia ti)
- [ ] **NO** gira constantemente la cabeza hacia ti mientras corre
- [ ] Solo mira hacia atrás cuando se detiene

**Resultado Esperado:**
- ✅ Movimiento natural y fluido
- ✅ Comportamiento realista (como un humano huyendo)

### Test 2: Acercamiento Natural

**Pasos:**
1. Alejarse mucho del NPC (> 12m)
2. Observar que el NPC se acerca

**Verificar:**
- [ ] El NPC camina mirando hacia donde va (hacia ti)
- [ ] **NO** hay rotaciones bruscas o erráticas
- [ ] Movimiento suave y directo

**Resultado Esperado:**
- ✅ Camina como una persona normal
- ✅ No parece un robot rígido

### Test 3: Ataque Enfocado

**Pasos:**
1. Estar a distancia media (4-8m)
2. Observar que el NPC ataca

**Verificar:**
- [ ] El NPC se detiene completamente
- [ ] **Gira hacia ti** antes de disparar
- [ ] El hechizo sale **perfectamente apuntado**
- [ ] No hay movimiento durante el ataque

**Resultado Esperado:**
- ✅ Ataque preciso y deliberado
- ✅ Rotación controlada hacia el objetivo

### Test 4: Evaluación Post-Huida

**Pasos:**
1. Hacer que el NPC huya
2. Dejar de perseguirlo
3. Observar cuando se detiene

**Verificar:**
- [ ] El NPC se detiene cuando está lejos
- [ ] **Mira hacia atrás** (hacia ti)
- [ ] Evalúa la distancia
- [ ] Decide si atacar o seguir huyendo

**Resultado Esperado:**
- ✅ Comportamiento inteligente
- ✅ Como un humano evaluando la situación

---

## 💡 Filosofía del Cambio

### Problema Original
El NPC controlaba su rotación manualmente en cada frame, resultando en:
- Movimiento robótico y antinatural
- Rotaciones innecesarias
- Sensación de "autómata programado"

### Solución
**Confiar en el NavMeshAgent cuando sea apropiado:**
- El NavMeshAgent es excelente rotando hacia destinos
- Solo tomar control manual cuando hay una razón específica
- Resultado: Comportamiento más natural y fluido

### Analogía
**ANTES:** Como un robot que sigue instrucciones cada milisegundo
**DESPUÉS:** Como un humano que toma decisiones contextuales

---

## 📝 Notas Técnicas

### updateRotation del NavMeshAgent

**`updateRotation = true`:**
- El NavMeshAgent rota automáticamente hacia `destination`
- Usa `angularSpeed` configurado (240°/s)
- Rotación suave y natural

**`updateRotation = false`:**
- El NavMeshAgent NO rota automáticamente
- Permite control manual (FacePlayer, etc.)
- Necesario para apuntar hechizos

### Sincronización con Animator

El sistema respeta el `syncWithNavAgent` del `NPCSimpleAnimator`:
- Cuando se detiene: `syncWithNavAgent = true`
- Cuando se mueve: `syncWithNavAgent = false`

Esto evita conflictos entre NavMeshAgent y animaciones.

---

## ✅ Resumen

**Cambios Principales:**
1. ✅ NavMeshAgent controla rotación durante movimiento
2. ✅ Control manual solo cuando es necesario (atacar, observar)
3. ✅ Eliminadas llamadas redundantes a `FaceMovement()`
4. ✅ Comportamiento más natural y realista

**Resultado:**
- ✅ NPC se mueve como un humano inteligente
- ✅ Ya no parece "máquina de los 60"
- ✅ Rotación contextual y natural

---

**Fecha:** 27 de diciembre de 2025  
**Prioridad:** 🟠 ALTA  
**Estado:** ✅ IMPLEMENTADO  
**Testing:** Requerido en Unity

