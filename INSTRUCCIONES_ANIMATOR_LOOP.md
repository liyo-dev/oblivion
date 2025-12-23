# 🔧 INSTRUCCIONES DE CONFIGURACIÓN - Animator Controller

## Problema: Idle_Battle_NoWeapon no hace loop

La animación `Idle_Battle_NoWeapon` se reproduce una vez y luego el NPC vuelve a `Idle_Normal_NoWeapon`. Esto sucede porque la animación no está configurada para hacer loop en el Animator Controller.

## Solución:

### Opción 1: Configurar Loop en el Animator Controller (RECOMENDADO)

1. Abre el **Animator Controller** del NPC:
   - Ruta: `Assets/Art/Characters/Animator/NPC_NoWeapon.controller`
   - O selecciona el NPC en la escena → Inspector → Animator component → Controller

2. En la ventana **Animator**, localiza el estado `Idle_Battle_NoWeapon`

3. Selecciona el estado `Idle_Battle_NoWeapon` (click en él)

4. En el **Inspector**, busca la sección **Motion**

5. Verifica estas configuraciones:
   - **Loop Time**: ✅ DEBE ESTAR ACTIVADO (checkbox marcado)
   - **Loop Pose**: ✅ Recomendado activar también
   - **Cycle Offset**: 0

6. Si el estado tiene transiciones automáticas de salida:
   - Revisa las **Transitions** desde `Idle_Battle_NoWeapon`
   - Elimina o desactiva transiciones automáticas a otros estados (como a `Locomotion` o `Idle_Normal`)
   - Las únicas transiciones válidas deberían ser:
     - **Cuando hay movimiento** (InputMagnitude > 0.1) → Locomotion
     - **Cuando sale de batalla** (parámetro custom o desde código)

### Opción 2: Configurar Loop en el Animation Clip

Si la Opción 1 no funciona, configura el loop directamente en el clip:

1. Localiza el **Animation Clip** original:
   - Busca en Project: `Idle_Battle_NoWeapon` (probablemente en `Assets/Art/Characters/Animations/`)

2. Selecciona el clip en el Project

3. En el **Inspector**, ve a la pestaña de **Import Settings**

4. En la sección **Animation**:
   - **Loop Time**: ✅ ACTIVAR
   - **Loop Pose**: ✅ ACTIVAR (opcional, mejora el blend)

5. Click en **Apply** al final del Inspector

6. Vuelve al Animator Controller y verifica que el estado ahora haga loop

## Verificación:

1. Entra en **Play Mode**
2. Acércate al NPC para iniciar combate
3. Una vez termine el diálogo de alerta, el NPC debe:
   - Reproducir `Challenging_NoWeapon` (una vez)
   - Luego pasar a `Idle_Battle_NoWeapon` (LOOP infinito)
   - Mantenerse en `Idle_Battle_NoWeapon` mientras esté quieto en combate
   - NO volver a `Idle_Normal_NoWeapon` hasta que acabe el combate

## Configuración adicional recomendada:

### States que deben hacer LOOP:
- ✅ `Idle_Normal_NoWeapon`
- ✅ `Idle_Battle_NoWeapon`
- ✅ `Free Locomotion` (Blend Tree)
- ✅ `UpperIdle` (en capa UpperBody)

### States que NO deben hacer loop (One-Shot):
- ❌ `Challenging_NoWeapon`
- ❌ `GetHit02_NoWeapon`
- ❌ `Die02_NoWeapon`
- ❌ `Greeting01_NoWeapon`
- ❌ `InteractWithPeople_NoWeapon`
- ❌ Todos los estados de `Magic` y `PickUp`

## Notas importantes:

- **Loop Time** = La animación se repite indefinidamente
- **Loop Pose** = Suaviza la transición entre el final y el inicio del loop
- Si la animación tiene keyframes que no se alinean bien al final, es posible que veas un "salto" visual incluso con Loop Time activado. En ese caso, el artista debe ajustar el clip original.

---

## 🔍 Debug en caso de problemas:

Si después de activar Loop Time el NPC aún vuelve a Idle_Normal:

1. Revisa las **Transitions** en el Animator:
   ```
   Idle_Battle_NoWeapon → [¿Hay transición automática?]
   ```

2. Busca condiciones como:
   - `Has Exit Time = true` → Desactivar
   - `Transition Duration` muy larga → Reducir a 0.15-0.2s
   - Condiciones extra que no deberían existir

3. Verifica en el código que `_isInBattle` se mantiene en `true`:
   - Activa `debugMode` en `NPCSimpleAnimator`
   - Observa los logs: `[NPCAnimator] Battle mode: True`

4. Si el NPC se mueve ligeramente (NavMeshAgent activo), podría activar transiciones:
   - Asegúrate de que el NavMeshAgent esté detenido durante batalla
   - O aumenta el `movementThreshold` para que no detecte micro-movimientos

