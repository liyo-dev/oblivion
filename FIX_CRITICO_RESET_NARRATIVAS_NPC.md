# FIX CRÍTICO: Reset de Narrativas NPC en Nueva Partida

## 🐛 Problema Identificado

**Síntoma**: Oliver (y otros NPCs con narrativas de un solo uso) NO se reseteaban al iniciar una nueva partida. Mantenían su estado de "completado".

## 🔍 Causa Raíz

El problema tenía **múltiples causas** relacionadas con la optimización del registro de NPCs:

### 1. **Registro Prematuro** (Menor)
En `OnEnable()`, el executor se registraba ANTES de que la configuración se cargara en `Start()`:
```csharp
OnEnable() -> Register(this) -> GetConfiguration() devuelve NULL
Start() -> Carga _config
```

**Resultado**: El executor se registraba en la lista general pero NO por ID de persistencia.

### 2. **Falta de Inicialización Lazy** (Medio)
`GetConfiguration()` devolvía `null` si se llamaba antes de `Start()`:
```csharp
public NPCInteractiveNarrativeConfig GetConfiguration()
{
    return _config; // NULL si no se ha llamado Start() aún
}
```

### 3. **Reset Incompleto** (CRÍTICO ⚠️)
`ResetState()` limpiaba **PlayerPrefs** pero NO limpiaba **GameBootService.Profile.completedInteractiveNarratives**, que es donde **realmente** se lee el estado:

```csharp
// En RestoreState():
var preset = GameBootService.Profile?.GetActivePresetResolved();
_hasBeenUsed = preset.completedInteractiveNarratives.Contains(_config.persistenceId);
// ☝️ Lee de aquí

// En ResetState() (ANTES):
PlayerPrefs.DeleteKey(key); // ❌ Limpia aquí
// ❌ NO limpiaba preset.completedInteractiveNarratives
```

## ✅ Solución Implementada

### 1. **Lazy Initialization en GetConfiguration()**
```csharp
public NPCInteractiveNarrativeConfig GetConfiguration()
{
    // Lazy initialization si aún no se ha cargado
    if (_config == null && _npcManager != null && _npcManager.Configuration != null)
    {
        _config = _npcManager.Configuration.interactiveNarrativeConfig;
    }
    
    return _config;
}
```

**Beneficio**: Ahora GetConfiguration() siempre devuelve la config correcta, incluso si se llama desde OnEnable().

### 2. **Re-registro después de cargar Config**
```csharp
private void Start()
{
    // ... cargar config ...
    
    // Re-registrar ahora que tenemos la config cargada (para registrar por ID)
    NPCInteractiveNarrativeRegistry.Register(this);
    
    // ... resto del código ...
}
```

**Beneficio**: Asegura que el executor se registre por ID de persistencia correctamente.

### 3. **Reset Completo con GameBootService** (CRÍTICO)
```csharp
public void ResetState()
{
    _hasBeenUsed = false;
    // ... otros resets ...
    
    // Limpiar PlayerPrefs
    PlayerPrefs.DeleteKey(key);
    
    // ✅ CRÍTICO: Limpiar también el GameBootService.Profile
    var preset = GameBootService.Profile?.GetActivePresetResolved();
    if (preset != null && preset.completedInteractiveNarratives != null)
    {
        bool wasCompleted = preset.completedInteractiveNarratives.Remove(_config.persistenceId);
        if (wasCompleted)
        {
            Debug.Log($"✅ Removido '{_config.persistenceId}' de completedInteractiveNarratives");
        }
    }
}
```

**Beneficio**: Ahora se limpia el estado REAL donde el sistema lo lee.

### 4. **Actualización de ClearAllSavedStates()**
```csharp
public static void ClearAllSavedStates()
{
    var preset = GameBootService.Profile?.GetActivePresetResolved();
    
    foreach (var executor in allExecutors)
    {
        // Limpiar PlayerPrefs
        PlayerPrefs.DeleteKey(key);
        
        // ✅ Limpiar del GameBootService.Profile
        if (preset != null && preset.completedInteractiveNarratives != null)
        {
            preset.completedInteractiveNarratives.Remove(config.persistenceId);
        }
    }
}
```

### 5. **Logging Detallado para Debug**
Se agregó logging extensivo en:
- `NPCInteractiveNarrativeRegistry.Register()`
- `NPCInteractiveNarrativeRegistry.Unregister()`
- `NPCNarrativeStateManager.ResetAllNPCs()`
- `NPCInteractiveNarrativeExecutor.ResetState()`

## 🧪 Testing

### Cómo Probar el Fix:

1. **Setup Inicial**:
   - Coloca a Oliver en la escena
   - Asegúrate de que tiene `persistenceId` configurado
   - Asegúrate de que `persistState = true`

2. **Primera Ejecución**:
   ```
   - Inicia el juego
   - Interactúa con Oliver (completa su narrativa)
   - Verifica que no vuelva a aparecer
   ```

3. **Reset de Nueva Partida**:
   ```csharp
   // Desde código o Context Menu:
   NPCNarrativeStateManager.ResetAllNPCs();
   ```

4. **Segunda Ejecución**:
   ```
   - Recarga la escena o reinicia el juego
   - Oliver debería estar disponible nuevamente ✅
   ```

### Verificación en Consola:

Busca estos logs:
```
✅ Registrado por ID 'oliver_intro': Oliver
🔄 Reseteando: Oliver
✅ Removido 'oliver_intro' de completedInteractiveNarratives
🔄 Estado reseteado completamente
```

## 📊 Flujo Correcto Ahora

```
1. OnEnable() 
   → Register(this) 
   → GetConfiguration() [lazy load] 
   → Registra en _all

2. Start() 
   → Carga _config explícitamente
   → Re-registra con Register(this)
   → Ahora registra en _byId con persistenceId ✅

3. RestoreState()
   → Lee de GameBootService.Profile.completedInteractiveNarratives
   → Encuentra el estado correcto ✅

4. ResetState()
   → Limpia PlayerPrefs
   → Limpia GameBootService.Profile.completedInteractiveNarratives ✅
   → Estado completamente limpio ✅

5. Nueva Partida
   → RestoreState() no encuentra el ID en completedInteractiveNarratives
   → _hasBeenUsed = false ✅
   → Narrativa disponible de nuevo ✅
```

## 🔧 Archivos Modificados

1. ✅ `NPCInteractiveNarrativeExecutor.cs`
   - GetConfiguration() con lazy initialization
   - ResetState() limpia GameBootService.Profile
   - Re-registro en Start()
   - Logging detallado

2. ✅ `NPCNarrativeStateManager.cs`
   - ClearAllSavedStates() limpia GameBootService.Profile
   - ResetAllNPCs() con mejor logging
   - Try-catch para errores

3. ✅ `NPCInteractiveNarrativeRegistry.cs`
   - Logging detallado en Register/Unregister
   - GetDebugInfo() mejorado
   - Manejo de re-registros

## ⚠️ Puntos Críticos

### Para el Usuario:
1. **SIEMPRE** llama `NPCNarrativeStateManager.ResetAllNPCs()` al iniciar nueva partida
2. **O** usa `NPCNarrativeReset.FullReset()` que hace todo

### Para Desarrollo:
1. El estado REAL está en `GameBootService.Profile.completedInteractiveNarratives`
2. PlayerPrefs es legacy pero se mantiene por compatibilidad
3. El registro se hace dos veces (OnEnable + Start) - esto es intencional

## 🎯 Resultado Final

- ✅ Oliver se resetea correctamente en nueva partida
- ✅ Todos los NPCs con narrativas se resetean
- ✅ Sistema de persistencia funciona correctamente
- ✅ Logging detallado para debugging futuro
- ✅ Optimización de rendimiento mantenida

## 📝 Notas Adicionales

### Por qué dos sistemas de persistencia?

1. **PlayerPrefs** (legacy): Se mantiene por compatibilidad con guardados antiguos
2. **GameBootService.Profile** (actual): Sistema principal que realmente se usa

Ambos deben limpiarse para evitar problemas de sincronización.

### Context Menu Útiles

En `NPCNarrativeReset`:
- **Reset All Narratives** - Resetea todos los NPCs
- **Clear All Saved States** - Limpia estados guardados
- **Full Reset (New Game)** - Hace ambos (recomendado)
- **Show Debug Info** - Muestra info de todos los NPCs

---

**Fecha del Fix**: 2025-12-24  
**Prioridad**: CRÍTICA ⚠️  
**Estado**: ✅ RESUELTO

