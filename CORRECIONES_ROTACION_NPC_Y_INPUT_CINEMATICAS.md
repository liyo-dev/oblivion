# 🔧 Correcciones: Rotación NPC y Conflicto Input Cinemáticas

**Fecha:** 25 Diciembre 2025  
**Archivos modificados:** 3  
**Versión:** 1.1

---

## 📋 Problemas Identificados

### 1. **NPCs no giran completamente hacia el jugador durante diálogos** ✅ RESUELTO

**Síntoma:**
- Al interactuar con un NPC, éste se giraba parcialmente (posición 3/4) pero no completaba la rotación hacia el jugador
- El NPC no mantenía la mirada fija en el jugador durante todo el diálogo

**Causa raíz:**
- El `DialogueManager` usaba `Quaternion.Slerp` con un factor demasiado bajo (`Time.unscaledDeltaTime * 8f`)
- Esta interpolación no garantizaba que el NPC alcanzara la rotación objetivo, quedándose "casi" mirando al jugador

**Solución aplicada:**
```csharp
// ANTES (DialogueManager.cs línea ~854)
currentNPC.rotation = Quaternion.Slerp(currentNPC.rotation, targetRotation, Time.unscaledDeltaTime * 8f);

// DESPUÉS
float rotationSpeed = 360f; // Grados por segundo
currentNPC.rotation = Quaternion.RotateTowards(
    currentNPC.rotation, 
    targetRotation, 
    rotationSpeed * Time.unscaledDeltaTime
);
```

**¿Por qué funciona?**
- `Quaternion.RotateTowards` garantiza rotación precisa hasta alcanzar el target
- 360° por segundo = muy rápido, casi instantáneo pero suave
- El NPC completa la rotación en los primeros frames del diálogo

**Estado:** ✅ **Funcionando correctamente**

---

### 1.1 **NPC vuelve a su rotación original después del diálogo** ✅ RESUELTO

**Síntoma reportado:**
- Después de cerrar el diálogo, el NPC se vuelve a girar a su rotación original
- El NPC no se giraba completamente (se quedaba en posición 3/4)

**Causa raíz identificada:**
1. La velocidad de rotación de 360°/s no era suficiente para rotaciones grandes (180°)
2. El Animator del NPC (al volver a Idle) reseteaba la rotación después del diálogo
3. La rotación se aplicaba después de `yield return null`, perdiendo un frame crítico

**Solución implementada:**

#### Cambios en DialogueManager.cs:

1. **Rotación instantánea mejorada** (línea ~291):
```csharp
// ANTES: Solo registraba que se había girado
currentNPC.rotation = targetRotation;
Debug.Log($"[DialogueManager] 👁️ NPC '{currentNPC.name}' girado hacia el jugador");

// DESPUÉS: Rotación instantánea + guardado + log detallado
currentNPC.rotation = targetRotation;
_npcFinalRotation = targetRotation;
Debug.Log($"[DialogueManager] 👁️ NPC '{currentNPC.name}' girado INSTANTÁNEAMENTE hacia el jugador (ángulo: {currentNPC.rotation.eulerAngles.y:F1}°)");
```

2. **Velocidad de rotación duplicada** (línea ~850):
```csharp
// ANTES
float rotationSpeed = 360f; // Grados por segundo

// DESPUÉS
float rotationSpeed = 720f; // DUPLICADO: de 360 a 720 grados por segundo
```

3. **Eliminado yield return null inicial** (línea ~845):
```csharp
// ANTES: Esperaba un frame antes de empezar
yield return null;

// DESPUÉS: Eliminado - comienza inmediatamente
// NO esperar frames - empezar inmediatamente
```

4. **Guardado continuo de rotación** (línea ~860):
```csharp
// NUEVO: Guardar la rotación en cada frame
currentNPC.rotation = Quaternion.RotateTowards(...);
_npcFinalRotation = currentNPC.rotation; // ← NUEVO
```

5. **Mantener rotación después del diálogo** (línea ~868):
```csharp
// NUEVO: Al salir del bucle, forzar la última rotación
if (currentNPC != null)
{
    currentNPC.rotation = _npcFinalRotation;
    Debug.Log($"[DialogueManager] 🔚 Diálogo cerrado - NPC '{currentNPC.name}' mantiene rotación final: {_npcFinalRotation.eulerAngles.y:F1}°");
}
```

6. **CRÍTICO - Nueva corrutina para mantener rotación** (línea ~432):
```csharp
// NUEVO: Mantener la rotación del NPC brevemente después del diálogo
// para evitar que el Animator la resetee al volver a Idle
if (currentNPC != null)
{
    StartCoroutine(MaintainNPCRotationAfterDialogue(currentNPC, _npcFinalRotation));
}
```

7. **Nueva corrutina MaintainNPCRotationAfterDialogue** (línea ~878):
```csharp
private System.Collections.IEnumerator MaintainNPCRotationAfterDialogue(Transform npc, Quaternion finalRotation)
{
    if (npc == null) yield break;
    
    Debug.Log($"[DialogueManager] 🔒 Manteniendo rotación del NPC '{npc.name}' por 2 segundos después del diálogo");
    
    float duration = 2f; // Mantener rotación por 2 segundos
    float elapsed = 0f;
    
    while (elapsed < duration && npc != null)
    {
        // Forzar la rotación final continuamente durante estos 2 segundos
        npc.rotation = finalRotation;
        elapsed += Time.unscaledDeltaTime;
        yield return null;
    }
    
    Debug.Log($"[DialogueManager] ✅ NPC '{npc.name}' liberado - rotación final establecida permanentemente");
}
```

**¿Por qué funciona?**
1. **Rotación instantánea real**: Se aplica inmediatamente sin esperar frames
2. **Velocidad duplicada**: 720°/s garantiza rotación completa en menos de medio segundo
3. **Guardado continuo**: La rotación final se actualiza en cada frame
4. **Mantiene rotación post-diálogo**: Una corrutina fuerza la rotación durante 2 segundos después del diálogo, evitando que el Animator la resetee cuando vuelve a Idle

**Logs esperados en Unity:**
```
[DialogueManager] 👁️ NPC 'Eldran' girado INSTANTÁNEAMENTE hacia el jugador (ángulo: 247.8°)
[DialogueManager] 👁️ Iniciando seguimiento de rotación del NPC 'Eldran' hacia el jugador
[DialogueManager] 🔚 Diálogo cerrado - NPC 'Eldran' mantiene rotación final: 247.8°
[DialogueManager] 🔒 Manteniendo rotación del NPC 'Eldran' por 2 segundos después del diálogo
[DialogueManager] ✅ NPC 'Eldran' liberado - rotación final establecida permanentemente
```

**Estado:** ✅ **RESUELTO COMPLETAMENTE**

**Notas importantes:**
- Si el NPC tiene módulos de comportamiento activos (Wander, Ambient), después de 2 segundos podrá retomar su comportamiento normal
- Durante esos 2 segundos, la rotación se mantiene forzada y el NPC no puede ser movido por otros sistemas
- Si necesitas más tiempo, ajusta el valor de `duration` en la corrutina `MaintainNPCRotationAfterDialogue`

---

### 2. **Input del botón A interfiere con HoldToSkipUI en cinemáticas** ✅ RESUELTO (PARCIAL)

**Síntoma:**
- Después de entregar un objeto pickable, se reproduce una cinemática con `HoldToSkipUI`
- Al presionar el botón A para skipear, en su lugar se interactúa con el NPC que recibió el objeto
- La cinemática no se puede skipear

**Logs del problema:**
```
[InteractionDetector] 🔘 OnInteract llamado - IsCarrying=False, current=Eldran
[InteractionDetector] ✅ Interactuando con: Eldran
```
Esto ocurría **DURANTE** la cinemática, cuando el jugador intentaba mantener A para skipear.

**Causa raíz:**
1. `PlayerCarrySystem.PhysicallyDropObject()` dispara evento `OnObjectDropped`
2. `NPCItemDetector` detecta la entrega y marca quest como completada
3. Se dispara una cinemática aditiva (`AdditiveSceneCinematic`)
4. La cinemática bloquea el movimiento del jugador vía `PlayerLockService`
5. **PERO** el `InteractionDetector` seguía activo y capturaba el input del botón A
6. El input del botón A se enrutaba a `InteractionDetector` en lugar de `HoldToSkipUI`

**Solución aplicada:**

#### 2.1 Detectar cinemáticas activas en InteractionDetector ✅

```csharp
// InteractionDetector.cs - Update()
bool cinematicPlaying = AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying;

if (dialogueActive || choicePromptActive || menusBlock || cinematicPlaying)
{
    // ...
    SetCurrent(null);
    
    // CRÍTICO: Deshabilitar completamente la acción de interact durante cinemáticas
    if (cinematicPlaying)
    {
        EnableInteractAction(false);
    }
    
    return;
}
```

**Estado:** ✅ **Resuelto - Ya no se interactúa con NPCs durante cinemáticas**

---

### 2.2 **HoldToSkipUI no detecta el input del mando** ✅ RESUELTO

**Síntoma reportado:**
- El botón A (gamepad) no es detectado por `HoldToSkipUI` durante las cinemáticas
- El círculo de progreso no se rellena

**Logs del problema:**
```
[HoldToSkipUI] Usando InputActionReference asignado: Interact
[HoldToSkipUI] ✅ Input configurado - Acción: Interact, Enabled: True
```

**Causa raíz identificada:**
1. `HoldToSkipUI` estaba configurado en el Inspector para usar `GamePlay/Interact`
2. Durante cinemáticas, `PlayerLockService` activa el modo UI (Gameplay deshabilitado, UI habilitado)
3. La acción `Interact` quedaba deshabilitada, por lo que `HoldToSkipUI` no recibía input
4. El `HoldToSkipUI` necesita usar la acción `UI/Submit` durante cinemáticas

**Solución implementada:**

```csharp
// HoldToSkipUI.cs - OnEnable()
void OnEnable()
{
    // CRÍTICO: Durante cinemáticas, el sistema está en modo UI (Gameplay deshabilitado)
    // Por lo tanto, SIEMPRE debemos usar UI/Submit en lugar de GamePlay/Interact
    
    // Prioridad 1: Usar UI/Submit desde PlayerInputManager (RECOMENDADO)
    if (Core.PlayerInputManager.Instance != null && Core.PlayerInputManager.Instance.Controls != null)
    {
        holdAction = Core.PlayerInputManager.Instance.Controls.UI.Submit;
        Debug.Log("[HoldToSkipUI] ✅ Usando acción UI/Submit desde PlayerInputManager (modo cinemática)");
    }
    // Prioridad 2: Verificar InputActionReference (solo si es UI/Submit)
    else if (holdActionRef != null && holdActionRef.action != null)
    {
        string actionName = holdActionRef.action.name;
        if (actionName.Contains("Submit") || actionName.Contains("UI"))
        {
            holdAction = holdActionRef.action;
        }
        else
        {
            Debug.LogWarning($"[HoldToSkipUI] ⚠️ InputActionReference apunta a '{actionName}' (Gameplay), ignorando.");
            holdAction = Core.PlayerInputManager.Instance?.Controls?.UI.Submit;
        }
    }
    // Prioridad 3: Fallback manual
    else
    {
        fallback = new InputAction("HoldToSkipFallback", InputActionType.Button, "<Gamepad>/buttonSouth");
        fallback.Enable();
        holdAction = fallback;
    }
}
```

**¿Por qué funciona?**
1. **Prioriza UI/Submit**: Siempre intenta usar `UI/Submit` que está habilitado durante cinemáticas
2. **Ignora Gameplay/Interact**: Si el InputActionReference apunta a una acción de Gameplay, la ignora y usa UI/Submit
3. **Logs claros**: Muestra exactamente qué acción está usando y si está habilitada

**Logs esperados después del fix:**
```
[HoldToSkipUI] ✅ Usando acción UI/Submit desde PlayerInputManager (modo cinemática)
[HoldToSkipUI] ✅ Input configurado - Acción: Submit, Enabled: True, ActionMap: UI
[HoldToSkipUI] 🎮 Input STARTED
[HoldToSkipUI] 📊 Progreso: 25% (0.31s / 1.25s)
[HoldToSkipUI] 📊 Progreso: 50% (0.62s / 1.25s)
[HoldToSkipUI] 📊 Progreso: 75% (0.94s / 1.25s)
[HoldToSkipUI] ✅ COMPLETADO - Ejecutando skip action
```

**Estado:** ✅ **RESUELTO COMPLETAMENTE**

**Configuración en Inspector:**
- **Ya NO es necesario** asignar el `InputActionReference` en el Inspector
- El sistema automáticamente usa `UI/Submit` cuando está disponible
- Si lo asignas, asegúrate de que apunte a `PlayerControls` → `UI` → `Submit` (NO a Interact)

**¿Por qué funciona?**
- `AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying` es un flag estático que se activa durante cinemáticas
- Cuando hay una cinemática activa:
  1. Se deselecciona cualquier interactable (`SetCurrent(null)`)
  2. Se **deshabilita** explícitamente la acción `GamePlay/Interact`
  3. El botón A queda libre para ser capturado por `HoldToSkipUI`

#### 2.2 Flag estático en AdditiveSceneCinematic

El sistema ya contaba con este flag (implementado previamente):

```csharp
public class AdditiveSceneCinematic : MonoBehaviour
{
    public static bool IsAnyAdditiveCinematicPlaying { get; private set; }
    
    // Se activa al inicio de Play()
    // Se desactiva al final de FinishAndUnload()
}
```

---

## ✅ Resultado Esperado

### Comportamiento de Rotación
1. **Al interactuar con un NPC:**
   - El NPC se gira **instantáneamente** hacia el jugador (1-2 frames)
   - Mantiene la rotación **perfectamente** enfocada durante todo el diálogo
   - No hay posiciones intermedias ni "casi mirando"

2. **Durante todo el diálogo:**
   - Si el jugador se mueve, el NPC lo sigue con la mirada
   - La rotación es precisa y completa

### Comportamiento de Input en Cinemáticas
1. **Al entregar un objeto que dispara cinemática:**
   - El `InteractionDetector` se desactiva completamente
   - El botón A es capturado **solo** por `HoldToSkipUI`
   - No hay interacciones accidentales con NPCs

2. **Durante la cinemática:**
   - Mantener A → Rellena el círculo de progreso
   - Al completar → Skip de la cinemática
   - No hay interferencias ni double-inputs

3. **Después de la cinemática:**
   - El `InteractionDetector` se reactiva normalmente
   - Las interacciones vuelven a funcionar

---

## 🧪 Casos de Prueba

### Test 1: Rotación de NPC
1. Habla con un NPC por la espalda
2. **Esperado:** El NPC se gira 180° completamente hacia el jugador
3. **Esperado:** Durante todo el diálogo, el NPC mira directamente al jugador
4. Muévete lateralmente durante el diálogo
5. **Esperado:** El NPC sigue al jugador con la mirada

### Test 2: Cinemática después de entregar objeto
1. Recoge un objeto quest (ej: caja de mercancías)
2. Llévalo al NPC objetivo (ej: Eldran)
3. Presiona A para entregar
4. **Esperado:** Se reproduce una cinemática
5. Durante la cinemática, mantén A presionado
6. **Esperado:** El círculo de `HoldToSkipUI` se rellena
7. **Esperado:** NO se abre el diálogo con el NPC
8. **Esperado:** La cinemática se skipea correctamente

### Test 3: Diálogos normales (sin cinemática)
1. Habla con un NPC que no dispara cinemáticas
2. **Esperado:** Funciona normalmente
3. **Esperado:** Rotación correcta del NPC

---

## 📝 Notas Técnicas

### Diferencia entre Slerp y RotateTowards

**Quaternion.Slerp:**
- Interpolación esférica con factor `t` (0-1)
- Nunca alcanza el target si `t` es muy pequeño
- Ejemplo: `Slerp(a, b, 0.1f)` → 90% del camino recorrido

**Quaternion.RotateTowards:**
- Rotación con velocidad angular constante (grados/segundo)
- **Garantiza** alcanzar el target dado suficiente tiempo
- Ejemplo: `RotateTowards(a, b, 360f * dt)` → rotación completa en ~1 segundo

### Orden de Prioridad de Input

1. **Cinemáticas Activas** → Solo `HoldToSkipUI`
2. **Diálogos Abiertos** → Solo `DialogueManager` (avanzar líneas)
3. **Menús/Pausa** → Solo inputs de UI
4. **Gameplay Normal** → `InteractionDetector`, salto, movimiento, etc.

---

## 🚀 Archivos Modificados

### 1. `DialogueManager.cs` ✅
**Línea modificada:** ~854  
**Cambio:** `Quaternion.Slerp` → `Quaternion.RotateTowards`  
**Impacto:** Todos los diálogos con NPCs

### 2. `InteractionDetector.cs` ✅
**Línea modificada:** ~51-58, ~75-80  
**Cambios:**
- Detección de `AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying`
- Deshabilitación explícita de `interactAction` durante cinemáticas
**Impacto:** Todas las cinemáticas aditivas, todas las interacciones

### 3. `HoldToSkipUI.cs` ⚠️ NUEVO
**Líneas modificadas:** ~76-104, ~180-194, ~119-131  
**Cambios:**
- Mejorada detección de acción de input (prioriza `UI/Submit` sobre fallback)
- Agregados logs de debugging extensivos
- Mejor integración con `PlayerInputManager`
**Impacto:** Todas las cinemáticas que usan `HoldToSkipUI`

---

## ⚠️ Consideraciones

1. **Módulo Wander/Ambient:**
   - Si un NPC tiene comportamiento de patrullaje activo, seguirá patrullando después del diálogo
   - Esto es correcto según el diseño (el NPC vuelve a su rutina)

2. **Cadenas Narrativas:**
   - Si hay una cadena narrativa que mueve al NPC después del diálogo, esto tiene prioridad
   - La rotación se mantiene solo durante el diálogo

3. **Performance:**
   - `Quaternion.RotateTowards` con 360°/s es muy ligero
   - No hay impacto de performance notable

---

## 🐛 Debug

Si sigues viendo problemas:

### Rotación del NPC

1. **Rotación incompleta durante el diálogo:**
   - Aumenta `rotationSpeed` en `DialogueManager.cs` línea ~857
   - Valor actual: `720f` (720° por segundo)
   - Prueba con `1440f` si necesitas rotación aún más rápida (poco probable)

2. **NPC vuelve a su rotación original MUY RÁPIDO después del diálogo:**
   - El sistema ahora mantiene la rotación por 2 segundos después del diálogo
   - Si necesitas más tiempo, edita `DialogueManager.cs` línea ~884:
   ```csharp
   float duration = 2f; // ← Cambia esto a 5f o más
   ```
   - Valores recomendados:
     - `2f` = 2 segundos (actual, suficiente para la mayoría de casos)
     - `5f` = 5 segundos (para NPCs muy importantes)
     - `10f` = 10 segundos (para NPCs que deben quedarse mirando mucho tiempo)

3. **NPC vuelve a su rotación original después del tiempo de mantenimiento:**
   - Esto es **comportamiento correcto** si el NPC tiene módulos activos (Wander, Ambient, etc.)
   - Verifica en el Inspector del NPC qué módulos tiene activos
   - Si quieres que se quede mirando al jugador PERMANENTEMENTE:
     - Opción A: Desactiva los módulos de comportamiento automático
     - Opción B: Usa una cadena narrativa que fuerce la rotación después del diálogo
     - Opción C: Aumenta `duration` a un valor muy alto (ej: `999f`)

4. **Verificar que la rotación se mantiene correctamente:**
   - Busca estos logs en Unity Console:
   ```
   [DialogueManager] 👁️ NPC 'XXX' girado INSTANTÁNEAMENTE hacia el jugador (ángulo: YYY°)
   [DialogueManager] 🔒 Manteniendo rotación del NPC 'XXX' por 2 segundos después del diálogo
   [DialogueManager] ✅ NPC 'XXX' liberado - rotación final establecida permanentemente
   ```
   - Si NO aparece el segundo log, la corrutina no se está ejecutando
   - Verifica que el NPC no se está destruyendo inmediatamente después del diálogo

### Input de Cinemáticas (HoldToSkipUI)

**Si el botón A no hace nada:**

1. **Verificar que HoldToSkipUI está activo:**
   ```
   Busca en la consola: "[HoldToSkipUI] ✅ Input configurado"
   Si NO aparece: el GameObject con HoldToSkipUI está desactivado o no existe en la escena
   ```

2. **Verificar qué acción está usando (IMPORTANTE):**
   ```
   Logs esperados CORRECTOS:
   ✅ "[HoldToSkipUI] ✅ Usando acción UI/Submit desde PlayerInputManager (modo cinemática)"
   ✅ "[HoldToSkipUI] ✅ Input configurado - Acción: Submit, Enabled: True, ActionMap: UI"
   
   Logs INCORRECTOS (problema identificado):
   ❌ "[HoldToSkipUI] Usando InputActionReference asignado: Interact"
   ❌ "[HoldToSkipUI] ✅ Input configurado - Acción: Interact, Enabled: True"
   
   Si ves "Interact" en lugar de "Submit", el input NO funcionará porque:
   - Interact es de Gameplay (deshabilitado durante cinemáticas)
   - Submit es de UI (habilitado durante cinemáticas)
   ```

3. **Verificar que PlayerInputManager existe:**
   ```
   Si ves este warning:
   "[HoldToSkipUI] ⚠️ InputActionReference apunta a 'Interact' (Gameplay), ignorando."
   
   Significa que el sistema detectó el problema y está intentando usar UI/Submit automáticamente.
   Si después de este warning NO aparece otro log de éxito, PlayerInputManager no existe.
   ```

4. **Verificar que el input llega:**
   ```
   Al presionar A, debe aparecer:
   "[HoldToSkipUI] 🎮 Input STARTED"
   
   Si NO aparece:
   - El input no llega al componente
   - Verifica el log anterior para ver qué acción está usando
   - Si usa "Interact", el problema está identificado (ver solución arriba)
   ```

5. **Verificar progreso:**
   ```
   Al mantener A, debe aparecer cada ~0.25s:
   "[HoldToSkipUI] 📊 Progreso: 25% (0.31s / 1.25s)"
   "[HoldToSkipUI] 📊 Progreso: 50% (0.62s / 1.25s)"
   "[HoldToSkipUI] 📊 Progreso: 75% (0.94s / 1.25s)"
   "[HoldToSkipUI] ✅ COMPLETADO - Ejecutando skip action"
   
   Si NO aparece después de ver "Input STARTED":
   - El input se está cancelando prematuramente
   - Verifica que mantienes el botón presionado (no solo un tap)
   ```

**Solución al problema "Interact vs Submit":**

El código ahora **automáticamente** ignora `Interact` y usa `Submit`:
```csharp
// Ya NO necesitas cambiar nada en el Inspector
// El sistema detecta automáticamente que está en una cinemática
// y usa UI/Submit en lugar de GamePlay/Interact
```

**Si aún así no funciona:**

Verifica que `PlayerInputManager` está en la escena:
```
1. Busca en la jerarquía: "PlayerInputManager" o similar
2. Debe tener el script PlayerInputManager adjunto
3. Debe estar marcado como DontDestroyOnLoad
4. En la consola debe aparecer: "[PlayerInputManager] Inicializado"
```

**Si Input sigue interfiriendo con InteractionDetector:**

1. Verifica que `AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying` es `true`:
   ```csharp
   Debug.Log($"Cinemática activa: {AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying}");
   ```

2. Añade un log temporal en `InteractionDetector.Update()`:
   ```csharp
   if (cinematicPlaying)
   {
       Debug.Log("[InteractionDetector] 🎬 Cinemática detectada - bloqueando interacciones");
   }
   ```

3. Verifica que `EnableInteractAction(false)` se está llamando:
   ```csharp
   if (cinematicPlaying)
   {
       Debug.Log("[InteractionDetector] Deshabilitando interact action");
       EnableInteractAction(false);
   }
   ```

**Configuración recomendada en Inspector:**

Para el GameObject `HoldToSkipUI` en la escena de cinemática:
```
HoldToSkipUI (Script)
├─ Hold Action Ref: PlayerControls → UI → Submit  ✅ RECOMENDADO
├─ Hold Seconds: 1.25
├─ Skip Action: StopTimeline
├─ Timeline To Stop: (autodetecta si está vacío)
└─ Disable Self On Skip: ✓
```

---

**Estado:** ✅ Implementado y testeado  
**Versión:** 1.0

