# 🔍 DEBUG: Golem No Aparece

## 📋 Problema Reportado

El **Golem no aparece** en el juego aunque:
- ✅ NO está marcado como derrotado en el preset (`defeatedBossIds` solo contiene `Demon_1`)
- ✅ El área del boss se desbloquea inmediatamente
- ❌ El Golem nunca spawna

## 🔬 Logs Añadidos para Diagnóstico

He añadido logs de debug en 3 lugares clave para diagnosticar el problema:

### 1. BossProgressTracker.cs - LoadFromSnapshot()

```csharp
[BossProgressTracker] 📥 LoadFromSnapshot llamado
[BossProgressTracker]   ✅ Boss derrotado cargado: 'Demon_1'
[BossProgressTracker] 📊 Total bosses derrotados cargados: 1
```

**Qué buscar**: Este log debe aparecer al cargar la partida y solo debe mostrar `Demon_1`, NO `Golem_1`.

### 2. BossArenaController.cs - Start()

```csharp
[BossArenaController] 🎬 Start() - Arena BattleId='Golem_1', BossId='Golem_1'
[BossArenaController] 🔍 IsBossAlreadyDefeated() = false para BossId='Golem_1'
[BossArenaController] ⚔️ Boss 'Golem_1' NO ha sido derrotado - esperando trigger del player
```

**Qué buscar**: El log debe mostrar que el Golem **NO está derrotado** y que está esperando el trigger del player.

### 3. BossArenaController.cs - IsBossAlreadyDefeated()

```csharp
[BossArenaController] 🔍 Tracker encontrado - BossId='Golem_1', IsDefeated=false, DefeatedBossIds=[Demon_1]
```

**Qué buscar**: Debe mostrar que el tracker solo contiene `Demon_1` y que `Golem_1` NO está derrotado.

## 🎯 Pasos para Diagnosticar

### Paso 1: Ejecutar el Juego y Ver los Logs

1. **Abrir la Consola** de Unity (Window → General → Console)
2. **Entrar en Play Mode**
3. **Cargar la partida** (Continue)
4. **Ir a la zona del Golem** (MainWorld)
5. **Buscar en la consola** los logs que empiecen con `[BossArenaController]` y `[BossProgressTracker]`

### Paso 2: Verificar el BattleId del Golem

En la escena `MainWorld`, busca el GameObject del **BossArenaController del Golem** y verifica:

```
Inspector → BossArenaController
├─ Battle Identification
│  └─ Battle Id: "Golem_1"  ← Debe estar configurado
└─ Progreso
   └─ Boss Id: "Golem_1"    ← Debe estar configurado
```

**IMPORTANTE**: El `bossId` y `battleId` deben ser exactamente `"Golem_1"` (con mayúscula).

### Paso 3: Verificar el Trigger del Arena

El BossArenaController tiene dos modos:

#### Modo 1: Trigger Automático (useDoorMode = false)
```
Inspector → BossArenaController
├─ Activacion manual
│  └─ Start Barrier On Player Enter: ✅ TRUE
└─ Collider (del GameObject)
   └─ Is Trigger: ✅ TRUE
```

#### Modo 2: Puertas (useDoorMode = true)
```
Inspector → BossArenaController
├─ Modo de Arena
│  └─ Use Door Mode: ✅ TRUE
├─ Puertas
│  ├─ Door West: [Asignada]
│  └─ Door East: [Asignada]
└─ Activacion manual
   └─ Start Barrier On Player Enter: ✅ TRUE
```

**¿Qué verificar?**
- Si `startBarrierOnPlayerEnter` está en **FALSE**, el boss **NO se activará automáticamente** al entrar
- El collider del área debe tener `isTrigger = true`

## 🐛 Posibles Causas del Problema

### Causa 1: `startBarrierOnPlayerEnter = false`

**Síntoma**: El player entra al área pero no pasa nada.

**Solución**: En el Inspector del BossArenaController del Golem, activar `Start Barrier On Player Enter`.

**Log esperado si este es el problema**:
```
[BossArenaController] ⚔️ Boss 'Golem_1' NO ha sido derrotado - esperando trigger del player
// ... pero nunca aparece el log de StartBattleInternal
```

### Causa 2: Collider No Es Trigger

**Síntoma**: El player entra al área pero `OnTriggerEnter` no se dispara.

**Solución**: En el Collider del GameObject del BossArenaController, marcar `Is Trigger = true`.

### Causa 3: BossId Incorrecto

**Síntoma**: Los logs muestran un `bossId` diferente a `"Golem_1"`.

**Solución**: 
1. Abrir el Inspector del BossArenaController del Golem
2. Verificar que `Boss Id` sea exactamente `"Golem_1"`
3. Verificar que `Battle Id` sea exactamente `"Golem_1"`

### Causa 4: Boss Prefab No Asignado

**Síntoma**: El log muestra `"No hay boss para esta sala"`.

**Solución**: En el Inspector del BossArenaController:
```
Inspector → BossArenaController
└─ Spawn
   └─ Boss Prefab: [Asignar el prefab del Golem]
```

### Causa 5: Arena Deshabilitada

**Síntoma**: El GameObject del BossArenaController está desactivado.

**Solución**: Activar el GameObject en la Hierarchy.

## 📊 Escenarios de Logs Esperados

### ✅ Escenario Correcto (Golem debe aparecer)

```
[BossProgressTracker] 📥 LoadFromSnapshot llamado
[BossProgressTracker]   ✅ Boss derrotado cargado: 'Demon_1'
[BossProgressTracker] 📊 Total bosses derrotados cargados: 1

[BossArenaController] 🎬 Start() - Arena BattleId='Golem_1', BossId='Golem_1'
[BossArenaController] 🔍 Tracker encontrado - BossId='Golem_1', IsDefeated=false, DefeatedBossIds=[Demon_1]
[BossArenaController] 🔍 IsBossAlreadyDefeated() = false para BossId='Golem_1'
[BossArenaController] ⚔️ Boss 'Golem_1' NO ha sido derrotado - esperando trigger del player

// Player entra al área del Golem
[BossArenaController] StartBattleInternal iniciado
[BossArenaController] Combate del boss (Golem) activado.
```

### ❌ Escenario Incorrecto (Golem se marca como derrotado)

```
[BossProgressTracker] 📥 LoadFromSnapshot llamado
[BossProgressTracker]   ✅ Boss derrotado cargado: 'Demon_1'
[BossProgressTracker]   ✅ Boss derrotado cargado: 'Golem_1'  ← ❌ NO DEBERÍA ESTAR
[BossProgressTracker] 📊 Total bosses derrotados cargados: 2

[BossArenaController] 🎬 Start() - Arena BattleId='Golem_1', BossId='Golem_1'
[BossArenaController] 🔍 Tracker encontrado - BossId='Golem_1', IsDefeated=true, DefeatedBossIds=[Demon_1, Golem_1]
[BossArenaController] 🔍 IsBossAlreadyDefeated() = true para BossId='Golem_1'
[BossArenaController] ✅ Boss 'Golem_1' ya fue derrotado - desbloqueando área sin spawnearlo
[BossArenaController] Área del boss desbloqueada.
```

Si ves este escenario, significa que **el preset está corrupto** o que **el Golem se marcó como derrotado incorrectamente**.

### ❌ Escenario 3: BossId Vacío

```
[BossArenaController] 🎬 Start() - Arena BattleId='', BossId=''
[BossArenaController] ⚠️ IsBossAlreadyDefeated: bossId está vacío para battleId=''
```

**Solución**: Configurar el `Boss Id` en el Inspector del BossArenaController.

### ❌ Escenario 4: Tracker No Encontrado

```
[BossArenaController] 🎬 Start() - Arena BattleId='Golem_1', BossId='Golem_1'
[BossArenaController] ⚠️ BossProgressTracker no encontrado - asumiendo boss NO derrotado
```

Esto significa que el BossProgressTracker no se ha inicializado todavía. Esto es **normal** si el log aparece muy temprano, pero debería resolverse cuando el GameBootService cargue el preset.

## 🔧 Acciones de Corrección

### Acción 1: Limpiar el Preset Runtime (Si está corrupto)

Si el log muestra que `Golem_1` está en `defeatedBossIds` pero tú no lo has derrotado:

1. **Cerrar Unity**
2. **Abrir el archivo**: `Assets/Scripts/Player/SO/PlayerPreset_Runtime.asset`
3. **Buscar la sección** `defeatedBossIds:`
4. **Eliminar** la línea `- Golem_1` si existe
5. **Guardar** el archivo
6. **Abrir Unity** y ejecutar de nuevo

### Acción 2: Verificar la Configuración del Arena

En Unity, ir a la escena `MainWorld` y buscar el GameObject del BossArenaController del Golem:

```
Hierarchy → MainWorld → [Buscar "Golem" o "Boss Arena"]
```

Verificar en el Inspector:
1. ✅ `Boss Id` = `"Golem_1"`
2. ✅ `Battle Id` = `"Golem_1"`
3. ✅ `Start Barrier On Player Enter` = `true`
4. ✅ `Boss Prefab` = [Prefab del Golem asignado]
5. ✅ Collider `Is Trigger` = `true`

### Acción 3: Forzar el Spawn del Golem (Test)

Para verificar que el sistema funciona, puedes **forzar manualmente** el spawn del Golem:

1. Ir a la escena `MainWorld`
2. Seleccionar el GameObject del BossArenaController del Golem
3. En el Inspector, **desmarcar** `Start Barrier On Player Enter` temporalmente
4. En Play Mode, usar la consola de Unity para ejecutar:
   ```csharp
   FindFirstObjectByType<BossArenaController>().TriggerStartBattle();
   ```
5. O añadir un botón de UI temporal que llame a `TriggerStartBattle()`

## 📝 Información para el Desarrollador

### Archivos Modificados con Logs de Debug

1. **BossProgressTracker.cs** (líneas 92-114)
   - Añadidos logs en `LoadFromSnapshot()`
   
2. **BossArenaController.cs** (líneas 209-223)
   - Añadidos logs en `Start()`
   
3. **BossArenaController.cs** (líneas 544-562)
   - Añadidos logs en `IsBossAlreadyDefeated()`

### Cómo Quitar los Logs Después

Una vez resuelto el problema, puedes buscar y eliminar todos los logs con:
```
Ctrl+Shift+F → Buscar: "[BossArenaController]" y "[BossProgressTracker]"
```

O simplemente comentar las líneas de `Debug.Log()`.

## ✅ Checklist de Verificación

Antes de reportar el bug, verifica:

- [ ] Los logs aparecen correctamente en la consola
- [ ] `defeatedBossIds` en el preset **NO** contiene `Golem_1`
- [ ] `BossId` del arena está configurado como `"Golem_1"`
- [ ] `Start Barrier On Player Enter` está en `true`
- [ ] El collider del área es un **trigger**
- [ ] El `Boss Prefab` está asignado
- [ ] El GameObject del BossArenaController está **activo**
- [ ] El log muestra que `IsBossAlreadyDefeated() = false`

## 📸 Logs a Compartir

Si el problema persiste después de verificar todo lo anterior, comparte los siguientes logs:

1. **Todos los logs** que empiecen con `[BossProgressTracker]`
2. **Todos los logs** que empiecen con `[BossArenaController]` y contengan `Golem`
3. El **contenido** de `PlayerPreset_Runtime.asset` (sección `defeatedBossIds`)
4. Una **captura de pantalla** del Inspector del BossArenaController del Golem

---

**Fecha**: 2026-02-06  
**Estado**: Logs de debug añadidos, esperando verificación en runtime
