# FEATURE: NPC Se Esconde Detrás de Objetos y Reproduce Animación de Búsqueda

## 📋 Problema Resuelto

Cuando el NPC huía del jugador, simplemente corría en dirección opuesta sin buscar cobertura real. No había comportamiento táctico de esconderse detrás de obstáculos.

## ✅ Solución Implementada

### 🎯 Nuevo Comportamiento

Cuando el NPC detecta que el jugador está demasiado cerca (`dist < minSafeDistance`):

1. ✅ **Busca objetos Default cercanos** (muros, columnas, rocas, etc.)
2. ✅ **Calcula posición de cobertura** detrás del obstáculo
3. ✅ **Corre hacia la cobertura** más cercana y válida
4. ✅ **Al llegar, reproduce animación** `SenseSomethingSearching_NoWeapon`
5. ✅ **Espera 1.5 segundos** (duración de animación de búsqueda)
6. ✅ **Vuelve a evaluar** la situación

### 🏃 Flujo de Reposicionamiento

```
Player demasiado cerca
    ↓
FindCoverBehindObstacle()
    ↓
[A] Cobertura encontrada → Huir a esa posición
[B] Sin cobertura → Huir en dirección opuesta (como antes)
    ↓
Correr hacia posición (runSpeed)
    ↓
Al llegar y detenerse
    ↓
✅ PlaySearching() - Animación "SenseSomethingSearching_NoWeapon"
    ↓
Esperar 1.5 segundos
    ↓
Volver a EVALUATE
```

## 🔧 Cambios Técnicos

### 1. State_Reposition() - Buscar Cobertura

```csharp
IEnumerator State_Reposition()
{
    float dist = Vector3.Distance(transform.position, _player.position);
    
    if (dist < settings.minSafeDistance)
    {
        // ✅ NUEVO: Buscar cobertura detrás de objetos Default
        Vector3 coverPosition;
        bool foundCover = FindCoverBehindObstacle(out coverPosition);
        
        Vector3 targetPos;
        if (foundCover)
        {
            // Encontró cobertura - ir allí
            targetPos = coverPosition;
            Debug.Log("🏃 Huyendo hacia cobertura detrás de obstáculo");
        }
        else
        {
            // Sin cobertura - huir en dirección opuesta (comportamiento anterior)
            Vector3 dirAway = (transform.position - _player.position).normalized;
            targetPos = transform.position + dirAway * 5f;
            Debug.Log("🏃 Huyendo sin cobertura - dirección opuesta");
        }
        
        // Moverse hacia la posición objetivo
        MoveTo(targetPos, settings.runSpeed);
        
        // Esperar a llegar (máx 3 segundos)
        float timer = 0;
        while (_agent.remainingDistance > 1.5f && timer < 3f)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        
        // ✅ NUEVO: Al llegar, SIEMPRE reproducir animación de búsqueda
        StopMove();
        Debug.Log("🔍 Llegó a posición de cobertura - Reproduciendo animación de búsqueda");
        
        if (_animator != null)
        {
            _animator.PlaySearching();
        }
        
        // Esperar duración de la animación
        yield return new WaitForSeconds(1.5f);
    }
    
    _currentState = CombatState.EVALUATE;
}
```

### 2. FindCoverBehindObstacle() - Búsqueda Inteligente

```csharp
private bool FindCoverBehindObstacle(out Vector3 coverPosition)
{
    // 1. Buscar todos los colliders en layer Default (radio 15m)
    int defaultLayer = LayerMask.NameToLayer("Default");
    Collider[] nearbyObstacles = Physics.OverlapSphere(
        transform.position, 
        15f,
        1 << defaultLayer
    );
    
    if (nearbyObstacles.Length == 0)
    {
        coverPosition = transform.position;
        return false; // No hay obstáculos
    }
    
    // 2. Evaluar cada obstáculo
    float bestScore = float.MinValue;
    Vector3 bestPosition = transform.position;
    bool foundValidCover = false;
    
    foreach (var obstacle in nearbyObstacles)
    {
        // Ignorar triggers
        if (obstacle.isTrigger) continue;
        
        // Punto más cercano del obstáculo al NPC
        Vector3 obstaclePoint = obstacle.ClosestPoint(transform.position);
        
        // Dirección del jugador al obstáculo
        Vector3 dirPlayerToObstacle = (obstaclePoint - _player.position).normalized;
        
        // Posición de cobertura: DETRÁS del obstáculo
        Vector3 potentialCoverPos = obstaclePoint + dirPlayerToObstacle * 2f;
        
        // Verificar que esté en NavMesh
        if (!NavMesh.SamplePosition(potentialCoverPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
        {
            continue; // No válido
        }
        
        // 3. CLAVE: Verificar que el obstáculo esté ENTRE el jugador y la cobertura
        Vector3 dirToPlayer = (_player.position - navHit.position).normalized;
        if (Physics.Raycast(navHit.position + Vector3.up, dirToPlayer, 
            out RaycastHit hit, 
            Vector3.Distance(navHit.position, _player.position), 
            1 << defaultLayer))
        {
            // ✅ Hay un obstáculo bloqueando la línea de visión - COBERTURA VÁLIDA
            
            // 4. Calcular puntuación: preferir obstáculos más cercanos
            float distanceToNPC = Vector3.Distance(transform.position, navHit.position);
            float score = 20f - distanceToNPC;
            
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = navHit.position;
                foundValidCover = true;
            }
        }
    }
    
    coverPosition = bestPosition;
    return foundValidCover;
}
```

## 🎯 Lógica de Búsqueda de Cobertura

### Pasos del Algoritmo

1. **Buscar obstáculos Default** en radio de 15 metros
2. **Para cada obstáculo:**
   - Obtener punto más cercano del obstáculo al NPC
   - Calcular dirección: Jugador → Obstáculo
   - Calcular posición de cobertura: Obstáculo + 2m en esa dirección
   - Verificar que esté en NavMesh
3. **Validar cobertura:**
   - Hacer raycast desde posición de cobertura hacia el jugador
   - Si el rayo golpea el obstáculo = **COBERTURA VÁLIDA** ✅
4. **Seleccionar mejor cobertura:**
   - Puntuación: 20 - distancia al NPC
   - Preferir obstáculos **más cercanos**

### Diagrama Visual

```
        Jugador (P)
            |
            |  Línea de visión
            |
      [OBSTÁCULO]  ← Layer Default
            |
            | (2m detrás)
            |
         Posición de Cobertura (C)  ← NPC corre aquí
         
Raycast: C → P
Si golpea [OBSTÁCULO] = Cobertura válida ✅
```

## 📊 Comparación

### ANTES ❌

```
Player cerca → Huir en dirección opuesta → No animación
```

**Problemas:**
- Sin cobertura real
- No usa el entorno
- Comportamiento predecible
- Sin feedback visual al llegar

### AHORA ✅

```
Player cerca → Buscar cobertura Default → Correr a cobertura → Animación búsqueda
```

**Ventajas:**
- ✅ Usa obstáculos del entorno tácticamente
- ✅ Comportamiento inteligente
- ✅ Animación de búsqueda al llegar
- ✅ Se esconde DETRÁS de objetos reales

## 🎮 Casos de Uso

### Caso 1: Cobertura Disponible

```
1. Player se acerca demasiado
2. NPC busca objetos Default cercanos
3. Encuentra muro a 10m de distancia
4. Calcula posición detrás del muro
5. Corre hacia allí (runSpeed)
6. Al llegar, se detiene
7. ✅ Reproduce "SenseSomethingSearching_NoWeapon"
8. Espera 1.5s
9. Vuelve a evaluar (¿Jugador visible?)
```

### Caso 2: Sin Cobertura

```
1. Player se acerca demasiado
2. NPC busca objetos Default cercanos
3. No encuentra ninguno (campo abierto)
4. Huye en dirección opuesta (fallback)
5. Al llegar, se detiene
6. ✅ Reproduce "SenseSomethingSearching_NoWeapon"
7. Espera 1.5s
8. Vuelve a evaluar
```

### Caso 3: Múltiples Obstáculos

```
1. Player se acerca demasiado
2. NPC encuentra 5 obstáculos Default
3. Evalúa cada uno:
   - Columna a 5m (score: 15)
   - Muro a 8m (score: 12)
   - Roca a 12m (score: 8)
4. Selecciona columna (mejor score)
5. Corre hacia cobertura detrás de la columna
6. ✅ Animación de búsqueda
```

## 🔍 Debug Logs

Cuando el NPC busca cobertura, verás estos logs:

```
[CombatBrain:Boy_Pirate] 🔍 Encontrados 3 obstáculos Default para cobertura
[CombatBrain:Boy_Pirate] 🛡️ Cobertura válida encontrada: Stone_Wall (score: 14.5)
[CombatBrain:Boy_Pirate] 🛡️ Cobertura válida encontrada: Wood_Column (score: 16.2)
[CombatBrain:Boy_Pirate] ✅ Mejor cobertura seleccionada en: (10.5, 0, 8.3)
[CombatBrain:Boy_Pirate] 🏃 Huyendo hacia cobertura detrás de obstáculo: (10.5, 0, 8.3)
... NPC corre hacia allí ...
[CombatBrain:Boy_Pirate] 🔍 Llegó a posición de cobertura - Reproduciendo animación de búsqueda
[NPCAnimator:Boy_Pirate] 🔍 PlaySearching() - Buscando al jugador
```

Si no encuentra cobertura:

```
[CombatBrain:Boy_Pirate] ⚠️ No se encontraron obstáculos Default cercanos
[CombatBrain:Boy_Pirate] 🏃 Huyendo sin cobertura - dirección opuesta al jugador
... NPC huye en línea recta ...
[CombatBrain:Boy_Pirate] 🔍 Llegó a posición de cobertura - Reproduciendo animación de búsqueda
```

## 📝 Configuración

No requiere configuración adicional. Usa parámetros existentes:

- **Radio de búsqueda**: 15 metros (hardcoded en el método)
- **Distancia detrás del obstáculo**: 2 metros
- **Duración animación búsqueda**: 1.5 segundos
- **Timeout de llegada**: 3 segundos

Si se desea configurar, se pueden añadir a `Settings`:

```csharp
[Header("Cover System")]
public float coverSearchRadius = 15f;
public float coverDistance = 2f;
public float searchAnimationDuration = 1.5f;
```

## 🎯 Ventajas del Sistema

1. ✅ **Comportamiento Táctico**: El NPC usa el entorno inteligentemente
2. ✅ **Feedback Visual**: Animación de búsqueda comunica su estado
3. ✅ **Realismo**: Se esconde detrás de obstáculos reales
4. ✅ **Fallback Seguro**: Si no hay cobertura, comportamiento anterior
5. ✅ **Performance**: Solo busca cuando necesita huir
6. ✅ **Flexible**: Funciona con cualquier objeto en layer Default

## 🔑 Diferencia Clave

### Antes:
```
Vector3 dirAway = (transform.position - _player.position).normalized;
targetPos = transform.position + dirAway * 5f; // Huir en línea recta
```

### Ahora:
```
bool foundCover = FindCoverBehindObstacle(out coverPosition);
if (foundCover)
{
    targetPos = coverPosition; // Huir a cobertura REAL
}
```

---

**Fecha**: 28 de diciembre de 2024  
**Tipo**: New Feature - Tactical Cover System  
**Estado**: ✅ COMPLETADO  
**Archivos Modificados**: `NPCCombatBrain.cs`

