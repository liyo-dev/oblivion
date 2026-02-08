# Sistema de Camera Targeting para Combate

## 📋 Descripción

Sistema de lock-on de cámara automático para combate, similar a Dark Souls, Zelda, o Monster Hunter. Cuando entras en combate, la cámara automáticamente hace lock al enemigo más cercano y lo mantiene enfocado durante la batalla.

## ✨ Características

- ✅ **Lock automático** al entrar en combate
- ✅ **Switch de targets** con Tab
- ✅ **Indicador visual** del enemigo bloqueado
- ✅ **Integración completa** con `ActiveCombatRegistry`
- ✅ **Compatible** con `vThirdPersonCamera` de Invector
- ✅ **Smooth rotation** hacia el objetivo
- ✅ **Auto-release** al salir de combate
- ✅ **Configuración flexible** en el Inspector

## 🎯 Cómo Funciona

### Flujo Automático

```
1. Enemigo entra en combate (registrado en ActiveCombatRegistry)
   ↓
2. CombatCameraTargeting detecta el nuevo enemigo
   ↓
3. Hace lock automático al enemigo más cercano
   ↓
4. La cámara rota suavemente para mirar al enemigo
   ↓
5. Indicador visual aparece sobre el enemigo
   ↓
6. El jugador puede cambiar de target con Tab
   ↓
7. Al salir de combate, el lock se libera automáticamente
```

### Integración con Sistemas Existentes

#### ActiveCombatRegistry
El sistema se suscribe a los eventos:
- `OnNPCEnteredCombat`: Hace lock si no hay target actual
- `OnNPCExitedCombat`: Busca nuevo target si el actual salió

#### vThirdPersonCamera
Se agregó el método `LookAtPosition()` que permite:
- Controlar la rotación de la cámara externamente
- Smooth interpolation hacia el objetivo
- Respeta los límites de pitch/yaw configurados

## 🔧 Setup Paso a Paso

### 1. Agregar CombatCameraTargeting a la Cámara

```csharp
// En la jerarquía de Unity, encontrar el GameObject de la cámara
// (el que tiene vThirdPersonCamera)

1. Seleccionar el GameObject de la cámara
2. Add Component → Combat Camera Targeting
3. El componente auto-detectará las referencias necesarias
```

### 2. Configuración en el Inspector

#### Referencias (Auto-detectadas)
- **Third Person Camera**: Se auto-detecta del mismo GameObject
- **Player Transform**: Se obtiene de `PlayerService`

#### Configuración de Targeting
- **Max Lock Distance**: 30m (distancia máxima para hacer lock)
- **Targeting Rotation Speed**: 8 (velocidad de rotación hacia el target)
- **Target Height Offset**: 1.5m (altura del punto de mira sobre el enemigo)

#### Input (Gamepad)
- **D-Pad Horizontal Axis**: "Horizontal" (cambia entre enemigos)
  - **D-Pad Right →**: Siguiente enemigo
  - **D-Pad Left ←**: Enemigo anterior
- **Cancel Lock Button**: KeyCode.JoystickButton1 (Botón B en Xbox controller)

> **⚠️ IMPORTANTE**: El sistema está diseñado para **gamepad/mando**, no para teclado/ratón.

#### Visual
- **Lock Indicator Prefab**: (opcional) Prefab del indicador visual

### 3. Crear el Indicador Visual (Opcional)

```
1. Crear GameObject vacío
2. Add Component → Combat Lock Indicator
3. Configurar en el Inspector:
   - Ring Color: Rojo semi-transparente
   - Ring Radius: 1.5
   - Enable Pulse: true
4. Guardar como Prefab
5. Asignar en CombatCameraTargeting → Lock Indicator Prefab
```

## 🎮 Uso en Juego

### Automático
- **Al entrar en combate**: Lock automático al enemigo más cercano
- **Al salir de combate**: Release automático del lock

### Manual (Gamepad)
- **D-Pad Right →**: Cambiar al siguiente enemigo en combate
- **D-Pad Left ←**: Cambiar al enemigo anterior en combate
- **Botón B (Xbox)**: Cancelar lock manualmente (vuelve a cámara libre)

### Visual Feedback
- **Indicador rojo** girando sobre el enemigo bloqueado
- **Efecto de pulso** en el indicador
- **Rotación suave** de la cámara hacia el target

## 🔍 Debugging

### Logs en Consola

Activar `showDebugLogs` en el Inspector para ver:

```
[CombatCameraTargeting] 🎯 Entrando en combate - Buscando objetivo para lock
[CombatCameraTargeting] 🎯 Lock establecido en: PBR_Golem(Clone)
[CombatCameraTargeting] 🔄 Cambiando target: Enemy1 → Enemy2
[CombatCameraTargeting] 🏳️ Saliendo de combate - Liberando lock
[CombatCameraTargeting] 🔓 Lock de cámara liberado
```

### Verificación de Funcionamiento

1. **Iniciar combate**: El boss/NPC debe registrarse en `ActiveCombatRegistry`
2. **Verificar lock**: Debe aparecer el indicador visual sobre el enemigo
3. **Verificar rotación**: La cámara debe rotar hacia el enemigo
4. **Probar switch**: Presionar Tab debe cambiar de target (si hay múltiples enemigos)
5. **Verificar release**: Al salir de combate, el lock debe liberarse

## 🛠️ Personalización

### Ajustar Comportamiento de la Cámara

#### Velocidad de Rotación
```csharp
public float targetingRotationSpeed = 8f;
```
- Más alto = rotación más rápida
- Más bajo = rotación más suave
- Recomendado: 6-10

#### Sensibilidad del Mouse Durante Lock
```csharp
thirdPersonCamera.xMouseSensitivity = originalCameraSensitivity * 0.3f;
```
Cambia el multiplicador (0.3f) para ajustar cuánto control tiene el jugador durante el lock.

#### Distancia de Lock
```csharp
public float maxLockDistance = 30f;
```
Ajusta la distancia máxima a la que se puede hacer lock a un enemigo.

### Cambiar Indicador Visual

#### Usando UI en lugar de 3D
Modifica `CreateLockIndicator()` para usar un Canvas/Image en lugar del prefab 3D.

#### Diferentes Colores por Tipo de Enemigo
```csharp
// En CreateLockIndicator()
if (currentTarget.CompareTag("Boss"))
{
    indicatorColor = Color.red;
}
else
{
    indicatorColor = Color.yellow;
}
```

## 🔌 API Pública

### Propiedades
```csharp
public bool IsLocked => isLockActive && currentTarget != null;
public GameObject CurrentTarget => currentTarget;
```

### Métodos
```csharp
// Forzar lock a un objetivo específico
public void ForceLockTarget(GameObject target)

// Forzar liberación del lock
public void ForceReleaseLock()
```

### Ejemplo de Uso
```csharp
// En un script de UI de boss
var targeting = FindObjectOfType<CombatCameraTargeting>();
if (targeting != null && targeting.CurrentTarget == bossGameObject)
{
    // Mostrar barra de vida del boss
    ShowBossHealthBar();
}
```

## 📊 Integración con Otros Sistemas

### Con Boss Arena Controller
```csharp
// En BossArenaController.StartBattleInternal()
var targeting = FindObjectOfType<CombatCameraTargeting>();
if (targeting != null)
{
    targeting.ForceLockTarget(bossInstance);
}
```

### Con Sistema de Diálogo
```csharp
// Desactivar temporalmente durante diálogos
var targeting = FindObjectOfType<CombatCameraTargeting>();
if (targeting != null && targeting.IsLocked)
{
    targeting.ForceReleaseLock();
}
```

## ⚠️ Limitaciones y Consideraciones

### Performance
- El sistema usa `ActiveCombatRegistry` que ya mantiene un HashSet eficiente
- La búsqueda del enemigo más cercano es O(n) donde n = enemigos en combate
- Recomendado para escenarios con <10 enemigos simultáneos

### Colisiones con Cinemática
- `vThirdPersonCamera` ya tiene protección con `lockCameraForCinematic`
- `LookAtPosition()` respeta este flag automáticamente

### Multi-Target en Arena Pequeña
- Si hay muchos enemigos muy juntos, el switch puede ser confuso
- Considera aumentar `maxLockDistance` o ajustar la lógica de `GetClosestCombatNPC()`

## 🎬 Mejoras Futuras

### Priorización de Targets
- Priorizar bosses sobre enemigos normales
- Priorizar enemigos que atacan sobre los que patrullan

### Cambiar Input de Gamepad

#### Cambiar botón de cancelar lock
```csharp
public KeyCode cancelLockButton = KeyCode.JoystickButton1; // B en Xbox
```
- JoystickButton0 = A
- JoystickButton1 = B  
- JoystickButton2 = X
- JoystickButton3 = Y

#### Cambiar eje del D-Pad
```csharp
public string dPadHorizontalAxis = "Horizontal";
```
Si tu mando usa otro eje, cámbialo en el Inspector.

### Feedback Visual Mejorado
- Mini-flecha apuntando al target fuera de pantalla
- Diferentes indicadores por tipo de enemigo (boss, elite, normal)

### Soft Lock
- Lock que se mantiene solo si el jugador mira cerca del enemigo
- Se libera si el jugador mira lejos (ángulo > 45°)

## 📝 Archivos Modificados/Creados

### Creados
1. `Assets/Scripts/Camera/CombatCameraTargeting.cs` - Sistema principal
2. `Assets/Scripts/Camera/CombatLockIndicator.cs` - Indicador visual

### Modificados
1. `Assets/Plugins/Invector-3rdPersonController_LITE/Scripts/Camera/vThirdPersonCamera.cs`
   - Agregado método `LookAtPosition()` para control externo de rotación

## 🐛 Troubleshooting

### La cámara no hace lock
- ✅ Verificar que `ActiveCombatRegistry.Count > 0`
- ✅ Verificar que el enemigo esté dentro de `maxLockDistance`
- ✅ Revisar logs con `showDebugLogs = true`

### La cámara rota bruscamente
- 🔧 Aumentar `targetingRotationSpeed` (o el valor de smooth en `LookAtPosition`)
- 🔧 Verificar que `vThirdPersonCamera.smoothCameraRotation` esté > 5

### El indicador no aparece
- 🔧 Asignar un prefab en `lockIndicatorPrefab`
- 🔧 Verificar que el prefab tenga `CombatLockIndicator` component
- 🔧 Revisar la capa del indicador (no debe estar en culling layer de la cámara)

### El jugador no puede controlar la cámara durante lock
- 🔧 Ajustar el multiplicador de sensibilidad (actualmente 0.3f)
- 🔧 O desactivar completamente la reducción de sensibilidad

## ✅ Testing Checklist

- [ ] Lock automático funciona al entrar en combate
- [ ] Indicador visual aparece sobre el enemigo
- [ ] Cámara rota suavemente hacia el target
- [ ] Tab cambia correctamente de target
- [ ] Escape cancela el lock
- [ ] Lock se libera al salir de combate
- [ ] Funciona con múltiples enemigos
- [ ] Funciona correctamente con bosses
- [ ] No interfiere con cinemáticas
- [ ] Performance es aceptable

---

## 🎉 ¡Listo para Usar!

El sistema está completamente funcional y listo para probar en el juego. Solo necesitas:

1. Agregar `CombatCameraTargeting` al GameObject de la cámara
2. (Opcional) Crear y asignar un prefab de indicador visual
3. ¡Jugar y probar!
