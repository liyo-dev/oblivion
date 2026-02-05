# FIX: Oscilación del Eje Y en NPCs del Party

## 📋 Problema Identificado

Cuando el jugador está quieto con NPCs en el party, los NPCs se detienen correctamente pero su **posición en el eje Y oscila constantemente**:
- Ejemplo: `298.221 → 301.389 → 299.887 → 298.221...`
- Los NPCs están "flotando" o ajustándose continuamente al NavMesh

## 🔍 Causa del Problema

El problema ocurría en el estado `FollowPlayerState` cuando el NPC se detenía:

```csharp
// ❌ ANTES:
context.Agent.isStopped = true;  // El agente se detiene...
// Pero context.Agent.updatePosition sigue en true
// El NavMeshAgent sigue ajustando la posición Y al NavMesh cada frame
```

**¿Por qué oscila?**
- Cuando `NavMeshAgent.updatePosition = true`, Unity sincroniza continuamente la posición del Transform con el NavMesh
- Incluso con `isStopped = true`, el agente sigue ajustando micro-movimientos en el eje Y
- Esto causa la oscilación visible en la posición

## ✅ Solución Implementada

### 1. Desactivar `updatePosition` cuando el NPC está quieto

```csharp
// ✅ AHORA:
if (distance <= stopDist)
{
    context.Agent.isStopped = true;
    context.Agent.updatePosition = false;  // ← FIX: Deja de actualizar posición
    context.Agent.ResetPath();             // ← Limpia cualquier path residual
    context.Animator?.SetMovementSpeed(0f);
}
```

### 2. Reactivar `updatePosition` cuando el NPC se mueve

```csharp
if (playerIsMoving || distance > stopDist * 1.2f)
{
    // ✅ Reactivar actualización cuando empieza a moverse
    if (!context.Agent.updatePosition)
    {
        // ✅ FIX ADICIONAL: Sincronizar posición ANTES de reactivar
        // Esto evita el "salto" o teletransporte
        if (context.Agent.isOnNavMesh)
        {
            context.Agent.nextPosition = context.Transform.position;
        }
        
        context.Agent.updatePosition = true;
        context.Agent.updateRotation = true;
    }
    
    context.Agent.isStopped = false;
    // ... actualizar destino y velocidad
}
```

**¿Por qué sincronizar `nextPosition`?**
- Cuando `updatePosition = false`, el NavMeshAgent sigue calculando internamente su posición (`nextPosition`)
- Al reactivar `updatePosition`, Unity sincroniza el Transform con `nextPosition` causando un "salto"
- Al hacer `nextPosition = Transform.position` ANTES de reactivar, no hay diferencia → no hay salto ✅

### 3. Optimización para evitar llamadas repetidas

```csharp
// ✅ Solo actualizar si el estado cambió (evita set repetidos cada frame)
if (!context.Agent.isStopped || context.Agent.updatePosition)
{
    context.Agent.isStopped = true;
    context.Agent.updatePosition = false;
    context.Agent.ResetPath();
}
```

## 🎯 Comportamiento Actualizado

### Cuando el jugador está quieto:
1. NPC se acerca al jugador
2. Al llegar a `stopDist` (1.2m):
   - `isStopped = true` → Deja de moverse
   - `updatePosition = false` → **Ya no oscila en Y** ✅
   - `ResetPath()` → Limpia el path para evitar movimientos residuales
3. El NPC gira suavemente hacia el jugador (solo rotación)
4. **La posición se mantiene estable**

### Cuando el jugador empieza a moverse:
1. Se detecta movimiento del jugador (`PLAYER_MOVE_THRESHOLD = 0.1`)
2. Se reactiva `updatePosition = true` y `updateRotation = true`
3. `isStopped = false` → El NPC empieza a seguir
4. El NavMeshAgent vuelve a sincronizar posición normalmente

### Al salir del estado:
```csharp
public override void OnExit(NPCStateContext context)
{
    // ✅ Restaurar valores por defecto
    context.Agent.updatePosition = true;
    context.Agent.updateRotation = true;
    // ... para que otros estados funcionen normalmente
}
```

## 📁 Archivo Modificado

- `Assets/Scripts/Behaviour NPC/States/FollowPlayerState.cs`
  - `OnEnter()`: Desactiva `updatePosition` y `updateRotation` al inicio
  - `OnUpdate()`: Activa/desactiva `updatePosition` según el estado de movimiento
  - `OnExit()`: Restaura los valores por defecto

## 🔍 Cómo Verificar el Fix

### Antes del fix:
```
Inspector del NPC (Transform):
Position: (100.0, 298.221, 50.0)
Position: (100.0, 301.389, 50.0)  // ← Oscila constantemente
Position: (100.0, 299.887, 50.0)
Position: (100.0, 298.221, 50.0)
```

### Después del fix:
```
Inspector del NPC (Transform):
Position: (100.0, 298.221, 50.0)
Position: (100.0, 298.221, 50.0)  // ← Estable ✅
Position: (100.0, 298.221, 50.0)
Position: (100.0, 298.221, 50.0)
```

## 🎮 Configuración Relacionada

Este fix funciona en conjunto con los valores configurados en `NPCPartyConfig`:
- `distanciaParaPararse`: Distancia a la que el NPC se detiene (default: 1.2m)
- `distanciaParaCorrer`: Distancia a la que empieza a correr (default: 3m)
- `velocidadCaminando`: Velocidad de caminata (default: 3.5 m/s)
- `velocidadCorriendo`: Velocidad de carrera (default: 7.5 m/s)

## 💡 Notas Técnicas

### ¿Por qué usar `updatePosition = false`?
- `NavMeshAgent.updatePosition`: Controla si Unity sincroniza automáticamente el Transform.position con NavMeshAgent.nextPosition
- Al desactivarlo cuando está quieto, el Transform.position ya no se actualiza → No hay oscilación
- Al reactivarlo cuando se mueve, el NavMeshAgent vuelve a controlar el movimiento normalmente

### ¿Por qué también `updateRotation = false`?
- Por consistencia: si no movemos, tampoco rotamos automáticamente
- Controlamos la rotación manualmente con `RotateTowardsPlayer()` cuando está quieto
- Más suave y predecible

### ¿Por qué `ResetPath()`?
- Limpia cualquier path residual que pudiera causar micro-movimientos
- Asegura que el NavMeshAgent está completamente detenido
- Previene movimientos inesperados al reactivar `updatePosition`

## 🚀 Beneficios

✅ **No más oscilación en Y**: Los NPCs del party se quedan completamente quietos  
✅ **No más teletransporte**: Transición suave al empezar a moverse  
✅ **Mejor rendimiento**: Menos actualizaciones innecesarias del NavMeshAgent  
✅ **Comportamiento más natural**: Los NPCs se ven más "plantados" cuando están parados  
✅ **Sin efectos secundarios**: Otros estados siguen funcionando normalmente  

## 🐛 Problema Adicional Resuelto: Teletransporte al Empezar a Moverse

### Problema Detectado:
Después del fix inicial, se detectó que cuando el jugador se movía después de estar quieto, el NPC hacía un pequeño "salto" o teletransporte antes de empezar a seguir.

### Causa:
Cuando `updatePosition = false`, el NavMeshAgent sigue calculando su posición interna (`nextPosition`) pero no actualiza el Transform. Al reactivar `updatePosition`, Unity sincroniza bruscamente el Transform con `nextPosition`, causando el salto.

### Solución:
```csharp
if (!context.Agent.updatePosition)
{
    // Sincronizar nextPosition con la posición actual del Transform
    context.Agent.nextPosition = context.Transform.position;
    
    // Ahora reactivar es seguro, no hay diferencia entre ambas posiciones
    context.Agent.updatePosition = true;
}
```

Esto asegura que cuando reactivamos `updatePosition`, no hay diferencia entre `nextPosition` y `Transform.position`, por lo que no hay salto visual.

---

**Fecha**: 2026-02-05  
**Estado**: ✅ Implementado y probado  
**Impacto**: Todos los NPCs en el party cuando el jugador está quieto
