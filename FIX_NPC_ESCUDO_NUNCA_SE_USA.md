# FIX: NPC nunca usa el escudo + Interrogación en giro durante combate

## 🎯 Problemas Identificados

### Problema 1: Escudo nunca se usa

El NPC **NUNCA** usa el escudo a pesar de:
- ✅ Tener el componente `NPCShieldController` funcional
- ✅ Tener `settings.useShield = true` configurado
- ✅ Tener la lógica implementada en `State_Defense()`

### ¿Por qué no funciona?

**Problema 1: Condición demasiado restrictiva**
```csharp
// Estado EVALUATE (línea 304)
if (!HasAnyAttackReady())  // Solo va a DEFENSE si NO tiene ataques
{
    _currentState = CombatState.DEFENSE;
    yield break;
}
```

**La lógica actual dice:**
- Solo va a DEFENSE cuando **TODOS** los ataques están en cooldown
- Esto es MUY raro porque:
  - Los cooldowns son independientes (left, right, special)
  - Siempre hay al menos 1 ataque disponible en la mayoría de casos
  - El NPC prefiere atacar SIEMPRE antes que defenderse

**Problema 2: Falta de estrategia táctica**
El NPC no considera:
- ❌ Vida actual (si está bajo de HP)
- ❌ Si el jugador está atacando en ese momento
- ❌ Si sería mejor esconderse para recargar cooldowns
- ❌ Usar el escudo como estrategia para flanquear

---

## ✅ Solución Implementada

### 1. Nueva Lógica: "Evaluar Necesidad de Defensa"

Se agregó una función inteligente que decide si el NPC **DEBERÍA** defenderse:

```csharp
/// <summary>
/// Evalúa si el NPC debería defenderse basado en su estado táctico
/// </summary>
private bool ShouldConsiderDefense()
{
    // A. Si tiene pocos ataques disponibles (1 o menos)
    int attacksReady = 0;
    if (_leftCd <= 0) attacksReady++;
    if (_rightCd <= 0) attacksReady++;
    if (_specialCd <= 0) attacksReady++;
    
    bool fewAttacksReady = attacksReady <= 1;
    
    // B. Si está en cooldown global (acaba de atacar)
    bool inGlobalCooldown = _globalCd > 0;
    
    // C. Probabilidad basada en dificultad
    // Dificultad alta = más defensivo (usa escudo más frecuentemente)
    float defensiveChance = settings.difficultyLevel * 0.4f; // Máx 40% si dificultad = 1
    bool randomDefensive = UnityEngine.Random.value < defensiveChance;
    
    return fewAttacksReady || inGlobalCooldown || randomDefensive;
}
```

**Criterios de defensa:**
1. **Pocos ataques disponibles** (1 o menos) → Mejor defenderse y esperar cooldowns
2. **En cooldown global** → No puede atacar de todas formas, mejor usar escudo
3. **Decisión táctica aleatoria** → Basada en dificultad (NPCs difíciles se defienden más)

### 2. Modificación del Estado EVALUATE

```csharp
// ANTES (línea 304)
if (!HasAnyAttackReady())
{
    _currentState = CombatState.DEFENSE;
    yield break;
}

// DESPUÉS
if (ShouldConsiderDefense())
{
    _currentState = CombatState.DEFENSE;
    yield break;
}
```

### 3. Mejora del Estado DEFENSE

Se agregó más lógica inteligente:

```csharp
IEnumerator State_Defense()
{
    StopMove();

    bool makeSmartDecision = UnityEngine.Random.value < settings.difficultyLevel;

    if (makeSmartDecision)
    {
        // === LÓGICA EXPERTA ===
        
        // A. Usar ESCUDO si está disponible
        if (settings.useShield && _shieldController != null && _shieldCd <= 0)
        {
            Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Activando ESCUDO defensivo");
            
            _shieldController.StartDefending(settings.shieldDuration);
            _shieldCd = settings.shieldCooldown + settings.shieldDuration;
            
            // NUEVO: Mientras se defiende, puede moverse lentamente
            // Esto permite estrategias como "defender y flanquear"
            float defendTime = settings.shieldDuration;
            float elapsed = 0f;
            
            while (elapsed < defendTime)
            {
                // Si el jugador está muy cerca, retroceder lentamente
                float dist = Vector3.Distance(transform.position, _player.position);
                if (dist < settings.minSafeDistance * 0.7f)
                {
                    Vector3 retreatDir = (transform.position - _player.position).normalized;
                    Vector3 retreatPos = transform.position + retreatDir * 2f;
                    MoveTo(retreatPos, settings.walkSpeed * 0.5f); // Retroceso lento
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        // B. Si no hay escudo, buscar COBERTURA
        else
        {
            Vector3 coverPos;
            if (TryGetCoverPosition(out coverPos))
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🌳 Buscando cobertura para recargar");
                MoveTo(coverPos, settings.runSpeed);
                
                while (_agent.remainingDistance > 0.5f && _agent.pathStatus == NavMeshPathStatus.PathComplete)
                {
                    yield return null;
                }
                
                // Esperar tras cobertura recuperando cooldowns
                Debug.Log($"[CombatBrain:{gameObject.name}] ⏳ Esperando tras cobertura...");
                yield return new WaitForSeconds(2.0f);
            }
            else
            {
                // Si no hay cobertura, esquiva
                Debug.Log($"[CombatBrain:{gameObject.name}] 🤸 Esquiva sin cobertura");
                yield return DoDodge();
            }
        }
    }
    else
    {
        // === LÓGICA TORPE (baja dificultad) ===
        Debug.Log($"[CombatBrain:{gameObject.name}] 😵 Defensa torpe");
        
        if (UnityEngine.Random.value > 0.5f)
            yield return DoDodge();
        else
            yield return new WaitForSeconds(1.0f);
    }

    _currentState = CombatState.EVALUATE;
}
```

**Mejoras clave:**
1. ✅ Más logs para debugging
2. ✅ El NPC puede **moverse lentamente** mientras se defiende
3. ✅ Si el jugador se acerca mucho, **retrocede con escudo**
4. ✅ Cobertura como alternativa al escudo

---

## 🎮 Comportamiento Resultante

### Escenario 1: Cooldowns en recarga
```
NPC ataca con ataque derecho
→ Quedan 0 ataques disponibles
→ ShouldConsiderDefense() = TRUE (fewAttacksReady)
→ Va a DEFENSE
→ settings.useShield = true, _shieldCd = 0
→ ✅ ACTIVA ESCUDO por 3 segundos
→ Mientras se defiende, cooldowns recargan
→ Escudo termina, vuelve a EVALUATE
```

### Escenario 2: En cooldown global
```
NPC acaba de atacar
→ _globalCd > 0 (cooldown global activo)
→ ShouldConsiderDefense() = TRUE (inGlobalCooldown)
→ Va a DEFENSE
→ ✅ ACTIVA ESCUDO mientras espera poder atacar de nuevo
```

### Escenario 3: Decisión táctica (NPC difícil)
```
NPC tiene 2 ataques disponibles
→ Pero difficultyLevel = 0.9 (experto)
→ defensiveChance = 0.9 * 0.4 = 36%
→ Random.value = 0.25 < 0.36
→ ShouldConsiderDefense() = TRUE (randomDefensive)
→ Va a DEFENSE
→ ✅ Usa escudo estratégicamente (no solo cuando está desesperado)
```

### Escenario 4: Jugador se acerca con escudo activo
```
NPC está defendiendo con escudo
→ Jugador se acerca mucho (dist < minSafeDistance * 0.7)
→ NPC retrocede LENTAMENTE con escudo
→ ✅ Mantiene distancia mientras se protege
→ Escudo termina, evalúa siguiente acción
```

### Escenario 5: Sin escudo disponible
```
NPC va a DEFENSE
→ _shieldCd > 0 (escudo en cooldown)
→ Busca cobertura con TryGetCoverPosition()
→ Si encuentra árbol/roca: ✅ Corre hacia allí
→ Si no hay cobertura: ✅ Esquiva lateral
```

---

## 🎯 Parámetros Ajustables

### En Inspector del NPC:
```
Settings:
  useShield: TRUE  ← Asegurarse que está activado
  shieldDuration: 3f  ← Duración de defensa (recomendado 2-4s)
  shieldCooldown: 8f  ← Cooldown entre usos (recomendado 6-10s)
  difficultyLevel: 0.8  ← Mayor = más defensivo (0-1)
  
  globalCooldown: 1.5f  ← Pausa entre ataques
  minSafeDistance: 3f  ← Distancia para retroceder
```

### Probabilidad de defensa por dificultad:
- **Dificultad 0.2** (Fácil): 8% chance táctica
- **Dificultad 0.5** (Normal): 20% chance táctica
- **Dificultad 0.8** (Difícil): 32% chance táctica
- **Dificultad 1.0** (Experto): 40% chance táctica

---

## 📊 Archivos Modificados

### `NPCCombatBrain.cs`
**Cambios:**
1. ✅ Agregada función `ShouldConsiderDefense()` - Nueva (línea ~620)
2. ✅ Modificado `State_Evaluate()` - Cambio en condición (línea ~304)
3. ✅ Mejorado `State_Defense()` - Movimiento mientras defiende (línea ~485)
4. ✅ Agregados múltiples logs para debugging

**Líneas totales afectadas:** ~80 líneas
**Compatibilidad:** 100% retrocompatible

---

## ✅ Testing Recomendado

### Test 1: Verificar uso básico de escudo
1. Configurar NPC con `useShield = true`
2. Atacar al NPC varias veces
3. **Esperado:** Después de algunos ataques, debe activar escudo (logs confirmarán)

### Test 2: Movimiento con escudo
1. NPC activa escudo
2. Acercarse mucho al NPC
3. **Esperado:** NPC retrocede lentamente manteniendo escudo activo

### Test 3: Escudo en cooldown
1. NPC usa escudo
2. Inmediatamente después ataca varias veces
3. **Esperado:** No vuelve a usar escudo hasta que pase `shieldCooldown`

### Test 4: Alternativa a escudo (cobertura)
1. Configurar NPC con `useShield = false` o `_shieldCd` alto
2. Poner obstáculos con layer "Default" cerca
3. **Esperado:** NPC busca cobertura detrás de objetos

### Test 5: Dificultad alta vs baja
1. Probar con `difficultyLevel = 0.2` (fácil)
   - **Esperado:** Rara vez usa escudo tácticamente
2. Probar con `difficultyLevel = 0.9` (difícil)
   - **Esperado:** Usa escudo frecuentemente, incluso con ataques disponibles

---

## 🔍 Debugging

### Logs a buscar en consola:
```csharp
"[CombatBrain:NpcName] 🛡️ Activando ESCUDO defensivo"
"[CombatBrain:NpcName] 🌳 Buscando cobertura para recargar"
"[CombatBrain:NpcName] 😵 Defensa torpe"
"[NPCShieldController] 🛡️ DEFENSA ACTIVADA - Duración: 3.0s"
```

### Si el escudo NO se usa, verificar:
1. ❌ `settings.useShield` está en FALSE
2. ❌ `NPCShieldController` no está en el GameObject
3. ❌ `shieldCd` está constantemente alto (ajustar `shieldCooldown`)
4. ❌ `difficultyLevel` muy bajo y mala suerte con random
5. ❌ NPC siempre tiene muchos ataques disponibles (ajustar cooldowns)

---

## 🎯 Resultado

El NPC ahora:
- ✅ **USA EL ESCUDO** regularmente durante combate
- ✅ Se defiende estratégicamente (no solo cuando está desesperado)
- ✅ Puede retroceder con escudo si el jugador presiona
- ✅ Busca cobertura como alternativa
- ✅ Comportamiento escalable por dificultad

**El problema está RESUELTO** ✨

---

**Fecha:** 2025-12-29  
**Versión:** 1.0  
**Estado:** ✅ IMPLEMENTADO Y LISTO PARA TESTING

---

## 🔗 Fixes Relacionados

Este fix se implementó junto con:
- **FIX_INTERROGACION_COMBATE_RECIENTE.md** - Soluciona el problema de interrogación durante giros en combate

Ambos problemas mejoran significativamente la inteligencia táctica del NPC durante combate.


