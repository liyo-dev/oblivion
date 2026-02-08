# ✅ IMPLEMENTADO: Cambio de Cámara Basado en Speaker en Diálogos

## 🎯 Problema Resuelto

Cuando hay diálogos con múltiples participantes (Eldran, Estela, Player), la cámara no cambiaba automáticamente según quién hablaba. La cámara se quedaba enfocando siempre al NPC principal.

### Ejemplo del problema:
```
Diálogo con Eldran después de volver con Estela del bosque:
- Línea 1: Eldran habla → Cámara en Eldran ✅
- Línea 2: Estela habla → Cámara seguía en Eldran ❌ 
- Línea 3: Player habla → Cámara seguía en Eldran ❌
```

## 🔧 Solución Implementada

### 1. Sistema de Tracking de Speakers

He agregado un sistema que detecta automáticamente quién habla en cada línea de diálogo y cambia la cámara en consecuencia:

```csharp
// Nuevos campos en DialogueCinematicController
private string currentSpeakerId;        // ID del speaker actual
private Transform currentSpeaker;        // Transform del speaker actual
private Dictionary<string, Transform> speakerCache;  // Cache para performance
```

### 2. Detección Automática de Speaker

El sistema determina quién habla basándose en:
1. **`speakerNameId`** del `DialogueLine` (ej: "CHAR_ELDRAN", "CHAR_ESTELA")
2. **`isPlayerSpeaking`** flag
3. Búsqueda automática en:
   - Player actual
   - NPC principal (con quien iniciamos el diálogo)
   - **Party members** (busca en `PlayerParty.Instance.Members`)
   - NPCs en la escena (fallback)

### 3. Búsqueda Inteligente de Party Members

```csharp
// Busca party members por:
// 1. persistenceId (configurado en NPCInteractiveNarrativeConfig)
// 2. narrativeID (NPCQuestConfig)
// 3. GameObject.name (fallback)
```

### 4. Cambio Automático de Cámara

Cuando detecta que el speaker cambió:
- ✅ Cambia la cámara para enfocar al nuevo speaker
- ✅ Usa planos apropiados del `DialogueCinematicProfile`
- ✅ Alterna entre planos para variedad visual
- ✅ Mantiene transiciones suaves

## 📋 Configuración Requerida

### En el DialogueAsset:
1. **Asignar `speakerNameId`** en cada línea:
   ```
   Línea 1:
     speakerNameId: "CHAR_ELDRAN"
     text: "Has traído a Estela"
   
   Línea 2:
     speakerNameId: "CHAR_ESTELA"  ← IMPORTANTE
     text: "Gracias por rescatarme"
   
   Línea 3:
     speakerNameId: "CHAR_WILL"    ← o marcar isPlayerSpeaking=true
     text: "De nada"
   ```

2. **El `speakerNameId` debe coincidir con**:
   - Para el Player: `"Player"` o `isPlayerSpeaking = true`
   - Para el NPC principal: El nombre del GameObject o dejar vacío
   - Para party members: El `persistenceId` configurado en su `NPCInteractiveNarrativeConfig`

### En el NPC (Party Member):
```
NPCInteractiveNarrativeConfig:
  persistenceId: "CHAR_ESTELA"  ← Debe coincidir con speakerNameId del diálogo
```

## 🎮 Flujo Completo

1. **Diálogo inicia** → `StartCinematic()` se llama
   - Se inicializa el cache de speakers
   - Se pre-cachean Player y NPC principal

2. **Cada línea de diálogo** → `OnDialogueLineAdvanced()` se llama
   - Se detecta el `speakerNameId` de la línea actual
   - Se compara con el speaker anterior
   - Si cambió → `CheckAndAdjustCameraForSpeaker()` se ejecuta

3. **Búsqueda del speaker**:
   - Si es "Player" → usa `currentPlayer`
   - Si es el NPC principal → usa `currentNPC`
   - Si es otro → busca en `PlayerParty.Members`
   - Cachea el resultado para próximas líneas

4. **Cambio de cámara**:
   - Aplica plano apropiado del `DialogueCinematicProfile`
   - Usa el Transform del speaker como target
   - Transición suave entre planos

## 🔍 Sistema de Debug

Los logs te ayudarán a diagnosticar problemas:

```
[DialogueCinematicController] 🎬 Speaker cambió: 'CHAR_ELDRAN' → 'CHAR_ESTELA' (NPC_Estela)
[DialogueCinematicController] 👥 Speaker 'CHAR_ESTELA' encontrado en party: NPC_Estela
```

Si ves `❌ NO encontrado`, verifica:
1. El `speakerNameId` en el DialogueAsset
2. El `persistenceId` en el NPC
3. Que el NPC esté en el party (`PlayerParty.Instance.Members`)

## 📁 Archivos Modificados

### DialogueCinematicController.cs
- `OnDialogueLineAdvanced()`: Ahora recibe `DialogueLine` completo
- `CheckAndAdjustCameraForSpeaker()`: Detecta cambios de speaker (NUEVO)
- `DetermineSpeakerId()`: Obtiene el ID del speaker (NUEVO)
- `FindSpeakerTransform()`: Busca el Transform del speaker (NUEVO)
- `ApplyShotForSpeaker()`: Aplica plano apropiado (NUEVO)
- `ApplyShotWithContext()`: Acepta `targetOverride` opcional
- `StartCinematic()`: Inicializa cache de speakers

### DialogueManager.cs
- Modificado `OnDialogueLineAdvanced()` para pasar el `DialogueLine` completo

## 💡 Ventajas del Sistema

✅ **Automático**: No requiere configuración especial de cámaras por línea  
✅ **Performance**: Cache de speakers para búsquedas rápidas  
✅ **Flexible**: Soporta Player, NPCs, y Party Members  
✅ **Robusto**: Múltiples métodos de búsqueda (persistenceId, narrativeID, nombre)  
✅ **Compatible**: No rompe diálogos existentes que no usan múltiples speakers  

## 🎬 Resultado Final

**ANTES**:
```
Eldran habla → 📷 Eldran
Estela habla → 📷 Eldran (sin cambio) ❌
Player habla → 📷 Eldran (sin cambio) ❌
```

**AHORA**:
```
Eldran habla → 📷 Eldran ✅
Estela habla → 📷 Estela (cambia automáticamente) ✅
Player habla → 📷 Player (cambia automáticamente) ✅
```

## 🚀 Próximos Pasos (Opcional)

Para mejorar aún más:
1. Agregar planos específicos por tipo de speaker en el Profile
2. Implementar `forcedShotType` por línea (requiere alinear enums)
3. Agregar configuración de prioridad de cámaras por personaje
4. Sistema de "shot-reverse-shot" automático para conversaciones

---

**Fecha**: 2026-02-05  
**Estado**: ✅ Implementado y funcionando  
**Requiere**: Configurar `speakerNameId` en DialogueAssets existentes
