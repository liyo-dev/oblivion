# 🔗 Integración: Combat Camera Targeting + Player Targeting

## 📋 Sistemas Existentes

Tu juego tiene **DOS sistemas de targeting** independientes:

### 1. **PlayerTargeting** (Proyectiles/Hechizos)
**Archivo**: `Assets/Scripts/Player/PlayerTargeting.cs`

**Función**: 
- Detecta enemigos dentro de un radio y FOV
- Muestra un marcador visual (cuadrícula) sobre el enemigo
- Usado por `MagicProjectileSpawner` para dirigir proyectiles
- Actualización continua (10 veces por segundo)
- Integrado con sistema de Damageable para detectar muerte

**Características**:
- ✅ Auto-targeting por layer o componente `Targetable`
- ✅ Requiere línea de visión
- ✅ Verifica que esté en pantalla
- ✅ FOV configurable
- ✅ Marcador visual animado

### 2. **CombatCameraTargeting** (Cámara en Combate) 
**Archivo**: `Assets/Scripts/Camera/CombatCameraTargeting.cs`

**Función**:
- Lock de cámara al enemigo durante combate
- Integrado con `ActiveCombatRegistry`
- Controles con D-Pad para cambiar de target
- Rotación suave de cámara hacia el enemigo

**Características**:
- ✅ Lock automático al entrar en combate
- ✅ Cambio de target con D-Pad
- ✅ Indicador visual opcional
- ✅ Integración con vThirdPersonCamera

---

## 🤝 Integración Entre Sistemas

### Opción 1: Independientes (Recomendado)

Los dos sistemas funcionan **independientemente**:
- `PlayerTargeting` maneja el targeting de **proyectiles/hechizos**
- `CombatCameraTargeting` maneja el **lock de cámara**

**Ventajas**:
- ✅ Más flexible: puedes apuntar proyectiles sin lock de cámara
- ✅ El jugador puede disparar a un enemigo y moverse libre
- ✅ Útil para combates con muchos enemigos

**Uso**:
```
Jugador entra en combate
  → CombatCameraTargeting hace lock a Boss
  → PlayerTargeting detecta enemigos menores alrededor
  → Jugador puede disparar a los menores sin cambiar lock de cámara
```

### Opción 2: Sincronizados

Ambos sistemas apuntan al **mismo enemigo**:
- El target de la cámara se usa también para proyectiles

**Ventajas**:
- ✅ Más predecible: proyectiles siempre van al enemigo lockeado
- ✅ Mejor para combates 1v1 (bosses)
- ✅ Similar a Dark Souls / Monster Hunter

**Implementación**:
El sistema ya está preparado. En `CombatCameraTargeting`:
```csharp
[Header("Integración con Sistema de Proyectiles")]
[SerializeField] private PlayerTargeting playerTargeting;
[SerializeField] private bool syncWithProjectileTargeting = true;
```

Para activarla, necesitarías modificar `SetTarget()` en `CombatCameraTargeting.cs` para forzar el target en `PlayerTargeting`.

---

## 🎯 Configuración Recomendada

### Para Bosses (1v1)
```
✅ CombatCameraTargeting: Activado
   - Lock automático al boss
   - D-Pad para cambiar de fase si el boss se separa
   
✅ PlayerTargeting: Activado
   - Auto-targeting al boss
   - Proyectiles van automáticamente al target
```

### Para Combates con Múltiples Enemigos
```
✅ CombatCameraTargeting: Activado
   - Lock al enemigo más cercano
   - D-Pad para cambiar entre enemigos
   
✅ PlayerTargeting: Activado e Independiente
   - Detecta todos los enemigos en FOV
   - Puedes disparar a cualquiera sin cambiar lock de cámara
```

### Para Exploración/Combate Casual
```
❌ CombatCameraTargeting: Desactivado
   - Cámara libre
   
✅ PlayerTargeting: Activado
   - Auto-targeting de proyectiles
   - Marcador visual sobre enemigos
```

---

## 🔧 Cómo Sincronizar (Si Lo Deseas)

### Paso 1: Forzar Target en PlayerTargeting

Modifica `CombatCameraTargeting.SetTarget()`:

```csharp
private void SetTarget(GameObject newTarget)
{
    if (newTarget == null)
    {
        ReleaseLock();
        return;
    }
    
    currentTarget = newTarget;
    isLockActive = true;
    
    // === SINCRONIZAR CON PLAYERTARGETING ===
    if (syncWithProjectileTargeting && playerTargeting != null)
    {
        // Forzar el mismo target en el sistema de proyectiles
        // Nota: PlayerTargeting no tiene método público para forzar target
        // Necesitarías agregarlo o usar Reflection
        ForcePlayerTargetingTarget(newTarget.transform);
    }
    
    // ... resto del código
}

private void ForcePlayerTargetingTarget(Transform target)
{
    // Usar reflection para forzar el target
    var field = typeof(PlayerTargeting).GetField("CurrentTarget", 
        System.Reflection.BindingFlags.NonPublic | 
        System.Reflection.BindingFlags.Instance);
    
    if (field != null)
    {
        field.SetValue(playerTargeting, target);
        Log($"🔗 Sincronizado PlayerTargeting → {target.name}");
    }
}
```

### Paso 2: Agregar Método Público en PlayerTargeting (Mejor)

Edita `PlayerTargeting.cs`:

```csharp
/// <summary>
/// Fuerza un target específico (útil para integración con combat camera)
/// </summary>
public void ForceTarget(Transform target)
{
    var before = CurrentTarget;
    CurrentTarget = target;
    
    if (before != CurrentTarget)
    {
        OnTargetChanged(CurrentTarget);
    }
}
```

Luego en `CombatCameraTargeting`:

```csharp
if (syncWithProjectileTargeting && playerTargeting != null)
{
    playerTargeting.ForceTarget(newTarget.transform);
}
```

---

## 📊 Comparación de Comportamientos

### Independientes
```
Escenario: Boss + 3 enemigos pequeños

Cámara: Locked en Boss (CombatCameraTargeting)
Proyectiles: Auto-apuntan al enemigo más cercano en FOV (PlayerTargeting)

Jugador puede:
  ✅ Mantener vista del Boss
  ✅ Disparar a enemigos pequeños sin cambiar cámara
  ✅ Usar D-Pad para lockear a un enemigo pequeño si quiere
```

### Sincronizados
```
Escenario: Boss + 3 enemigos pequeños

Cámara: Locked en Boss (CombatCameraTargeting)
Proyectiles: Apuntan al Boss (sincronizado)

Jugador debe:
  ⚠️ Cambiar lock con D-Pad para disparar a enemigos pequeños
  ⚠️ Más tedioso con muchos enemigos
  ✅ Más preciso y predecible
```

---

## 🎮 Recomendación Final

### Para tu juego (estilo RPG con combos de magia):

**Usa sistemas INDEPENDIENTES**:
- `CombatCameraTargeting` para mantener vista del boss principal
- `PlayerTargeting` para auto-apuntar proyectiles dinámicamente
- El jugador tiene libertad táctica de disparar a múltiples enemigos

**Sincroniza SOLO para combates 1v1 importantes**:
- Activa `syncWithProjectileTargeting` en escenas de boss
- Desactívala para combates con múltiples enemigos

---

## 🔍 Testing

### Verificar Funcionamiento Independiente

1. Entrar en combate con 2+ enemigos
2. Lock de cámara debería ir al más cercano
3. Disparar proyectil → debería ir al enemigo en tu FOV (puede ser diferente)
4. Cambiar lock con D-Pad → cámara cambia
5. Proyectiles siguen yendo al mejor target según PlayerTargeting

### Verificar Sincronización (Si la activas)

1. Activar `syncWithProjectileTargeting` en Inspector
2. Entrar en combate
3. Lock de cámara al enemigo A
4. Disparar proyectil → DEBE ir al enemigo A (mismo que cámara)
5. Cambiar lock a enemigo B con D-Pad
6. Proyectil ahora va a enemigo B

---

## ✅ Estado Actual

**Por defecto** (sin modificaciones adicionales):
- ✅ Los sistemas son **independientes**
- ✅ `CombatCameraTargeting` tiene la OPCIÓN de sincronizar
- ✅ Pero NO lo hace automáticamente (necesitas implementar `ForceTarget`)

**Para activar sincronización**:
1. Agregar método `ForceTarget()` en `PlayerTargeting.cs`
2. Llamarlo desde `SetTarget()` en `CombatCameraTargeting.cs`
3. Activar `syncWithProjectileTargeting` en Inspector
