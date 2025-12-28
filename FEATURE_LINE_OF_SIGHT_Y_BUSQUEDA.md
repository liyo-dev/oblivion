# 🔍 FEATURE: Line of Sight y Sistema de Búsqueda del Jugador

## 📋 Problema Resuelto

El NPC estaba:
- ❌ Disparando **a través de obstáculos** (murallas, paredes)
- ❌ **Sabiendo la posición exacta** del jugador aunque hubiera un muro
- ❌ **No deteniendo sus ataques** cuando el jugador se escondía

## ✅ Solución Implementada

### 🎯 Sistema de Line of Sight (Línea de Visión)

El NPC ahora verifica **cada frame** si hay obstáculos bloqueando su visión al jugador usando un **Raycast**:

```csharp
private bool CheckLineOfSight()
{
    Vector3 origin = transform.position + Vector3.up * 1.5f; // Altura de ojos
    Vector3 targetPos = _player.position + Vector3.up * 1.0f; // Centro del jugador
    Vector3 direction = targetPos - origin;
    
    // Raycast que detecta obstáculos (layer Default, etc.)
    if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, settings.obstacleLayerMask))
    {
        // HAY UN OBSTÁCULO BLOQUEANDO
        return false;
    }
    
    return true; // Visión clara
}
```

### 🔍 Nuevo Estado: SEARCHING

Cuando el NPC pierde de vista al jugador, entra en el estado **SEARCHING** que:

1. ✅ **Reproduce la animación** `SenseSomethingSearching_NoWeapon`
2. ✅ **Hace pequeños movimientos** aleatorios para buscar al jugador
3. ✅ **Busca durante un tiempo configurable** (`searchDuration`)
4. ✅ **Vuelve al origen** si no encuentra al jugador (configurable)
5. ✅ **Sale del modo batalla** si agota el tiempo de búsqueda

### ⚙️ Configuración en Inspector

Se añadieron nuevos parámetros configurables en `NPCCombatBrain.Settings`:

```csharp
[Header("Line of Sight & Searching")]
public LayerMask obstacleLayerMask;      // Capas que bloquean visión (Default, etc.)
public float searchDuration;             // Tiempo de búsqueda (ej: 15 segundos)
public float searchMovementRadius;       // Radio de movimiento (ej: 5 metros)
public bool returnToOriginAfterSearch;   // Si vuelve al origen (true/false)
```

### 🎮 Comportamiento Nuevo

#### Caso 1: Jugador Visible (Sin Obstáculos)
```
NPC detecta visión → Comportamiento normal de combate
  ↓
EVALUATE → ATTACK → Dispara proyectiles
```

#### Caso 2: Jugador Se Esconde Tras Muro
```
NPC pierde visión → CheckLineOfSight() retorna false
  ↓
EVALUATE detecta !_hasLineOfSight
  ↓
Cambia a estado SEARCHING
  ↓
Reproduce animación SenseSomethingSearching_NoWeapon
  ↓
Hace pequeños movimientos aleatorios buscando
  ↓
[Opción A] Encuentra al jugador → Vuelve a EVALUATE
[Opción B] Agota tiempo → Vuelve al origen → Sale de batalla
```

#### Caso 3: Jugador Se Mueve Durante Búsqueda
```
NPC en SEARCHING → Movimiento de búsqueda
  ↓
Jugador sale de detrás del muro
  ↓
CheckLineOfSight() retorna true
  ↓
INMEDIATAMENTE cambia a EVALUATE → Retoma combate
```

## 🔧 Cambios Técnicos Implementados

### 1. Nuevas Variables de Estado

```csharp
// Line of Sight & Searching
bool _hasLineOfSight;              // Si hay visión actual
float _lastSeenTime;               // Timestamp última vez visto
Vector3 _lastKnownPlayerPosition;  // Última posición conocida
Vector3 _combatStartPosition;      // Posición original (para volver)
```

### 2. Modificación en Update()

```csharp
private void Update()
{
    // ...código existente...
    
    // ✅ Verificar Line of Sight cada frame
    if (_player != null)
    {
        _hasLineOfSight = CheckLineOfSight();
        
        if (_hasLineOfSight)
        {
            _lastSeenTime = Time.time;
            _lastKnownPlayerPosition = _player.position;
        }
    }
}
```

### 3. Modificación en State_Evaluate()

```csharp
IEnumerator State_Evaluate()
{
    // ✅ PRIORIDAD MÁXIMA: Si no veo al jugador -> BUSCAR
    if (!_hasLineOfSight)
    {
        _currentState = CombatState.SEARCHING;
        yield break;
    }
    
    // ...resto del código...
}
```

### 4. Modificación en State_Attack()

```csharp
IEnumerator State_Attack()
{
    // ✅ Verificar visión antes de atacar
    if (!_hasLineOfSight)
    {
        _currentState = CombatState.SEARCHING;
        yield break;
    }
    
    // ...preparar ataque...
    
    // ✅ Verificar visión de nuevo antes de disparar
    if (!_hasLineOfSight)
    {
        // Cancelar ataque
        _currentState = CombatState.SEARCHING;
        yield break;
    }
    
    // Disparar proyectil
}
```

### 5. Nuevo Estado: State_Searching()

```csharp
IEnumerator State_Searching()
{
    // 1. Reproducir animación de búsqueda
    _animator.PlaySearching();
    
    float searchStartTime = Time.time;
    
    // 2. Bucle de búsqueda (searchDuration segundos)
    while (Time.time - searchStartTime < settings.searchDuration)
    {
        // Si recuperamos visión -> Volver a combate
        if (_hasLineOfSight)
        {
            _currentState = CombatState.EVALUATE;
            yield break;
        }
        
        // 3. Movimientos aleatorios de búsqueda
        Vector3 searchPoint = _lastKnownPlayerPosition + RandomOffset();
        MoveTo(searchPoint, settings.walkSpeed);
        
        // Esperar a llegar
        // Reproducir animación de búsqueda de nuevo
        
        yield return null;
    }
    
    // 4. Tiempo agotado
    if (settings.returnToOriginAfterSearch)
    {
        // Volver al origen
        MoveTo(_combatStartPosition, settings.walkSpeed);
    }
    
    // 5. Salir de combate
    StopCombat();
    _manager.Context.IsInCombat = false;
}
```

## 📊 Flujo Completo del Sistema

```
┌─────────────────────────────────────────┐
│          UPDATE (cada frame)            │
│  CheckLineOfSight() → _hasLineOfSight   │
└──────────────┬──────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────┐
│           STATE: EVALUATE                │
│                                          │
│  if (!_hasLineOfSight)                   │
│     → SEARCHING                          │
│  else                                    │
│     → ATTACK / DEFENSE / REPOSITION      │
└──────────────┬───────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────┐
│           STATE: SEARCHING               │
│                                          │
│  1. PlaySearching() animación            │
│  2. Bucle (searchDuration segundos):     │
│     - Si _hasLineOfSight → EVALUATE      │
│     - Movimientos aleatorios             │
│     - Reproducir animación búsqueda      │
│  3. Tiempo agotado:                      │
│     - Volver al origen (opcional)        │
│     - StopCombat()                       │
│     - IsInCombat = false                 │
└──────────────────────────────────────────┘
```

## 🎯 Configuración Recomendada

### En Unity Inspector (NPCCombatBrain.Settings):

```
Line of Sight & Searching:
├── Obstacle Layer Mask: Default ✓
├── Search Duration: 15.0 segundos
├── Search Movement Radius: 5.0 metros
└── Return To Origin After Search: True ✓
```

### LayerMask Setup:

El `obstacleLayerMask` debe incluir todas las capas que bloqueen la visión:
- ✅ **Default** (paredes, suelos, objetos estáticos)
- ✅ **Terrain** (si aplica)
- ✅ Cualquier capa custom de obstáculos

**NO incluir:**
- ❌ Player (permitir ver al jugador)
- ❌ Ignore Raycast
- ❌ Triggers

## 🔍 Debug Visual

El sistema incluye visualización de rayos en la Scene View:

- **Verde**: Línea de visión clara al jugador
- **Rojo**: Línea bloqueada por obstáculo (muestra hasta donde llega)
- **Cyan**: Radio de búsqueda alrededor de última posición conocida

También hay logs detallados:

```
[CombatBrain:Boy_Pirate] 🚫 Visión bloqueada por: Wall_Stone (Layer: Default)
[CombatBrain:Boy_Pirate] ❌ Sin línea de visión al jugador - Iniciando búsqueda
[CombatBrain:Boy_Pirate] 🔍 INICIANDO BÚSQUEDA - Última posición conocida: (10, 0, 5)
[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda hacia: (12, 0, 3)
[CombatBrain:Boy_Pirate] ✅ Jugador encontrado - Retomando combate
```

## 🎮 Casos de Uso

### Caso 1: Jugador Tras Columna
```
1. NPC dispara al jugador
2. Jugador se esconde tras columna
3. Raycast golpea columna → _hasLineOfSight = false
4. NPC detiene ataque → SEARCHING
5. NPC reproduce animación de búsqueda
6. NPC hace movimientos laterales buscando
7. Jugador sale de columna → _hasLineOfSight = true
8. NPC retoma ataque inmediatamente
```

### Caso 2: Jugador Huye Tras Muro
```
1. NPC en combate con jugador
2. Jugador huye tras muro lejos
3. NPC pierde visión → SEARCHING
4. NPC busca durante 15 segundos
5. No encuentra al jugador
6. NPC vuelve a su posición original
7. NPC sale de modo batalla
```

### Caso 3: Jugador Se Esconde y Espera
```
1. NPC persiguiendo jugador
2. Jugador se esconde tras roca
3. NPC llega a última posición conocida
4. NPC reproduce animación búsqueda
5. NPC hace movimientos aleatorios cerca
6. Jugador permanece oculto
7. Después de searchDuration → NPC se rinde
```

## 📝 Notas Importantes

### Altura del Raycast

El raycast se lanza desde:
- **Origen**: `transform.position + Vector3.up * 1.5f` (altura de ojos del NPC)
- **Destino**: `_player.position + Vector3.up * 1.0f` (centro del jugador)

Esto previene falsos positivos por pequeños obstáculos en el suelo.

### Movimientos Durante Búsqueda

Los movimientos son:
- ✅ **Pequeños** (dentro de `searchMovementRadius`)
- ✅ **Aleatorios** (no predecibles)
- ✅ **Cerca de última posición conocida** (comportamiento lógico)
- ✅ **Con animación de búsqueda** entre movimientos

### Salida de Combate

Si el NPC no encuentra al jugador después de `searchDuration`:
1. ✅ Vuelve a su posición inicial (si `returnToOriginAfterSearch = true`)
2. ✅ Llama a `StopCombat()`
3. ✅ Establece `IsInCombat = false`
4. ✅ Desactiva modo batalla
5. ✅ El NPC vuelve a su comportamiento normal (patrullaje/idle)

---

**Fecha**: 28 de diciembre de 2024  
**Tipo**: New Feature - Line of Sight System  
**Estado**: ✅ COMPLETADO  
**Archivos Modificados**: 
- `NPCCombatBrain.cs` - Sistema completo de LoS y búsqueda
- Configuración en Inspector necesaria

