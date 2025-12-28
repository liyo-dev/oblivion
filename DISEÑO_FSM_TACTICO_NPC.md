# 🎯 DISEÑO: Sistema FSM Táctico Mejorado para NPC

## 📋 ANÁLISIS DEL DIAGRAMA DE FLUJO

### Estados Principales (FSM)

```csharp
public enum TacticalState
{
    EVALUATE,        // Estado de análisis y toma de decisiones
    REPOSITION,      // Alejarse/acercarse para mantener distancia óptima
    ATTACK,          // Modo ofensivo con maná disponible
    DEFENSE          // Modo defensivo sin maná (escudo/cobertura/esquiva)
}
```

---

## 🔍 ESTADO: EVALUATE (Inicial)

### Propósito
Evaluar la situación y decidir el próximo estado.

### Lógica de Decisión

```
1. ¿Player muy cerca? (< minSafeDistance)
   → Ir a STATE_REPOSITION (huir)

2. ¿Tengo maná/cooldowns listos?
   → SÍ: Ir a STATE_ATTACK
   → NO: Ir a STATE_DEFENSE
```

### Implementación

```csharp
void State_Evaluate()
{
    float distance = Vector3.Distance(transform.position, _player.position);
    
    // Prioridad 1: Demasiado cerca → Reposicionarse
    if (distance < _settings.minDistance)
    {
        _tacticalState = TacticalState.REPOSITION;
        return;
    }
    
    // Prioridad 2: ¿Tengo ataques disponibles?
    bool hasAttackReady = HasAnyAttackReady();
    
    if (hasAttackReady)
    {
        _tacticalState = TacticalState.ATTACK;
    }
    else
    {
        _tacticalState = TacticalState.DEFENSE;
    }
}
```

---

## 🏃 ESTADO: REPOSITION

### Propósito
Mantener distancia segura del jugador.

### Flujo
```
1. Calcular punto opuesto al jugador
2. Moverse a ese punto
3. Al llegar → Mirar al jugador (LookAt)
4. Volver a EVALUATE
```

### Implementación

```csharp
IEnumerator State_Reposition()
{
    // 1. Calcular posición de escape (opuesta al jugador)
    Vector3 retreatPos = ComputeRetreatPosition(distanceToPlayer);
    
    // 2. Moverse a esa posición
    NavMeshAgentUtility.SetDestination(_agent, retreatPos, 0.5f);
    
    // ✅ Girar inmediatamente hacia el punto de escape
    Vector3 dirToEscape = (retreatPos - transform.position).normalized;
    dirToEscape.y = 0;
    if (dirToEscape.sqrMagnitude > 0.01f)
    {
        Quaternion targetRot = Quaternion.LookRotation(dirToEscape);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
    }
    
    StartMoving(1.2f); // Correr
    
    // 3. Esperar a llegar
    while (!_agent.hasPath || _agent.remainingDistance > 0.5f)
    {
        yield return null;
    }
    
    // 4. Al llegar → Mirar al jugador
    StopAndIdle();
    FacePlayer();
    
    // 5. Volver a evaluar
    _tacticalState = TacticalState.EVALUATE;
}
```

---

## ⚔️ ESTADO: ATTACK

### Propósito
Modo ofensivo - atacar mientras tenga recursos.

### Flujo
```
1. Lanzar hechizo (CastSpell)
2. Después del ataque:
   - ¿Queda maná/cooldowns? 
     → SÍ: Decisión Táctica (RNG):
       a) Seguir atacando (60%)
       b) Flanquear/Reposicionar (25%)
       c) Buscar refugio ofensivo (15%)
     → NO: Ir a STATE_DEFENSE
```

### Implementación

```csharp
IEnumerator State_Attack()
{
    while (_tacticalState == TacticalState.ATTACK)
    {
        // 1. Verificar que tenemos ataque disponible
        if (!HasAnyAttackReady())
        {
            _tacticalState = TacticalState.DEFENSE;
            yield break;
        }
        
        // 2. Posicionarse para atacar
        float distance = Vector3.Distance(transform.position, _player.position);
        bool inRange = distance >= _settings.minDistance && distance <= _settings.maxDistance;
        
        if (!inRange)
        {
            // Acercarse o alejarse
            Vector3 targetPos = inRange ? transform.position : ComputeApproachPosition(distance);
            NavMeshAgentUtility.SetDestination(_agent, targetPos, 0.5f);
            StartMoving(0.8f);
            yield return new WaitForSeconds(0.5f);
            continue;
        }
        
        // 3. Detenerse y apuntar
        StopAndIdle();
        FacePlayer();
        
        // 4. Windup (preparación)
        float windupTime = UnityEngine.Random.Range(_settings.windupMin, _settings.windupMax);
        yield return new WaitForSeconds(windupTime);
        
        // 5. Ejecutar ataque
        bool attackExecuted = TryExecuteAttack();
        
        if (!attackExecuted)
        {
            // No pudo atacar (sin LOS, etc.)
            yield return new WaitForSeconds(0.5f);
            continue;
        }
        
        // 6. Después del ataque → Decisión táctica
        yield return new WaitForSeconds(1f); // Post-ataque breve
        
        if (HasAnyAttackReady())
        {
            // Loop agresivo con variación táctica
            float decision = UnityEngine.Random.value;
            
            if (decision < 0.60f)
            {
                // 60% - Seguir atacando desde la posición actual
                Debug.Log("[TacticalAI] Decisión: Seguir atacando");
            }
            else if (decision < 0.85f)
            {
                // 25% - Flanquear (moverse a un lado)
                Debug.Log("[TacticalAI] Decisión: Flanquear");
                Vector3 flankPos = ComputeFlankPosition();
                NavMeshAgentUtility.SetDestination(_agent, flankPos, 0.5f);
                StartMoving(1.0f);
                yield return new WaitForSeconds(1.5f);
            }
            else
            {
                // 15% - Buscar refugio ofensivo (cubrir mientras recarga)
                Debug.Log("[TacticalAI] Decisión: Buscar refugio ofensivo");
                Vector3 coverPos = FindNearestCover();
                if (coverPos != Vector3.zero)
                {
                    NavMeshAgentUtility.SetDestination(_agent, coverPos, 0.5f);
                    StartMoving(1.0f);
                    yield return new WaitForSeconds(2f);
                }
            }
        }
        else
        {
            // Sin ataques disponibles → Modo defensivo
            _tacticalState = TacticalState.DEFENSE;
        }
        
        yield return null;
    }
}
```

---

## 🛡️ ESTADO: DEFENSE

### Propósito
Modo defensivo sin recursos - sobrevivir hasta recuperar cooldowns.

### Gestión de Dificultad

```
difficultyLevel (0.0 - 1.0):

- ALTA (> 0.7):
  a) Usar Escudo Mágico (si cooldown OK)
  b) Buscar Cobertura Inteligente (FindCover con Raycast)

- MEDIA (0.3 - 0.7):
  a) Buscar Cobertura Simple
  b) Circular alrededor del jugador

- BAJA (< 0.3):
  a) Esquiva Simple (moverse → mirar → esperar)
```

### Implementación

```csharp
IEnumerator State_Defense()
{
    Debug.Log($"[TacticalAI] Entrando en modo DEFENSA (dificultad: {_settings.difficultyLevel:F2})");
    
    // 1. Decidir acción basada en dificultad
    float difficulty = _settings.difficultyLevel;
    
    if (difficulty > 0.7f)
    {
        // DIFICULTAD ALTA - Comportamiento experto
        
        // Opción A: Escudo Mágico
        if (_settings.useShield && _shieldCooldownTimer <= 0f)
        {
            yield return UseShieldDefense();
        }
        // Opción B: Cobertura Inteligente con Raycast
        else
        {
            yield return FindAndUseCover_Advanced();
        }
    }
    else if (difficulty > 0.3f)
    {
        // DIFICULTAD MEDIA - Comportamiento competente
        
        // Buscar cobertura simple o circular
        Vector3 coverPos = FindNearestCover();
        if (coverPos != Vector3.zero)
        {
            yield return MoveToAndHideBehindCover(coverPos);
        }
        else
        {
            // Sin cobertura → Circular alrededor del jugador
            yield return CircleAroundPlayer();
        }
    }
    else
    {
        // DIFICULTAD BAJA - Esquiva simple
        yield return SimpleEvasion();
    }
    
    // 2. Después de defender → Volver a evaluar
    _tacticalState = TacticalState.EVALUATE;
}
```

---

## 🎯 SISTEMA DE COBERTURA (FindCover con Raycast)

### Concepto
Encontrar objetos con tag "Cover" y usar Raycast para verificar que bloquean la línea de visión del jugador.

### Lógica

```
1. Buscar todos los objetos "Cover" en un radio
2. Para cada objeto:
   a) Lanzar Raycast desde Player hacia el objeto
   b) Si el Raycast impacta el objeto → Cobertura válida
   c) Calcular posición "detrás" del objeto (opuesta al jugador)
3. Elegir la cobertura más cercana/segura
4. Moverse a esa posición
5. Esperar hasta que cooldowns estén listos
```

### Implementación

```csharp
/// <summary>
/// Encuentra cobertura usando Raycasts para verificar bloqueo de línea de visión
/// </summary>
Vector3 FindCover_WithRaycast()
{
    // 1. Buscar objetos marcados como "Cover"
    Collider[] covers = Physics.OverlapSphere(
        transform.position, 
        _settings.coverSearchRadius, 
        _settings.coverLayerMask
    );
    
    if (covers.Length == 0)
        return Vector3.zero;
    
    Vector3 bestCoverPos = Vector3.zero;
    float bestScore = float.MinValue;
    
    foreach (var coverCollider in covers)
    {
        // 2. Verificar que el objeto tiene tag "Cover"
        if (!coverCollider.CompareTag("Cover"))
            continue;
        
        Vector3 coverCenter = coverCollider.bounds.center;
        
        // 3. Raycast desde Player hacia el objeto de cobertura
        Vector3 dirFromPlayerToCover = (coverCenter - _player.position).normalized;
        float distPlayerToCover = Vector3.Distance(_player.position, coverCenter);
        
        RaycastHit hit;
        bool blocksLineOfSight = Physics.Raycast(
            _player.position,
            dirFromPlayerToCover,
            out hit,
            distPlayerToCover + 2f, // Margen extra
            _settings.coverLayerMask
        );
        
        // 4. Si el raycast impacta este objeto → Bloquea visión ✅
        if (blocksLineOfSight && hit.collider == coverCollider)
        {
            // 5. Calcular posición "detrás" del objeto (opuesta al jugador)
            Vector3 dirPlayerToMe = (transform.position - _player.position).normalized;
            Vector3 hidePos = coverCenter + dirPlayerToMe * (coverCollider.bounds.extents.magnitude + 1f);
            
            // Proyectar al NavMesh
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(hidePos, out navHit, 2f, NavMesh.AllAreas))
            {
                // 6. Scoring: Priorizar cobertura cercana y que bloquea bien
                float distToMe = Vector3.Distance(transform.position, navHit.position);
                float distToPlayer = Vector3.Distance(_player.position, navHit.position);
                
                // Queremos: Cerca de mí, lejos del jugador
                float score = (distToPlayer / (distToMe + 1f)) * 100f;
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCoverPos = navHit.position;
                    _currentCoverObject = coverCollider.transform;
                }
            }
        }
    }
    
    return bestCoverPos;
}

/// <summary>
/// Moverse a cobertura y esperar
/// </summary>
IEnumerator MoveToAndHideBehindCover(Vector3 coverPos)
{
    Debug.Log("[TacticalAI] Moviéndose a cobertura");
    
    // 1. Ir a la cobertura
    NavMeshAgentUtility.SetDestination(_agent, coverPos, 0.5f);
    
    // Girar hacia la cobertura
    Vector3 dirToCover = (coverPos - transform.position).normalized;
    dirToCover.y = 0;
    if (dirToCover.sqrMagnitude > 0.01f)
    {
        Quaternion targetRot = Quaternion.LookRotation(dirToCover);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
    }
    
    StartMoving(1.2f); // Correr
    
    // 2. Esperar a llegar
    while (!_agent.hasPath || _agent.remainingDistance > 0.5f)
    {
        yield return null;
    }
    
    StopAndIdle();
    _isBehindCover = true;
    
    // 3. Mirar hacia el jugador (asomaándose)
    FacePlayer();
    
    // 4. Esperar en cobertura hasta que cooldowns estén listos
    Debug.Log("[TacticalAI] En cobertura, esperando cooldowns...");
    
    float waitTime = 0f;
    float maxWaitTime = _settings.coverStayDuration;
    
    while (waitTime < maxWaitTime && !HasAnyAttackReady())
    {
        // Verificar si player aún no me ve (opcional)
        bool playerCanSeeMe = HasLineOfSight(_player.position, transform.position);
        
        if (playerCanSeeMe)
        {
            Debug.Log("[TacticalAI] Player me ve, ajustando posición...");
            // Reposicionarse un poco
            Vector3 adjustedPos = coverPos + transform.right * UnityEngine.Random.Range(-1f, 1f);
            NavMeshAgentUtility.SetDestination(_agent, adjustedPos, 0.5f);
            yield return new WaitForSeconds(0.5f);
        }
        
        waitTime += Time.deltaTime;
        yield return null;
    }
    
    _isBehindCover = false;
    Debug.Log("[TacticalAI] Saliendo de cobertura");
}
```

---

## 🎲 DECISIONES TÁCTICAS

### Flanquear (Reposicionarse Ofensivamente)

```csharp
Vector3 ComputeFlankPosition()
{
    // Moverse a un lado del jugador (perpendicular)
    Vector3 toPlayer = (_player.position - transform.position).normalized;
    Vector3 perpendicular = Vector3.Cross(toPlayer, Vector3.up).normalized;
    
    // Elegir lado aleatoriamente
    float side = UnityEngine.Random.value > 0.5f ? 1f : -1f;
    
    Vector3 flankPos = transform.position + perpendicular * side * 5f;
    
    // Proyectar al NavMesh
    NavMeshHit hit;
    if (NavMesh.SamplePosition(flankPos, out hit, 3f, NavMesh.AllAreas))
    {
        return hit.position;
    }
    
    return transform.position;
}
```

### Esquiva Simple (Dificultad Baja)

```csharp
IEnumerator SimpleEvasion()
{
    Debug.Log("[TacticalAI] Esquiva simple");
    
    // 1. Moverse a punto aleatorio cercano
    Vector3 randomDir = UnityEngine.Random.insideUnitCircle;
    Vector3 dodgePos = transform.position + new Vector3(randomDir.x, 0, randomDir.y) * 3f;
    
    NavMeshHit hit;
    if (NavMesh.SamplePosition(dodgePos, out hit, 2f, NavMesh.AllAreas))
    {
        NavMeshAgentUtility.SetDestination(_agent, hit.position, 0.5f);
        StartMoving(1.0f);
        
        // 2. Esperar a llegar
        yield return new WaitForSeconds(1f);
    }
    
    // 3. Detenerse y mirar al jugador
    StopAndIdle();
    FacePlayer();
    
    // 4. Esperar (simulando "pensar qué hacer")
    yield return new WaitForSeconds(UnityEngine.Random.Range(1f, 2f));
}
```

---

## 🔄 INTEGRACIÓN CON CÓDIGO EXISTENTE

### Variables a Agregar

```csharp
// FSM Táctico
TacticalState _tacticalState = TacticalState.EVALUATE;
Coroutine _tacticalRoutine;

// Recursos
float _currentMana = 100f; // O usar cooldowns existentes
float _maxMana = 100f;

// Dificultad
float _difficultyLevel = 0.5f; // 0.0 (fácil) a 1.0 (experto)
```

### Método Principal (CombatLoop Mejorado)

```csharp
IEnumerator TacticalCombatLoop()
{
    _tacticalState = TacticalState.EVALUATE;
    
    while (true)
    {
        // Update cooldowns
        UpdateCooldowns();
        
        // Ejecutar estado actual
        switch (_tacticalState)
        {
            case TacticalState.EVALUATE:
                State_Evaluate();
                break;
                
            case TacticalState.REPOSITION:
                yield return State_Reposition();
                break;
                
            case TacticalState.ATTACK:
                yield return State_Attack();
                break;
                
            case TacticalState.DEFENSE:
                yield return State_Defense();
                break;
        }
        
        yield return null;
    }
}
```

---

## 🎯 VENTAJAS DEL NUEVO SISTEMA

### 1. **Claridad**
- Estados bien definidos con propósitos claros
- Fácil de debuggear y mantener

### 2. **Escalabilidad**
- Fácil agregar nuevos estados (ej: PATROL, SEARCH)
- Fácil agregar nuevas decisiones tácticas

### 3. **Dificultad Adaptativa**
- Comportamiento inteligente en dificultad alta
- Comportamiento torpe en dificultad baja
- Escalado natural

### 4. **Cobertura Realista**
- Usa Raycasts para verificar bloqueo de visión
- Se esconde detrás de objetos físicos
- Comportamiento táctico creíble

### 5. **Decisiones Variadas**
- No siempre hace lo mismo
- Flanquea, busca cobertura, ataca agresivamente
- Comportamiento menos predecible

---

## 📊 DIAGRAMA DE TRANSICIONES

```
    EVALUATE
    /   |   \
   /    |    \
  /     |     \
REPOS  ATT   DEF
  |     / \    |
  |    /   \   |
  |   /     \  |
  |  /       \ |
  | /         \|
EVALUATE ← → EVALUATE
```

**Flujo natural:**
- EVALUATE es el hub central
- Todos los estados vuelven a EVALUATE
- EVALUATE decide el próximo estado

---

## 🧪 TESTING

### Test 1: Reposicionamiento
```
Player se acerca (< minDistance)
  ↓
NPC entra en REPOSITION
  ↓
Se aleja corriendo
  ↓
Llega a distancia segura
  ↓
Mira al player
  ↓
Vuelve a EVALUATE
```

### Test 2: Loop Agresivo
```
NPC tiene maná/cooldowns
  ↓
EVALUATE → ATTACK
  ↓
Dispara hechizo
  ↓
Decisión táctica (60% seguir)
  ↓
Dispara otro hechizo
  ↓
Se queda sin maná
  ↓
ATTACK → DEFENSE
```

### Test 3: Cobertura Inteligente
```
NPC sin maná
  ↓
EVALUATE → DEFENSE
  ↓
Dificultad ALTA
  ↓
FindCover_WithRaycast()
  ↓
Encuentra árbol/caja
  ↓
Verifica Raycast (bloquea visión)
  ↓
Se mueve detrás
  ↓
Espera hasta recuperar cooldowns
  ↓
DEFENSE → EVALUATE → ATTACK
```

---

## ✅ CONCLUSIÓN

Este sistema FSM:
- ✅ **Más claro** que el código actual
- ✅ **Más mantenible** (switch case en lugar de if-else anidados)
- ✅ **Más inteligente** (decisiones tácticas variadas)
- ✅ **Escalable** (fácil agregar comportamientos)
- ✅ **Integrable** con el código existente

**Recomendación:** Refactorizar `CombatLoop()` existente para usar este sistema FSM, manteniendo las funciones auxiliares que ya funcionan bien (`ComputeRetreatPosition`, `FacePlayer`, etc.).

