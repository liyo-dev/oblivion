# Sistema de Localización de Interacciones

## 📋 Descripción

Los elementos interactivos (NPCs, SavePoints, objetos, etc.) ahora soportan traducciones multiidioma para sus prompts y mensajes de interacción.

## 🔧 Componentes Actualizados

### 1. **SavePoint.cs**
El componente de puntos de guardado ahora soporta localización.

#### Campos agregados:
```csharp
[Header("Localización")]
public string promptId;    // ID: "SAVEPOINT_PROMPT"
public string prompt;      // Fallback: "Guardar partida (E)"
```

#### Método helper:
```csharp
public string GetLocalizedPrompt()
```

### Configuración en Unity:
```yaml
SavePoint Component:
  Prompt Id: "SAVEPOINT_PROMPT"
  Prompt: "Guardar partida [E]" (fallback)
```

## 📁 Traducciones de Interacción

### Español (ui_es.json)
```json
{
  "key": "SAVEPOINT_PROMPT",
  "value": "Guardar partida [E]"
},
{
  "key": "SAVEPOINT_SAVING",
  "value": "Guardando partida..."
},
{
  "key": "SAVEPOINT_SAVED",
  "value": "¡Partida guardada con éxito!"
},
{
  "key": "INTERACT_NPC_TALK",
  "value": "Hablar [A]"
},
{
  "key": "INTERACT_CHEST_OPEN",
  "value": "Abrir cofre [A]"
},
{
  "key": "INTERACT_DOOR_OPEN",
  "value": "Abrir puerta [A]"
},
{
  "key": "INTERACT_ITEM_PICKUP",
  "value": "Recoger [A]"
},
{
  "key": "INTERACT_EXAMINE",
  "value": "Examinar [A]"
}
```

### Inglés (ui_en.json)
```json
{
  "key": "SAVEPOINT_PROMPT",
  "value": "Save Game [E]"
},
{
  "key": "SAVEPOINT_SAVING",
  "value": "Saving game..."
},
{
  "key": "SAVEPOINT_SAVED",
  "value": "Game saved successfully!"
},
{
  "key": "INTERACT_NPC_TALK",
  "value": "Talk [A]"
},
{
  "key": "INTERACT_CHEST_OPEN",
  "value": "Open chest [A]"
},
{
  "key": "INTERACT_DOOR_OPEN",
  "value": "Open door [A]"
},
{
  "key": "INTERACT_ITEM_PICKUP",
  "value": "Pick up [A]"
},
{
  "key": "INTERACT_EXAMINE",
  "value": "Examine [A]"
}
```

## 🎯 Convenciones de nombres para Interacciones

### SavePoints:
- `SAVEPOINT_PROMPT` - Mensaje de interacción
- `SAVEPOINT_SAVING` - Mensaje mientras guarda
- `SAVEPOINT_SAVED` - Mensaje de confirmación

### NPCs:
- `INTERACT_NPC_TALK` - Hablar con NPC
- `INTERACT_NPC_<NOMBRE>` - NPC específico

### Objetos:
- `INTERACT_CHEST_OPEN` - Abrir cofre
- `INTERACT_DOOR_OPEN` - Abrir puerta
- `INTERACT_ITEM_PICKUP` - Recoger item
- `INTERACT_EXAMINE` - Examinar objeto

### Sistema general:
- `Interact_Press` - "Presiona [A] para interactuar"
- `Interact_Talk` - "Hablar"
- `Interact_Open` - "Abrir"
- `Interact_Examine` - "Examinar"
- `Interact_PickUp` - "Recoger"

## 🔄 Cómo agregar localización a otros elementos interactivos

### Paso 1: Agregar campos de localización
```csharp
[Header("Localización")]
[Tooltip("ID de localización (ej: 'INTERACT_DOOR_OPEN')")]
public string promptId;

[Tooltip("Texto del prompt (fallback)")]
public string prompt = "Abrir puerta [A]";
```

### Paso 2: Crear método helper
```csharp
public string GetLocalizedPrompt()
{
    if (!string.IsNullOrEmpty(promptId) && LocalizationManager.Instance != null)
    {
        return LocalizationManager.Instance.Get(promptId, prompt);
    }
    return prompt;
}
```

### Paso 3: Usar en lugar del texto directo
```csharp
// Antes:
Debug.Log(prompt);

// Después:
string localizedText = GetLocalizedPrompt();
Debug.Log(localizedText);
```

### Paso 4: Agregar traducciones en JSON
Agregar las claves en `ui_es.json` y `ui_en.json`:
```json
{
  "key": "INTERACT_DOOR_OPEN",
  "value": "Abrir puerta [A]"
}
```

## 📝 Ejemplo completo: Chest (Cofre)

### 1. Script del cofre:
```csharp
public class Chest : MonoBehaviour
{
    [Header("Localización")]
    public string promptId = "INTERACT_CHEST_OPEN";
    public string prompt = "Abrir cofre [A]";
    
    public string GetLocalizedPrompt()
    {
        if (!string.IsNullOrEmpty(promptId) && LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.Get(promptId, prompt);
        }
        return prompt;
    }
    
    void ShowPrompt()
    {
        string text = GetLocalizedPrompt();
        // Mostrar en UI...
    }
}
```

### 2. Agregar traducciones:
**ui_es.json:**
```json
{
  "key": "INTERACT_CHEST_OPEN",
  "value": "Abrir cofre [A]"
}
```

**ui_en.json:**
```json
{
  "key": "INTERACT_CHEST_OPEN",
  "value": "Open chest [A]"
}
```

## ✅ Componentes con localización

| Componente | Estado | Descripción |
|------------|--------|-------------|
| SavePoint | ✅ Implementado | Prompts de guardado |
| DialogueManager | ✅ Implementado | Diálogos completos |
| QuestData | ✅ Implementado | Nombres y descripciones |
| Interactable | ⚠️ Usa íconos | No muestra texto directamente |
| NPCs | 🔄 Via diálogos | Usan el sistema de diálogos |

## 🎮 Sistema de Hints visuales

Tu sistema de interacción actual usa **íconos visuales (hint)** en lugar de texto, lo cual es una buena práctica para UI limpia. Los íconos son universales y no necesitan traducción.

Si quieres agregar texto a los hints:
1. Agrega un componente `TextMeshProUGUI` al GameObject del hint
2. Actualiza el `Interactable` para configurar el texto
3. Usa los IDs de localización del sistema

## 🚀 Ventajas del sistema

✅ **Multiidioma**: Todos los prompts se traducen automáticamente  
✅ **Fallback robusto**: Si falta traducción, usa el texto directo  
✅ **Consistente**: Mismo sistema para todos los componentes  
✅ **Extensible**: Fácil de agregar a nuevos componentes  
✅ **Reutilizable**: Los mismos IDs se pueden usar en múltiples lugares  

## 📋 Resumen de archivos actualizados

1. ✅ **SavePoint.cs** - Soporte de localización agregado
2. ✅ **ui_es.json** - 8 traducciones de interacción agregadas
3. ✅ **ui_en.json** - 8 traducciones de interacción agregadas
4. ✅ Este documento de guía creado

Todo listo para usar. El sistema de localización ahora cubre:
- ✅ Menús y UI general
- ✅ Cinemáticas y subtítulos
- ✅ Diálogos completos
- ✅ Quests y misiones
- ✅ Prompts de interacción

