# 📋 INSERCIÓN CONSOLIDADA: Sección 3.4.1 FSM Táctica del NPCCombatBrain

**INSTRUCCIONES:** Insertar este contenido en DOCUMENTACION_TECNICA.md después de la línea 643 (justo después de "#### NPCCombatBrain (IA Táctica)").

---

##### 🎯 PREMISA DEL SISTEMA (Actualizado 29 Dic 2024)

El NPC es un **MAGO COMBATIENTE** cuyo objetivo principal es **MATAR AL PLAYER**, pero también **TEME POR SU VIDA**.

**Estrategia de Combate:**

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
   └─ Animaciones visuales (❓ → ❗ → ⚔️)
```

##### Estados Tácticos de la FSM de Combate

```csharp
public enum TacticalCombatState
{
    EVALUATE,               // Análisis y toma de decisiones (hub central)
    ATTACK,                 // Modo ofensivo con ataques disponibles
    HIDING_TO_RECHARGE,     // ⭐ NUEVO: Esconderse estratégicamente para recargar
    SEARCHING,              // Buscar activamente al player
    REPOSITION              // Mantener distancia óptima (huir o acercarse)
}
```

**Diagrama de Transiciones Completo:**

```
┌──────────────┐
│  EVALUATE    │ ← Estado central (hub de decisiones)
└──────┬───────┘
       │
       ├─ ¿Ataques listos? → ATTACK
       ├─ ¿Sin ataques? → HIDING_TO_RECHARGE ⭐ NUEVO
       ├─ ¿Sin LoS? → SEARCHING
       └─ ¿Player muy cerca/lejos? → REPOSITION
       
┌──────────────┐
│  ATTACK      │ ⚔️ Modo ofensivo
└──────┬───────┘
       ├─ Lanza hechizos secuencialmente (Left → Right → Special)
       ├─ Movimiento circular alrededor del player
       ├─ Micro-pausas entre ataques (ritmo natural)
       ├─ Burst attacks seguidos de reposicionamiento
       ├─ Sin ataques listos → HIDING_TO_RECHARGE
       └─ Todos los ataques gastados → EVALUATE
       
┌──────────────────────┐
│ HIDING_TO_RECHARGE   │ 🛡️ ⭐ NUEVO (29 Dic 2024)
└──────┬───────────────┘
       ├─ Busca cobertura detrás de obstáculos (FindCoverBehindObstacle)
       ├─ Muestra animación "SenseSomethingSearching_NoWeapon" + ❓
       ├─ Espera a recargar mínimo 2 ataques
       ├─ Si es atacado mientras recarga:
       │   ├─ Gira 180° hacia atacante
       │   ├─ Usa escudo si disponible 🛡️
       │   └─ Contraataca si tiene hechizos
       ├─ Recargado + tiene LoS → EVALUATE (con alerta visual ❗)
       └─ Recargado sin LoS → SEARCHING
       
┌──────────────┐
│  SEARCHING   │ 🔍 Buscar al player
└──────┬───────┘
       ├─ Busca en última posición conocida
       ├─ Animación "SenseSomethingSearching_NoWeapon" + ❓
       ├─ Pausas mirando alrededor (2s cada parada)
       ├─ Vuelve a áreas clave del combate
       ├─ Recupera LoS → Animación "SenseSomethingStart_NoWeapon" + ❗
       └─ Encuentra al player → EVALUATE → ATTACK
       
┌──────────────┐
│ REPOSITION   │ 🏃 Mantener distancia óptima
└──────┬───────┘
       ├─ Huye si player muy cerca (< minSafeDistance)
       ├─ Busca cobertura si está disponible
       ├─ Se acerca si player muy lejos (> maxDistance)
       ├─ Gira hacia punto de escape inmediatamente (no espera)
       ├─ Al llegar → Mira al player (LookAt)
       └─ Distancia normalizada → EVALUATE
```

##### 🔧 Refactorización Completa (29 Dic 2024)

**Problema resuelto:** NPCs actuaban de forma nerviosa con cambios de estado constantes.

**Causa raíz:** Lógica con componentes aleatorios (`Random.value`) en decisiones críticas.

**1. Eliminación de Componentes Aleatorios**

**❌ ANTES (causaba nerviosismo):**
```csharp
// Método ShouldConsiderDefense() con Random.value
private bool ShouldConsiderDefense()
{
    // ...
    if (Random.value < defenseProbability) { // ← Cambios cada frame
        return true;
    }
    // ...
}

// En State_Evaluate()
if (ShouldConsiderDefense()) {
    _currentState = CombatState.DEFENSE; // Cambio constante
}
```

**✅ AHORA (determinista y predecible):**
```csharp
// En State_Evaluate()
int attacksReady = CountAttacksReady();

if (attacksReady > 0) {
    // Tiene hechizos → ATACAR agresivamente
    if (dist <= settings.maxDistance && _globalCd <= 0) {
        _currentState = CombatState.ATTACK;
    } else if (dist < settings.minDistance) {
        _currentState = CombatState.REPOSITION; // Muy cerca, huir
    }
} else {
    // Sin hechizos → ESCONDERSE para recargar
    _currentState = CombatState.HIDING_TO_RECHARGE;
}

// ✅ Sin Random.value, comportamiento 100% predecible
```

**Resultado:**
- ✅ Sin cambios de estado aleatorios
- ✅ Comportamiento predecible: "Atacar hasta gastar, luego esconderse"
- ✅ Estrategia agresiva coherente con un mago combatiente

**2. Nuevo Estado: HIDING_TO_RECHARGE**

Reemplaza la lógica confusa de `DEFENSE` con un estado dedicado para recarga estratégica:

```csharp
IEnumerator State_HidingToRecharge()
{
    Debug.Log("[NPCCombatBrain] 🏃 Escond iéndose para recargar hechizos");
    
    // A. Buscar cobertura detrás de obstáculos
    Vector3 coverPosition;
    bool foundCover = FindCoverBehindObstacle(out coverPosition);
    
    if (!foundCover) {
        // Sin cobertura → Huir en dirección opuesta al player
        Vector3 dirAway = (transform.position - _player.position).normalized;
        coverPosition = transform.position + dirAway * 5f;
    }
    
    // B. Moverse hacia la cobertura (corriendo)
    MoveTo(coverPosition, settings.runSpeed);
    
    // C. Esperar a llegar (máx 3s)
    float timer = 0;
    while (_agent.remainingDistance > 1.5f && timer < 3f) {
        timer += Time.deltaTime;
        yield return null;
    }
    
    StopMove();
    
    // D. Al llegar → Mostrar animación de búsqueda + ❓
    if (_animator != null) {
        _animator.PlaySearching(); // SenseSomethingSearching_NoWeapon
    }
    
    if (_alertIconController != null) {
        _alertIconController.ShowQuestionMark(2f);
    }
    
    Debug.Log("[NPCCombatBrain] 🛡️ En cobertura, recargando hechizos...");
    
    // E. Esperar a recargar hechizos (mínimo 2 ataques listos)
    while (CountAttacksReady() < 2) {
        
        // Si es atacado durante la recarga → Responder
        if (IsPlayerAttacking()) {
            // Opción A: Usar escudo si está disponible
            if (settings.useShield && _shieldCd <= 0) {
                Debug.Log("[NPCCombatBrain] 🛡️ Activando escudo defensivo");
                ActivateShield();
                yield return new WaitForSeconds(settings.shieldDuration);
            }
            // Opción B: Si ya tiene algún ataque, contraatacar
            else if (CountAttacksReady() > 0) {
                Debug.Log("[NPCCombatBrain] ⚔️ Contraataque desde cobertura");
                break; // Salir para EVALUATE
            }
        }
        
        yield return new WaitForSeconds(0.3f);
    }
    
    // F. Recargado → Buscar al player
    Debug.Log("[NPCCombatBrain] ✅ Hechizos recargados");
    
    if (_hasLineOfSight) {
        // Puede ver al player → Alertar y atacar
        if (_animator != null) {
            _animator.PlaySenseSomething(); // SenseSomethingStart_NoWeapon
        }
        if (_alertIconController != null) {
            _alertIconController.ShowExclamation(1.5f); // ❗
        }
        
        yield return new WaitForSeconds(0.5f); // Pausa dramática
        _currentState = CombatState.EVALUATE; // → ATTACK
    } else {
        // No puede ver al player → Buscar activamente
        _currentState = CombatState.SEARCHING;
    }
}
```

**3. Sistema de Búsqueda de Cobertura Inteligente**

**Método:** `FindCoverBehindObstacle(out Vector3 coverPosition)`

**Algoritmo:**
1. Busca objetos en capa "Default" con `Physics.OverlapSphere(coverSearchRadius)`
2. Para cada objeto cercano:
   - Lanza `Raycast` desde Player hacia el objeto
   - Verifica que el objeto **bloquee la línea de visión**
   - Calcula posición "detrás" del objeto (opuesta al jugador)
3. Evalúa con scoring:
   - Distancia al NPC (más cerca = mejor)
   - Distancia al Player (más lejos = mejor)
   - Tamaño del objeto (más grande = mejor protección)
4. Elige la mejor posición y la devuelve

```csharp
/// <summary>
/// Encuentra cobertura detrás de obstáculos usando Raycast
/// </summary>
bool FindCoverBehindObstacle(out Vector3 coverPosition)
{
    coverPosition = transform.position;
    
    // 1. Buscar objetos Default cercanos
    Collider[] nearbyObjects = Physics.OverlapSphere(
        transform.position, 
        settings.coverSearchRadius, 
        LayerMask.GetMask("Default")
    );
    
    if (nearbyObjects.Length == 0) {
        Debug.Log("[NPCCombatBrain] ⚠️ No hay objetos para cobertura");
        return false;
    }
    
    float bestScore = -1f;
    bool foundValid = false;
    
    foreach (var obj in nearbyObjects) {
        // 2. Raycast desde Player hacia el objeto
        Vector3 dirToObj = (obj.transform.position - _player.position).normalized;
        float distPlayerToObj = Vector3.Distance(_player.position, obj.transform.position);
        
        RaycastHit hit;
        bool blocksLineOfSight = Physics.Raycast(
            _player.position,
            dirToObj,
            out hit,
            distPlayerToObj + 2f, // Margen extra
            LayerMask.GetMask("Default")
        );
        
        // 3. Verificar que el raycast impactó este objeto
        if (blocksLineOfSight && hit.collider == obj) {
            // ✅ Este objeto bloquea la visión del player
            
            // 4. Calcular posición "detrás" del objeto (opuesta al jugador)
            Vector3 dirNPCtoPlayer = (transform.position - _player.position).normalized;
            Vector3 behindPos = obj.transform.position + 
                dirNPCtoPlayer * (obj.bounds.extents.magnitude + 1.5f);
            
            // Proyectar al NavMesh
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(behindPos, out navHit, 3f, NavMesh.AllAreas)) {
                // 5. Scoring: Priorizar cobertura cercana al NPC y lejos del player
                float distToMe = Vector3.Distance(transform.position, navHit.position);
                float distToPlayer = Vector3.Distance(_player.position, navHit.position);
                
                // Score: (distancia al player / distancia a mí) * tamaño del objeto
                float score = (distToPlayer / (distToMe + 1f)) * obj.bounds.size.magnitude;
                
                if (score > bestScore) {
                    bestScore = score;
                    coverPosition = navHit.position;
                    foundValid = true;
                    
                    Debug.Log($"[NPCCombatBrain] 🎯 Cobertura candidata: {obj.name} (score: {score:F2})");
                }
            }
        }
    }
    
    if (foundValid) {
        Debug.Log($"[NPCCombatBrain] ✅ Cobertura seleccionada en {coverPosition}");
    }
    
    return foundValid;
}
```

**4. Animaciones Contextuales Correctas**

Todas las situaciones de detección/alerta ahora reproducen las animaciones apropiadas:

| Evento | Animación | Icono | Cuándo |
|--------|-----------|-------|--------|
| **Encuentra al player tras búsqueda** | `SenseSomethingStart_NoWeapon` | ❗ | SEARCHING → EVALUATE |
| **Encuentra al player en movimiento** | `SenseSomethingStart_NoWeapon` | ❗ | REPOSITION con LoS |
| **Recargado y tiene LoS** | `SenseSomethingStart_NoWeapon` | ❗ | HIDING → EVALUATE |
| **Atacado por la espalda** | `SenseSomethingStart_NoWeapon` | ❗ | OnTakeDamage() |
| **Llegó a cobertura sin LoS** | `SenseSomethingSearching_NoWeapon` | ❓ | HIDING_TO_RECHARGE |
| **Pausa durante búsqueda** | `SenseSomethingSearching_NoWeapon` | ❓ | SEARCHING (pausas) |

**Llamada en código:**
```csharp
// Alerta (encontró al player)
_animator.PlaySenseSomething(); // Reproduce SenseSomethingStart_NoWeapon

// Búsqueda (no sabe dónde está el player)
_animator.PlaySearching(); // Reproduce SenseSomethingSearching_NoWeapon
```

**5. Respuesta Inteligente a Daño (OnTakeDamage)**

Cuando el NPC recibe daño, responde según su estado actual:

```csharp
public void OnTakeDamage(Vector3 damageSourcePosition)
{
    // Calcular dirección del ataque
    Vector3 directionToDamage = (damageSourcePosition - transform.position).normalized;
    float angle = Vector3.Angle(transform.forward, directionToDamage);
    bool attackedFromBehind = angle > 90f;
    
    // Si está en estados vulnerables
    if (_currentState == CombatState.SEARCHING || 
        _currentState == CombatState.HIDING_TO_RECHARGE ||
        _currentState == CombatState.REPOSITION)
    {
        Debug.Log($"[NPCCombatBrain] 💥 Atacado desde {(attackedFromBehind ? "atrás" : "adelante")}");
        
        // 🔄 GIRAR inmediatamente hacia la fuente del daño
        transform.rotation = Quaternion.LookRotation(directionToDamage);
        
        // 🎬 Animación de alerta + icono de admiración
        if (_animator != null) {
            _animator.PlaySenseSomething(); // SenseSomethingStart_NoWeapon
        }
        
        if (_alertIconController != null) {
            _alertIconController.ShowExclamation(1.5f); // ❗
        }
        
        // Decidir respuesta inteligente
        int attacksAvailable = CountAttacksReady();
        
        if (attacksAvailable > 0) {
            // ✅ Tiene ataques → CONTRAATACAR
            Debug.Log("[NPCCombatBrain] ⚔️ Contraataque");
            _currentState = CombatState.EVALUATE; // → ATTACK
        } 
        else if (settings.useShield && _shieldCd <= 0) {
            // ✅ No tiene ataques pero sí escudo → DEFENDER
            Debug.Log("[NPCCombatBrain] 🛡️ Activando escudo");
            ActivateShield();
        } 
        else {
            // ❌ Sin recursos → Seguir huyendo
            Debug.Log("[NPCCombatBrain] 🏃 Sin recursos, huyendo");
            // Permanece en estado actual (HIDING_TO_RECHARGE o REPOSITION)
        }
    }
}
```

##### 🎮 Escenarios de Combate Completos

**Escenario 1: Combate Ofensivo Puro**
```
1. NPC ve al player → EVALUATE
2. Tiene 3 ataques disponibles (✅✅✅) → ATTACK
3. Ataca con right hand → 2 ataques restantes (✅✅❌)
4. Micro-pausa 0.5s
5. Vuelve a EVALUATE → Aún tiene 2 → ATTACK
6. Ataca con left hand → 1 ataque restante (✅❌❌)
7. Micro-pausa 0.5s
8. Vuelve a EVALUATE → Aún tiene 1 → ATTACK
9. Ataca con special → 0 ataques restantes (❌❌❌)
10. Vuelve a EVALUATE → Sin ataques → HIDING_TO_RECHARGE 🏃
```

**Escenario 2: Recarga Estratégica**
```
1. NPC sin ataques (❌❌❌) → HIDING_TO_RECHARGE
2. Busca cobertura con Raycast → Encuentra árbol/roca ✅
3. Corre hacia cobertura → Llega y se detiene
4. Muestra animación "SenseSomethingSearching_NoWeapon" + ❓
5. Debug: "🛡️ En cobertura, recargando hechizos..."
6. Espera recargando cooldowns... (5s)
7. Player le dispara desde lejos 💥
   ├─ ¿Tiene escudo? → Activa escudo 🛡️ (3s)
   └─ ¿No tiene escudo? → Sigue esperando
8. Recargó 2+ ataques (✅✅❌) → Listo
9. Busca al player → Lo ve → Animación "SenseSomethingStart_NoWeapon" + ❗
10. EVALUATE → ATTACK ⚔️
```

**Escenario 3: Atacado Durante Huida (Por la Espalda)**
```
1. NPC huyendo hacia cobertura (HIDING_TO_RECHARGE)
2. Player le dispara por la espalda 💥
3. OnTakeDamage() detecta:
   - Angle > 90° → Atacado por la espalda ✅
4. Respuesta automática:
   ├─ GIRAR 180° hacia el player 🔄
   ├─ Animación "SenseSomethingStart_NoWeapon" 🎬
   ├─ Icono de admiración ❗
   └─ Decidir:
       ├─ Tiene hechizos (✅) → CONTRAATACAR (EVALUATE → ATTACK)
       ├─ Tiene escudo (🛡️) → DEFENDER (activa escudo)
       └─ Sin recursos (❌) → Seguir huyendo más rápido
```

**Escenario 4: Búsqueda Activa**
```
1. NPC recargado (✅✅✅) pero sin LoS → SEARCHING
2. Muestra ❓ + "SenseSomethingSearching_NoWeapon"
3. Se mueve a última posición conocida del player
4. Parada #1:
   ├─ Muestra ❓ + "SenseSomethingSearching_NoWeapon"
   ├─ Mira alrededor 360° (2s)
   └─ No encuentra al player → Sigue buscando
5. Parada #2: Vuelve a punto inicial del combate
   ├─ Misma rutina de búsqueda
   └─ Tampoco lo encuentra
6. Parada #3: Área clave cercana
   ├─ Muestra ❓
   └─ ¡Recupera LoS! Ve al player ✅
7. Alerta:
   ├─ Animación "SenseSomethingStart_NoWeapon"
   ├─ Icono ❗
   └─ Pausa dramática 0.5s
8. EVALUATE → ATTACK ⚔️
```

##### 📊 Comparación: ANTES vs. DESPUÉS

| Aspecto | ❌ ANTES (Con Nerviosismo) | ✅ DESPUÉS (Refactorizado 29 Dic) |
|---------|-------------------------|-------------------------|
| **Toma de decisiones** | `Random.value` en `ShouldConsiderDefense()` | Determinista basado en cooldowns |
| **Frecuencia de cambios** | Cada frame (~60 veces/seg) | Controlados por lógica |
| **Recarga de hechizos** | Estado DEFENSE confuso | Estado dedicado HIDING_TO_RECHARGE |
| **Búsqueda de cobertura** | Básica o inexistente | Raycast inteligente con scoring |
| **Animaciones** | Inconsistentes o ausentes | Contextuales y correctas |
| **Respuesta a daño** | Genérica | Inteligente según contexto y ángulo |
| **Comportamiento** | "Nervioso" e impredecible | Agresivo y estratégico |
| **Legibilidad de código** | Difícil de mantener | Clara y modular |

##### 🔧 Configuración en Inspector

```
NPCCombatConfig (ScriptableObject)

[Combat Stats]
├─ health: 100
├─ attackDamage: 10
└─ attackCooldown: 1.5

[Tactical Settings] ⭐ NUEVO
├─ useHidingToRecharge: ☑
├─ minAttacksToExitCover: 2          // Mín. ataques antes de salir
├─ coverSearchRadius: 15             // Radio de búsqueda de cobertura
├─ coverStayDuration: 5              // Tiempo máx en cobertura
├─ activelySearchForPlayer: ☑        // Búsqueda activa vs pasiva
├─ searchDuration: 15                // Tiempo total de búsqueda
└─ searchMovementRadius: 10          // Radio de movimiento al buscar

[Shield System]
├─ useShield: ☑
├─ shieldCooldown: 10                // Cooldown entre usos
└─ shieldDuration: 3                 // Duración del escudo

[Animaciones Requeridas] ⭐ IMPORTANTE
├─ En el Animator Controller deben existir:
│   ├─ SenseSomethingStart_NoWeapon   (alerta, encuentra al player)
│   ├─ SenseSomethingSearching_NoWeapon (búsqueda, no sabe dónde está)
│   ├─ Attack01_NoWeapon → Attack05_NoWeapon (ataques variados)
│   ├─ Defend_NoWeapon (escudo/defensa)
│   └─ Idle_Battle_NoWeapon (idle de combate)
```

**Verificar en Scene:**
- Objetos con capa "Default" sirven como cobertura automáticamente
- Si no hay cobertura disponible, el NPC huirá en dirección opuesta

##### ⚠️ Métodos Eliminados/Deprecados

**Eliminados en la refactorización:**
- ❌ `ShouldConsiderDefense()` - Reemplazado por `CountAttacksReady() == 0`
- ❌ Uso de `Random.value` en EVALUATE
- ❌ `ChooseRandomDefensiveBehavior()` - Lógica determinista ahora

**Disponibles pero no usados actualmente:**
- ⚠️ `TryChangeState()` - Anti-nerviosismo adicional (por si se necesita)
- ⚠️ `IsPlayerInFieldOfView()` - Implementado pero redundante con LoS

##### ✅ Testing Checklist

**Comportamiento Básico:**
- [ ] NPC ataca agresivamente cuando tiene hechizos disponibles
- [ ] NPC se esconde para recargar cuando gasta todos los hechizos
- [ ] No hay comportamiento "nervioso" (cambios rápidos de estado)

**Estado HIDING_TO_RECHARGE:**
- [ ] Busca cobertura detrás de objetos Default
- [ ] Animación "SenseSomethingSearching_NoWeapon" + ❓ al llegar a cobertura
- [ ] Espera a recargar mínimo 2 ataques antes de salir
- [ ] Usa escudo si es atacado durante la recarga (si tiene escudo)

**Estado SEARCHING:**
- [ ] Animación "SenseSomethingSearching_NoWeapon" + ❓ durante búsqueda
- [ ] Al encontrar al player → Animación "SenseSomethingStart_NoWeapon" + ❗
- [ ] Pausas de 2s mirando alrededor en cada punto

**Respuesta a Daño:**
- [ ] NPC se gira 180° si es atacado por la espalda
- [ ] Animación "SenseSomethingStart_NoWeapon" + ❗ al recibir daño
- [ ] Contraataca si tiene hechizos disponibles
- [ ] Activa escudo si no tiene hechizos pero sí escudo

**Flujo Completo:**
- [ ] EVALUATE → ATTACK → HIDING_TO_RECHARGE → SEARCHING → EVALUATE funciona correctamente
- [ ] Iconos visuales (❗❓) se muestran en los momentos correctos
- [ ] Transiciones suaves entre estados sin saltos

##### 🐛 Problemas Conocidos

**Warning: Capa "PlayerProjectile"**
```csharp
// En IsPlayerAttacking(), línea ~852
Collider[] nearbyProjectiles = Physics.OverlapSphere(
    transform.position, 10f, 
    LayerMask.GetMask("PlayerProjectile") // ⚠️ Verificar que existe
);
```

**Solución pendiente:** Verificar que la capa "PlayerProjectile" existe en Project Settings → Tags & Layers, o cambiar la detección a otra capa.

##### 📝 Notas Finales

Esta refactorización **elimina completamente el comportamiento nervioso** causado por:
1. ✅ Componentes aleatorios en decisiones críticas
2. ✅ Falta de estado dedicado para recarga
3. ✅ Animaciones incorrectas o ausentes

El nuevo sistema es:
- 🎯 **Determinista**: Mismas condiciones = mismo comportamiento
- 🧠 **Estratégico**: El NPC tiene un plan claro (atacar → esconderse → atacar)
- 👀 **Visual**: Animaciones e iconos reflejan correctamente su estado mental
- ⚔️ **Agresivo**: Prioriza atacar sobre defenderse (como un mago de combate)
- 🛡️ **Inteligente**: Usa cobertura real del escenario, no posiciones aleatorias

**Referencias:**
- `FIX_IA_COMBATE_NPC_REFACTORIZACION_COMPLETA.md` (29 Dic 2024)
- `DISEÑO_FSM_TACTICO_NPC.md`
- `FEATURE_NPC_COBERTURA_Y_BUSQUEDA.md`

**¡El NPC ahora es un oponente digno que pelea inteligentemente por su vida!**

---

**FIN DE LA INSERCIÓN**

