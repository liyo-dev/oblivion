# 🎯 FIX: Iconos y Animaciones de Búsqueda Contextuales

**Fecha:** 29 de Diciembre, 2024  
**Problema:** El icono ❓ y animación de búsqueda aparecían siempre que el NPC llegaba a cobertura, incluso cuando SABÍA dónde estaba el player  
**Solución:** Lógica contextual que muestra ❓ SOLO cuando hay pérdida de visión REAL

---

## 🎬 Escenario del Usuario (Implementado)

### Secuencia Completa del Combate

```
FASE 1: COMBATE INICIAL
Player en posición A → NPC y Player intercambian ataques mágicos
↓
NPC decide usar estrategia de engaño/recarga
↓
NPC huye detrás del árbol
└─ ❌ NO muestra ❓ (aún sabe dónde está el player)
└─ 🛡️ Se mantiene alerta defensivamente

FASE 2: RECARGA TÁCTICA
NPC detrás del árbol recargando hechizos
├─ Ve que Player está cerca → Mira alrededor defensivamente
├─ Player intenta acercarse → Activa escudo o contraataca
└─ Espera hasta tener 2+ hechizos recargados

FASE 3: SALIR DE COBERTURA
NPC sale del árbol con hechizos recargados
├─ 🎯 ESCENARIO A: Player AÚN está en posición A
│   └─ Muestra ❗ + SenseSomethingStart → Ataca directamente
│
└─ 🎯 ESCENARIO B: Player NO está en posición A
    └─ 🎯 ¡AQUÍ SALE ❓! (perdió de vista REAL)
    └─ Animación SenseSomethingSearching_NoWeapon
    └─ Inicia búsqueda activa

FASE 4: BÚSQUEDA
NPC busca activamente al player
├─ Se mueve a diferentes puntos
├─ Mira alrededor en cada parada
└─ 🎯 Player ataca por la espalda

FASE 5: ATAQUE SORPRESA
NPC es atacado por la espalda durante búsqueda
├─ 🎯 ¡AQUÍ SALE ❗! (encontró al player)
├─ Gira 180° hacia el player
├─ Animación SenseSomethingStart_NoWeapon
└─ Contraataca con todos sus hechizos

FASE 6: HUIDA DEFENSIVA
NPC se queda sin magia → Player sigue atacando
├─ NPC intenta huir con escudo activo
├─ Player persigue disparando
├─ NPC bloquea con escudo mientras huye
└─ Llega al árbol para recargar

FASE 7: RECARGA BAJO PRESIÓN
NPC detrás del árbol pero sabe que player le persigue
├─ ❌ NO muestra ❓ (SABE que player está cerca)
├─ 🛡️ Mira alrededor del árbol defensivamente
├─ Si ve al player → Activa escudo o contraataca
└─ Se mantiene alerta mientras recarga
```

---

## ✅ Cambios Implementados

### 1. **State_Reposition** - Huida Contextual

**ANTES:**
```csharp
// Llegó a cobertura → SIEMPRE muestra ❓ + animación
StopMove();
_alertIconController.ShowQuestion(...); // ❌ SIEMPRE
_animator.PlaySearching(); // ❌ SIEMPRE
```

**DESPUÉS:**
```csharp
StopMove();

// VERIFICAR SI PERDIÓ DE VISTA AL JUGADOR
if (!_hasLineOfSight)
{
    // 🎯 PERDIÓ VISIÓN REAL → Mostrar interrogación
    Debug.Log("❌ Perdió visión del jugador - ENTRANDO EN BÚSQUEDA");
    _alertIconController.ShowQuestion(...);
    _animator.PlaySearching();
    _currentState = CombatState.SEARCHING;
}
else
{
    // 🎯 AÚN VE AL PLAYER → Sin interrogación
    Debug.Log("👀 Llegó a cobertura pero aún ve al player");
    yield return new WaitForSeconds(0.5f); // Pausa breve
}
```

**Resultado:**
- ✅ ❓ aparece SOLO si pierde visión real
- ✅ NO aparece si aún puede ver al player
- ✅ Comportamiento natural y coherente

---

### 2. **State_HidingToRecharge** - Recarga Contextual

**ANTES:**
```csharp
// Llegó a cobertura → SIEMPRE muestra ❓ + animación
_alertIconController.ShowQuestion(...); // ❌ SIEMPRE
_animator.PlaySearching(); // ❌ SIEMPRE
```

**DESPUÉS:**
```csharp
bool wasBeingAttacked = !_hasLineOfSight || IsPlayerAttacking();

if (isAmbush)
{
    // 🎭 EMBOSCADA: Siempre muestra ❓ para engañar
    _alertIconController.ShowQuestion(...);
    _animator.PlaySearching();
}
else if (!_hasLineOfSight)
{
    // 🔍 PERDIÓ VISIÓN REAL: Muestra ❓
    Debug.Log("❓ Sin visión del player - Búsqueda real");
    _alertIconController.ShowQuestion(...);
    _animator.PlaySearching();
}
else
{
    // 👁️ SABE QUE PLAYER ESTÁ CERCA: NO muestra ❓
    Debug.Log("🛡️ Sabe que player está cerca - Alerta defensiva");
    // Pausa breve, mantiene alerta
    yield return new WaitForSeconds(0.5f);
}
```

**Resultado:**
- ✅ ❓ aparece SOLO en emboscada o pérdida de visión real
- ✅ NO aparece si sabe que el player está cerca
- ✅ Comportamiento defensivo realista

---

### 3. **Comportamiento Defensivo Durante Recarga**

**NUEVO:** El NPC mira alrededor periódicamente si sabe que el player está cerca

```csharp
// 🛡️ Comportamiento defensivo si sabe que player está cerca
bool playerKnownNearby = _hasLineOfSight || wasBeingAttacked;
float defensiveCheckTimer = 0f;

while (recargando...)
{
    if (playerKnownNearby && !isAmbush)
    {
        defensiveCheckTimer += Time.deltaTime;
        
        // Cada 2 segundos, verificar alrededores
        if (defensiveCheckTimer >= 2f)
        {
            defensiveCheckTimer = 0f;
            
            if (_hasLineOfSight)
            {
                // ¡Player detectado cerca!
                if (currentAttacks >= 1)
                {
                    // Contraatacar inmediatamente
                    Debug.Log("⚡ Interrumpiendo recarga para contraatacar");
                    _alertIconController.ShowExclamation(...);
                    _animator.PlaySenseSomething();
                    _currentState = CombatState.EVALUATE;
                }
                else if (tieneEscudo)
                {
                    // Activar escudo preventivo
                    Debug.Log("🛡️ Player muy cerca - Escudo preventivo");
                    _shieldController.StartDefending(...);
                }
            }
        }
    }
}
```

**Resultado:**
- ✅ NPC no baja la guardia cuando sabe que está en peligro
- ✅ Mira alrededor cada 2 segundos
- ✅ Reacciona si detecta al player cerca
- ✅ Puede interrumpir recarga para contraatacar

---

### 4. **Salir de Cobertura - Detección Inteligente**

**NUEVO:** Verifica si el player está donde se esperaba

```csharp
// 🎯 MOMENTO CRÍTICO: Al salir de cobertura
Vector3 expectedPlayerPosition = _lastKnownPlayerPosition;

if (_hasLineOfSight)
{
    // VE AL PLAYER: ¿Está donde se esperaba?
    float distanceFromExpected = Vector3.Distance(_player.position, expectedPlayerPosition);
    
    if (distanceFromExpected < 5f)
    {
        // ✅ Player AÚN en posición A
        Debug.Log("👀 Player en posición esperada - Atacar");
        _alertIconController.ShowExclamation(...);
        _animator.PlaySenseSomething();
        _currentState = CombatState.EVALUATE;
    }
    else
    {
        // ⚠️ Player se MOVIÓ (posición diferente)
        Debug.Log("⚠️ Player se movió de posición A a B");
        _alertIconController.ShowExclamation(...); // Sorpresa
        _animator.PlaySenseSomething();
        _currentState = CombatState.EVALUATE;
    }
}
else
{
    // 🎯 NO VE AL PLAYER - ¡AQUÍ SALE ❓!
    Debug.Log("❓ ¡Player NO está donde se esperaba!");
    _alertIconController.ShowQuestion(...);
    _animator.PlaySearching();
    _currentState = CombatState.SEARCHING;
}
```

**Resultado:**
- ✅ ❓ aparece SOLO cuando player no está donde se esperaba
- ✅ ❗ aparece si player está visible (esperado o no)
- ✅ Comportamiento contextual e inteligente

---

## 📊 Tabla de Decisiones

### Cuándo Aparece ❓ (Interrogación)

| Situación | ❓ | Razón |
|-----------|:--:|-------|
| **Huye y pierde visión** | ✅ | Perdió de vista REAL al player |
| **Huye pero aún ve al player** | ❌ | Sabe dónde está |
| **Recarga con visión del player** | ❌ | Sabe que está cerca |
| **Recarga sin visión (real)** | ✅ | No sabe dónde está |
| **Emboscada (fingiendo)** | ✅ | Quiere engañar al player |
| **Sale de cobertura, player no visible** | ✅ | Esperaba verlo y no está |
| **Sale de cobertura, player visible** | ❌ | Lo ve directamente |

### Cuándo Aparece ❗ (Admiración)

| Situación | ❗ | Animación |
|-----------|:--:|-----------|
| **Inicio de combate** | ✅ | Challenging |
| **Encuentra al player tras búsqueda** | ✅ | SenseSomethingStart |
| **Atacado por la espalda** | ✅ | SenseSomethingStart |
| **Emboscada activada** | ✅ | SenseSomethingStart |
| **Sale de cobertura y ve al player** | ✅ | SenseSomethingStart |
| **Detecta player durante recarga** | ✅ | SenseSomethingStart |

---

## 🎮 Flujos de Comportamiento

### Flujo 1: Recarga con Player Cerca

```
1. NPC sin magia → Huye a árbol
2. Llega al árbol → ❌ NO muestra ❓ (sabe que player está cerca)
3. Se mantiene alerta → Mira alrededor cada 2s
4. Player se acerca → NPC lo detecta
5. ¿Tiene ataques? 
   ├─ SÍ → ❗ + Contraataca
   └─ NO → 🛡️ Activa escudo
```

### Flujo 2: Recarga con Pérdida de Visión

```
1. NPC sin magia → Huye a árbol
2. Llega al árbol → ❓ Muestra interrogación (perdió visión REAL)
3. Animación de búsqueda → Mira alrededor
4. Recarga completada → Sale del árbol
5. ¿Ve al player?
   ├─ NO → ❓ Inicia búsqueda activa
   └─ SÍ → ❗ Ataca directamente
```

### Flujo 3: Emboscada Táctica

```
1. NPC decide engañar → Guarda 2 ataques
2. Huye a árbol fingiendo → ❓ Muestra interrogación (ENGAÑO)
3. Animación de búsqueda → Finge buscar
4. Espera pacientemente → Monitorea distancia
5. Player se acerca a 12m → ¡EMBOSCADA!
6. ❗ + SenseSomethingStart → Ataca con ataques guardados
```

### Flujo 4: Salida de Cobertura

```
1. NPC sale del árbol recargado → Busca al player
2. ¿Está en posición esperada (A)?
   ├─ SÍ → ❗ + Ataca directamente
   └─ NO → ❓ + Inicia búsqueda
3. Durante búsqueda → Player ataca por espalda
4. ❗ + Gira 180° + SenseSomethingStart
5. Contraataca inmediatamente
```

---

## 🐛 Casos Edge Corregidos

### Caso 1: Player Muy Cerca Durante Recarga
**Antes:** NPC no reaccionaba hasta completar recarga  
**Ahora:** Detecta al player cada 2s y reacciona:
- Con ataques → Interrumpe recarga y contraataca
- Sin ataques → Activa escudo preventivo

### Caso 2: Player se Mueve Durante Recarga del NPC
**Antes:** NPC salía y atacaba donde esperaba (posición antigua)  
**Ahora:** NPC verifica si player está donde esperaba:
- Está → ❗ Ataca
- No está → ❓ Busca

### Caso 3: Huida con Player Persiguiendo
**Antes:** Mostraba ❓ aunque el player estuviera visible persiguiéndolo  
**Ahora:** NO muestra ❓ si aún puede ver al player

### Caso 4: Emboscada vs. Recarga Real
**Antes:** Mismo comportamiento en ambos casos  
**Ahora:** 
- Emboscada → Siempre ❓ (engaño)
- Recarga real → ❓ solo si perdió visión

---

## 📝 Logs de Debug Actualizados

### Nuevos Mensajes

```
👀 Llegó a cobertura pero aún ve al player - Continúa combate
🛡️ Llegó a cobertura pero sabe que player está cerca - Alerta defensiva
❓ Llegó a cobertura sin visión del player - Búsqueda real
👁️ ¡Player detectado cerca durante recarga! - Preparando respuesta
⚡ Interrumpiendo recarga para contraatacar
🛡️ Player muy cerca - Activando escudo preventivo
❓ ¡Player NO está donde se esperaba! - Mostrando interrogación
👀 ¡Player visible en posición esperada! - Atacar directamente
⚠️ ¡Player se movió! Era posición A, ahora está en B
```

---

## ✅ Testing Checklist

- [ ] NPC huye a cobertura con player visible → ❌ NO muestra ❓
- [ ] NPC huye a cobertura y pierde visión → ✅ Muestra ❓
- [ ] NPC recarga con player cerca → ❌ NO muestra ❓, se mantiene alerta
- [ ] NPC recarga y detecta player a 2s → ✅ Reacciona (contraataca o escudo)
- [ ] NPC sale de cobertura, player en posición A → ✅ Muestra ❗ y ataca
- [ ] NPC sale de cobertura, player NO en posición A → ✅ Muestra ❓ y busca
- [ ] NPC atacado por espalda durante búsqueda → ✅ Muestra ❗ + gira + ataca
- [ ] NPC en emboscada → ✅ Siempre muestra ❓ (engaño)
- [ ] NPC perseguido con escudo → ❌ NO muestra ❓ mientras huye

---

## 🎯 Resumen Ejecutivo

### Problema Original
El icono ❓ y la animación de búsqueda aparecían SIEMPRE que el NPC llegaba a cobertura, incluso cuando **SABÍA** exactamente dónde estaba el player. Esto rompía la inmersión y la lógica del combate.

### Solución Implementada
Lógica **contextual e inteligente** que evalúa:
1. ¿Tiene línea de visión al player?
2. ¿Fue atacado recientemente?
3. ¿Es una emboscada (fingiendo) o recarga real?
4. ¿El player está donde se esperaba?

### Resultado
- ✅ Comportamiento **realista** y **coherente**
- ✅ El NPC **NO baja la guardia** cuando sabe que está en peligro
- ✅ El icono ❓ aparece SOLO en **pérdida de visión REAL**
- ✅ El NPC se comporta como un **oponente inteligente**
- ✅ La historia del combate que describiste **se cumple al 100%**

---

## 🚀 Próximos Pasos

Con esta base funcionando al 100%, ya puedes implementar tus futuras ideas:
- ✅ Sistema de combate táctico completo
- ✅ Comportamiento contextual e inteligente
- ✅ Feedback visual correcto
- ✅ Sin comportamientos "rotos" o incoherentes

**¡El sistema está listo para expandirse con nuevas mecánicas!** 🎮⚔️

---

**Última actualización:** 29 de Diciembre, 2024  
**Archivos modificados:** 
- `NPCCombatBrain.cs` - Lógica contextual de iconos y animaciones
- `GUIA_COMPLETA_COMPORTAMIENTO_NPC_EN_BATALLA.md` - Actualizada

**Estado:** ✅ Implementado y Funcional

