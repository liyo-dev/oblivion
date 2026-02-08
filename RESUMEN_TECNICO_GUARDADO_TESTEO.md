# Resumen Técnico: Sistema de Guardado en Modo Testeo

## 🎯 Objetivo

Permitir que el usuario pueda:
1. Usar un preset de testeo para arrancar en un punto específico del juego
2. Jugar normalmente y guardar el progreso en un SavePoint
3. Desactivar el modo testeo y continuar desde ese guardado

## 🔧 Implementación

### Cambios Realizados

#### 1. SavePoint.cs - Mensaje informativo en modo testeo

**Ubicación**: `Assets/Scripts/World/SavePoint.cs` - Método `DoSave()`

**Cambio**:
```csharp
// ✅ NUEVO: Si estamos en modo testeo, guardar el runtime actual para poder continuar
// sin modo testeo más adelante. Esto permite "capturar" el progreso desde un preset.
bool wasTestingMode = GameBootService.IsPresetOverrideActive;

bool success = bootProfile.SaveCurrentGameState(saveSystem);

if (success)
{
    if (wasTestingMode)
    {
        Debug.Log("[SavePoint] 🧪 Partida guardada en MODO TESTEO - El estado runtime actual se ha guardado en el JSON. " +
                  "Ahora puedes desactivar 'usePresetInsteadOfSave' para continuar desde aquí.");
    }
    else
    {
        Debug.Log("[SavePoint] Partida guardada correctamente");
    }
    OnSaveCompleted?.Invoke();
    ShowSaveSuccessFeedback();
}
```

**Propósito**: Informar al usuario que puede desactivar el modo testeo y continuar desde el guardado.

## 📊 Flujo de Datos

### Modo Testeo Activo

```
GameBootService.Awake()
    └─> PrepareActivePreset()
        └─> profile.ShouldBootFromPreset() == true
            └─> profile.EnsureRuntimePresetFromTemplate(bootPreset)
                └─> CopyPreset(bootPreset, runtimePreset)
                    └─> runtimePreset contiene datos del bootPreset

Durante el juego:
    └─> runtimePreset evoluciona (HP, MP, quests, etc.)

SavePoint.DoSave()
    └─> bootProfile.SaveCurrentGameState(saveSystem)
        └─> UpdateRuntimePresetFromCurrentState()
            └─> Sincroniza runtimePreset con estado actual
        └─> SaveProfile(saveSystem)
            └─> BuildSaveDataFromProfile()
                └─> Serializa runtimePreset a JSON
                    └─> savegame.json actualizado ✅
```

### Desactivar Modo Testeo

```
Usuario desactiva usePresetInsteadOfSave en GameBootProfile

GameBootService.Awake() (siguiente boot)
    └─> PrepareActivePreset()
        └─> profile.ShouldBootFromPreset() == false
            └─> saveSystem.HasSave() == true
                └─> profile.LoadProfile(saveSystem)
                    └─> ApplySaveDataToProfile(data)
                        └─> SetRuntimePresetFromSave(data)
                            └─> runtimePreset contiene datos del JSON ✅
```

## 🔍 Verificación del Sistema

### 1. Verificar que el runtime NO se resetea al cambiar escenas

**Código relevante**: `GameBootService.OnSceneLoaded()`

#### ❌ COMPORTAMIENTO ANTERIOR (PROBLEMÁTICO)

```csharp
private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    // ❌ Reseteaba al bootPreset en CADA cambio de escena
    if (_profile != null && _profile.ShouldBootFromPreset() && _profile.bootPreset != null)
    {
        _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
        // ...
    }
}
```

**Problema**: El progreso del runtime se perdía al cambiar de escena.

#### ✅ COMPORTAMIENTO NUEVO (CORRECTO)

```csharp
private static bool _testingModeInitialized; // Flag para controlar primera inicialización

private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    // ✅ Solo aplica el bootPreset la PRIMERA vez
    if (_profile != null && _profile.ShouldBootFromPreset() && _profile.bootPreset != null)
    {
        if (!_testingModeInitialized)
        {
            _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
            // Restaurar quest desde el preset de testeo
            var qm = QuestManager.Instance;
            if (qm != null)
            {
                var preset = _profile.GetActivePresetResolved();
                if (preset != null)
                {
                    qm.RestoreFromProfileFlags(preset.flags);
                }
            }
            _testingModeInitialized = true;
        }
        // else: mantener runtime actual (no resetear)
    }
}
```

**Solución**: El runtime evoluciona libremente entre escenas.

### 2. Gestión del Flag `_testingModeInitialized`

El flag se gestiona en varios puntos:

#### Inicialización en `PrepareActivePreset()`

```csharp
if (profile.ShouldBootFromPreset())
{
    profile.EnsureRuntimePresetFromTemplate(profile.bootPreset);
    ApplyPresetAsLoadedGame(profile);
    _testingModeInitialized = true; // ✅ Marcar como inicializado
}
else if (saveSystem != null && saveSystem.HasSave())
{
    if (profile.LoadProfile(saveSystem))
    {
        _testingModeInitialized = false; // ✅ Modo normal
    }
}
else
{
    // Preset por defecto
    _testingModeInitialized = false; // ✅ Modo normal
}
```

#### Reset en `NewGameReset()`

```csharp
public static void NewGameReset()
{
    if (_profile.ShouldBootFromPreset() && _profile.bootPreset != null)
    {
        _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
        _testingModeInitialized = true; // ✅ Mantener modo testeo
        return;
    }
    
    var save = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
    _profile.NewGameReset(save);
    _testingModeInitialized = false; // ✅ Modo normal
}
```

## ✅ SOLUCIÓN IMPLEMENTADA

### Cambios Realizados

#### 1. GameBootService.cs - Flag de control de inicialización

**Campo añadido**:
```csharp
private static bool _testingModeInitialized;
```

**Propósito**: Controlar si el bootPreset ya se aplicó la primera vez, evitando resetear en cada escena.

#### 2. GameBootService.OnSceneLoaded() - Lógica condicional

**Antes**: Reseteaba al bootPreset siempre
**Ahora**: Solo resetea la primera vez, luego mantiene el runtime

#### 3. SavePoint.cs - Mensaje informativo

**Añadido**: Log especial cuando se guarda en modo testeo

### Resultado Final

✅ El runtime **evoluciona libremente** entre escenas en modo testeo
✅ Puedes **acumular progreso** jugando normalmente
✅ Al guardar en un SavePoint, se guarda el **runtime actual** (no el bootPreset)
✅ Al desactivar el modo testeo, el juego **carga desde el JSON** guardado

