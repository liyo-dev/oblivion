# FIX: Slow Motion Durante Animación de Hit Letal

## 📋 Problema Identificado

El slow motion se estaba aplicando **DESPUÉS** de que la animación de muerte ya había comenzado, causando una secuencia incorrecta:

### Secuencia INCORRECTA (Antes):
```
1. Golpe letal recibido
2. PlayGetHit() - Animación TakeDamage (SIN slow-mo)
3. Die() se llama
4. StopCombat() → CrossFade a Idle ❌ (NPC se queda de pie)
5. DeathRoutine() inicia
6. AHORA aplica slow-mo ❌ (demasiado tarde)
7. PlayDeath() → Animación de muerte
```

### Logs que mostraban el problema:
```
[NPCAnimator] ✅ CrossFade a estado 'TakeDamage'  ← Hit sin slow-mo
[Damageable] 💀 VIDA AGOTADA
[Lifecycle] 💀💀💀 OnDied() LLAMADO
[Lifecycle] 💀 Iniciando secuencia de muerte
[NPCAnimator] ✅ CrossFade a estado 'Idle_Normal_NoWeapon'  ← ❌ PROBLEMA
... tiempo pasa...
[NPCAnimator] 💀 PlayDeath() llamado  ← Slow-mo aquí (tarde)
```

## ✅ Solución Implementada

El slow motion ahora se aplica **INMEDIATAMENTE** cuando el NPC recibe el golpe letal, **DURANTE** la animación de Hit (TakeDamage).

### Secuencia CORRECTA (Ahora):
```
1. Golpe letal recibido
2. DamageSequence() detecta !IsAlive
3. ✅ Aplica slow-mo INMEDIATAMENTE
4. ✅ PlayGetHit() - Animación TakeDamage CON slow-mo
5. ✅ Espera deathSlowMoDuration en tiempo real
6. ✅ Restaura Time.timeScale = 1f
7. Die() se llama
8. DeathRoutine() inicia (SIN slow-mo adicional)
9. ✅ PlayDeath() → Transición directa Hit → Muerte
10. Celebración del jugador (mientras NPC cae)
```

## 🔧 Cambios Técnicos

### 1. DamageSequence() - Detectar Golpe Letal y Aplicar Slow-Mo

```csharp
private IEnumerator DamageSequence()
{
    IsStunned = true;
    _isInvulnerable = true;

    // ✅ NUEVO: Si este golpe es LETAL, aplicar slow-mo AHORA
    bool isLethalHit = !_damageable.IsAlive;
    
    if (isLethalHit && enableDeathEffects)
    {
        Debug.Log($"[Lifecycle] 💀 GOLPE LETAL detectado - Aplicando slow motion durante animación de Hit");
        FeedbackService.CameraShake(cameraShakeIntensity * 2f, 0.5f);
        Time.timeScale = deathSlowMoScale; // ← SLOW-MO AQUÍ
    }

    // 1. Reproducir animación de Hit (ahora CON slow-mo si es letal)
    if (playDamageAnimation && _animator) _animator.PlayGetHit();
    
    // Feedback normal solo si NO es letal
    if (!isLethalHit && enableCameraShake) FeedbackService.CameraShake(...);
    if (!isLethalHit && enableHitStop) FeedbackService.HitStop(...);

    // ...detener movimiento...

    // 3. Esperar durante slow-mo
    if (isLethalHit && enableDeathEffects)
    {
        // ✅ Esperar en tiempo REAL (no afectado por Time.timeScale)
        yield return new WaitForSecondsRealtime(deathSlowMoDuration);
        
        // ✅ Restaurar time scale DESPUÉS de la animación de Hit
        Time.timeScale = 1f;
        Debug.Log($"[Lifecycle] ⏱️ Slow motion terminado - Time scale restaurado");
    }
    else
    {
        yield return new WaitForSeconds(damageStunDuration);
    }

    // 4. Recuperación solo si NO murió
    if (_damageable.IsAlive)
    {
        // ...transición a Idle...
    }
    // ✅ Si murió, NO hacer nada - DeathRoutine toma el control
}
```

### 2. DeathRoutine() - Eliminar Slow-Mo Redundante

```csharp
private IEnumerator DeathRoutine()
{
    Debug.Log($"[Lifecycle] 💀 Iniciando secuencia de muerte: {name}");

    // ...detener todo, VFX, rotar...

    // ❌ ELIMINADO: El slow-mo ya se aplicó en DamageSequence
    // if (enableDeathEffects)
    // {
    //     Time.timeScale = deathSlowMoScale;
    //     yield return new WaitForSecondsRealtime(deathSlowMoDuration);
    //     Time.timeScale = 1f;
    // }

    // ✅ Pequeña pausa para asegurar transición suave
    yield return new WaitForSeconds(0.1f);

    // ✅ Iniciar animación de muerte directamente
    if (behavior == PostDeathBehavior.GetUpDizzy && _animator)
    {
        _animator.PlayDeath();
        Debug.Log($"[Lifecycle] 💀 Animación de muerte iniciada - transición directa desde Hit");
    }

    // ...celebración jugador...
    // ...resto del flujo...
}
```

## 📊 Comparación

### ANTES ❌
| Momento | Acción | Slow-Mo |
|---------|--------|---------|
| t=0.0s | PlayGetHit() - TakeDamage | ❌ No |
| t=0.5s | CrossFade a Idle | ❌ (bug) |
| t=1.0s | DeathRoutine inicia | ❌ No |
| t=1.5s | Slow-mo aplicado | ✅ Sí (tarde) |
| t=2.0s | PlayDeath() | ✅ Sí |

### DESPUÉS ✅
| Momento | Acción | Slow-Mo |
|---------|--------|---------|
| t=0.0s | ✅ Slow-mo activado | ✅ Sí |
| t=0.0s | PlayGetHit() - TakeDamage | ✅ Sí |
| t=0.5s | ✅ Slow-mo termina | ❌ No |
| t=0.5s | Time scale restaurado | ❌ No |
| t=0.6s | DeathRoutine inicia | ❌ No |
| t=0.7s | PlayDeath() | ❌ No |

## 🎬 Flujo Visual Correcto

```
GOLPE LETAL
     ↓
[SLOW MOTION ACTIVADO] ⏱️
     ↓
Animación TakeDamage (en slow-mo, épico)
     ↓
[SLOW MOTION TERMINA] ⏱️
     ↓
Transición directa
     ↓
Animación de Muerte (velocidad normal)
     ↓
Transición a Dizzy
     ↓
Diálogo
```

## 🎯 Por Qué Funciona Ahora

### 1. Detección Temprana
```csharp
bool isLethalHit = !_damageable.IsAlive;
```
Detectamos inmediatamente si el golpe es letal **ANTES** de reproducir la animación.

### 2. Slow-Mo Durante la Acción
El slow-mo se activa **DURANTE** la animación de impacto, creando el efecto dramático correcto.

### 3. WaitForSecondsRealtime
```csharp
yield return new WaitForSecondsRealtime(deathSlowMoDuration);
```
Usamos tiempo **REAL** (no afectado por `Time.timeScale`) para esperar la duración correcta.

### 4. Restauración Inmediata
```csharp
Time.timeScale = 1f;
```
Restauramos la velocidad normal **ANTES** de que DeathRoutine inicie, evitando efectos secundarios.

### 5. Sin Transición a Idle
```csharp
if (_damageable.IsAlive)
{
    // Solo transicionar a Idle si NO murió
}
```
Si el NPC murió, NO hace transición a Idle, evitando el bug visual.

## 🎮 Experiencia del Jugador

### ANTES ❌
```
Jugador dispara golpe letal
    ↓
NPC recibe hit (normal)
    ↓
NPC se queda de pie (Idle) 😕 ← BUG
    ↓
... 1 segundo pasa ...
    ↓
Slow-mo extraño (fuera de contexto)
    ↓
Animación de muerte (confusa)
```

### AHORA ✅
```
Jugador dispara golpe letal
    ↓
[SLOW MOTION] ⏱️ ← INMEDIATO
    ↓
NPC recibe hit (épico, en slow-mo) 😮
    ↓
[Velocidad normal restaurada] ⏱️
    ↓
NPC cae al suelo (natural) 💀
    ↓
Transición a Dizzy
    ↓
Diálogo
```

## 📝 Logs Esperados Ahora

```
[Lifecycle] ⚔️ Boy_Pirate recibió 50 de daño - Vida: 0/100 - IsAlive: False
[Lifecycle] 💀 GOLPE LETAL detectado - Aplicando slow motion durante animación de Hit  ← NUEVO
[NPCAnimator] 💥 PlayGetHit() - Animación seleccionada: 'TakeDamage'
[NPCAnimator] ✅ CrossFade a estado 'TakeDamage'
... slow motion aquí (0.5 segundos) ...
[Lifecycle] ⏱️ Slow motion terminado - Time scale restaurado  ← NUEVO
[Damageable] 💀 VIDA AGOTADA - Llamando a Die()
[Lifecycle] 💀💀💀 OnDied() LLAMADO
[Lifecycle] 💀 Iniciando secuencia de muerte
[NPCAnimator] 💀 PlayDeath() llamado
[Lifecycle] 💀 Animación de muerte iniciada - transición directa desde Hit  ← NUEVO
```

## 🔑 Ventajas del Fix

1. ✅ **Feedback Inmediato**: El jugador VE el impacto letal con slow-mo
2. ✅ **Sin Idle Bug**: No hay transición a Idle cuando muere
3. ✅ **Transición Suave**: Hit → Muerte es directa y fluida
4. ✅ **Timing Correcto**: Slow-mo durante la acción, no después
5. ✅ **Épico Visual**: El golpe letal se siente poderoso

## ⚠️ Notas Importantes

### WaitForSecondsRealtime vs WaitForSeconds

- **WaitForSecondsRealtime**: No afectado por `Time.timeScale` → Usamos esto durante slow-mo
- **WaitForSeconds**: Afectado por `Time.timeScale` → Usamos esto en gameplay normal

### Restauración de Time.timeScale

Es **CRÍTICO** restaurar `Time.timeScale = 1f` al terminar el slow-mo, de lo contrario:
- ❌ Todo el juego sigue en slow-mo
- ❌ La animación de muerte se ve lenta
- ❌ El diálogo se ve afectado

### Sin Doble Slow-Mo

Con este fix, **solo hay UN slow-mo** por muerte (durante el Hit), no dos.

---

**Fecha**: 28 de diciembre de 2024  
**Tipo**: Bug Fix Crítico - Timing de Slow Motion  
**Estado**: ✅ COMPLETADO  
**Impacto**: Visual dramático mejorado, sin bugs de Idle

