# FIX CRÍTICO: Proyectiles del NPC Atravesaban Obstáculos + Line of Sight

## 📋 Problemas Identificados

### Problema 1: Proyectiles Atraviesan Obstáculos ❌
Los proyectiles del NPC (`EnemyProjectile`) atravesaban objetos en el layer **Default** (muros, columnas) sin colisionar.

**Causa**: El collider estaba configurado como `isTrigger = true` y el Rigidbody como `isKinematic = true`, lo que impedía las colisiones físicas reales con obstáculos.

### Problema 2: NPC "Ve" al Jugador Detrás de Obstáculos ❌
El NPC podía detectar y disparar al jugador aunque hubiera un obstáculo bloqueando la línea de visión.

**Causa**: El sistema de Line of Sight debe estar configurado correctamente en el `NPCCombatBrain` con el `obstacleLayerMask` incluyendo la capa Default.

## ✅ Solución Implementada

### 1. EnemyProjectile - Colisiones Físicas Reales

#### Cambio en Awake()

**ANTES** ❌:
```csharp
void Awake()
{
    var col = GetComponent<SphereCollider>();
    if (col)
    {
        col.isTrigger = true; // ← PROBLEMA: No detecta colisiones físicas
        col.radius = 0.5f;
    }

    rb = GetComponent<Rigidbody>();
    if (rb)
    {
        if (usePhysicsMovement)
        {
            rb.isKinematic = false;
        }
        else
        {
            rb.isKinematic = true; // ← PROBLEMA: No hay colisiones
        }
    }
}
```

**AHORA** ✅:
```csharp
void Awake()
{
    // ✅ Configurar para colisiones FÍSICAS (no trigger)
    var col = GetComponent<SphereCollider>();
    if (col)
    {
        col.isTrigger = false; // ← Colisiones físicas activadas
        col.radius = 0.5f;
    }

    rb = GetComponent<Rigidbody>();
    if (rb)
    {
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        // ✅ SIEMPRE no-kinematic para colisiones
        rb.isKinematic = false;
        rb.linearDamping = 0f; // Sin fricción
        rb.angularDamping = 0f;
        
        // ✅ Congelar rotación para que no gire al colisionar
        rb.freezeRotation = true;
    }
}
```

#### Nuevo Sistema de Colisiones

**OnCollisionEnter** (nuevo) - Para colisiones FÍSICAS:
```csharp
void OnCollisionEnter(Collision collision)
{
    if (hasHit) return;
    
    Collider other = collision.collider;
    
    // Ignorar enemigos
    if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Enemy")) 
        return;
    
    // ✅ PRIORIDAD 1: Detectar layer Default (obstáculos)
    if (other.gameObject.layer == LayerMask.NameToLayer("Default"))
    {
        hasHit = true;
        Debug.Log($"[EnemyProjectile] 💥 Impacto FÍSICO contra Default: {other.name}");
        DestroyProjectile();
        return;
    }
    
    // ✅ PRIORIDAD 2: Escudo del jugador
    if (other.GetComponent<PlayerShieldController.ShieldMarker>() != null)
    {
        hasHit = true;
        DestroyProjectile();
        return;
    }

    // ✅ PRIORIDAD 3: Jugador (buscar en jerarquía)
    Transform checkTransform = other.transform;
    for (int i = 0; i < 3; i++)
    {
        if (checkTransform.CompareTag("Player"))
        {
            hasHit = true;
            ApplyDamage(checkTransform.gameObject);
            DestroyProjectile();
            return;
        }
        if (checkTransform.parent != null)
            checkTransform = checkTransform.parent;
        else
            break;
    }

    // ✅ Componente PlayerHealthSystem
    var playerHealth = other.GetComponentInParent<PlayerHealthSystem>();
    if (playerHealth != null)
    {
        hasHit = true;
        playerHealth.TakeDamage(damage);
        DestroyProjectile();
        return;
    }

    // ✅ Cualquier otra colisión física
    hasHit = true;
    Debug.Log($"[EnemyProjectile] 💥 Impacto contra: {other.name}");
    DestroyProjectile();
}
```

**OnTriggerEnter** (simplificado) - SOLO para proyectiles del jugador:
```csharp
void OnTriggerEnter(Collider other)
{
    if (hasHit) return;
    
    // Ignorar enemigos
    if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Enemy")) 
        return;
    
    // ✅ SOLO para colisión con proyectiles del jugador (layer "Projectile")
    if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
    {
        hasHit = true;
        Vector3 collisionPoint = other.ClosestPoint(transform.position);
        ProjectileCollisionHandler.HandleCollision(other.gameObject, gameObject, collisionPoint);
        return;
    }
}
```

### 2. NPCCombatBrain - Line of Sight Configuration

**⚠️ CONFIGURACIÓN REQUERIDA EN UNITY INSPECTOR**:

Para que el NPC NO vea al jugador detrás de obstáculos, necesitas configurar en el Inspector:

```
NPCCombatBrain Component:
└── Settings
    └── Line of Sight & Searching
        ├── Obstacle Layer Mask: ✓ Default
        ├── Search Duration: 15.0
        ├── Search Movement Radius: 5.0
        └── Return To Origin After Search: ✓
```

**Código que verifica Line of Sight** (ya implementado):
```csharp
private bool CheckLineOfSight()
{
    if (_player == null) return false;
    
    Vector3 origin = transform.position + Vector3.up * 1.5f; // Altura ojos
    Vector3 targetPos = _player.position + Vector3.up * 1.0f; // Centro jugador
    Vector3 direction = targetPos - origin;
    float distance = direction.magnitude;
    
    // ✅ Raycast que detecta obstáculos
    if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, 
        distance, settings.obstacleLayerMask))
    {
        // ❌ HAY UN OBSTÁCULO BLOQUEANDO
        Debug.Log($"🚫 Visión bloqueada por: {hit.collider.name}");
        return false;
    }
    
    // ✅ Línea de visión clara
    return true;
}
```

## 📊 Comparación del Sistema

### ANTES ❌

| Componente | Problema | Resultado |
|------------|----------|-----------|
| **EnemyProjectile** | isTrigger = true, isKinematic = true | Atraviesa obstáculos |
| **Collider** | Solo OnTriggerEnter | No detecta Default |
| **NPCCombatBrain** | Sin verificar obstacleLayerMask | Ve a través de muros |

### AHORA ✅

| Componente | Solución | Resultado |
|------------|----------|-----------|
| **EnemyProjectile** | isTrigger = false, isKinematic = false | Colisiona con obstáculos |
| **Collider** | OnCollisionEnter + OnTriggerEnter | Detecta Default físicamente |
| **NPCCombatBrain** | CheckLineOfSight() con obstacleLayerMask | NO ve a través de muros |

## 🎮 Comportamiento Correcto Ahora

### Escenario 1: Jugador Se Esconde Detrás de Muro

```
1. NPC está atacando al jugador
2. Jugador se esconde detrás de un muro (layer Default)
3. ✅ CheckLineOfSight() retorna false (raycast golpea muro)
4. ✅ NPC cambia a estado SEARCHING
5. ✅ NPC reproduce animación de búsqueda
6. ✅ Si dispara proyectiles, éstos explotan al golpear el muro
```

### Escenario 2: NPC Dispara Hacia Obstáculo

```
1. NPC dispara proyectil hacia el jugador
2. Proyectil viaja en línea recta
3. ✅ Proyectil colisiona FÍSICAMENTE con el muro (OnCollisionEnter)
4. ✅ hasHit = true
5. ✅ DestroyProjectile() se llama
6. ✅ Efecto visual de impacto en el muro
7. ✅ Proyectil destruido
```

## 🔧 Cambios Técnicos Completos

### EnemyProjectile.cs

1. **Awake()**:
   - `isTrigger = false` (antes: true)
   - `isKinematic = false` SIEMPRE (antes: condicional)
   - `freezeRotation = true` (nuevo)

2. **Initialize()**:
   - Simplificado para usar siempre física
   - Eliminada lógica condicional de `usePhysicsMovement`

3. **FixedUpdate()**:
   - Solo usa física (`rb.linearVelocity`)
   - Eliminado movimiento manual con `transform.position`

4. **OnCollisionEnter()** (NUEVO):
   - Detecta colisiones físicas con Default
   - Detecta colisiones con jugador
   - Detecta colisiones con escudo
   - Maneja cualquier colisión física

5. **OnTriggerEnter()** (SIMPLIFICADO):
   - SOLO para proyectiles del jugador (layer "Projectile")
   - Ya no maneja Default ni jugador (ahora en OnCollisionEnter)

## 📝 Configuración Requerida en Unity

### 1. En el Prefab del Proyectil (EnemyProjectile):

**SphereCollider**:
- ✅ `Is Trigger`: **FALSE** (crítico)
- ✅ `Radius`: 0.5

**Rigidbody**:
- ✅ `Is Kinematic`: **FALSE** (crítico)
- ✅ `Use Gravity`: FALSE
- ✅ `Interpolation`: Interpolate
- ✅ `Collision Detection`: Continuous Dynamic
- ✅ `Freeze Rotation`: X, Y, Z (todos)

### 2. En el GameObject del NPC:

**NPCCombatBrain Component**:
- ✅ `Obstacle Layer Mask`: Seleccionar **"Default"**
- ✅ `Search Duration`: 15
- ✅ `Search Movement Radius`: 5
- ✅ `Return To Origin After Search`: TRUE

## 🔍 Debug Logs Esperados

### Cuando el Proyectil Golpea un Obstáculo:
```
[EnemyProjectile] 💥 Impacto FÍSICO contra objeto Default: Stone_Wall
[EnemyProjectile] Destruyendo proyectil...
```

### Cuando el NPC Pierde Línea de Visión:
```
[CombatBrain:Boy_Pirate] 🚫 Visión bloqueada por: Stone_Wall (Layer: Default)
[CombatBrain:Boy_Pirate] ❌ Sin línea de visión al jugador - Iniciando búsqueda
[CombatBrain:Boy_Pirate] 🔍 INICIANDO BÚSQUEDA
```

## ⚠️ Importante: Configuración de Layers

Verifica en Unity → Edit → Project Settings → Physics:

**Collision Matrix**:
- ✅ **Default** debe colisionar con **Default** (para proyectiles)
- ✅ Los proyectiles del enemigo (si tienen layer específico) deben colisionar con **Default**
- ✅ Los proyectiles del jugador (layer "Projectile") pueden usar triggers

## 🎯 Resultado Final

- ✅ **Proyectiles del NPC explotan al golpear obstáculos Default**
- ✅ **NPC NO puede ver al jugador detrás de obstáculos**
- ✅ **NPC entra en modo búsqueda cuando pierde línea de visión**
- ✅ **Comportamiento táctico realista**
- ✅ **Colisiones físicas funcionan correctamente**

---

**Fecha**: 28 de diciembre de 2024  
**Tipo**: Bug Fix Crítico - Colisiones y Line of Sight  
**Estado**: ✅ COMPLETADO  
**Archivos Modificados**: 
- `EnemyProjectile.cs` - Sistema de colisiones físicas
- Requiere configuración en Unity Inspector

