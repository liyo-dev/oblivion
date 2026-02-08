# Guía: Guardar en Modo Testeo y Continuar en Modo Normal

## 📋 Resumen

Esta guía explica cómo usar el modo testeo con presets para probar una sección específica del juego y luego guardar el progreso para continuar en modo normal.

## 🎯 Caso de Uso

**Escenario**: Quieres testear la Misión 6 con un preset específico, jugar un rato, y luego guardar ese progreso para continuar la partida en modo normal sin tener que volver a jugar desde el principio.

## 🔧 Configuración del Modo Testeo

### 1. Activar el Preset de Testeo

En el **GameBootProfile** (Assets/_BootProfile/GameBootProfile.asset):

- ✅ Activa: `usePresetInsteadOfSave`
- 📎 Asigna: `bootPreset` → PlayerPreset_Mision_6 (o el preset que quieras)

### 2. ¿Qué hace el Modo Testeo?

Cuando `usePresetInsteadOfSave` está activo:

- ✅ El juego **siempre arranca** desde el `bootPreset` (ignora el save JSON)
- ✅ El preset se carga como si fuera una partida cargada (con todos los sistemas activos)
- ✅ Al cambiar de escena, el estado se **mantiene desde el runtime** (no resetea al preset original)
- ⚠️ **IMPORTANTE**: El preset solo se aplica en el **primer boot** (cuando se carga el GameBootService)

## 💾 Guardar el Progreso en Modo Testeo

### Flujo Normal de Guardado

1. **Juega normalmente** con el preset activo
2. **Ve a un SavePoint** cuando quieras guardar el progreso
3. **Interactúa con el SavePoint** (el juego te curará y guardará)

### ¿Qué se Guarda?

Cuando guardas en un SavePoint estando en modo testeo:

- ✅ Se guarda el **estado runtime actual** (no el bootPreset original)
- ✅ Incluye todo lo que has hecho desde que arrancó el preset:
  - HP/MP actuales
  - Misiones completadas
  - NPCs con los que has hablado
  - Items obtenidos
  - Blackboards narrativos
  - Posiciones de party members
  - Puntos de teleporte desbloqueados
  - Bosses derrotados
  - Etc.

### Mensaje de Confirmación

Al guardar en modo testeo verás este mensaje en la consola:

```
[SavePoint] 🧪 Partida guardada en MODO TESTEO - El estado runtime actual se ha guardado en el JSON.
Ahora puedes desactivar 'usePresetInsteadOfSave' para continuar desde aquí.
```

## 🔄 Continuar en Modo Normal

### Desactivar el Modo Testeo

1. Abre el **GameBootProfile** (Assets/_BootProfile/GameBootProfile.asset)
2. ❌ Desactiva: `usePresetInsteadOfSave`
3. 💾 Guarda el asset (Ctrl+S)

### ¿Qué pasa ahora?

- ✅ El juego **cargará desde el save JSON** (el que guardaste en el SavePoint)
- ✅ Continuarás exactamente desde donde guardaste
- ✅ El `bootPreset` ya no se usará (queda como referencia)

## 📊 Diagrama de Flujo

```
┌─────────────────────────────────────────────┐
│ MODO TESTEO ACTIVO                          │
│ (usePresetInsteadOfSave = true)             │
└─────────────────┬───────────────────────────┘
                  │
                  v
         ┌────────────────┐
         │ Juego arranca  │
         │ desde          │
         │ bootPreset     │
         └────────┬───────┘
                  │
                  v
         ┌────────────────┐
         │ Juegas y el    │
         │ runtime        │
         │ evoluciona     │
         └────────┬───────┘
                  │
                  v
         ┌────────────────┐
         │ Vas al         │
         │ SavePoint      │
         └────────┬───────┘
                  │
                  v
         ┌────────────────┐
         │ Se guarda el   │
         │ RUNTIME actual │
         │ en el JSON     │
         └────────┬───────┘
                  │
                  v
         ┌────────────────┐
         │ Desactivas     │
         │ modo testeo    │
         └────────┬───────┘
                  │
                  v
         ┌────────────────┐
         │ Siguiente boot │
         │ carga desde    │
         │ el JSON        │
         └────────────────┘
```

## ⚠️ Notas Importantes

### Durante el Modo Testeo

- ✅ Puedes guardar cuantas veces quieras
- ✅ Cada guardado sobrescribe el anterior
- ✅ El runtime **NO se resetea** al cambiar de escena (mantiene el progreso acumulado)
- ✅ Puedes acumular progreso jugando normalmente (misiones, items, diálogos, etc.)
- ⚠️ Si reinicias el juego (cierra Unity o re-entras a Play Mode), volverás al bootPreset inicial

### Después de Desactivar el Modo Testeo

- ✅ El juego funciona como modo normal
- ✅ Los saves se cargan automáticamente
- ❌ El bootPreset ya no se usa (a menos que lo reactives)

## 🧪 Ejemplo Práctico

### Paso 1: Configurar Testeo

```
GameBootProfile:
  usePresetInsteadOfSave: ✅ TRUE
  bootPreset: PlayerPreset_Mision_6
```

### Paso 2: Jugar

- Entras en Play Mode
- Apareces en el spawn point de la Misión 6
- Juegas, completas objetivos, luchas, etc.

### Paso 3: Guardar

- Vas al SavePoint más cercano
- Interactúas (E o botón de acción)
- Ves el mensaje: "¡Partida guardada con éxito!"
- En consola: "🧪 Partida guardada en MODO TESTEO..."

### Paso 4: Continuar en Modo Normal

- Sales de Play Mode
- Abres GameBootProfile
- Desactivas `usePresetInsteadOfSave`
- Guardas el asset
- Entras en Play Mode
- ✅ Apareces exactamente donde guardaste, con todo tu progreso

## 🔍 Verificación

### Comprobar que el Save se Creó

El archivo de guardado se encuentra en:

```
Windows: %USERPROFILE%/AppData/LocalLow/DefaultCompany/El Sendero de las Estrellas/savegame.json
Mac: ~/Library/Application Support/DefaultCompany/El Sendero de las Estrellas/savegame.json
```

Puedes abrirlo con un editor de texto para verificar:

```json
{
  "lastSpawnAnchorId": "Woods_Entrance_SavePoint",
  "level": 6,
  "currentHp": 100,
  "maxHp": 100,
  // ... etc
}
```

### Comprobar en el GameBootProfileDebugger

Si tienes el debugger activo (ventana de juego), verás:

```
Runtime Preset:
  Anchor: Woods_Entrance_SavePoint
  HP: 100/100
  MP: 50/50
  Flags: 15 activas
```

## 🚨 Solución de Problemas

### El juego no guarda en modo testeo

- ✅ Verifica que hay un `SaveSystem` en la escena
- ✅ Asegúrate de que el SavePoint tiene un `anchorId` válido
- ✅ Revisa la consola para ver errores

### El save no se carga al desactivar modo testeo

- ✅ Confirma que `usePresetInsteadOfSave` está en `false`
- ✅ Verifica que el archivo `savegame.json` existe
- ✅ Comprueba que el GameBootProfile está asignado en el GameBootService

### El runtime se resetea al cambiar de escena

- ⚠️ Esto **NO debería pasar** en modo testeo
- ✅ Si pasa, revisa que el GameBootService no se está destruyendo
- ✅ Asegúrate de que solo hay un GameBootService en escena (DontDestroyOnLoad)

## 📚 Referencias

- `GameBootProfile.cs` - Lógica de guardado y carga
- `GameBootService.cs` - Gestión del profile entre escenas
- `SavePoint.cs` - Guardado desde puntos de control
- `PlayerPresetSO.cs` - Estructura de datos del preset

## ✅ Checklist Rápido

Para usar esta funcionalidad correctamente:

- [ ] Activa `usePresetInsteadOfSave` en GameBootProfile
- [ ] Asigna el `bootPreset` que quieras usar
- [ ] Entra en Play Mode y juega
- [ ] Ve a un SavePoint cuando quieras guardar
- [ ] Desactiva `usePresetInsteadOfSave` para continuar en modo normal
- [ ] Verifica que el save se cargó correctamente

---

**Última actualización**: 2026-02-06
