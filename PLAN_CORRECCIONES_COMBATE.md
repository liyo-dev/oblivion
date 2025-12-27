# 🎯 PLAN DE CORRECCIONES FINALES - SISTEMA DE COMBATE

**Fecha:** 2025-12-26

---

## 📋 **PROBLEMAS PENDIENTES**

### **🚨 CRÍTICO:**
1. ❌ NPC se queda en bucle andando en el sitio tras recibir daño
2. ❌ NPC se sigue poniendo de espaldas al jugador
3. ❌ NPC se sale del mundo (NavMesh)
4. ❌ Idle de batalla no funciona correctamente en Player

### **⚠️ IMPORTANTE:**
5. ❌ NPC se mueve demasiado (necesita comportamiento más "duelo de magos")
6. ❌ NPC no se protege con escudo
7. ❌ Erika no dice frase antes del combate

### **📊 CONFIGURACIÓN:**
8. ❌ Nombres confusos en el Inspector (Combat Range, Melee Range, Detection Range)
9. ❌ Falta tooltips explicativos en los campos

---

## 🔧 **SOLUCIONES A IMPLEMENTAR**

### **1. BUCLE DE ANIMACIÓN TRAS RECIBIR DAÑO**

**Problema:** Cuando el NPC recibe daño, reproduce `TakeDamage` pero después se queda en bucle andando en el sitio.

**Causa probable:** 
- El animator no vuelve al estado correcto tras `TakeDamage`
- Posible conflicto entre el NavMeshAgent y el animator

**Solución:**
1. Revisar `NPCSimpleAnimator.OnTakeDamage()`
2. Asegurar transición correcta tras la animación de daño
3. Verificar que `StartMoving()` y `StopMoving()` se llaman correctamente
4. Añadir logs temporales para debugging

---

### **2. NPC SE PONE DE ESPALDAS (AÚN PERSISTE)**

**Problema:** A pesar de las correcciones previas, el NPC sigue poniéndose de perfil/espaldas.

**Análisis adicional necesario:**
1. Verificar que `FacePlayer()` NO se llama solo en el loop, sino también en:
   - `ExecuteAttack()`
   - `TryShield()`
   - Cualquier otra acción de combate
2. Revisar si el NavMeshAgent está sobrescribiendo la rotación

**Solución:**
```csharp
// Antes de CUALQUIER acción, garantizar rotación
void BeforeAnyAction()
{
    FacePlayer();
    
    // Detener el NavMeshAgent para evitar que sobrescriba la rotación
    if (_agent != null && _agent.isActiveAndEnabled)
    {
        _agent.isStopped = true;
        _agent.updateRotation = false; // ✅ CLAVE: No dejar que el agent rote
    }
}
```

---

### **3. NPC SE SALE DEL NAVMESH (MEJORAR DETECCIÓN)**

**Problema:** A pesar de las correcciones, el NPC sigue saliendo del mundo.

**Solución mejorada:**
1. Verificar en **CADA** movimiento que el destino es válido:
```csharp
bool SetDestination(Vector3 destination)
{
    if (_agent == null || !_agent.isOnNavMesh)
    {
        EnsureAgentOnNavMesh();
        return false;
    }
    
    // ✅ VERIFICAR QUE EL DESTINO ESTÁ EN EL NAVMESH
    if (UnityEngine.AI.NavMesh.SamplePosition(destination, out var hit, 2f, _agent.areaMask))
    {
        return _agent.SetDestination(hit.position);
    }
    
    Debug.LogWarning($"[NPCCombatBrain] Destino inválido: {destination}");
    return false;
}
```

2. Añadir verificación en `Update()`:
```csharp
void Update()
{
    if (_agent != null && _agent.enabled && !_agent.isOnNavMesh)
    {
        Debug.LogError($"[NPCCombatBrain] ¡{gameObject.name} SE SALIÓ DEL NAVMESH!");
        EnsureAgentOnNavMesh();
    }
}
```

---

### **4. COMPORTAMIENTO MÁS "DUELO DE MAGOS"**

**Problema:** El NPC se mueve demasiado, no se siente como un duelo de Harry Potter.

**Nueva lógica propuesta:**

```csharp
// Estados del duelo
enum DuelState
{
    Observing,      // Mirando al jugador, esperando oportunidad
    Attacking,      // Lanzando magia
    Defending,      // Usando escudo
    Retreating,     // Retrocediendo (jugador muy cerca)
    Strafing        // Moviéndose lateral para esquivar
}

private DuelState _duelState = DuelState.Observing;
private float _stateTimer = 0f;

// Parámetros de comportamiento
[Header("Duelo")]
[Tooltip("Distancia mínima cómoda con el jugador")]
[SerializeField] private float minComfortDistance = 3f;
[Tooltip("Distancia máxima efectiva de combate")]
[SerializeField] private float maxComfortDistance = 8f;
[Tooltip("Tiempo mínimo observando antes de atacar")]
[SerializeField] private float minObserveTime = 1f;
[Tooltip("Tiempo máximo observando antes de atacar")]
[SerializeField] private float maxObserveTime = 3f;
[Tooltip("Probabilidad de esquivar lateral (0-1)")]
[SerializeField, Range(0f, 1f)] private float strafeProbability = 0.3f;

private IEnumerator DuelLoop()
{
    while (!_defeated && _player != null)
    {
        float distToPlayer = Vector3.Distance(transform.position, _player.position);
        
        // SIEMPRE mirar al jugador
        FacePlayer();
        
        _stateTimer += Time.deltaTime;
        
        switch (_duelState)
        {
            case DuelState.Observing:
                HandleObserving(distToPlayer);
                break;
                
            case DuelState.Attacking:
                HandleAttacking(distToPlayer);
                break;
                
            case DuelState.Defending:
                HandleDefending(distToPlayer);
                break;
                
            case DuelState.Retreating:
                HandleRetreating(distToPlayer);
                break;
                
            case DuelState.Strafing:
                HandleStrafing(distToPlayer);
                break;
        }
        
        yield return null;
    }
}

private void HandleObserving(float distToPlayer)
{
    StopMoving();
    
    // Jugador demasiado cerca → retroceder
    if (distToPlayer < minComfortDistance)
    {
        ChangeDuelState(DuelState.Retreating);
        return;
    }
    
    // Tiempo de observación cumplido → decidir acción
    if (_stateTimer >= Random.Range(minObserveTime, maxObserveTime))
    {
        // ¿Atacar o esquivar?
        if (Random.value > strafeProbability && CanAttack())
        {
            ChangeDuelState(DuelState.Attacking);
        }
        else if (distToPlayer > minComfortDistance * 1.5f)
        {
            ChangeDuelState(DuelState.Strafing);
        }
    }
}

private void HandleAttacking(float distToPlayer)
{
    StopMoving();
    FacePlayer();
    
    // Ejecutar ataque
    StartCoroutine(ExecuteAttackSequence());
    
    // Tras atacar, volver a observar
    ChangeDuelState(DuelState.Observing);
}

private void HandleRetreating(float distToPlayer)
{
    // Retroceder hasta distancia cómoda
    if (distToPlayer >= minComfortDistance * 1.2f)
    {
        ChangeDuelState(DuelState.Observing);
        return;
    }
    
    Vector3 retreatDir = (transform.position - _player.position).normalized;
    Vector3 retreatPos = transform.position + retreatDir * 2f;
    
    if (SetDestination(retreatPos))
    {
        StartMoving(1f);
    }
}

private void HandleStrafing(float distToPlayer)
{
    // Movimiento lateral para esquivar
    Vector3 dirToPlayer = (_player.position - transform.position).normalized;
    Vector3 rightDir = Vector3.Cross(Vector3.up, dirToPlayer);
    
    // Alternar entre derecha e izquierda
    float strafeDir = Random.value > 0.5f ? 1f : -1f;
    Vector3 strafePos = transform.position + rightDir * strafeDir * 2f;
    
    if (SetDestination(strafePos))
    {
        StartMoving(0.7f);
    }
    
    // Tras esquivar, volver a observar
    if (_stateTimer > 1.5f)
    {
        ChangeDuelState(DuelState.Observing);
    }
}

private void ChangeDuelState(DuelState newState)
{
    _duelState = newState;
    _stateTimer = 0f;
    Debug.Log($"[NPCCombatBrain] {gameObject.name} → {newState}");
}
```

---

### **5. SISTEMA DE ESCUDO**

**Problema:** NPC no se protege aunque `useShield=true`.

**Verificación necesaria:**
1. ¿Existe `NPCShieldController` en el GameObject?
2. ¿Está configurado correctamente?
3. ¿Se llama a `TryShield()` en el momento correcto?

**Solución:**
```csharp
private bool TryShield()
{
    if (!_settings.useShield || _shieldController == null)
        return false;
    
    // Solo usar escudo si:
    // 1. El jugador está atacando
    // 2. O el NPC tiene poca vida
    
    bool playerIsAttacking = IsPlayerAttacking();
    bool lowHealth = _ctx.Damageable != null && 
                     _ctx.Damageable.CurrentHealth < _ctx.Damageable.MaxHealth * 0.3f;
    
    if (!playerIsAttacking && !lowHealth)
        return false;
    
    Debug.Log($"[NPCCombatBrain] {gameObject.name} ¡ACTIVANDO ESCUDO!");
    _shieldController.ActivateShield(3f); // Escudo por 3 segundos
    return true;
}

private bool IsPlayerAttacking()
{
    // Detectar si el player está en animación de ataque
    // Esto requiere acceso al animator del player
    
    if (_player == null) return false;
    
    var playerAnimator = _player.GetComponent<Animator>();
    if (playerAnimator == null) return false;
    
    // Verificar si está en estado de ataque
    var stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(1); // UpperBody layer
    return stateInfo.IsTag("Attack") || stateInfo.IsName("Magic");
}
```

---

### **6. NOMBRES Y TOOLTIPS EN INSPECTOR**

**Cambios necesarios:**

```csharp
[Header("Rangos de Combate")]
[Tooltip("Distancia máxima para detectar al jugador")]
[SerializeField] private float detectionRange = 10f;

[Tooltip("Distancia MÁXIMA de combate (alejarse si está más lejos)")]
[SerializeField] private float maxCombatRange = 8f;

[Tooltip("Distancia MÍNIMA de combate (alejarse si está más cerca)")]
[SerializeField] private float minCombatRange = 3f;

[Tooltip("Rango de ataques cuerpo a cuerpo (si aplica)")]
[SerializeField] private float meleeRange = 2f;
```

**Migración de valores:**
- `Detection Range` (3) → `detectionRange` (10-15)
- `Combat Range` (2) → `maxCombatRange` (6-8)
- `Melee Range` (2) → `minCombatRange` (3-4)

---

## 📁 **ARCHIVOS A MODIFICAR**

```
✅ Assets/Scripts/Behaviour NPC/NPCCombatBrain.cs
   - Añadir sistema de estados de duelo
   - Mejorar FacePlayer() para evitar rotación del agent
   - Mejorar SetDestination() con validación
   - Añadir Update() con verificación de NavMesh
   - Implementar TryShield() mejorado
   - Renombrar y documentar campos del Inspector

✅ Assets/Scripts/Behaviour NPC/NPCSimpleAnimator.cs
   - Revisar OnTakeDamage() para evitar bucle
   - Asegurar transiciones correctas tras daño

⏳ Assets/Scripts/Behaviour NPC/NPCShieldController.cs
   - Verificar que existe y funciona correctamente

⏳ Assets/Scripts/Behaviour NPC/NPCInteractiveNarrativeExecutor.cs
   - Verificar configuración de diálogo pre-combate
```

---

## 🎯 **PRIORIDAD DE IMPLEMENTACIÓN**

```
1. 🚨 CRÍTICO - Bucle de animación tras daño
2. 🚨 CRÍTICO - NPC se pone de espaldas
3. 🚨 CRÍTICO - Salida del NavMesh
4. ⚠️ IMPORTANTE - Comportamiento "duelo de magos"
5. ⚠️ IMPORTANTE - Sistema de escudo
6. 📊 CONFIG - Nombres y tooltips
7. 📊 CONFIG - Diálogo pre-combate
```

---

## 🚀 **SIGUIENTE PASO**

Implementar las correcciones en el orden de prioridad establecido.


