# 📝 GUÍA: Configuración Completa de Diálogos Multi-Speaker

## 🎯 Solución: Separar ID de Speaker vs Nombre Mostrado

El sistema **ya está diseñado correctamente**. Usa:
- `speakerNameId` como **identificador único** para la cámara
- **LocalizationManager** para traducir el ID al nombre real en la UI

## 📋 Configuración Paso a Paso

### 1. Configurar el LocalizationManager

Debes agregar las traducciones para cada personaje en tu tabla de localización:

```
ID de Localización    | Español      | Inglés
---------------------|--------------|-------------
CHAR_ELDRAN          | Eldran       | Eldran
CHAR_ESTELA          | Estela       | Estela
CHAR_WILL            | Will         | Will
Player               | [PlayerName] | [PlayerName]
```

**Dónde configurarlo**:
- Si usas un archivo CSV/JSON de localización → Agregar estas entradas
- Si usas ScriptableObjects → Agregar las claves en tu `LocalizationTable`
- Si usas un sistema custom → Verificar dónde se configuran los strings

### 2. Configurar el DialogueAsset

En el Inspector, para cada línea:

```
Línea 0:
  Speaker Name Id: "CHAR_ELDRAN"    ← ID (no el nombre)
  Text Id: "DLG_ELDRAN_MISSION_01"
  Text: "¡Has vuelto!"

Línea 1:
  Speaker Name Id: "CHAR_ESTELA"    ← ID (no el nombre)
  Text Id: "DLG_ELDRAN_MISSION_02"  
  Text: "Estoy bien, gracias"

Línea 2:
  Speaker Name Id: "CHAR_WILL"      ← o usar "Player"
  Is Player Speaking: ✓ true
  Text Id: "DLG_ELDRAN_MISSION_03"
  Text: "Fue un placer"
```

### 3. Configurar el NPC (Party Member)

El `persistenceId` debe coincidir con el `speakerNameId`:

```
GameObject: NPC_Estela
  └─ NPCBehaviourManagerV2
      └─ Configuration
          └─ InteractiveNarrativeConfig
              └─ persistenceId: "CHAR_ESTELA"  ← Mismo ID que speakerNameId
```

## 🎬 Flujo Completo

1. **DialogueLine** contiene:
   ```
   speakerNameId: "CHAR_ESTELA"
   ```

2. **DialogueManager** traduce para la UI:
   ```csharp
   LocalizationManager.Get("CHAR_ESTELA") → "Estela"
   ```
   - En la UI se muestra: **"Estela"** ✅

3. **DialogueCinematicController** busca al speaker:
   ```csharp
   FindSpeakerTransform("CHAR_ESTELA")
   → Busca en PlayerParty.Members donde persistenceId == "CHAR_ESTELA"
   → Encuentra a NPC_Estela
   → Cambia cámara a Estela
   ```

## 🔧 Si No Tienes LocalizationManager

Si tu juego no usa localización o aún no está configurada, tienes 2 opciones:

### Opción A: Usar Nombres Directos (Simple pero limitado)

```
DialogueLine:
  speakerNameId: "Estela"  ← Nombre directo (sin ID)

NPC:
  persistenceId: "Estela"  ← Mismo nombre
```

**Desventaja**: No podrás localizar a otros idiomas más adelante.

### Opción B: Implementar LocalizationManager Básico

Si no existe, el sistema usa el ID como fallback:
```csharp
speakerNameToShow = LocalizationManager.Instance.Get(
    line.speakerNameId,  // "CHAR_ESTELA"
    line.speakerNameId   // Fallback: si no encuentra, usa "CHAR_ESTELA"
);
```

Esto significa que **funcionará**, pero mostrará "CHAR_ESTELA" en lugar de "Estela".

## ✅ Checklist de Configuración

- [ ] **LocalizationManager configurado** con IDs de personajes
  - [ ] CHAR_ELDRAN → "Eldran"
  - [ ] CHAR_ESTELA → "Estela"
  - [ ] CHAR_WILL → "Will" (o el nombre de tu player)

- [ ] **DialogueAssets actualizados** con `speakerNameId` correcto
  - [ ] Líneas de Eldran: `speakerNameId = "CHAR_ELDRAN"`
  - [ ] Líneas de Estela: `speakerNameId = "CHAR_ESTELA"`
  - [ ] Líneas del Player: `speakerNameId = "CHAR_WILL"` o `isPlayerSpeaking = true`

- [ ] **NPCs configurados** con `persistenceId` correcto
  - [ ] Estela: `persistenceId = "CHAR_ESTELA"`
  - [ ] Otros party members con sus IDs correspondientes

## 🔍 Debugging

### Si ves el ID en lugar del nombre en la UI:

**Causa**: LocalizationManager no tiene la traducción

**Solución**: Agregar la entrada en la tabla de localización:
```
CHAR_ESTELA → Estela
```

### Si la cámara no cambia:

**Causa**: El `persistenceId` del NPC no coincide con el `speakerNameId`

**Logs a revisar**:
```
[DialogueCinematicController] 🎬 Speaker cambió: 'CHAR_ELDRAN' → 'CHAR_ESTELA'
[DialogueCinematicController] 👥 Speaker 'CHAR_ESTELA' encontrado en party: NPC_Estela ✅
```

o

```
[DialogueCinematicController] ⚠️ No se encontró Transform para speaker 'CHAR_ESTELA' ❌
```

## 💡 Convención Recomendada de IDs

Para mantener consistencia:

```
CHAR_<NOMBRE_MAYUSCULAS>  → Para personajes
  Ejemplos:
    - CHAR_ELDRAN
    - CHAR_ESTELA
    - CHAR_WILL
    - CHAR_ARIA
    - CHAR_LUMINA

Player o CHAR_PLAYER       → Para el jugador
```

Esta convención:
- ✅ Fácil de identificar en código
- ✅ Evita colisiones con otros IDs
- ✅ Clara separación entre ID y nombre mostrado
- ✅ Facilita búsqueda/reemplazo masivo

## 📁 Archivos a Configurar

1. **Tabla de Localización** (CSV, JSON, o ScriptableObject)
   - Agregar traducciones de IDs de personajes

2. **DialogueAssets** (ScriptableObjects)
   - Actualizar `speakerNameId` en cada línea

3. **NPCs** (Prefabs o escena)
   - Configurar `persistenceId` en `NPCInteractiveNarrativeConfig`

## 🎯 Ejemplo Completo: Diálogo de Eldran

### Tabla de Localización:
```csv
Key,Spanish,English
CHAR_ELDRAN,Eldran,Eldran
CHAR_ESTELA,Estela,Estela
CHAR_WILL,Will,Will
DLG_ELDRAN_TURNIN_01,"¡Has vuelto con Estela!","You brought Estela back!"
DLG_ELDRAN_TURNIN_02,"Gracias por rescatarme.","Thank you for rescuing me."
DLG_ELDRAN_TURNIN_03,"Fue un placer ayudar.","It was a pleasure to help."
```

### DialogueAsset (DLG_ELDRAN_MISSION_TURNIN):
```
Lines: 3

[0] CHAR_ELDRAN:
    speakerNameId: "CHAR_ELDRAN"
    textId: "DLG_ELDRAN_TURNIN_01"
    text: "¡Has vuelto con Estela!" (fallback)

[1] CHAR_ESTELA:
    speakerNameId: "CHAR_ESTELA"
    textId: "DLG_ELDRAN_TURNIN_02"
    text: "Gracias por rescatarme." (fallback)

[2] CHAR_WILL:
    speakerNameId: "CHAR_WILL"
    isPlayerSpeaking: true
    textId: "DLG_ELDRAN_TURNIN_03"
    text: "Fue un placer ayudar." (fallback)
```

### NPC Estela (Prefab):
```
NPC_Estela
  └─ NPCBehaviourManagerV2
      └─ Configuration
          └─ NPCInteractiveNarrativeConfig
              └─ persistenceId: "CHAR_ESTELA"
```

### Resultado:
- **UI mostrará**: "Eldran", "Estela", "Will"
- **Cámara enfocará**: Eldran → Estela → Player
- **Todo automático** ✅

---

**Fecha**: 2026-02-05  
**Estado**: ✅ Sistema completo, solo requiere configuración  
**Prioridad**: Configurar LocalizationManager primero
