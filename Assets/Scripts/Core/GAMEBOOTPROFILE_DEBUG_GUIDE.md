# GameBootProfile Debugger - Guía de Uso

## ¿Qué Problema Resuelve?

`GameBootProfile` maneja flujos complejos de inicialización y persistencia:
- Múltiples fuentes de presets (default, boot, runtime)
- Sincronización con muchos sistemas (Health, Mana, Quests, Inventory, etc.)
- Lógica de save/load con validaciones
- Nueva partida vs continuar partida

**Sin el debugger** es difícil saber:
- ❓ ¿Qué preset está activo ahora?
- ❓ ¿Por qué el HP guardado no coincide con el HP actual?
- ❓ ¿Se sincronizaron todos los sistemas antes del save?
- ❓ ¿Qué pasó cuando cargué la partida?

---

## Instalación

### 1. Añadir el Componente
1. Busca el GameObject que tiene `GameBootService` (normalmente en la escena de arranque o DontDestroyOnLoad)
2. Añade el componente `GameBootProfileDebugger`

### 2. Configuración Opcional
En el Inspector del `GameBootProfileDebugger`:

```
Show Debug Panel: ✅ (mostrar panel en pantalla)
Toggle Key: F4 (tecla para mostrar/ocultar)
Track History: ✅ (registrar historial de operaciones)
Max History Entries: 20
Profile: (auto-detecta desde GameBootService)
Save System: (auto-detecta)
```

### 3. Auto-Referencias
El debugger busca automáticamente:
- `GameBootProfile` desde `GameBootService`
- `SaveSystem` en la escena

Si no los encuentra, puedes asignarlos manualmente en el Inspector.

---

## Cómo Usar

### Panel Visual (F4)

**Presiona F4 en cualquier momento** para ver el panel de debug.

El panel muestra:

#### 1️⃣ Estado General
- Qué profile está activo
- Si `usePresetInsteadOfSave` está activado
- Si `allowAutoSaves` está activado
- Si existe un save guardado

#### 2️⃣ Presets Configurados
Muestra los 3 presets:
- **Default Preset**: Template base para nueva partida
- **Boot Preset**: Preset para testing (ignorar save)
- **Runtime Preset** 🟢: El preset ACTIVO (siempre este se usa en runtime)

Para cada preset:
- Nombre
- Spawn Anchor configurado
- HP/MP actuales

#### 3️⃣ Estado Runtime Actual
El contenido completo del `runtimePreset`:
- ✅ Spawn Anchor, Level, HP, MP
- ✅ Abilities desbloqueadas y Spells desbloqueados
- ✅ Slots de spells (Left, Right, Special)
- ✅ Permisos de acciones (Swim, Jump, Climb)
- ✅ Conteo de Flags, Appearance, Inventory, Bosses
- ✅ Desglose de tipos de flags (Quest/Cinematic/Other)
- ✅ Estado de blackboards narrativos

#### 4️⃣ Estado de Sistemas Vivos
Compara el `runtimePreset` con los sistemas activos en la escena:
- **PlayerHealthSystem**: HP actual del jugador vivo
- **ManaPool**: MP actual del jugador vivo
- **PlayerActionManager**: Permisos actuales (Swim/Jump/Climb)
- **SpawnManager**: Anchor actual en runtime
- **QuestManager**: Si está activo
- **NarrativeGraphHub**: Cuántos runners activos

**⚠️ Útil para detectar desincronización**: Si el `runtimePreset` dice HP=80 pero `PlayerHealthSystem` dice HP=50, algo no se sincronizó correctamente.

#### 5️⃣ Historial de Operaciones
Registro cronológico de todas las operaciones:
- 🟢 **Operaciones exitosas**: Save, Load, Update
- 🟠 **Advertencias**: Auto-save omitido, sin save disponible
- 🔴 **Errores**: Fallo al guardar/cargar

Cada entrada muestra:
- Timestamp (HH:mm:ss)
- Operación realizada
- Detalles específicos

#### 6️⃣ Acciones de Debug
Botones para testing manual:
- **🔄 Update Runtime from State**: Sincroniza `runtimePreset` con sistemas vivos
- **💾 Force Save**: Guarda inmediatamente (manual)
- **📂 Load Save**: Carga el save actual
- **🗑️ Clear History**: Limpia el historial

---

## Flujos Típicos de Debug

### Caso 1: Testing de Save/Load

```
1. Inicia el juego
2. Presiona F4 para ver el panel
3. Verifica Estado Runtime Actual:
   - ¿El HP/MP son correctos?
   - ¿El Spawn Anchor es correcto?
4. Haz cambios en el juego (daño, colectar items, etc.)
5. Presiona "🔄 Update Runtime from State"
6. Verifica que los valores se actualizaron
7. Presiona "💾 Force Save"
8. Mira el historial: debe decir "✅ Guardado exitoso"
9. Cierra y reinicia
10. Presiona "📂 Load Save"
11. Compara Estado Runtime con Estado de Sistemas Vivos
    - ¿Coinciden? ✅ Save/Load funciona
    - ¿Difieren? ❌ Hay un problema de sincronización
```

### Caso 2: Debugging de Desincronización

**Síntoma**: "El HP se guarda bien pero al cargar está diferente"

```
1. Carga la partida
2. Presiona F4
3. Mira "Estado Runtime Actual":
   - HP: 50/100 (del runtimePreset)
4. Mira "Estado de Sistemas Vivos":
   - PlayerHealthSystem: 100/100
5. ❌ PROBLEMA DETECTADO: El preset tiene HP=50 pero el sistema tiene HP=100
6. Posibles causas:
   - El PlayerHealthSystem se reseteó DESPUÉS de aplicar el preset
   - Hay un script que sobreescribe el HP al cargar
   - El preset no se aplicó correctamente al PlayerHealthSystem
```

### Caso 3: Nueva Partida vs Continuar

```
NUEVA PARTIDA:
1. Presiona F4
2. Historial muestra: "🆕 Nueva partida desde defaultPlayerPreset: DefaultPlayer"
3. Estado Runtime debe coincidir con Default Preset

CONTINUAR:
1. Presiona F4
2. Historial muestra: "✅ Cargado exitoso - Anchor: Bedroom, HP: 45"
3. Estado Runtime debe coincidir con los datos del save
```

### Caso 4: Testing de Auto-Save

```
1. Presiona F4
2. Verifica "Allow Auto-Saves: ✅ SÍ" o "❌ NO"
3. Si NO:
   - Los auto-saves se omitirán
   - Verás en historial: "⏭️ Auto-guardado omitido (allowAutoSaves = false)"
4. Si SÍ:
   - Los auto-saves funcionarán normalmente
   - Verás: "✅ Guardado exitoso (context: Auto)"
```

---

## Logs Automáticos en Operaciones

El debugger se integra automáticamente con `GameBootProfile`:

### SaveProfile()
```
✅ Guardado exitoso (context: Manual)
❌ SaveSystem no disponible
❌ Error al guardar
```

### LoadProfile()
```
✅ Cargado exitoso - Anchor: Bedroom, HP: 45.0
❌ Sin SaveSystem o sin save disponible
❌ Error al cargar datos
```

### SaveCurrentGameState()
```
🔄 Runtime actualizado antes de guardar (context: Manual)
⏭️ Auto-guardado omitido (allowAutoSaves = false)
```

### UpdateRuntimePresetFromCurrentState()
```
✅ Sincronizados: SpawnAnchor(Bedroom), Health(45/100), Mana(30/50), QuestFlags(8), Abilities(S:True,J:True,C:False), Inventory(3), Appearance(5), Bosses(1), Narratives(2)
```

### NewGameReset()
```
🆕 Nueva partida desde defaultPlayerPreset: DefaultPlayer
✅ Reset completado - sistemas reiniciados
🆕 Nueva partida con preset vacío (sin defaultPlayerPreset) ⚠️
```

---

## Preguntas Frecuentes

### ¿Por qué el Runtime Preset no coincide con los sistemas vivos?

**Causa común**: No se llamó `UpdateRuntimePresetFromCurrentState()` antes de comparar.

**Solución**: Presiona "🔄 Update Runtime from State" y verifica si se sincronizan.

### ¿Por qué no se guarda el progreso?

**Posibles causas**:
1. `allowAutoSaves = false` y estás intentando auto-save
2. El `SaveSystem` no está configurado
3. No se llama `UpdateRuntimePresetFromCurrentState()` antes de guardar

**Verificación**: Mira el historial de operaciones en el debugger.

### ¿Cómo sé si el save se cargó correctamente?

**Verifica**:
1. Historial muestra: "✅ Cargado exitoso"
2. Estado Runtime tiene datos (no todo en cero)
3. Estado Runtime coincide con Estado de Sistemas Vivos

### ¿Puedo usar el debugger en build de producción?

**Recomendación**: Desactiva el componente en builds finales.

En el Inspector:
- Desmarca `Show Debug Panel`
- O quita el componente completamente

El debugger usa OnGUI que tiene impacto de rendimiento.

---

## Beneficios

✅ **Visibilidad completa** del flujo de save/load
✅ **Detección inmediata** de desincronización
✅ **Historial de operaciones** para debugging
✅ **Testing manual** de save/load sin reiniciar
✅ **Comparación visual** entre preset y sistemas vivos
✅ **Logs integrados** en todas las operaciones críticas

---

## Teclas de Acceso Rápido

| Tecla | Acción |
|-------|--------|
| **F4** | Mostrar/Ocultar panel de GameBootProfile |
| **F3** | Panel de Narrative Graphs (si está instalado) |

---

## Integración con Narrative Debugger

Si también tienes `NarrativeGraphDebugger`:
- **F3** → Narrative Graphs
- **F4** → GameBootProfile

Ambos sistemas se complementan:
- Narrative muestra estado de grafos
- GameBootProfile muestra estado de partida (HP, quests, inventory)

---

## Ejemplo de Sesión Completa

```
[14:23:15] NewGameReset → 🆕 Nueva partida desde defaultPlayerPreset: DefaultPlayer
[14:23:15] NewGameReset → ✅ Reset completado - sistemas reiniciados

[14:25:30] UpdateRuntimePreset → ✅ Sincronizados: Health(80/100), Mana(40/50), Inventory(2)
[14:25:30] SaveCurrentGameState → 🔄 Runtime actualizado antes de guardar (context: Manual)
[14:25:30] SaveProfile → ✅ Guardado exitoso (context: Manual)

[14:28:10] LoadProfile → ✅ Cargado exitoso - Anchor: ForestEntry, HP: 80.0

[14:30:45] SaveCurrentGameState → ⏭️ Auto-guardado omitido (allowAutoSaves = false)
```

Esto te cuenta toda la historia de la sesión de juego.
