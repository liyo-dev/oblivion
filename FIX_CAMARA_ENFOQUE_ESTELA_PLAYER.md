# ✅ FIX: Corrección del Enfoque de Cámara - Estela y Player

## 🎯 Problemas Identificados

### 1. **Estela se ve de espaldas**
Cuando Estela habla, la cámara la enfoca pero se ve su cogote/espalda en lugar de su cara.

### 2. **Estela mira hacia otro lado**
Estela no está mirando al NPC con quien está hablando, sino que mira en una dirección aleatoria.

### 3. **El NPC aparece parcialmente en el encuadre del Player**
Cuando el Player habla, el NPC (Will) aparece parcialmente visible en el encuadre, cuando debería estar completamente oculto.

## 🔧 Soluciones Implementadas

### 1. **Sistema de Ocultación Mejorado**

Se creó un sistema completo para ocultar/mostrar tanto al Player como al NPC:

```csharp
// ✅ Nuevos métodos agregados
private void HideNPC()     // Oculta al NPC durante planos del Player/Party
private void ShowNPC()     // Restaura visibilidad del NPC
private Renderer[] npcRenderers;  // Variables para guardar estado del NPC
private bool[] npcRenderersWereEnabled;
```

### 2. **Lógica de Ocultación Basada en el Speaker**

**Antes:** La ocultación se basaba solo en el tipo de plano (`shotType`).

**Ahora:** La ocultación se basa en **quién está hablando**:

```csharp
// ✅ NUEVA LÓGICA EN ApplyShotWithContext

if (isPlayerOrPartySpeaking)
{
    // Cuando habla el Player o Party Member (como Estela):
    // → SIEMPRE ocultar al NPC para evitar que salga en el encuadre
    HideNPC();
    ShowPlayer();
    
    // ✅ HACER QUE EL SPEAKER MIRE AL NPC
    MakeSpeakerLookAtNPC(effectiveTarget);
}
else if (isNPCSpeaking)
{
    // Cuando habla el NPC:
    // → Mostrar NPC
    // → Ocultar Player solo en planos cerrados (CloseUp/Medium)
    ShowNPC();
    if (isCloseUpShot)
    {
        HidePlayer();
    }
    else
    {
        ShowPlayer();
    }
}
```

### 3. **Sistema de Rotación de Speakers**

Nuevo método `MakeSpeakerLookAtNPC()` que hace que el speaker mire hacia el NPC durante el diálogo:

```csharp
private void MakeSpeakerLookAtNPC(Transform speaker)
{
    if (speaker == null || currentNPC == null) return;
    
    // Calcular dirección hacia el NPC (solo en plano horizontal)
    Vector3 directionToNPC = currentNPC.position - speaker.position;
    directionToNPC.y = 0;
    
    if (directionToNPC.sqrMagnitude > 0.01f)
    {
        Quaternion targetRotation = Quaternion.LookRotation(directionToNPC);
        speaker.rotation = targetRotation;
    }
}
```

### 4. **Verificación de Party Members**

Nuevo método auxiliar para identificar si un transform es un party member:

```csharp
private bool IsPartyMember(Transform t)
{
    if (!Game.NPC.PlayerParty.HasInstance) return false;
    
    var party = Game.NPC.PlayerParty.Instance;
    return party.Members.Any(m => m != null && m.transform == t);
}
```

### 5. **Restauración al Finalizar Diálogo**

Se asegura que tanto Player como NPC se muestren al terminar:

```csharp
// En EndCinematicImmediate()
ShowPlayer();
ShowNPC();  // ✅ AGREGADO
```

## 📊 Comportamiento Esperado

### Cuando Habla Estela (o cualquier Party Member):
- ✅ **Estela mira hacia el NPC** (Will)
- ✅ **La cámara enfoca la cara de Estela** (no su espalda)
- ✅ **El NPC (Will) está completamente oculto** del encuadre
- ✅ La cámara está **más alejada** (multiplicador 2.5x)

### Cuando Habla el Player:
- ✅ **El Player mira hacia el NPC** (Will)
- ✅ **La cámara enfoca la cara del Player**
- ✅ **El NPC (Will) está completamente oculto** del encuadre
- ✅ La cámara está **más alejada** (multiplicador 2.5x)

### Cuando Habla el NPC (Will):
- ✅ **El NPC está visible**
- ✅ **La cámara enfoca la cara del NPC**
- ✅ **El Player está oculto** solo en planos cerrados (CloseUp/Medium)
- ✅ **El Player está visible** en planos amplios (Wide/OverShoulder)

## 🎬 Combinación con Fix Anterior

Este fix se combina con el fix anterior de distancias y posicionamiento:
- **Distancias aumentadas** (2.2x base, 3m mínimo)
- **Multiplicador especial** para Player/Party (2.5x)
- **Posicionamiento correcto** de la cámara frente a los personajes

## 🧪 Pruebas Recomendadas

1. **Diálogo con Estela en el party:**
   - Verificar que Estela mira a Will
   - Verificar que se ve la cara de Estela
   - Verificar que Will no aparece en el encuadre

2. **Diálogo como Player:**
   - Verificar que el Player mira a Will
   - Verificar que se ve la cara del Player
   - Verificar que Will no aparece en el encuadre

3. **Diálogo con el NPC hablando:**
   - Verificar que se ve la cara del NPC
   - Verificar que el Player se oculta en planos cerrados
   - Verificar que el Player se ve en planos amplios

4. **Finalizar diálogo:**
   - Verificar que todos los personajes se restauran correctamente
   - Verificar que no quedan personajes ocultos

---

**Fecha:** 2025-02-05  
**Archivos Modificados:**
- `DialogueCinematicController.cs`
  - Métodos agregados: `HideNPC()`, `ShowNPC()`, `IsPartyMember()`, `MakeSpeakerLookAtNPC()`
  - Métodos modificados: `ApplyShotWithContext()`, `EndCinematicImmediate()`
  - Variables agregadas: `npcRenderers`, `npcRenderersWereEnabled`

**Líneas modificadas:** ~150 líneas (agregados y modificados)
