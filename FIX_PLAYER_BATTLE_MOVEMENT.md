# Fix: Animación de Movimiento en Modo Batalla + Sistema de Audio

## Problemas Identificados

### 1. Animación de Locomoción en Batalla
Cuando el jugador está en modo batalla y en su idle de batalla (`Idle_Battle`), al mover el joystick el personaje se quedaba atascado en el idle de batalla sin poder reproducir las animaciones de locomoción (caminar/correr).

### 2. Sistema de Audio Duplicado
El `PlayerBattleModeController` tenía `AudioSource` y `AudioClip` para reproducir música de victoria directamente, cuando el proyecto ya tiene un sistema de audio centralizado (`AudioService` + `AudioGraphProfile`) que debe gestionar todo el audio.

## Causas Raíz

### Problema de Animación
El sistema `PlayerBattleModeController` detectaba correctamente cuando el jugador se movía, pero simplemente no hacía nada, esperando que Invector manejara las animaciones. Sin embargo, el jugador ya estaba en el estado `Idle_Battle` que probablemente no tiene las transiciones adecuadas hacia las animaciones de locomoción de Invector.

### Problema de Audio
Se estaba gestionando audio directamente en el controlador en lugar de delegar al sistema centralizado, violando la arquitectura del proyecto.

## Soluciones Implementadas

### Archivo Modificado
- ✅ `Assets/Scripts/Player/PlayerBattleModeController.cs`

### Cambios Realizados

#### 1. Modificación en el método `Update()` (líneas 130-147)
**Antes:**
```csharp
// ✅ Si está quieto Y en Battle Mode, asegurar Battle Idle
// ✅ Si se mueve, NO hacer nada - Invector maneja la locomoción
if (!isMoving)
{
    EnsureBattleIdle();
}
// Si se mueve, no hacer nada - dejar que Invector maneje las animaciones
```

**Después:**
```csharp
// ✅ Si está quieto Y en Battle Mode, asegurar Battle Idle
if (!isMoving)
{
    EnsureBattleIdle();
}
// ✅ Si se mueve, transicionar a Idle normal para enganchar con locomoción
else
{
    EnsureNormalIdleForMovement();
}
```

#### 2. Nueva función `EnsureNormalIdleForMovement()` (líneas 248-268)
Se agregó una nueva función que gestiona la transición del Battle Idle al Idle normal cuando el jugador empieza a moverse:

```csharp
void EnsureNormalIdleForMovement()
{
    if (!_isInBattleMode) return;
    
    AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
    
    // Si está en Battle Idle, cambiar a Idle normal para permitir locomoción
    if (currentState.shortNameHash == _battleIdleHash)
    {
        if (animator.HasState(0, _normalIdleHash))
        {
            animator.CrossFadeInFixedTime(_normalIdleHash, 0.15f, 0);
            
            if (debugMode)
                Debug.Log("[PlayerBattleMode] 🏃 Cambiado a Idle normal para permitir locomoción");
        }
    }
}
```

#### 3. Eliminación de campos de audio duplicados (líneas 42-50)
**Antes:**
```csharp
[Header("Audio")]
[Tooltip("AudioSource para reproducir música de victoria")]
[SerializeField] private AudioSource victoryAudioSource;

[Tooltip("Clip de audio de música de victoria (opcional)")]
[SerializeField] private AudioClip victoryMusicClip;

[Tooltip("Volumen de la música de victoria")]
[Range(0f, 1f)]
[SerializeField] private float victoryMusicVolume = 0.7f;
```

**Después:**
```csharp
[Header("Audio")]
[Tooltip("Clave del evento de audio para victoria (configurado en AudioGraphProfile)")]
[SerializeField] private string victorySFXKey = "Player_Victory";
```

#### 4. Refactorización del método `PlayVictorySequence()` (líneas 300-310)
**Antes:**
```csharp
// Reproducir música de victoria si está configurada
if (victoryMusicClip != null)
{
    if (victoryAudioSource != null)
    {
        victoryAudioSource.clip = victoryMusicClip;
        victoryAudioSource.volume = victoryMusicVolume;
        victoryAudioSource.Play();
        
        if (debugMode)
            Debug.Log("[PlayerBattleMode] 🎵 Reproduciendo música de victoria");
    }
    else
    {
        Debug.LogWarning("[PlayerBattleMode] ⚠️ victoryAudioSource no está asignado");
    }
}
```

**Después:**
```csharp
// Reproducir SFX de victoria usando el sistema de audio centralizado
if (!string.IsNullOrEmpty(victorySFXKey) && AudioService.Instance != null)
{
    AudioService.Instance.PlaySFX(victorySFXKey, volume: 1f);
    
    if (debugMode)
        Debug.Log($"[PlayerBattleMode] 🎵 Reproduciendo SFX de victoria: {victorySFXKey}");
}
```

## Funcionamiento

### Flujo de Animaciones en Batalla
1. **Jugador quieto con enemigos cerca:** 
   - Se reproduce `Idle_Battle`
   
2. **Jugador empieza a moverse (mueve el joystick):**
   - Se detecta movimiento (`isMoving = true`)
   - Se llama a `EnsureNormalIdleForMovement()`
   - Se verifica que esté en `Idle_Battle`
   - Se hace una transición rápida (0.15s) a `Idle` normal
   - El `Idle` normal tiene Exit Time configurado y transiciona automáticamente a las animaciones de locomoción de Invector
   
3. **Jugador se detiene:**
   - Se detecta que está quieto (`isMoving = false`)
   - Se llama a `EnsureBattleIdle()`
   - Se vuelve a `Idle_Battle`

## Ventajas de la Solución

### Animación
✅ **Transición fluida:** El cambio al Idle normal es rápido (0.15s) pero suave  
✅ **Respeta Exit Time:** El Idle normal ya tiene configurado Exit Time para enganchar con locomoción  
✅ **Sin spam:** Solo cambia si está en Battle Idle, evitando llamadas innecesarias  
✅ **Debuggeable:** Incluye logs opcionales para depuración  
✅ **Mantiene Battle Mode:** El jugador sigue en Battle Mode, solo cambia la animación temporalmente  

### Audio
✅ **Arquitectura centralizada:** Todo el audio pasa por `AudioService`  
✅ **Configuración en un solo lugar:** El audio se configura en `AudioGraphProfile` (ScriptableObject)  
✅ **Reutilizable:** El mismo sistema de audio funciona para NPCs, UI, efectos, etc.  
✅ **Mantenible:** No hay referencias de audio dispersas por el código  
✅ **Flexible:** Cambiar un sonido no requiere modificar código, solo el ScriptableObject  

## Configuración de Audio

Para configurar el audio de victoria:

1. Abrir el `AudioGraphProfile` del proyecto
2. En la sección `Event SFX`, agregar una entrada:
   - **Event Key:** `Player_Victory`
   - **SFX:** Asignar el AudioClip de fanfarria/victoria
3. Guardar el ScriptableObject

El `PlayerBattleModeController` automáticamente reproducirá este SFX cuando se active la animación de victoria.  

## Testing

Para probar la corrección:

1. Iniciar una batalla con un NPC
2. Verificar que el jugador entre en `Idle_Battle`
3. Mover el joystick
4. **Resultado esperado:** El jugador debe transicionar suavemente a caminar/correr
5. Soltar el joystick
6. **Resultado esperado:** El jugador debe volver a `Idle_Battle`

Activar `debugMode` en el Inspector del componente `PlayerBattleModeController` para ver los logs detallados de las transiciones.

## Fecha de Implementación
27 de diciembre de 2025

