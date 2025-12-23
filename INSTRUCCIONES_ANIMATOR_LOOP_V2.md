# 🔧 INSTRUCCIONES DE CONFIGURACIÓN - Sistema NPC Combat

## ❗ PROBLEMAS IDENTIFICADOS Y SOLUCIONES

### ✅ PROBLEMA 1: Bucle infinito de combate (SOLUCIONADO EN CÓDIGO)

**Síntoma:** El NPC vuelve a atacar después de ser derrotado indefinidamente.

**Causa:** El NPC, al ser derrotado, volvía a `IdleState` y **volvía a detectar al jugador**, iniciando un nuevo combate infinitamente.

**✅ SOLUCIÓN IMPLEMENTADA:**

1. **NPCCombatLifecycleHandler.cs**: Ahora marca correctamente `Context.WasDefeatedInCombat = true`
2. **IdleState.cs**: Verifica `WasDefeatedInCombat` antes de detectar al jugador
3. **AlertState.cs**: No permite entrar en alerta si el NPC fue derrotado
4. **CombatState.cs**: No inicia combate si el NPC fue derrotado

✅ **NO REQUIERE ACCIÓN MANUAL** - El código ya está corregido.

---

### ⚠️ PROBLEMA 2: Idle_Battle_NoWeapon no hace loop (REQUIERE CONFIGURACIÓN MANUAL)

**Síntoma:** La animación `Idle_Battle_NoWeapon` se reproduce una vez y el NPC vuelve a `Idle_Normal_NoWeapon`.

**Causa:** La animación NO tiene configurado el **Loop Time** en el Animator o en el Animation Clip.

**✅ SOLUCIÓN - OPCIÓN 1: Configurar Loop en el Animation Clip (RECOMENDADO)**

**Paso a Paso:**

1. **Localizar el Animation Clip:**
   - En la ventana **Project**, busca: `Idle_Battle_NoWeapon`
   - Ruta probable: `Assets/Art/Characters/Animations/` o similar
   - Es un archivo `.anim` o `.fbx` con animaciones embebidas

2. **Seleccionar el clip:**
   - Click en el archivo `.anim` o `.fbx`
   - Si es `.fbx`, expande la jerarquía (flecha) y selecciona la animación `Idle_Battle_NoWeapon`

3. **Configurar Loop en Inspector:**
   - Pestaña: **Animation** (o **Rig** si es `.fbx`)
   - En la sección **Clips** (si es `.fbx`), selecciona `Idle_Battle_NoWeapon`
   - Activa estas opciones:
     - ✅ **Loop Time** (checkbox)
     - ✅ **Loop Pose** (checkbox) - Opcional, mejora el blend
     - **Cycle Offset**: 0
   
4. **Aplicar cambios:**
   - Click en **Apply** (botón al final del Inspector)
   - Espera a que Unity recompile

5. **Verificar en el Animator:**
   - Abre el **Animator Controller**: `NPC_NoWeapon.controller`
   - Selecciona el estado `Idle_Battle_NoWeapon`
   - En el Inspector, verifica que **Loop Time** ahora está en ✅ (checkbox verde marcado)

**✅ SOLUCIÓN - OPCIÓN 2: Forzar Loop en el Animator State**

Si la Opción 1 no funciona (por ejemplo, animación compartida que no puedes modificar):

1. Abre el **Animator Controller**: `NPC_NoWeapon.controller`
2. Click en el estado `Idle_Battle_NoWeapon` (en Base Layer)
3. En el **Inspector**:
   - Busca la sección **Motion**
   - Verifica que **Loop Time** esté ✅ activado
   - Si no, márcalo manualmente

---

### ✅ PROBLEMA 2.1: Challenging_NoWeapon no se reproduce (SOLUCIONADO EN CÓDIGO)

**Síntoma:** La animación de Challenge pasa muy rápido (1 frame) y va directo a Idle de batalla, o no se ve en absoluto.

**Causa Raíz:**
- El **Animation Clip** tiene **Loop Time = OFF** (desactivado)
- El **Animator State** tiene **Exit Time = 0** o muy bajo
- El nombre del estado no coincide exactamente con el nombre del clip
- La duración del clip es muy corta (<1 segundo)

**✅ SOLUCIÓN IMPLEMENTADA EN CÓDIGO:**

Se mejoró el método `PlayChallengingForBattle()` en `NPCSimpleAnimator.cs` para:

1. **Validar duración mínima**: Si el clip es menor a 0.5s, usa 2 segundos como mínimo
2. **Logs detallados**: Muestra la duración del clip y si se encontró el estado en el Animator
3. **Esperar normalizedTime**: Espera hasta que la animación llegue al 90% antes de transicionar
4. **Coroutine dedicada**: Usa una coroutine especializada en lugar del método genérico `PlayOneShot`

✅ **NO REQUIERE ACCIÓN MANUAL EN CÓDIGO** - El código ya está corregido.

---

### ⚠️ PROBLEMA 2.2: Challenging_NoWeapon - Configuración del Animator State (REQUIERE CONFIGURACIÓN MANUAL)

**Aunque el código ahora fuerza la duración mínima, es importante configurar correctamente el Animator State:**

**Paso a Paso:**

1. **Abre el Animator Controller**: `NPC_NoWeapon.controller`
2. **Localiza el estado**: `Challenging_NoWeapon` (en Base Layer)
3. **Selecciona el estado** (click en él)
4. **En el Inspector, verifica/configura:**
   - ✅ **Motion**: Debe tener asignado el clip de animación correcto
   - ❌ **Loop Time**: Debe estar **DESACTIVADO** (es una animación one-shot)
   - ⚠️ **Speed**: 1 (normal)
   - ❌ **Mirror**: Desactivado (a menos que lo necesites)

5. **Configurar transiciones DESDE Challenging_NoWeapon:**
   - **Transición a Idle_Battle_NoWeapon**:
     - ✅ **Has Exit Time**: TRUE
     - ✅ **Exit Time**: 0.9 (90% del clip)
     - **Fixed Duration**: FALSE
     - **Transition Duration**: 0.15 - 0.25 segundos
     - **Interruption Source**: None
     - ❌ **NO añadir Conditions** (solo Exit Time)

6. **Verificar que NO haya transiciones automáticas a Idle_Normal:**
   - Si existe una transición desde `Challenging` a `Idle_Normal_NoWeapon`, **elimínala**
   - Solo debe haber transición a `Idle_Battle_NoWeapon`

---

### ✅ PROBLEMA 3: CapsuleCollider desactivado (SOLUCIONADO EN CÓDIGO)

**Síntoma:** El Interactable no funciona después del combate, no aparece el botón A.

**Causa:** El `CapsuleCollider` con `isTrigger = true` (usado por `Interactable`) estaba desactivado después del combate.

**✅ SOLUCIÓN IMPLEMENTADA:**

1. **CombatState.OnExit()**: Ahora activa TODOS los colliders trigger después del combate
2. Si el NPC fue derrotado, asegura que el collider principal sea trigger

✅ **NO REQUIERE ACCIÓN MANUAL** - El código ya está corregido.

---

## 🧪 VERIFICACIÓN Y DEBUG

### ✅ Verificar que el bucle infinito está solucionado:

1. **Entra en Play Mode**
2. **Acércate al NPC** para iniciar combate
3. **Derrota al NPC**
4. **Revisa la Console** - Debes ver estos logs:
   ```
   [NPCCombatLifecycleHandler:Boy_Pirate] ⚔️ NPC derrotado - Iniciando proceso de derrota
   [NPCCombatLifecycleHandler:Boy_Pirate] 🔍 _npcManager: ✅, Context: ✅
   [NPCCombatLifecycleHandler:Boy_Pirate] ✅ Context.WasDefeatedInCombat = true
   ```

5. **Si ves** `❌ NO SE PUDO ESTABLECER WasDefeatedInCombat - Context es null`:
   - El NPC NO tiene configurado correctamente el `NPCBehaviourManagerV2`
   - Verifica que el GameObject tiene el componente activo

6. **Después del diálogo de derrota**, acércate de nuevo:
   - ✅ El NPC NO debe volver a atacarte
   - ✅ NO debe aparecer el icono de alerta (!)
   - ✅ Debe quedar en `IdleState` pacíficamente

### ⚠️ Verificar que Challenging_NoWeapon se reproduce correctamente:

1. **Entra en Play Mode**
2. **Acércate al NPC** para iniciar el diálogo de alerta
3. **Espera** a que termine el diálogo
4. **Observa la Console** - Debe aparecer:
   ```
   [NPCSimpleAnimator] Reproduciendo Challenge para batalla: Challenging_NoWeapon
   [NPCSimpleAnimator] Challenge clip length: X.XXs
   [NPCSimpleAnimator] Esperando X.XXs para completar Challenge
   [NPCSimpleAnimator] Challenge completado por normalizedTime: 0.9X
   [NPCSimpleAnimator] Challenge completado → Idle de batalla: Idle_Battle_NoWeapon
   ```

5. **Si ves**:
   - `⚠️ Clip de Challenge muy corto (0.XXs), usando duración mínima de 2s`: El clip no se encontró correctamente
   - `⚠️ No se encontró el estado 'Challenging_NoWeapon' en el Animator`: El nombre del estado está mal escrito o no existe

6. **Observa visualmente**:
   - ✅ El NPC debe hacer la animación completa de Challenge (~2-3 segundos)
   - ✅ Después debe quedarse en `Idle_Battle_NoWeapon` en loop
   - ❌ Si vuelve a `Idle_Normal_NoWeapon`, hay un problema de transiciones en el Animator

### ⚠️ Verificar que Idle_Battle_NoWeapon hace loop:

1. **Después de que termine Challenge**, observa al NPC
2. **El NPC debe quedarse en idle de batalla indefinidamente**
3. **Abre el Animator en Play Mode**:
   - Window → Animation → Animator
   - Observa qué estado está activo (resaltado en azul)
   - Debe quedar en `Idle_Battle_NoWeapon` mientras NO te muevas

4. **Revisa la Console** - Debe aparecer:
   ```
   [NPCSimpleAnimator] Challenge completado → Idle de batalla: Idle_Battle_NoWeapon
   ```

5. **Abre el Animator en Play Mode**:
   - Window → Animation → Animator
   - Observa qué estado está activo (resaltado en azul)
   - Debe quedar en `Idle_Battle_NoWeapon` mientras NO te muevas

---

## 📋 CONFIGURACIÓN RECOMENDADA PARA ANIMATOR

### States que DEBEN hacer LOOP (✅):

```
Base Layer:
  - Idle_Normal_NoWeapon ✅
  - Idle_Battle_NoWeapon ✅ ← IMPORTANTE
  - Free Locomotion (Blend Tree) ✅

UpperBody Layer:
  - UpperIdle ✅
```

### States que NO deben hacer loop (One-Shot ❌):

```
Base Layer:
  - Challenging_NoWeapon ❌
  - GetHit02_NoWeapon ❌
  - Die02_NoWeapon ❌
  - TakeDamage ❌
  
UpperBody Layer:
  - Magic ❌
  - PickUp ❌
  - Todos los estados de interacción ❌
```

---

## 🛠️ SOLUCIÓN DE PROBLEMAS

### Problema: "El NPC sigue volviendo a atacar después de derrotarlo"

**Diagnóstico:**

1. **Revisa la Console** - ¿Aparece este log?
   ```
   ✅ Context.WasDefeatedInCombat = true
   ```

2. **Si NO aparece:**
   - El componente `NPCCombatLifecycleHandler` no está inicializado
   - Asegúrate de que el NPC tiene `NPCBehaviourManagerV2` activo
   - Busca el log: `⚙️ Inicializando - NPCManager: ✅, Damageable: ✅`

3. **Si SÍ aparece pero el NPC sigue atacando:**
   - Puede haber otro script que resetea `IsInCombat` o `WasDefeatedInCombat`
   - Busca en el proyecto: `Context.IsInCombat = true` o `WasDefeatedInCombat = false`
   - Verifica que `IdleState.CheckPlayerDetection()` tiene el return temprano si `WasDefeatedInCombat`

### Problema: "Idle_Battle_NoWeapon no hace loop"

**Diagnóstico:**

1. **Abre el Animator** en Play Mode (Window → Animation → Animator)
2. **Observa qué estado está activo** después de `Challenging_NoWeapon`
3. **Si es `Idle_Normal_NoWeapon`** en lugar de `Idle_Battle_NoWeapon`:
   - Hay una transición automática incorrecta
   - Revisa las **Transitions** desde `Challenging_NoWeapon`
   - Elimina transiciones con `Has Exit Time = true` que vayan a `Idle_Normal`

4. **Si es `Idle_Battle_NoWeapon`** pero solo se reproduce una vez:
   - El **Loop Time** NO está activado
   - Sigue la "OPCIÓN 1" de arriba para configurar el loop

5. **Si el NPC se mueve ligeramente** (micro-movimientos del NavMeshAgent):
   - Podría activar la transición a `Locomotion`
   - Aumenta el threshold de movimiento en las transiciones

### Problema: "El CapsuleCollider sigue desactivado"

**Diagnóstico:**

1. **En Play Mode**, selecciona el NPC después del combate
2. **En el Inspector**, busca **CapsuleCollider**
3. **Verifica:**
   - ✅ `enabled = true`
   - ✅ `isTrigger = true` (si el NPC fue derrotado)

4. **Si está desactivado:**
   - Revisa la Console: ¿Aparece este log?
     ```
     [CombatState] ✅ CapsuleCollider trigger activado en [nombre]
     ```
   - Si NO aparece, el NPC no tiene un collider trigger
   - Añade manualmente un `CapsuleCollider` con `isTrigger = true` en el GameObject del NPC

---

## 🎯 CHECKLIST FINAL

Antes de declarar todo solucionado, verifica:

- [ ] ¿El NPC **NO** vuelve a atacar después de ser derrotado?
- [ ] ¿Aparece el log `✅ Context.WasDefeatedInCombat = true` en Console?
- [ ] ¿`Idle_Battle_NoWeapon` se repite infinitamente durante el combate?
- [ ] ¿El CapsuleCollider está activo y trigger después del combate?
- [ ] ¿El botón de interacción (A) aparece después de derrotar al NPC?

---

## 📞 SI NADA FUNCIONA...

1. **Exporta estos logs** y compártelos:
   - Logs desde que detecta al jugador hasta después de derrotarlo
   - Especialmente busca: `WasDefeatedInCombat`, `Context.IsInCombat`, `CapsuleCollider`

2. **Captura de pantalla** del Animator Controller:
   - Estado `Idle_Battle_NoWeapon` seleccionado
   - Inspector mostrando la configuración del Motion

3. **Verifica la versión** del código:
   - ¿Los archivos editados tienen las correcciones más recientes?
   - Busca en `NPCCombatLifecycleHandler.cs` la línea con emoji: `⚔️ NPC derrotado`

---

## 📝 RESUMEN DE CAMBIOS EN CÓDIGO

Los siguientes archivos fueron modificados para solucionar los problemas:

1. **NPCCombatLifecycleHandler.cs**:
   - Añadido método `Initialize()` público para inicialización manual
   - Mejorado logging para detectar cuando Context es null
   - Asegura que `WasDefeatedInCombat` se establece correctamente

2. **CombatState.cs**:
   - Llama a `Initialize()` al añadir `NPCCombatLifecycleHandler` en runtime
   - Activa todos los colliders trigger en `OnExit()`
   - No permite re-iniciar combate si `WasDefeatedInCombat = true`

3. **IdleState.cs**:
   - Verifica `WasDefeatedInCombat` antes de detectar jugador (ya existía)
   - Desactiva animaciones de batalla si el NPC fue derrotado

4. **AlertState.cs**:
   - No permite entrar en alerta si `WasDefeatedInCombat = true` (ya existía)

✅ **Todos los cambios ya están aplicados** - Solo necesitas configurar el loop de `Idle_Battle_NoWeapon` manualmente en el Animator.

