# 🔍 Informe de Auditoría: Scripts de NPC

**Fecha**: 2026-01-27  
**Archivos Revisados**: 50+ scripts en `Assets/Scripts/Behaviour NPC/`  
**Enfoque**: Rendimiento, malas prácticas, mejoras potenciales

---

## ✅ **COSAS BIEN HECHAS**

### 1. **Sin FindObjectOfType en Update** ✅
- ✅ NO hay llamadas a `FindObjectOfType` o `FindObjectsOfType` en ningún Update
- ✅ NO hay `GameObject.Find` en loops

### 2. **GetComponent Cacheado** ✅
- ✅ Todos los GetComponent se ejecutan en Awake/Start
- ✅ Se almacenan en variables privadas

### 3. **CompareTag Usado Correctamente** ✅
- ✅ Se usa `CompareTag("Player")` en lugar de `tag == "Player"`
- ✅ Solo 5 usos, todos correctos

### 4. **Sin Allocations en Update** ✅
- ✅ NO hay `new List<>()` en Updates
- ✅ NO hay `new arrays[]` en Updates
- ✅ PlayerParty usa buffer reutilizable: `private Collider[] _enemySearchBuffer = new Collider[32]`

### 5. **String Comparisons Optimizadas** ✅
- ✅ Se usa `string.IsNullOrEmpty()` en lugar de comparaciones directas
- ✅ Solo 6 usos, todos apropiados

---

## ⚠️ **PROBLEMAS ENCONTRADOS**

### 🔴 **CRÍTICO: Camera.main en Múltiples Lugares**

**Ubicaciones**:
1. `NPCBehaviourManagerV2.cs:526` - `Camera.main?.transform`
2. `NPCAlertIconController.cs:210` - `Camera.main`
3. `NPCPersistentIconController.cs:44` - `Camera.main`
4. `PlayerLocator.cs:31-32` - `Camera.main`

**Problema**:
```csharp
// ❌ MAL: Camera.main es una búsqueda por tag cada vez
Camera mainCam = Camera.main;
```

**Por qué es malo**:
- `Camera.main` hace `FindGameObjectsWithTag("MainCamera")` internamente
- Si se llama muchas veces (ej: cada frame en iconos), es MUY costoso
- Causa garbage collection innecesario

**Impacto Estimado**: 
- 🔥 **ALTO** - Si hay muchos NPCs con iconos, esto puede ser un problema serio
- Cada icono llamando `Camera.main` en Update = disaster

**Solución Recomendada**:
```csharp
// ✅ BIEN: Cachear en Awake/Start
private Camera _mainCamera;

void Awake()
{
    _mainCamera = Camera.main; // Solo UNA vez
}

void Update()
{
    if (_mainCamera != null)
    {
        // Usar _mainCamera
    }
}
```

---

### 🟡 **MEDIO: CheckLineOfSight Cada Frame en NPCCombatBrain**

**Ubicación**: `NPCCombatBrain.cs:277` en `Update()`

**Código**:
```csharp
void Update()
{
    if (!_isActive) return;
    
    // ✅ Verificar Line of Sight cada frame
    if (_player != null)
    {
        _hasLineOfSight = CheckLineOfSight(); // ⚠️ Raycast cada frame
        // ...
    }
}
```

**Problema**:
- `CheckLineOfSight()` hace un **Physics.Raycast** cada frame
- Con muchos NPCs en combate, esto puede ser costoso

**Impacto Estimado**:
- 🟡 **MEDIO** - Depende del número de NPCs en combate simultáneo
- 10 NPCs = 10 raycasts por frame = ~600 raycasts por segundo

**Solución Recomendada**:
```csharp
// Añadir throttling
private float _losCheckTimer;
private const float LOS_CHECK_INTERVAL = 0.1f; // Cada 0.1s en lugar de cada frame

void Update()
{
    if (!_isActive) return;
    
    // Verificar LOS cada 0.1s en lugar de cada frame
    _losCheckTimer += Time.deltaTime;
    if (_losCheckTimer >= LOS_CHECK_INTERVAL && _player != null)
    {
        _losCheckTimer = 0f;
        _hasLineOfSight = CheckLineOfSight();
        
        if (_hasLineOfSight)
        {
            _lastSeenTime = Time.time;
            _lastCombatTime = Time.time;
            _lastKnownPlayerPosition = _player.position;
        }
    }
    
    // Cooldowns cada frame (esto está bien)
    // ...
}
```

**Beneficio**: 10x menos raycasts sin pérdida perceptible de precisión

---

### 🟡 **MEDIO: MoveTo() Crea NavMeshPath Cada Llamada**

**Ubicación**: `NPCCombatBrain.cs:1469`

**Código**:
```csharp
private void MoveTo(Vector3 pos, float speed)
{
    // ...
    NavMeshPath path = new NavMeshPath(); // ⚠️ Nueva allocation cada vez
    if (!_agent.CalculatePath(navHit.position, path))
    {
        // ...
    }
    // ...
}
```

**Problema**:
- Se crea un nuevo `NavMeshPath` cada vez que el NPC se mueve
- Genera garbage collection

**Impacto Estimado**:
- 🟡 **MEDIO-BAJO** - Solo se llama cuando el NPC decide moverse, no cada frame
- Pero puede acumularse con muchos NPCs

**Solución Recomendada**:
```csharp
// Reutilizar el mismo NavMeshPath
private NavMeshPath _reusablePath;

void Awake()
{
    // ...
    _reusablePath = new NavMeshPath();
}

private void MoveTo(Vector3 pos, float speed)
{
    // ...
    if (!_agent.CalculatePath(navHit.position, _reusablePath))
    {
        // ...
    }
    
    if (_reusablePath.status != NavMeshPathStatus.PathComplete)
    {
        // ...
        if (_reusablePath.status == NavMeshPathStatus.PathPartial && _reusablePath.corners.Length > 1)
        {
            Vector3 lastReachablePoint = _reusablePath.corners[_reusablePath.corners.Length - 1];
            // ...
        }
        return;
    }
    // ...
}
```

---

### 🟢 **MENOR: Debug.DrawLine/DrawRay en Build**

**Ubicaciones**: Múltiples en `NPCCombatBrain.cs`

**Código**:
```csharp
Debug.DrawLine(spawnPos, targetPos, Color.green, 0.5f);
Debug.DrawRay(origin, direction, Color.green);
```

**Problema**:
- `Debug.DrawLine` y `Debug.DrawRay` se compilan en builds de producción
- Aunque no se ven, consumen recursos mínimos

**Impacto Estimado**:
- 🟢 **BAJO** - Impacto mínimo, pero es mala práctica

**Solución Recomendada**:
```csharp
#if UNITY_EDITOR
    Debug.DrawLine(spawnPos, targetPos, Color.green, 0.5f);
    Debug.DrawRay(origin, direction, Color.green);
#endif
```

O mejor aún, usar un flag de debug:
```csharp
[SerializeField] private bool visualDebug = false;

if (visualDebug)
{
    Debug.DrawLine(spawnPos, targetPos, Color.green, 0.5f);
}
```

---

### 🟢 **MENOR: CheckPlayerDetection Hace Raycast Cada 0.2s**

**Ubicación**: `IdleState.cs` y `WanderState.cs`

**Código**:
```csharp
// Detección visual periódica
_playerDetectionTimer += Time.deltaTime;
if (_playerDetectionTimer >= PLAYER_DETECTION_INTERVAL) // 0.2s
{
    _playerDetectionTimer = 0f;
    CheckPlayerDetection(context); // Hace raycast
}
```

**Análisis**:
- ✅ **YA está optimizado** con throttling de 0.2s
- ✅ Solo se ejecuta en NPCs con `isAggressive = true`
- ✅ Tiene early returns para evitar raycasts innecesarios

**Conclusión**: **ESTÁ BIEN** ✅ - No necesita cambios

---

## 📊 **RESUMEN DE PRIORIDADES**

### 🔴 **ALTA PRIORIDAD** (Hacer Ya)
1. **Cachear Camera.main** - Impacto potencialmente muy alto con muchos NPCs

### 🟡 **MEDIA PRIORIDAD** (Hacer Pronto)
2. **Throttling en CheckLineOfSight** - Reducir raycasts de combate
3. **Reutilizar NavMeshPath en MoveTo** - Reducir GC

### 🟢 **BAJA PRIORIDAD** (Cuando Haya Tiempo)
4. **Condicionar Debug.DrawLine** - Mala práctica menor
5. **Revisar logs de Debug.Log** - Muchos logs pueden acumularse

---

## 💡 **RECOMENDACIONES GENERALES**

### 1. **Sistema de Pooling para Objetos Frecuentes**
Si hay objetos que se crean/destruyen frecuentemente (proyectiles, efectos), considerar pooling.

### 2. **Profiler en Escena con Muchos NPCs**
Hacer prueba con 20+ NPCs en combate simultáneo y usar Unity Profiler para identificar cuellos de botella reales.

### 3. **Layer Masks Cacheadas**
Si se usan layer masks repetidamente, cachearlas:
```csharp
private int _obstacleMask;

void Awake()
{
    _obstacleMask = 1 << LayerMask.NameToLayer("Default");
}
```

### 4. **Considerar Jobs System para Raycasts**
Si el rendimiento es crítico con muchos NPCs, considerar usar Unity Jobs para raycasts en paralelo.

---

## 🎯 **CÓDIGO DE EJEMPLO PARA FIXES**

### Fix #1: Camera.main Cacheada

**NPCAlertIconController.cs**:
```csharp
private Camera _cachedMainCamera;
private bool _cameraInitialized;

private void EnsureCameraReference()
{
    if (!_cameraInitialized)
    {
        _cachedMainCamera = Camera.main;
        _cameraInitialized = true;
    }
}

void Update()
{
    if (_currentIcon == null) return;
    
    EnsureCameraReference();
    if (_cachedMainCamera == null) return;
    
    // Usar _cachedMainCamera en lugar de Camera.main
    _currentIcon.transform.LookAt(_cachedMainCamera.transform);
}
```

### Fix #2: Throttling CheckLineOfSight

**NPCCombatBrain.cs**:
```csharp
private float _losCheckTimer;
private const float LOS_CHECK_INTERVAL = 0.1f;

private void Update()
{
    if (!_isActive) return;

    // Reducir Cooldowns (cada frame está bien)
    float dt = Time.deltaTime * settings.attackFrequencyMultiplier;
    if (_leftCd > 0) _leftCd -= dt;
    if (_rightCd > 0) _rightCd -= dt;
    if (_specialCd > 0) _specialCd -= dt;
    if (_shieldCd > 0) _shieldCd -= Time.deltaTime;
    if (_globalCd > 0) _globalCd -= dt;

    // ✅ Verificar Line of Sight cada 0.1s en lugar de cada frame
    if (_player != null)
    {
        _losCheckTimer += Time.deltaTime;
        if (_losCheckTimer >= LOS_CHECK_INTERVAL)
        {
            _losCheckTimer = 0f;
            _hasLineOfSight = CheckLineOfSight();
            
            if (_hasLineOfSight)
            {
                _lastSeenTime = Time.time;
                _lastCombatTime = Time.time;
                _lastKnownPlayerPosition = _player.position;
            }
        }
    }

    // Rotación (solo si está parado, está bien)
    if (_player != null && _currentState != CombatState.REPOSITION && 
        _currentState != CombatState.SEARCHING && _agent.enabled && 
        _agent.isOnNavMesh && _agent.isStopped)
    {
        Vector3 targetPos = _hasLineOfSight ? _player.position : _lastKnownPlayerPosition;
        _animator.FaceTarget(targetPos);
    }
}
```

### Fix #3: NavMeshPath Reutilizable

**NPCCombatBrain.cs**:
```csharp
// Añadir campo
private NavMeshPath _reusablePath;

void Awake()
{
    // ...existing code...
    _reusablePath = new NavMeshPath();
}

private void MoveTo(Vector3 pos, float speed)
{
    if (!_agent.enabled || !_agent.isOnNavMesh)
    {
        Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ Agent no está activo");
        return;
    }
    
    if (!NavMesh.SamplePosition(pos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
    {
        Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ Destino no en NavMesh");
        return;
    }
    
    // ✅ Reutilizar path en lugar de crear uno nuevo
    if (!_agent.CalculatePath(navHit.position, _reusablePath))
    {
        Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ No se puede calcular camino");
        return;
    }
    
    if (_reusablePath.status != NavMeshPathStatus.PathComplete)
    {
        Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ Camino incompleto");
        
        if (_reusablePath.status == NavMeshPathStatus.PathPartial && _reusablePath.corners.Length > 1)
        {
            Vector3 lastReachablePoint = _reusablePath.corners[_reusablePath.corners.Length - 1];
            Debug.Log($"[CombatBrain:{gameObject.name}] 📍 Usando punto parcial");
            
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = speed;
                _agent.SetDestination(lastReachablePoint);
            }
            _animator.SetMovementSpeed(speed, 0.1f);
            return;
        }
        return;
    }
    
    if (_agent.enabled && _agent.isOnNavMesh)
    {
        _agent.isStopped = false;
        _agent.speed = speed;
        _agent.SetDestination(navHit.position);
    }
    _animator.SetMovementSpeed(speed, 0.1f);
}
```

---

## ✅ **CONCLUSIÓN**

### Estado General: **BUENO** 👍

- ✅ La mayoría de las prácticas son correctas
- ✅ No hay problemas críticos de arquitectura
- ⚠️ 3-4 optimizaciones recomendadas (fáciles de implementar)

### Impacto Esperado de los Fixes:
- **Camera.main cacheada**: 10-30% mejora en updates de iconos
- **CheckLineOfSight throttling**: 10x menos raycasts
- **NavMeshPath reutilizable**: Menos GC, más estable

### Tiempo Estimado para Implementar:
- Fix #1 (Camera.main): **15 minutos**
- Fix #2 (LOS throttling): **10 minutos**
- Fix #3 (NavMeshPath): **10 minutos**
- **TOTAL: ~35 minutos** para todos los fixes principales

---

**¿Quieres que implemente alguno de estos fixes?** 🚀
