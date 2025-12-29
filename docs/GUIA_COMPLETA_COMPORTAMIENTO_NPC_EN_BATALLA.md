# 📖 GUÍA COMPLETA: Comportamiento del NPC en Batalla

**Fecha:** 29 de Diciembre, 2024  
**Sistema:** NPCCombatBrain - IA de Combate Táctica  
**Tipo:** Mago Combatiente con Escudo Mágico

---

## 🎯 PREMISA FUNDAMENTAL

El NPC es un **MAGO DE COMBATE** con dos objetivos principales:
1. **MATAR AL PLAYER** - Es su objetivo primario
2. **SOBREVIVIR** - Teme por su vida y actúa estratégicamente

**Filosofía:** El NPC es **INTELIGENTE** y **TÁCTICO**. No es un enemigo torpe que solo ataca. Puede engañar, esconderse, defender y contraatacar según la situación.

---

## 🧙 CAPACIDADES DEL NPC

### ⚔️ Habilidades Ofensivas

| Habilidad | Descripción | Cooldown |
|-----------|-------------|----------|
| **Ataque Mano Izquierda** | Hechizo básico, disparo rápido | Configurable (ej: 3s) |
| **Ataque Mano Derecha** | Hechizo medio, más potente | Configurable (ej: 4s) |
| **Ataque Especial** | Hechizo poderoso, ambas manos | Configurable (ej: 8s) |

**Prioridad de Uso:**
1. Special (60% probabilidad si está disponible)
2. Right Hand
3. Left Hand

### 🛡️ Habilidades Defensivas

| Habilidad | Descripción | Duración | Cooldown |
|-----------|-------------|----------|----------|
| **Escudo Mágico** | Bloquea proyectiles del player | 3s (configurable) | 10s (configurable) |
| **Búsqueda de Cobertura** | Esconderse detrás de objetos | Instantáneo | Sin cooldown |
| **Esquiva Lateral** | Salto rápido a un lado | 0.5s | Sin cooldown |

### 🧠 Habilidades Tácticas

| Habilidad | Descripción | Cuando se Usa |
|-----------|-------------|---------------|
| **Flanqueo** | Moverse a un lado del player | 30% tras atacar |
| **Reposicionamiento** | Huir si está muy cerca | Distancia < minSafeDistance |
| **Engaño y Emboscada** | Fingir recarga para atraer | Probabilidad configurable |
| **Búsqueda Activa** | Buscar al player si lo pierde | Sin línea de visión |

---

## 🎮 MÁQUINA DE ESTADOS (FSM)

El NPC opera con una **Máquina de Estados Finitos** con 6 estados:

```
┌─────────────┐
│   EVALUATE  │ ← Centro de decisión
└─────┬───────┘
      │
      ├─→ REPOSITION ────→ Huir del player
      ├─→ ATTACK ────────→ Disparar hechizos
      ├─→ DEFENSE ───────→ Usar escudo/cobertura
      ├─→ HIDING_TO_RECHARGE → Esconderse para recargar
      └─→ SEARCHING ─────→ Buscar al player
```

### 1️⃣ EVALUATE (Evaluar)

**Función:** Cerebro del NPC - Decide qué hacer en cada momento

**Prioridades (en orden):**

```
A. ¿Puedo ver al player?
   └─ NO → IR A SEARCHING (buscar)

B. ¿Está muy cerca?
   └─ SÍ (< minSafeDistance) → IR A REPOSITION (huir)

C. ¿Debería usar estrategia de engaño?
   └─ SÍ (probabilidad × dificultad) → IR A HIDING_TO_RECHARGE (emboscada)

D. ¿Tengo ataques disponibles?
   ├─ SÍ → ¿Estoy en rango?
   │   ├─ SÍ → IR A ATTACK (atacar)
   │   └─ NO → Acercarse
   └─ NO → IR A HIDING_TO_RECHARGE (recargar)
```

**Logs:**
```
❌ Sin línea de visión - Iniciando búsqueda
⚠️ Player demasiado cerca (5.2m < 7m) - Reposicionando
🎭 ESTRATEGIA DE ENGAÑO ACTIVADA - Reservando 2 ataques
⚔️ Atacando - 3 ataques disponibles
🔋 Sin ataques disponibles - Necesito esconderme para recargar
```

---

### 2️⃣ REPOSITION (Reposicionar)

**Función:** Huir cuando el player está demasiado cerca

**Comportamiento:**

```
1. Detecta que player está < minSafeDistance
2. Busca cobertura detrás de objetos (árboles, rocas, cajas)
3. Si encuentra cobertura → Corre hacia ella (runSpeed)
4. Si NO encuentra → Huye en dirección opuesta
5. Al llegar → Muestra ❓ + animación "SenseSomethingSearching_NoWeapon"
6. Espera 1.5s (animación)
7. Verifica si aún puede ver al player:
   ├─ SÍ → Vuelve a EVALUATE
   └─ NO → Va a SEARCHING
```

**Animaciones:**
- Durante huida: Locomoción corriendo (gestionada por NPCSimpleAnimator)
- Al detenerse: `SenseSomethingSearching_NoWeapon`

**Velocidad:** `runSpeed` (ej: 5 m/s)

**Logs:**
```
🏃 Huyendo hacia cobertura detrás de obstáculo: (23.5, 0, 15.2)
🔍 Llegó a posición de cobertura - Reproduciendo animación de búsqueda
❌ Perdió visión del jugador tras llegar a cobertura - ENTRANDO EN BÚSQUEDA
```

---

### 3️⃣ ATTACK (Atacar)

**Función:** Lanzar hechizos al player

**Comportamiento:**

```
1. Verifica línea de visión
   └─ NO → Cancelar y ir a SEARCHING

2. Se detiene (isStopped = true)

3. Gira hacia el player (FaceTarget)

4. Selecciona ataque disponible:
   Priority: Special (60%) > Right > Left

5. Windup (0.2-0.5s) - Preparación

6. Verifica línea de visión de nuevo
   └─ NO → Cancelar y ir a SEARCHING

7. Ejecuta animación (ej: "MagicRight" en UpperBody layer)

8. Dispara proyectil:
   ├─ Via AnimEvent → Espera a que la animación lo dispare
   └─ Manual → Espera fireDelaySeconds y dispara

9. Verifica línea de visión después del disparo
   └─ NO → Ir a SEARCHING

10. Global Cooldown (pausa 0.5s)

11. Decisión post-ataque:
    ├─ Tiene más ataques:
    │   ├─ 30% → Flanquear (moverse al lado)
    │   └─ 70% → ATTACK de nuevo (seguir atacando)
    └─ Sin ataques → Volver a EVALUATE
```

**Animaciones:**
- Upper Body: `MagicLeft`, `MagicRight`, `MagicSpecial`
- Lower Body: Idle Battle (sin movimiento)

**Velocidad de Ataque:** 
- Multiplicador: `attackFrequencyMultiplier`
- Global Cooldown: `globalCooldown` (ej: 0.5s entre ataques)

**Logs:**
```
❌ Ataque cancelado - Sin línea de visión
❌ Ataque cancelado durante windup - Perdida línea de visión
❌ Disparo cancelado - Jugador se escondió durante animación
❌ Jugador se escondió después del ataque - Iniciando búsqueda
```

---

### 4️⃣ DEFENSE (Defensa)

**Función:** Protegerse del player usando escudo o cobertura

**Comportamiento basado en Dificultad:**

```
Decisión Inteligente (probabilidad = difficultyLevel):

SI ES INTELIGENTE:
    A. ¿Tengo escudo disponible?
       ├─ SÍ:
       │   1. Activa escudo por shieldDuration segundos
       │   2. Mientras se defiende:
       │      └─ Si player muy cerca → Retrocede lentamente con escudo
       │   3. Escudo termina → Vuelve a EVALUATE
       │
       └─ NO:
           1. Busca cobertura (TryGetCoverPosition)
           2. Corre hacia cobertura
           3. Espera 2s tras cobertura (recarga parcial)
           4. Vuelve a EVALUATE

SI ES TORPE (baja dificultad):
    ├─ 50% → Esquiva lateral tonta
    └─ 50% → Se queda pasmado 1s
```

**Animaciones:**
- Con escudo: `Defend_NoWeapon` (loop durante shieldDuration)
- Esquiva: Locomoción lateral rápida

**Logs:**
```
🛡️ Activando ESCUDO defensivo por 3.0s
🚶 Retrocediendo con escudo activo
⏳ Escudo en cooldown (7.5s) - buscando cobertura
🌳 Corriendo hacia cobertura para recargar
🤸 No hay cobertura - esquiva táctica
😵 Defensa torpe (baja dificultad)
```

---

### 5️⃣ HIDING_TO_RECHARGE (Esconderse para Recargar)

**Función:** Buscar lugar seguro para recuperar hechizos O preparar emboscada

**Dos Modos:**

#### 🔋 Modo RECARGA REAL (Sin ataques)

```
1. Busca cobertura detrás de obstáculos Default
2. Corre hacia la cobertura (runSpeed)
3. Durante huida:
   └─ Si es atacado → Activa escudo si está disponible
4. Al llegar:
   ├─ Muestra ❓
   └─ Animación "SenseSomethingSearching_NoWeapon"
5. Espera hasta recargar mínimo 2 ataques
6. Durante recarga:
   └─ Si es atacado:
       ├─ Tiene ataques → Contraatacar (ir a EVALUATE)
       └─ Tiene escudo → Defender (activar escudo)
7. Una vez recargado:
   ├─ Ve al player → Mostrar ❗ + "SenseSomethingStart" + Atacar
   └─ No lo ve → Ir a SEARCHING
```

#### 🎭 Modo EMBOSCADA (Con ataques guardados)

```
1. Busca cobertura (igual que recarga)
2. Al llegar:
   ├─ Muestra ❓ (para engañar)
   └─ Animación "SenseSomethingSearching_NoWeapon"
3. Finge estar recargando
4. MONITOREA distancia del player constantemente
5. Si player se acerca a ≤ (optimalDistance × 1.2):
   ┌──────────────────────────────────┐
   │  🎯 ¡EMBOSCADA ACTIVADA!         │
   │  1. Gira 180° hacia el player    │
   │  2. Muestra ❗                    │
   │  3. Animación "SenseSomething    │
   │     Start_NoWeapon"              │
   │  4. Pausa dramática (0.5s)       │
   │  5. ¡ATACAR con hechizos         │
   │     guardados!                   │
   └──────────────────────────────────┘
6. Si player NO se acerca (timeout):
   └─ Cancelar estrategia, ir a SEARCHING
7. Si player ataca durante emboscada:
   └─ ¡Emboscada descubierta! Contraatacar
```

**Animaciones:**
- Durante huida: Locomoción corriendo
- Al llegar: `SenseSomethingSearching_NoWeapon` (recarga o engaño)
- Emboscada activada: `SenseSomethingStart_NoWeapon`

**Logs:**
```
🏃 ESCONDERSE PARA RECARGAR - Buscando cobertura (recarga real)
🎭 ESCONDERSE PARA EMBOSCADA - Fingiendo recarga (tiene 2 ataques guardados)
⚔️ ¡ATACADO DURANTE LA HUIDA!
🛡️ Activando escudo durante huida
🔍 Llegó a cobertura - Mostrando animación de búsqueda
⏳ Recargando hechizos...
🎭 Fingiendo recarga... esperando que el player se acerque
🎯 ¡EMBOSCADA ACTIVADA! Player a 11.5m - ¡ATAQUE SORPRESA!
🎭 Emboscada no activada (player no se acercó) - Cancelando estrategia
🎭 ¡Emboscada descubierta! - Contratatacando
✅ Hechizos recargados (3 disponibles) - Buscando al player
```

---

### 6️⃣ SEARCHING (Buscando)

**Función:** Buscar activamente al player cuando lo pierde de vista

**Comportamiento:**

```
1. Se detiene

2. ¿Combate reciente? (< 5s desde último contacto)
   ├─ NO → Muestra ❓ + "SenseSomethingSearching_NoWeapon"
   └─ SÍ → Sin interrogación (búsqueda directa)

3. Modo de búsqueda (configurable):

   A. BÚSQUEDA ACTIVA (activelySearchForPlayer = true):
      └─ Se mueve a diferentes puntos cercanos
         1. Calcula punto aleatorio cerca de última posición
         2. Camina hacia ese punto (walkSpeed)
         3. Durante movimiento:
            └─ Si encuentra al player:
                ├─ Muestra ❗
                ├─ Animación "SenseSomethingStart_NoWeapon"
                └─ Ir a EVALUATE
         4. Al llegar a punto:
            ├─ Muestra ❓
            ├─ Animación "SenseSomethingSearching_NoWeapon"
            └─ Espera 2s mirando alrededor
         5. Si encuentra al player:
            ├─ Muestra ❗
            ├─ Animación "SenseSomethingStart_NoWeapon"
            └─ Ir a EVALUATE
         6. Si NO encuentra → Repetir en otro punto (máx 5 intentos)
   
   B. BÚSQUEDA PASIVA (activelySearchForPlayer = false):
      └─ Se queda quieto esperando
         └─ Verifica constantemente si ve al player

4. Si agota búsqueda sin encontrar:
   ├─ returnToOriginAfterSearch = true:
   │   1. Vuelve a posición inicial
   │   2. Durante regreso:
   │      └─ Si encuentra al player → Alertar y atacar
   │   3. Al llegar → Salir de combate
   │
   └─ returnToOriginAfterSearch = false:
       └─ Abandonar combate directamente

5. Salir de modo combate (StopCombat)
```

**Animaciones:**
- Inicial: `SenseSomethingSearching_NoWeapon` (si no combate reciente)
- En movimiento: Locomoción caminando
- Al detenerse: `SenseSomethingSearching_NoWeapon`
- Al encontrar: `SenseSomethingStart_NoWeapon`

**Duración:**
- Activa: `searchDuration` (ej: 15s)
- Pasiva: `passiveSearchDuration` (ej: 10s)

**Logs:**
```
🔍 INICIANDO BÚSQUEDA - Última posición conocida: (15.2, 0, 8.5)
🎯 Combate reciente detectado (2.3s) - Búsqueda sin interrogación
🔍 Modo: BÚSQUEDA ACTIVA - Duración: 15.0s
👣 Movimiento de búsqueda #1 hacia: (18.5, 0, 10.2)
✅ ¡Jugador encontrado durante movimiento!
❓ Parada de búsqueda #2 - No encontrado
✅ ¡Jugador encontrado mientras miraba alrededor!
😞 Búsqueda agotada - 5 intentos completados sin éxito
🏠 Volviendo al origen tras búsqueda fallida: (10.0, 0, 5.0)
🏳️ Abandonando modo combate - Jugador no encontrado
```

---

## 🎯 SISTEMA DE DECISIONES

### Árbol de Decisión Principal

```
┌─────────────────────────────────────┐
│ ¿Puedo ver al player?               │
└─────┬───────────────────────────────┘
      │
      ├─ NO → SEARCHING
      │
      └─ SÍ
         ├─ ¿Distancia < minSafe?
         │  └─ SÍ → REPOSITION (huir)
         │
         └─ NO
            ├─ ¿Debería engañar? (probabilidad)
            │  └─ SÍ → HIDING_TO_RECHARGE (emboscada)
            │
            └─ NO
               ├─ ¿Tengo ataques?
               │  ├─ SÍ:
               │  │  ├─ ¿En rango?
               │  │  │  ├─ SÍ → ATTACK
               │  │  │  └─ NO → Acercarse
               │  │  └─ ¿Debo defender?
               │  │     └─ SÍ → DEFENSE
               │  │
               │  └─ NO → HIDING_TO_RECHARGE (recarga real)
```

### Factores de Decisión

| Factor | Influencia |
|--------|-----------|
| **Distancia al Player** | < minSafe → Huir<br>> maxDistance → Acercarse<br>Entre ambos → Atacar |
| **Ataques Disponibles** | 0 → Recargar<br>1-3 → Atacar o engañar |
| **Línea de Visión** | NO → Buscar<br>SÍ → Combatir |
| **Dificultad (0-1)** | Baja → Torpe<br>Alta → Inteligente |
| **Cooldowns** | Global CD activo → Esperar<br>Escudo disponible → Defender más |

---

## 🎭 ESTRATEGIA DE ENGAÑO

### Probabilidad de Engañar

```
Probabilidad Real = deceptionChance × difficultyLevel
```

**Ejemplos:**
- NPC Fácil: 0.2 × 0.3 = **6%**
- NPC Normal: 0.4 × 0.6 = **24%**
- NPC Difícil: 0.6 × 0.9 = **54%**
- NPC Boss: 0.8 × 1.0 = **80%**

### Condiciones para Engañar

```
✅ Tiene > minAttacksToKeepForAmbush ataques
✅ No está ya usando estrategia de engaño
✅ Pasa el check de probabilidad
```

### Proceso de Engaño

```
1. Decide engañar (ej: tiene 3 ataques, guarda 2)
2. Ataca 1 vez solamente
3. Se esconde fingiendo "recarga"
4. Muestra ❓ (player cree que está indefenso)
5. Espera pacientemente...
6. Player se acerca → ¡EMBOSCADA!
7. Ataca con los 2 hechizos guardados
```

---

## ⚡ REACCIÓN A EVENTOS

### Cuando Recibe Daño (OnTakeDamage)

```
Si está en estado vulnerable (SEARCHING, HIDING, REPOSITION):
    1. Calcular si fue por la espalda (ángulo > 90°)
    2. GIRAR hacia fuente del daño
    3. Animación "SenseSomethingStart_NoWeapon"
    4. Mostrar ❗
    5. Decidir reacción:
       ├─ Tiene ataques → CONTRAATACAR (ir a EVALUATE)
       ├─ Tiene escudo → DEFENDER (activar escudo)
       └─ Sin recursos → Continuar huyendo
```

**Logs:**
```
⚠️ ¡ATACADO POR LA ESPALDA! Estado: HIDING_TO_RECHARGE
🎬 Reproduciendo animación SenseSomethingStart_NoWeapon
⚡ Contratatacando inmediatamente
🛡️ Activando escudo defensivo
🏃 Continúa huyendo - sin recursos para contraatacar
```

### Durante Combate (Update Loop)

```
Cada Frame:
    1. Reducir Cooldowns:
       ├─ leftCd, rightCd, specialCd (× attackFrequencyMultiplier)
       └─ shieldCd, globalCd (tiempo normal)
    
    2. Verificar Línea de Visión:
       └─ CheckLineOfSight() → actualizar _hasLineOfSight
    
    3. Si tiene línea de visión:
       ├─ Actualizar _lastSeenTime
       ├─ Actualizar _lastCombatTime
       └─ Actualizar _lastKnownPlayerPosition
    
    4. Gestionar Rotación:
       └─ Si parado (no REPOSITION/SEARCHING):
           └─ Girar hacia player (FaceTarget)
```

---

## 🧮 PARÁMETROS CONFIGURABLES

### Distancias y Movimiento

```csharp
minSafeDistance = 7f        // Distancia mínima segura (huye si < esto)
optimalDistance = 12f       // Distancia ideal de combate
maxDistance = 20f           // Distancia máxima (se acerca si > esto)
runSpeed = 5f               // Velocidad al huir/reposicionar
walkSpeed = 2.5f            // Velocidad al acercarse/buscar
```

### Ataques

```csharp
leftAttack.cooldown = 3f
rightAttack.cooldown = 4f
specialAttack.cooldown = 8f
globalCooldown = 0.5f       // Pausa entre ataques
attackFrequencyMultiplier = 1f  // Velocidad de recarga (2 = doble)
fireDelaySeconds = 0.3f     // Delay antes de disparar
```

### Defensa

```csharp
difficultyLevel = 0.8f      // 0=Torpe, 1=Experto
useShield = true
shieldDuration = 3f
shieldCooldown = 10f
coverSearchRadius = 15f     // Radio para buscar cobertura
dodgeDistance = 3f          // Distancia de esquiva lateral
```

### Búsqueda

```csharp
searchDuration = 15f        // Tiempo buscando activamente
searchMovementRadius = 10f  // Radio de movimiento en búsqueda
activelySearchForPlayer = true  // ¿Busca activamente?
passiveSearchDuration = 10f // Si no busca activamente
returnToOriginAfterSearch = true  // ¿Vuelve al origen?
```

### Engaño (Nuevo)

```csharp
deceptionChance = 0.5f      // Probabilidad base de engañar (0-1)
minAttacksToKeepForAmbush = 2  // Ataques mínimos a guardar (1-3)
```

---

## 📊 EJEMPLOS DE CONFIGURACIÓN

### NPC Fácil (Principiante)
```
difficultyLevel = 0.3
deceptionChance = 0.2
minAttacksToKeepForAmbush = 1
useShield = false
attackFrequencyMultiplier = 0.8

RESULTADO:
- Torpe en defensa (70% decisiones tontas)
- Rara vez engaña (6% probabilidad)
- Sin escudo
- Ataques lentos
```

### NPC Normal (Equilibrado)
```
difficultyLevel = 0.6
deceptionChance = 0.4
minAttacksToKeepForAmbush = 2
useShield = true
shieldCooldown = 15f
attackFrequencyMultiplier = 1.0

RESULTADO:
- Inteligente 60% del tiempo
- Engaña ocasionalmente (24%)
- Usa escudo tácticamente
- Ataques normales
```

### NPC Difícil (Avanzado)
```
difficultyLevel = 0.9
deceptionChance = 0.6
minAttacksToKeepForAmbush = 2
useShield = true
shieldCooldown = 8f
attackFrequencyMultiplier = 1.3

RESULTADO:
- Muy inteligente (90%)
- Engaña frecuentemente (54%)
- Escudo rápido
- Ataques 30% más rápidos
```

### NPC Boss (Maestro)
```
difficultyLevel = 1.0
deceptionChance = 0.8
minAttacksToKeepForAmbush = 3
useShield = true
shieldCooldown = 5f
attackFrequencyMultiplier = 1.5
globalCooldown = 0.3f

RESULTADO:
- Siempre inteligente (100%)
- Casi siempre engaña (80%)
- Escudo muy disponible
- Ataques 50% más rápidos
- Menos pausa entre ataques
```

---

## 🎬 ANIMACIONES COMPLETAS

### Lista de Todas las Animaciones

| Estado | Animación | Cuándo |
|--------|-----------|--------|
| **Idle** | `Idle_Battle_NoWeapon` | Parado en combate |
| **Locomoción** | `Free Locomotion` | Moviéndose (blend tree) |
| **Ataque Izq** | `MagicLeft` | Upper body ataque mano izquierda |
| **Ataque Der** | `MagicRight` | Upper body ataque mano derecha |
| **Ataque Especial** | `MagicSpecial` | Upper body ataque especial |
| **Defensa** | `Defend_NoWeapon` | Escudo activo |
| **Defensa Impacto** | `DefendHit_NoWeapon` | Escudo bloqueó ataque |
| **Alerta** | `SenseSomethingStart_NoWeapon` | ¡Encontró al player! |
| **Búsqueda** | `SenseSomethingSearching_NoWeapon` | Buscando al player |
| **Muerte** | `Die02_NoWeapon` | NPC muere |
| **Victoria** | `Victory_NoWeapon` | NPC gana |
| **Mareo** | `Dizzy_NoWeapon` | Tras levantarse |
| **Daño** | `TakeDamage`, `TakeDamage_2` | Recibe impacto |

### Secuencias de Animación Típicas

**Inicio de Combate:**
```
Idle_Normal → ❗ → Challenging → Idle_Battle
```

**Ataque Completo:**
```
Idle_Battle → MagicRight (upper) → Idle_Battle
```

**Huida y Búsqueda:**
```
Idle_Battle → Free Locomotion (corriendo) → SenseSomethingSearching → Idle_Battle
```

**Emboscada:**
```
SenseSomethingSearching → ❗ → SenseSomethingStart → MagicSpecial
```

**Defensa con Escudo:**
```
Idle_Battle → Defend (loop) → DefendHit (si recibe golpe) → Defend → Idle_Battle
```

**Encontrar al Player:**
```
SenseSomethingSearching → ❗ → SenseSomethingStart → Idle_Battle → ATTACK
```

---

## 🎯 ICONOS VISUALES

### Sistema de Iconos (NPCAlertIconController)

| Icono | Significado | Cuándo Aparece |
|-------|-------------|----------------|
| **❗ Admiración** | ¡Te vi! / ¡Alerta! | - Inicio combate<br>- Encuentra al player<br>- Emboscada activada<br>- Atacado por espalda |
| **❓ Interrogación** | ¿Dónde está? | - Pierde visión del player<br>- Llega a cobertura<br>- Búsqueda activa |

### Secuencias de Iconos

**Combate Normal:**
```
❗ (inicio) → [combate] → ❓ (pierde visión) → [búsqueda] → ❗ (encuentra) → [combate]
```

**Emboscada:**
```
❗ (inicio) → [ataca poco] → ❓ (finge buscar) → [espera] → ❗ (¡sorpresa!) → [ataca]
```

**Búsqueda Fallida:**
```
❗ (inicio) → [combate] → ❓ (pierde visión) → [búsqueda] → [sin icono] (abandona)
```

---

## 🔊 LOGS DE DEBUG

### Categorías de Logs

**Decisiones Tácticas (🧠):**
```
🎭 ESTRATEGIA DE ENGAÑO ACTIVADA
⚔️ Atacando - 3 ataques disponibles
🔋 Sin ataques disponibles - Necesito esconderme
⚠️ Player demasiado cerca - Reposicionando
```

**Movimiento (🏃):**
```
🏃 Huyendo hacia cobertura detrás de obstáculo
🏃 Corriendo hacia cobertura
🚶 Acercándose al player
👣 Movimiento de búsqueda #2
```

**Defensa (🛡️):**
```
🛡️ Activando ESCUDO defensivo por 3.0s
🛡️ Activando escudo durante huida
🛡️ Defendiendo con escudo
```

**Búsqueda (🔍):**
```
🔍 INICIANDO BÚSQUEDA - Última posición conocida
🔍 Llegó a cobertura - Mostrando animación
🔍 Modo: BÚSQUEDA ACTIVA - Duración: 15.0s
```

**Emboscadas (🎯):**
```
🎭 ESCONDERSE PARA EMBOSCADA - Fingiendo recarga
🎯 ¡EMBOSCADA ACTIVADA! Player a 11.5m
🎭 Emboscada no activada - Player no se acercó
🎭 ¡Emboscada descubierta! - Contratatacando
```

**Eventos (⚡):**
```
⚡ Contratatacando inmediatamente
⚔️ ¡ATACADO POR LA ESPALDA! Estado: HIDING_TO_RECHARGE
❌ Ataque cancelado - Sin línea de visión
✅ Hechizos recargados (3 disponibles)
```

**Éxito/Fracaso (✅/❌):**
```
✅ ¡JUGADOR ENCONTRADO! - Mostrando alerta
❌ Sin línea de visión al jugador - Iniciando búsqueda
😞 Búsqueda agotada - 5 intentos completados sin éxito
🏳️ Abandonando modo combate - Jugador no encontrado
```

---

## 🎯 ESCENARIOS DE COMBATE TÍPICOS

### Escenario 1: Combate Agresivo Directo

```
INICIO: Player a 15m, NPC tiene 3 ataques

1. EVALUATE → Player visible, en rango → ATTACK
2. ATTACK → Dispara Right Hand → 2 ataques restantes
3. EVALUATE → Aún en rango → ATTACK
4. ATTACK → Dispara Left Hand → 1 ataque restante
5. EVALUATE → Aún en rango → ATTACK
6. ATTACK → Dispara Special → 0 ataques
7. EVALUATE → Sin ataques → HIDING_TO_RECHARGE
8. HIDING → Busca cobertura, se esconde
9. Recarga 2 ataques → Ve al player
10. EVALUATE → Tiene ataques → ATTACK
11. Ciclo se repite...

RESULTADO: NPC agresivo que ataca hasta gastar todo
```

### Escenario 2: Emboscada Táctica

```
INICIO: Player a 15m, NPC tiene 3 ataques, deceptionChance=0.8

1. EVALUATE → Decide engañar (pasa probabilidad)
2. Guarda 2 ataques para emboscada
3. ATTACK → Dispara Special solamente → Finge 0 ataques
4. EVALUATE → "Sin ataques" (mentira) → HIDING_TO_RECHARGE
5. HIDING (EMBOSCADA) → Busca cobertura, muestra ❓
6. Player: "Está indefenso, voy a atacarlo"
7. Player se acerca: 14m → 13m → 12m → 11m
8. ¡EMBOSCADA! → ❗ + SenseSomethingStart
9. EVALUATE → ATTACK → Dispara Right Hand
10. EVALUATE → ATTACK → Dispara Left Hand
11. Player: "¡Me engañó!"

RESULTADO: NPC inteligente que usa psicología
```

### Escenario 3: Defensa Táctica

```
INICIO: Player a 6m (MUY CERCA), NPC tiene 2 ataques

1. EVALUATE → Player < minSafeDistance → REPOSITION
2. REPOSITION → Busca cobertura, huye corriendo
3. Player persigue disparando
4. OnTakeDamage → Tiene escudo disponible
5. Activa escudo mientras huye
6. Llega a cobertura con escudo activo
7. Escudo termina → EVALUATE
8. Ahora player a 15m → En rango óptimo
9. EVALUATE → Tiene ataques → ATTACK
10. Contraataca desde posición segura

RESULTADO: NPC táctico que se reposiciona antes de atacar
```

### Escenario 4: Búsqueda y Encuentro

```
INICIO: Player se esconde detrás de muro, NPC pierde visión

1. ATTACK → Verifica visión → NO → Cancela
2. EVALUATE → Sin línea de visión → SEARCHING
3. SEARCHING → Muestra ❓ + animación búsqueda
4. Búsqueda activa: Se mueve a punto #1
5. Llega, mira alrededor → No encuentra
6. Se mueve a punto #2
7. Llega, mira alrededor → No encuentra
8. Se mueve a punto #3
9. Durante movimiento → ¡Ve al player!
10. ❗ + SenseSomethingStart
11. EVALUATE → ATTACK → Reanuda combate

RESULTADO: NPC persistente que busca activamente
```

### Escenario 5: Atacado por la Espalda

```
INICIO: NPC escondido recargando, Player flanquea

1. HIDING_TO_RECHARGE → Recargando (tiene 1 ataque)
2. Player se mueve silenciosamente detrás
3. Player dispara por la espalda
4. OnTakeDamage → Detecta ángulo > 90°
5. "¡ATACADO POR LA ESPALDA!"
6. Gira 180° hacia player
7. SenseSomethingStart + ❗
8. Tiene 1 ataque → CONTRAATACAR
9. EVALUATE → ATTACK → Dispara el ataque
10. EVALUATE → Sin ataques → DEFENSE
11. DEFENSE → Activa escudo
12. Retrocede con escudo mientras recarga

RESULTADO: NPC reactivo que responde a ataques sorpresa
```

### Escenario 6: Combate de Desgaste

```
INICIO: Player mantiene distancia, NPC difficultyLevel=0.9

1-3. NPC ataca, player esquiva
4. EVALUATE → Decide usar escudo tácticamente (90% smart)
5. DEFENSE → Activa escudo
6. Player dispara → Escudo bloquea
7. Escudo termina → EVALUATE → ATTACK
8. Dispara 2 veces más
9. EVALUATE → Sin ataques → HIDING_TO_RECHARGE
10. Se esconde, player mantiene distancia
11. Recarga completa (3 ataques)
12. SEARCHING → Sale a buscar
13. Encuentra player → ATTACK
14. Player retrocede manteniendo distancia
15. Ciclo se repite...

RESULTADO: Combate prolongado, ambos tácticos
```

---

## 💡 CONSEJOS PARA EL PLAYER

### Cómo Combatir al NPC

**Si el NPC es Agresivo:**
- Mantén distancia óptima (12-15m)
- Usa cobertura para romper línea de visión
- Ataca cuando esté recargando (icono ❓)
- Esquiva sus ataques lateralmente

**Si el NPC Usa Engaño:**
- NO te acerques cuando muestre ❓ rápido
- Dispara desde lejos para forzar revelación
- Cuenta sus ataques (si ataca poco, sospecha)
- Flanquea en lugar de acercarte directo

**Si el NPC Se Defiende Mucho:**
- Espera a que termine su escudo
- Atácalo durante sus movimientos
- Usa ataques rápidos para mantener presión
- Rómpele el ritmo cambiando distancias

**Tácticas Avanzadas:**
- Usa terreno para bloquear visión → Fuerza búsqueda
- Ataca durante sus reposicionamientos
- Presiona cuando tenga 0 ataques (no esperes)
- Cuidado con emboscadas si muestra ❓ cerca de ti

---

## 📈 DIFICULTAD DINÁMICA

### Cómo la Dificultad Afecta el Comportamiento

| Aspecto | Dificultad Baja (0.3) | Dificultad Alta (0.9) |
|---------|----------------------|----------------------|
| **Defensa** | 70% decisiones tontas | 90% decisiones inteligentes |
| **Escudo** | Usa 30% cuando debería | Usa 90% cuando debería |
| **Engaño** | Rara vez (6% con chance=0.2) | Frecuente (54% con chance=0.6) |
| **Cobertura** | A veces busca mal | Siempre encuentra la mejor |
| **Búsqueda** | Pasiva, se rinde fácil | Activa, muy persistente |
| **Reacción** | Lenta, predecible | Rápida, adaptativa |

### Progresión Sugerida

```
Nivel 1-5: difficultyLevel = 0.2-0.4 (Aprendizaje)
Nivel 6-10: difficultyLevel = 0.5-0.7 (Intermedio)
Nivel 11-15: difficultyLevel = 0.8-0.9 (Avanzado)
Boss Final: difficultyLevel = 1.0 (Maestro)
```

---

## 🔧 DEBUGGING Y TROUBLESHOOTING

### Problemas Comunes

**NPC se ve nervioso / cambia de estado muy rápido:**
- ✅ SOLUCIONADO: Implementado MIN_STATE_DURATION
- Verificar que no hay componentes aleatorios en EVALUATE

**NPC nunca usa escudo:**
- Verificar `useShield = true`
- Verificar `_shieldCd <= 0` (no en cooldown)
- Aumentar `difficultyLevel` (más probabilidad de decisión inteligente)

**NPC nunca engaña:**
- Aumentar `deceptionChance`
- Aumentar `difficultyLevel`
- Verificar que tiene > `minAttacksToKeepForAmbush` ataques

**NPC no busca al player:**
- Verificar `activelySearchForPlayer = true`
- Aumentar `searchDuration`
- Verificar NavMesh en el área

**Emboscada no se activa:**
- Verificar que player se acerca a ≤ `optimalDistance × 1.2`
- Ver logs: "🎯 ¡EMBOSCADA ACTIVADA!"
- Aumentar tiempo de espera antes de timeout

### Logs a Monitorear

Para verificar que funciona correctamente:
```
✅ Ver logs de decisiones (🎭, ⚔️, 🔋)
✅ Ver transiciones de estado (EVALUATE → X)
✅ Ver activaciones de emboscada (🎯)
✅ Ver reacciones a daño (⚡)
✅ Verificar que NO hay spam de logs
```

---

## 📚 RESUMEN EJECUTIVO

### Qué Puede Hacer el NPC

✅ **Atacar** con 3 tipos de hechizos (Left, Right, Special)  
✅ **Defender** con escudo mágico que bloquea proyectiles  
✅ **Esconderse** detrás de cobertura (árboles, rocas, cajas)  
✅ **Recargar** sus hechizos en lugar seguro  
✅ **Engañar** fingiendo estar sin magia para emboscada  
✅ **Buscar** activamente al player si lo pierde  
✅ **Contraatacar** si es atacado por sorpresa  
✅ **Flanquear** para atacar desde ángulos diferentes  
✅ **Esquivar** lateralmente  
✅ **Reposicionarse** si el player está muy cerca  

### Cómo Se Comporta

**Personalidad:** Mago combatiente inteligente y táctico  
**Objetivo:** Matar al player, pero sobrevivir  
**Estrategia:** Agresivo cuando tiene recursos, defensivo cuando no  
**Inteligencia:** Basada en difficultyLevel (0=torpe, 1=experto)  
**Sorpresas:** Puede usar engaño y emboscadas  

### Filosofía de Diseño

```
NO es un enemigo estúpido que solo dispara
NO es predecible
NO es fácil de explotar

ES un oponente digno
ES adaptativo
ES desafiante
ES justo pero difícil
```

---

## 🎯 CONCLUSIÓN

El **NPCCombatBrain** es un sistema de IA complejo y sofisticado que crea **combates dinámicos, impredecibles y desafiantes**. El NPC no sigue un script simple, sino que **toma decisiones** basadas en:

- Su situación actual (vida, magia, posición)
- El comportamiento del player
- Su nivel de dificultad
- Probabilidades tácticas
- Memoria de combate

Esto resulta en combates que se sienten como enfrentarse a un **jugador humano inteligente**, no a un bot predecible.

---

**Última actualización:** 29 de Diciembre, 2024  
**Versión del sistema:** NPCCombatBrain v2.0 con Estrategia de Engaño  
**Estado:** Producción

