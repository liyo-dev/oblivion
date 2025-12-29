# 🎭 FEATURE: Estrategia de Engaño y Emboscada NPC

**Fecha:** 29 de Diciembre, 2024  
**Versión:** 1.0  
**Tipo:** Táctica Avanzada de IA

---

## 🎯 Concepto

El NPC ahora puede **FINGIR** que se quedó sin magia para atraer al player a una **EMBOSCADA**. Esta es una estrategia avanzada que añade un nivel de profundidad psicológica al combate.

### Comportamiento

```
NPC en combate con 3 ataques disponibles
    ↓
¿Debería usar estrategia de engaño?
    ├─ Probabilidad = deceptionChance × difficultyLevel
    └─ Solo si tiene > minAttacksToKeepForAmbush ataques
    
SI DECIDE ENGAÑAR:
    1. Guarda 1-3 ataques en secreto
    2. Se esconde fingiendo "recarga"
    3. Muestra ❓ + animación de búsqueda
    4. Espera pacientemente...
    
CUANDO EL PLAYER SE ACERCA:
    1. ¡SORPRESA! → Animación SenseSomethingStart_NoWeapon
    2. Icono de admiración ❗
    3. Pausa dramática (0.5s)
    4. ¡ATAQUE POR LA ESPALDA!
```

---

## ⚙️ Parámetros de Configuración

Se agregaron 2 nuevos parámetros en `NPCCombatBrain.Settings`:

### 1. **deceptionChance** (Range: 0-1)
```csharp
[Tooltip("Probabilidad de fingir quedarse sin magia para atraer al player")]
[Range(0f, 1f)] public float deceptionChance;
```

**Valores recomendados:**
- `0.0` = Nunca usa engaño (NPC honesto)
- `0.3` = 30% base (NPCs normales)
- `0.5` = 50% base (NPCs astutos)
- `0.7` = 70% base (NPCs muy inteligentes)
- `1.0` = 100% base (NPCs maestros del engaño)

**Nota:** Se multiplica por `difficultyLevel`, así que un NPC con:
- `deceptionChance = 0.5` y `difficultyLevel = 0.8`
- Probabilidad real = 0.5 × 0.8 = **40% de usar engaño**

### 2. **minAttacksToKeepForAmbush** (Range: 1-3)
```csharp
[Tooltip("Mínimo de ataques que debe conservar cuando finge")]
[Range(1, 3)] public int minAttacksToKeepForAmbush;
```

**Valores:**
- `1` = Guarda mínimo 1 ataque (más arriesgado, emboscada rápida)
- `2` = Guarda mínimo 2 ataques (equilibrado)
- `3` = Guarda mínimo 3 ataques (muy seguro, emboscada poderosa)

**Ejemplo:**
- Si `minAttacksToKeepForAmbush = 2` y el NPC tiene 3 ataques
- Decide engañar → Guardará 2 ataques para la emboscada
- Atacará 1 vez, luego fingirá recarga

---

## 🧠 Lógica Implementada

### 1. Decisión de Engaño (State_Evaluate)

```csharp
// ¿Debería fingir quedarse sin magia?
if (!_isUsingDeceptionStrategy && attacksReady > settings.minAttacksToKeepForAmbush)
{
    // Probabilidad basada en dificultad
    float actualDeceptionChance = settings.deceptionChance * settings.difficultyLevel;
    
    if (UnityEngine.Random.value < actualDeceptionChance)
    {
        // 🎭 ENGAÑO ACTIVADO
        _isUsingDeceptionStrategy = true;
        _attacksReservedForAmbush = Mathf.Min(attacksReady, settings.minAttacksToKeepForAmbush);
        
        Debug.Log($"🎭 ESTRATEGIA DE ENGAÑO ACTIVADA - Guardando {_attacksReservedForAmbush} ataques");
        
        // Ir a esconderse fingiendo necesitar recarga
        _currentState = CombatState.HIDING_TO_RECHARGE;
        yield break;
    }
}
```

**Condiciones para activar engaño:**
1. ✅ No está ya usando estrategia de engaño
2. ✅ Tiene más ataques que el mínimo requerido
3. ✅ El Random.value pasa el check de probabilidad

### 2. Esconderse Fingiendo (State_HidingToRecharge)

```csharp
bool isAmbush = _isUsingDeceptionStrategy;

if (isAmbush)
{
    Debug.Log("🎭 ESCONDERSE PARA EMBOSCADA - Fingiendo recarga");
}

// Busca cobertura (igual que recarga real)
// Muestra ❓ + animación SenseSomethingSearching_NoWeapon
// PERO no está recargando... ¡está esperando!
```

### 3. Activación de Emboscada

```csharp
// 🎭 Monitorear distancia del player
float ambushTriggerDistance = settings.optimalDistance * 1.2f;

while (esperando...)
{
    if (isAmbush && _player != null)
    {
        float distToPlayer = Vector3.Distance(transform.position, _player.position);
        
        // ¿El player se acerca?
        if (distToPlayer <= ambushTriggerDistance)
        {
            Debug.Log("🎯 ¡EMBOSCADA ACTIVADA! - ¡ATAQUE SORPRESA!");
            
            // Mostrar ❗ + animación SenseSomethingStart_NoWeapon
            // Pausa dramática (0.5s)
            // ¡ATACAR!
            
            _isUsingDeceptionStrategy = false;
            _currentState = CombatState.EVALUATE;
            yield break;
        }
    }
}
```

**Distancia de activación:**
- `optimalDistance × 1.2`
- Por ejemplo: si optimalDistance = 10m → activa a 12m
- Esto da margen para que el NPC reaccione antes de que el player llegue

### 4. Casos Especiales

#### A. Emboscada Descubierta
```csharp
// Si el player ataca al NPC mientras finge
if (isAmbush && IsPlayerAttacking())
{
    Debug.Log("🎭 ¡Emboscada descubierta! - Contratatacando");
    _isUsingDeceptionStrategy = false;
    _currentState = CombatState.EVALUATE;
    yield break;
}
```

#### B. Emboscada No Activada (Timeout)
```csharp
// Si el player no se acerca en el tiempo máximo
if (isAmbush && timeout)
{
    Debug.Log("🎭 Emboscada no activada - Player no se acercó");
    _isUsingDeceptionStrategy = false; // Cancelar estrategia
    // Continuar con búsqueda normal
}
```

---

## 🎮 Ejemplo de Combate con Engaño

### Escenario: NPC Astuto (deceptionChance=0.6, difficultyLevel=1.0, minAttacks=2)

```
INICIO
├─ NPC tiene 3 ataques disponibles
├─ Player a 15 metros
│
TURNO 1
├─ NPC: "¿Debería engañar?" → Random.value = 0.4 < 0.6 → ¡SÍ!
├─ NPC: "Reservando 2 ataques para emboscada"
├─ NPC: Ataca 1 vez con Right Hand
├─ Player: "¡Me está atacando!"
│
TURNO 2
├─ NPC: "Fingiendo quedarse sin magia"
├─ NPC: Corre hacia cobertura (árbol)
├─ Player: "¿Se está escondiendo? ¿Sin magia?"
│
TURNO 3
├─ NPC: Llega al árbol
├─ NPC: Muestra ❓ + animación SenseSomethingSearching_NoWeapon
├─ Player: "Parece que me perdió de vista..."
│
TURNO 4-10 (Esperando)
├─ NPC: Quieto detrás del árbol fingiendo buscar
├─ Player: "Voy a atacarlo mientras recarga"
├─ Player: Se acerca caminando... 14m... 13m... 12m...
│
TURNO 11 - ¡EMBOSCADA!
├─ Player: A 11 metros (< 12m = trigger)
├─ NPC: "🎯 ¡EMBOSCADA ACTIVADA!"
├─ NPC: Gira 180° hacia el player
├─ NPC: Muestra ❗ + animación SenseSomethingStart_NoWeapon
├─ NPC: Pausa dramática (0.5s)
├─ Player: "¡¿QUÉ?! ¡Aún tiene magia!"
│
TURNO 12-13
├─ NPC: ¡Ataca con Left Hand! → Impacto
├─ NPC: ¡Ataca con Special! → Impacto crítico
├─ Player: "¡Me engañó!"
│
RESULTADO: NPC ganó la ventaja psicológica
```

---

## 📊 Comparación: Con vs. Sin Engaño

| Aspecto | SIN Engaño | CON Engaño |
|---------|-----------|------------|
| **Predictibilidad** | Alta - siempre gasta todos los ataques | Baja - puede fingir |
| **Presión al player** | Constante durante ataque | Psicológica durante "recarga" |
| **Momento de vulnerabilidad** | Real - sin ataques | Fingido - tiene ataques guardados |
| **Ventaja táctica** | El player sabe cuándo atacar | El player puede caer en trampa |
| **Dificultad percibida** | Media | **Alta** |
| **Diversión** | Combate estándar | **Combate impredecible** |

---

## ⚠️ Balanceo Recomendado

### NPCs Fáciles (difficultyLevel = 0.3)
```csharp
settings.deceptionChance = 0.2f; // 20% base
settings.minAttacksToKeepForAmbush = 1;
// Probabilidad real: 0.2 × 0.3 = 6% (rara vez engaña)
```

### NPCs Normales (difficultyLevel = 0.6)
```csharp
settings.deceptionChance = 0.4f; // 40% base
settings.minAttacksToKeepForAmbush = 2;
// Probabilidad real: 0.4 × 0.6 = 24% (ocasionalmente engaña)
```

### NPCs Difíciles (difficultyLevel = 0.9)
```csharp
settings.deceptionChance = 0.6f; // 60% base
settings.minAttacksToKeepForAmbush = 2;
// Probabilidad real: 0.6 × 0.9 = 54% (frecuentemente engaña)
```

### NPCs Boss (difficultyLevel = 1.0)
```csharp
settings.deceptionChance = 0.8f; // 80% base
settings.minAttacksToKeepForAmbush = 3;
// Probabilidad real: 0.8 × 1.0 = 80% (casi siempre engaña)
```

---

## 🎬 Animaciones y Feedback Visual

### Durante el Engaño
1. **Huida a cobertura:** Movimiento normal (runSpeed)
2. **Al llegar:** Animación `SenseSomethingSearching_NoWeapon`
3. **Icono:** ❓ (Interrogación - "¿Dónde está el player?")
4. **Comportamiento:** Quieto, mirando alrededor

### Al Activar Emboscada
1. **Rotación:** Gira hacia el player inmediatamente
2. **Animación:** `SenseSomethingStart_NoWeapon` (¡Te encontré!)
3. **Icono:** ❗ (Admiración - "¡Aquí estoy!")
4. **Pausa:** 0.5s dramática
5. **Ataque:** Inicia combate con ataques reservados

---

## 🔧 Variables de Debug

Mensajes de consola para tracking:

```
🎭 ESTRATEGIA DE ENGAÑO ACTIVADA - Fingiendo quedarse sin magia (reservando 2 ataques para emboscada)
🎭 ESCONDERSE PARA EMBOSCADA - Fingiendo recarga (tiene 2 ataques guardados)
🎭 Fingiendo recarga... esperando que el player se acerque
🎯 ¡EMBOSCADA ACTIVADA! Player a 11.5m - ¡ATAQUE SORPRESA!
🎭 Emboscada no activada (player no se acercó) - Cancelando estrategia
🎭 ¡Emboscada descubierta! - Contratatacando
```

---

## 💡 Consejos para el Player

**Cómo detectar una emboscada:**

1. **Tiempo de "recarga" sospechoso:** Si el NPC ataca poco y huye rápido
2. **Posición estratégica:** Se esconde en lugar con buena visibilidad
3. **No intenta huir más lejos:** Se queda cerca, esperando
4. **Cooldowns cortos:** Si sus ataques tienen cooldown corto, probablemente no necesita recargar tanto tiempo

**Cómo contra-atacar:**

1. **Mantener distancia:** No acercarse a < 12m cuando esté "recargando"
2. **Atacar a distancia:** Disparar desde lejos para forzar revelación
3. **Flanquear:** Acercarse por un lado diferente
4. **Esperar más:** Si el NPC espera mucho, probablemente no está recargando realmente

---

## 🎯 Resultado Final

Esta feature transforma el combate de **predecible** a **psicológico**:

- ✅ El player ya no puede asumir que el NPC está indefenso cuando se esconde
- ✅ Añade tensión: "¿Está realmente sin magia o es una trampa?"
- ✅ Recompensa la cautela: Los players agresivos pueden caer en emboscadas
- ✅ Sensación de estar luchando contra un **oponente inteligente**, no un script simple

**¡El NPC ahora puede ser un maestro del engaño táctico!** 🎭⚔️

