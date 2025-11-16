# Mejoras de Debugging para GameBootProfile

## Problema Original

`GameBootProfile` es complejo de debuggear porque:
- ❌ Múltiples presets (default, boot, runtime) sin visibilidad de cuál está activo
- ❌ Sincronización con ~10 sistemas diferentes (Health, Mana, Quests, Inventory, etc.)
- ❌ No se sabe si save/load funcionó correctamente hasta ver efectos secundarios
- ❌ Difícil detectar desincronización entre preset y sistemas vivos
- ❌ Sin historial de operaciones (save/load/reset)

---

## Solución: GameBootProfileDebugger

### 🎯 Sistema de Debugging Visual

Similar al `NarrativeGraphDebugger` pero para el sistema de partidas.

### Características Implementadas

#### 1️⃣ Panel Visual OnGUI (F4)
- **Estado General**: preset activo, configuración de auto-save
- **Presets**: muestra default, boot y runtime con sus datos
- **Estado Runtime**: contenido completo del `runtimePreset`
- **Sistemas Vivos**: estado actual de PlayerHealth, Mana, Actions, etc.
- **Historial**: log cronológico de todas las operaciones
- **Acciones Rápidas**: botones para testing manual

#### 2️⃣ Logging Automático Integrado
Todas las operaciones críticas ahora loggean automáticamente:

```csharp
SaveProfile()
  ✅ Guardado exitoso (context: Manual)
  ❌ SaveSystem no disponible

LoadProfile()
  ✅ Cargado exitoso - Anchor: Bedroom, HP: 45.0
  ❌ Sin SaveSystem o sin save disponible

SaveCurrentGameState()
  🔄 Runtime actualizado antes de guardar
  ⏭️ Auto-guardado omitido (allowAutoSaves = false)

UpdateRuntimePresetFromCurrentState()
  ✅ Sincronizados: SpawnAnchor(Bedroom), Health(45/100), Mana(30/50), 
     QuestFlags(8), Abilities(S:True,J:True,C:False), Inventory(3), 
     Appearance(5), Bosses(1), Narratives(2)

NewGameReset()
  🆕 Nueva partida desde defaultPlayerPreset: DefaultPlayer
  ✅ Reset completado - sistemas reiniciados
```

#### 3️⃣ Detección de Desincronización
El panel muestra lado a lado:
- **Preset Runtime** → HP: 50/100
- **Sistema Vivo** → PlayerHealth: 100/100

Si no coinciden → problema detectado visualmente.

#### 4️⃣ Desglose Detallado
Para cada sistema muestra:
- **Health/Mana**: valores actuales vs máximos
- **Abilities**: qué está desbloqueado
- **Flags**: conteo por tipo (Quest/Cinematic/Other)
- **Inventory**: número de items
- **Bosses**: cuántos derrotados
- **Narratives**: cuántos grafos guardados

---

## Archivos Creados/Modificados

### ✅ Nuevos Archivos

1. **GameBootProfileDebugger.cs** (430 líneas)
   - Componente OnGUI con panel scrollable
   - Toggle con F4
   - Historial de operaciones
   - Comparación preset vs sistemas vivos
   - Botones de testing manual

2. **GAMEBOOTPROFILE_DEBUG_GUIDE.md**
   - Guía completa de uso
   - Casos de uso típicos
   - Ejemplos de sesiones de debug
   - FAQ

### ✅ Archivos Modificados

**GameBootProfile.cs** (5 métodos):
- `SaveProfile()` → logging de éxito/error
- `LoadProfile()` → logging con datos cargados
- `SaveCurrentGameState()` → logging de sincronización
- `UpdateRuntimePresetFromCurrentState()` → logging detallado de sistemas
- `NewGameReset()` → logging de reset

---

## Cómo Usar

### Instalación Rápida
1. Buscar GameObject con `GameBootService`
2. Añadir componente `GameBootProfileDebugger`
3. Referencias se auto-detectan

### Uso Básico
1. Presionar **F4** en cualquier momento
2. Ver panel con estado completo
3. Revisar historial de operaciones
4. Usar botones para testing manual

### Testing de Save/Load
```
1. F4 → Ver estado inicial
2. Hacer cambios en juego
3. "🔄 Update Runtime from State"
4. "💾 Force Save"
5. Cerrar y reiniciar
6. "📂 Load Save"
7. Comparar "Estado Runtime" vs "Sistemas Vivos"
   - ¿Coinciden? ✅ OK
   - ¿Difieren? ❌ Problema
```

---

## Beneficios

### Para Desarrollo
✅ **Visibilidad completa** del estado de partida
✅ **Detección inmediata** de problemas de sincronización
✅ **Testing rápido** sin reiniciar Unity
✅ **Historial** para entender qué pasó

### Para Debugging
✅ **Comparación visual** preset vs vivo
✅ **Logs automáticos** en todas las operaciones
✅ **Desglose detallado** de cada sistema
✅ **Identificación rápida** de causas raíz

### Para Testing
✅ **Botones manuales** para save/load
✅ **Verificación instantánea** de estado
✅ **Tracking de auto-saves** vs manual
✅ **Validación de nueva partida** vs continuar

---

## Casos de Uso Resueltos

### Caso 1: "El HP se guarda mal"
**Antes**: 
- Guardar, cargar, ver que HP está mal
- No saber si el problema es al guardar o al cargar
- No saber qué valor tenía antes vs después

**Ahora**:
- F4 → Ver HP en preset: 50/100
- Ver HP en PlayerHealth: 100/100
- ❌ Desincronización detectada visualmente
- Historial muestra si se sincronizó antes de guardar

### Caso 2: "Los auto-saves no funcionan"
**Antes**: 
- Asumir que se guardó
- Cargar y ver que no se guardó
- No saber por qué

**Ahora**:
- Historial muestra: "⏭️ Auto-guardado omitido (allowAutoSaves = false)"
- Causa clara: configuración desactivada

### Caso 3: "Al cargar, algo se resetea"
**Antes**:
- Cargar y ver que algo cambió
- No saber qué fue
- No saber cuándo pasó

**Ahora**:
- Historial muestra: "✅ Cargado exitoso - Anchor: X, HP: Y"
- Panel muestra TODOS los sistemas después de cargar
- Comparar con save anterior
- Identificar qué sistema se reseteó

### Caso 4: "Nueva partida arrastra datos antiguos"
**Antes**:
- Nueva partida pero tiene items viejos
- No saber de dónde vienen

**Ahora**:
- Historial muestra: "🆕 Nueva partida desde defaultPlayerPreset"
- Panel muestra estado completo del preset
- Verificar que inventory/flags estén limpios

---

## Integración con Otros Debuggers

### NarrativeGraphDebugger (F3)
- Estado de grafos narrativos
- Nodos actuales
- Eventos recibidos

### GameBootProfileDebugger (F4)
- Estado de partida
- HP/Mana/Inventory
- Quests y flags

**Juntos** → Visibilidad completa del estado del juego

---

## Rendimiento

- **OnGUI**: Solo activo cuando panel visible
- **F4**: Toggle on/off
- **Producción**: Desactivar componente en builds finales

---

## Comparación: Antes vs Ahora

| Situación | Antes | Ahora |
|-----------|-------|-------|
| Ver estado de preset | ❌ Inspector read-only | ✅ Panel dinámico F4 |
| Saber si save funcionó | ❌ Logs dispersos | ✅ Historial centralizado |
| Detectar desincronización | ❌ Trial & error | ✅ Comparación visual |
| Testing save/load | ❌ Play → Save → Restart | ✅ Botones en panel |
| Debugging de problemas | ❌ Horas buscando | ✅ Minutos con panel |

---

## Próximos Pasos

### Uso Inmediato
1. Añadir `GameBootProfileDebugger` al GameBootService
2. Testing de save/load con F4
3. Verificar que todos los sistemas sincronizan

### Opcional
1. Añadir más detalles al panel (ej: lista completa de flags)
2. Exportar historial a archivo de texto
3. Añadir alertas visuales para desincronización
4. Integrar con sistema de telemetría

---

## Lecciones de Diseño

### Lo que funcionó
✅ **API estática simple**: `GameBootProfileDebugger.Log(operation, details, type)`
✅ **Auto-detección**: Referencias se buscan automáticamente
✅ **Historial acumulativo**: Ver toda la sesión de golpe
✅ **Comparación visual**: Preset vs vivo lado a lado

### Inspiración de NarrativeGraphDebugger
- Panel OnGUI scrollable
- Toggle con tecla (F3/F4)
- Color coding (verde/amarillo/rojo)
- Historial temporal con timestamps
- Secciones colapsables

---

## Conclusión

El debugging de `GameBootProfile` pasó de ser **"imposible sin breakpoints"** a **"visible en tiempo real con F4"**.

El sistema es **profesional**, **no invasivo** y **extremadamente útil** para development y testing.
