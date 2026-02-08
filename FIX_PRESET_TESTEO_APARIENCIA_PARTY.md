# Fix: Preset de Testeo - Apariencia y Party Members

## 📋 Problema Identificado

Al crear un preset de testeo desde el estado actual del juego, **NO se estaban volcando correctamente**:
1. ❌ La apariencia del personaje (appearance)
2. ❌ Los miembros del party (partyMemberIds)  
3. ❌ El vestuario desbloqueado (unlockedWardrobeIds)

## 🔍 Causa Raíz

El método `GameBootProfile.UpdateRuntimePresetFromCurrentState()` **NO estaba sincronizando el wardrobe** desde el componente `WardrobeInventory`. Aunque sí sincronizaba la apariencia y el party, faltaba la sincronización del wardrobe.

## ✅ Solución Implementada

### 1. **Agregado Método en WardrobeInventory**

**Archivo:** `Assets/Scripts/Player/WardrobeInventory.cs`

```csharp
/// <summary>
/// Obtiene una lista de todos los IDs de wardrobe desbloqueados y persistidos.
/// Se usa para sincronizar con el preset durante el guardado.
/// </summary>
public List<string> GetUnlockedIds()
{
    return new List<string>(_persistedIds);
}
```

### 2. **Sincronización de Wardrobe en GameBootProfile**

**Archivo:** `Assets/Scripts/Core/GameBootProfile.cs`

Agregado en el método `UpdateRuntimePresetFromCurrentState()`:

```csharp
// === NUEVO: sincronizar Wardrobe desde WardrobeInventory ===
if (PlayerService.TryGetComponent<WardrobeInventory>(out var wardrobe, includeInactive: true, allowSceneLookup: true))
{
    p.unlockedWardrobeIds = wardrobe.GetUnlockedIds();
    syncedSystems.Add($"Wardrobe({p.unlockedWardrobeIds?.Count ?? 0})");
    Debug.Log($"[GameBootProfile] Wardrobe sincronizado al preset: {p.unlockedWardrobeIds?.Count ?? 0} items desbloqueados");
}
else
{
    p.unlockedWardrobeIds = new List<string>();
    Debug.LogWarning("[GameBootProfile] WardrobeInventory no disponible - Wardrobe guardado vacío");
}
```

### 3. **Logging Mejorado**

Se agregó logging detallado para diagnosticar problemas:

#### En GameBootProfile:
- Log de apariencia capturada con detalle de cada parte
- Log de wardrobe con cantidad de items
- Warnings si los componentes no están disponibles

#### En PlayerPresetSOEditor:
- Log detallado de apariencia capturada (categoría y partName)
- Log detallado de party members capturados
- Log de wardrobe capturado
- Resumen mejorado en el diálogo que incluye party members y teleport points

## 🧪 Verificación

Para verificar que el sistema funciona correctamente:

1. **En Play Mode**, con tu personaje configurado:
   - Cambia la apariencia
   - Desbloquea items de wardrobe
   - Añade NPCs al party
   
2. **Crea un preset de test**:
   - Inspector del `PlayerPreset_Runtime` → Botón "Crear Test Preset desde Estado Actual"
   
3. **Revisa los logs** de la consola:
   ```
   [GameBootProfile] Apariencia sincronizada: X partes [Body:..., Hair:..., etc.]
   [GameBootProfile] Wardrobe sincronizado al preset: X items desbloqueados
   [GameBootProfile] Party sincronizado al preset: X miembros [...]
   ```

4. **Revisa el preset creado**:
   - Debe tener datos en `appearance`
   - Debe tener IDs en `unlockedWardrobeIds`
   - Debe tener IDs en `partyMemberIds`

## 📊 Flujo de Datos Actualizado

```
╔══════════════════════════════════════════════════════════════╗
║                    GUARDADO (Save Point)                      ║
╠══════════════════════════════════════════════════════════════╣
║                                                               ║
║  1. UpdateRuntimePresetFromCurrentState()                    ║
║     ├─ ModularAutoBuilder → appearance                       ║
║     ├─ WardrobeInventory → unlockedWardrobeIds  ✨ NUEVO    ║
║     ├─ PlayerParty → partyMemberIds                          ║
║     ├─ QuestManager → flags                                  ║
║     ├─ Inventory → inventoryItems                            ║
║     ├─ NarrativeGraphHub → narrativeBlackboards              ║
║     └─ BossProgressTracker → defeatedBossIds                 ║
║                                                               ║
║  2. PlayerSaveData.FromGameBootProfile(runtimePreset)        ║
║     └─ Copia todos los datos del preset → JSON              ║
║                                                               ║
╚══════════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════════╗
║              CREAR PRESET DE TESTEO (Editor)                 ║
╠══════════════════════════════════════════════════════════════╣
║                                                               ║
║  1. UpdateRuntimePresetFromCurrentState()                    ║
║     └─ Mismo proceso que guardado normal                     ║
║                                                               ║
║  2. CopyPresetData(runtimePreset → nuevoPreset)              ║
║     └─ Copia TODOS los campos incluyendo:                    ║
║        ├─ appearance ✅                                       ║
║        ├─ unlockedWardrobeIds ✅                              ║
║        └─ partyMemberIds ✅                                   ║
║                                                               ║
║  3. Guarda como nuevo ScriptableObject                       ║
║                                                               ║
╚══════════════════════════════════════════════════════════════╝
```

## 🎯 Consistencia Total

Ahora **todos los sistemas tienen consistencia**:

### ✅ Guardado Normal (JSON)
- Captura appearance ✓
- Captura wardrobe ✓
- Captura party ✓

### ✅ Guardado Test (ScriptableObject)
- Captura appearance ✓
- Captura wardrobe ✓
- Captura party ✓

### ✅ Carga Normal (JSON → Runtime)
- Restaura appearance ✓
- Restaura wardrobe ✓
- Restaura party ✓

### ✅ Carga Test (Preset → Runtime)
- Restaura appearance ✓
- Restaura wardrobe ✓
- Restaura party ✓

## 📝 Archivos Modificados

1. `Assets/Scripts/Core/GameBootProfile.cs`
   - Agregada sincronización de wardrobe
   - Mejorado logging de apariencia

2. `Assets/Scripts/Player/WardrobeInventory.cs`
   - Agregado método público `GetUnlockedIds()`

3. `Assets/Editor/PlayerPresetSOEditor.cs`
   - Agregado logging detallado de datos capturados
   - Mejorado resumen del diálogo

4. `Assets/Scripts/Core/GameBootProfile.cs` (corrección adicional)
   - Excluido `EXIT_FROM_WOODS_ESTELA` de auto-emisión en corrección narrativa

## 🚀 Resultado

El sistema de testeo ahora funciona **exactamente igual** que el sistema de guardado normal. Los presets de testeo capturan el estado COMPLETO del juego, permitiendo:

- Probar diferentes estados del juego rápidamente
- Compartir estados de juego entre desarrolladores
- Debugging preciso de bugs en estados específicos
- Verificación de que todos los sistemas se guardan/cargan correctamente
