# ✅ SOLUCIÓN FINAL: Sistema de IDs Separados para Diálogos

## 🎯 Problema Resuelto

El problema era que había **dos IDs diferentes** que no coincidían:

1. **`speakerNameId`** en el diálogo: `"CHAR_ESTELA"` (ID de localización, se traduce a "Estela")
2. **`persistenceId`** del NPC: `"NPC_InteractiveNarrative_Config_Estela_b17a2d68"` (ID único de asset)

El sistema de cámara buscaba por `persistenceId`, pero el diálogo usaba `speakerNameId` → **No coincidían** → Cámara no cambiaba.

## 🔧 Solución Implementada

He agregado un **nuevo campo** en `NPCInteractiveNarrativeConfig`:

```csharp
[Tooltip("ID del personaje para diálogos (ej: 'CHAR_ESTELA'). Se usa para identificar al speaker en diálogos con múltiples participantes.")]
public string dialogueCharacterId;
```

### Ahora hay 3 IDs con roles específicos:

1. **`persistenceId`**: ID único de asset (autogenerado, largo, para sistema interno)
   - Ejemplo: `"NPC_InteractiveNarrative_Config_Estela_b17a2d68"`
   - **Uso**: Persistencia de estado, guardado de partida

2. **`dialogueCharacterId`** ⭐ **NUEVO**: ID para diálogos (corto, legible)
   - Ejemplo: `"CHAR_ESTELA"`
   - **Uso**: Identificar speaker en diálogos, cambio de cámara, localización

3. **`narrativeID`**: ID narrativo (opcional)
   - Ejemplo: `"Estela"`
   - **Uso**: Identificación en quests y narrativa

### Prioridad de Búsqueda

El sistema ahora busca en este orden:

1. **`dialogueCharacterId`** (PRIORIDAD 1) ✅ **Nuevo, más específico**
2. `persistenceId` (PRIORIDAD 2) - Compatibilidad hacia atrás
3. `narrativeID` (PRIORIDAD 3)
4. `GameObject.name` (PRIORIDAD 4) - Último recurso

## 📋 Configuración Necesaria

### 1. En el NPC (Inspector):

Selecciona el ScriptableObject `NPC_InteractiveNarrative_Config_Estela`:

```
NPCInteractiveNarrativeConfig:
  persistenceId: "NPC_InteractiveNarrative_Config_Estela_b17a2d68"  ← Ya existe (auto)
  dialogueCharacterId: "CHAR_ESTELA"  ← ⭐ AGREGAR ESTE NUEVO CAMPO
```

### 2. En el DialogueAsset:

```
Línea 0:
  Speaker Name Id: "CHAR_ELDRAN"    ← Debe coincidir con dialogueCharacterId de Eldran

Línea 1:
  Speaker Name Id: "CHAR_ESTELA"    ← Debe coincidir con dialogueCharacterId de Estela
  Text: "Gracias por rescatarme"

Línea 2:
  Speaker Name Id: "CHAR_WILL"      ← Debe coincidir con dialogueCharacterId del Player
  Is Player Speaking: ✓ true
```

### 3. En el LocalizationManager:

```
ID de Localización | Nombre Mostrado
-------------------|----------------
CHAR_ELDRAN        | Eldran
CHAR_ESTELA        | Estela
CHAR_WILL          | Will
```

## 🎬 Flujo Completo

1. **DialogueLine** contiene:
   ```
   speakerNameId: "CHAR_ESTELA"
   ```

2. **LocalizationManager** traduce para UI:
   ```
   "CHAR_ESTELA" → "Estela" (mostrado en pantalla)
   ```

3. **DialogueCinematicController** busca speaker:
   ```csharp
   FindSpeakerTransform("CHAR_ESTELA")
   → Busca en PlayerParty.Members
   → Compara con dialogueCharacterId primero ✅
   → config.dialogueCharacterId == "CHAR_ESTELA" → Match!
   → Encuentra a NPC_Estela
   → Cambia cámara a Estela
   ```

## ✅ Ventajas del Sistema

✅ **IDs Cortos y Legibles**: `"CHAR_ESTELA"` en lugar de `"NPC_InteractiveNarrative_Config_Estela_b17a2d68"`

✅ **Separación de Responsabilidades**:
- `persistenceId` → Sistema interno (guardado)
- `dialogueCharacterId` → Diálogos y UI (localización)

✅ **Compatibilidad Hacia Atrás**: Si `dialogueCharacterId` está vacío, busca por `persistenceId` (fallback)

✅ **Sin Colisiones**: Cada ID tiene su propósito específico

✅ **Fácil de Configurar**: Un solo campo nuevo para agregar

## 🔍 Debugging

Los logs mejorados mostrarán qué método de búsqueda funcionó:

```
[DialogueCinematicController] 🎯 Match por dialogueCharacterId: 'CHAR_ESTELA' == 'CHAR_ESTELA' ✅
[DialogueCinematicController] 👥 Speaker 'CHAR_ESTELA' encontrado en party: NPC_Estela
```

Si no funciona, verás cuál fue el problema:

```
[DialogueCinematicController] 🎯 Match por persistenceId: 'NPC_..._b17a2d68' != 'CHAR_ESTELA' ❌
[DialogueCinematicController] ⚠️ No se encontró Transform para speaker 'CHAR_ESTELA'
```

## 📝 Checklist de Migración

Para cada NPC que participa en diálogos multi-speaker:

- [ ] Abrir el `NPCInteractiveNarrativeConfig` del NPC en Inspector
- [ ] Agregar el campo **`dialogueCharacterId`** con el ID corto (ej: `"CHAR_ESTELA"`)
- [ ] Verificar que coincida con el `speakerNameId` usado en los DialogueAssets
- [ ] Verificar que el LocalizationManager tenga la traducción del ID
- [ ] Probar en juego y verificar logs

## 💡 Convención Recomendada

```
CHAR_<NOMBRE>  → Para personajes principales
  - CHAR_ELDRAN
  - CHAR_ESTELA
  - CHAR_WILL
  - CHAR_ARIA
  - CHAR_LUMINA

NPC_<NOMBRE>   → Para NPCs secundarios
  - NPC_GUARDIA_01
  - NPC_MERCADER
  - NPC_ALDEANO_A

Player         → Para el jugador
```

## 📁 Archivos Modificados

### NPCInteractiveNarrativeConfig.cs
- **Nuevo campo**: `public string dialogueCharacterId;`
- Se configura en el Inspector del ScriptableObject

### DialogueCinematicController.cs
- `FindSpeakerTransform()`: Busca primero por `dialogueCharacterId`
- Logs mejorados para debugging
- Búsqueda en party members y escena actualizada

## 🎯 Ejemplo Real: Estela

### NPCInteractiveNarrativeConfig (Estela):
```
persistenceId: "NPC_InteractiveNarrative_Config_Estela_b17a2d68"
dialogueCharacterId: "CHAR_ESTELA"  ← ⭐ Nuevo
```

### DialogueAsset:
```
speakerNameId: "CHAR_ESTELA"  ← Coincide con dialogueCharacterId
```

### LocalizationManager:
```
CHAR_ESTELA → "Estela"
```

### Resultado:
- ✅ UI muestra: **"Estela"**
- ✅ Cámara enfoca: **NPC_Estela**
- ✅ Sistema robusto y escalable

---

**Fecha**: 2026-02-05  
**Estado**: ✅ Implementado completamente  
**Requiere**: Configurar `dialogueCharacterId` en cada NPC que participe en diálogos
