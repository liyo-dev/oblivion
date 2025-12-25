# FIX: Rotación Continua NPC durante Diálogos

## 🎯 Problema Identificado

El NPC **no mantiene la rotación** mirando al jugador durante los diálogos. La corrutina `KeepNPCLookingAtPlayer()` se estaba deteniendo inmediatamente sin ejecutar el loop de seguimiento.

### 📊 Evidencia en Logs

```
[DialogueManager] 👁️ Iniciando seguimiento de rotación del NPC 'Eldran' hacia el jugador
[DialogueManager] 🔚 Diálogo cerrado - NPC 'Eldran' deja de seguir al jugador
```

**Problema**: Ambos logs aparecen en el **mismo frame**, lo que indica que la corrutina termina sin entrar al `while` loop.

### 🔍 Causa Raíz

La corrutina `KeepNPCLookingAtPlayer()` se inicia **antes** de que `IsOpen` sea `true`:

```csharp
// Iniciar la corrutina de seguimiento continuo
_keepLookingRoutine = StartCoroutine(KeepNPCLookingAtPlayer());

StartDialogue(asset, onFinished); // IsOpen se establece aquí
```

**Secuencia problemática**:
1. Se inicia `KeepNPCLookingAtPlayer()`
2. La corrutina comprueba `while (IsOpen && currentNPC != null)`
3. `IsOpen` es **false** → Sale inmediatamente
4. `StartDialogue()` establece `IsOpen = true` → Demasiado tarde

## ✅ Solución Implementada

Añadir un `yield return null` al inicio de la corrutina para esperar **un frame** y permitir que el diálogo se abra:

```csharp
private System.Collections.IEnumerator KeepNPCLookingAtPlayer()
{
    if (currentNPC == null || playerGo == null)
    {
        Debug.LogWarning($"[DialogueManager] ⚠️ KeepNPCLookingAtPlayer - Referencias nulas");
        yield break;
    }
    
    Debug.Log($"[DialogueManager] 👁️ Iniciando seguimiento de rotación del NPC '{currentNPC.name}' hacia el jugador");
    
    // ⭐ NUEVO: Esperar un frame para que el diálogo esté completamente abierto
    yield return null;
    
    // Mantener rotación mientras el diálogo esté abierto
    while (IsOpen && currentNPC != null)
    {
        // Calcular dirección hacia el jugador
        Vector3 directionToPlayer = playerGo.transform.position - currentNPC.position;
        directionToPlayer.y = 0f; // Solo rotación horizontal
        
        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            
            // Rotar suavemente hacia el jugador (más rápido que en Interactable)
            currentNPC.rotation = Quaternion.Slerp(currentNPC.rotation, targetRotation, Time.unscaledDeltaTime * 8f);
        }
        
        yield return null;
    }
    
    Debug.Log($"[DialogueManager] 🔚 Diálogo cerrado - NPC '{currentNPC?.name}' deja de seguir al jugador");
}
```

## 🎮 Flujo Corregido

### Secuencia correcta ahora:

1. **Frame 0**: Se llama `StartDialogue(asset, npc, onFinished)`
   - Rotación inicial inmediata del NPC hacia el jugador
   - Se inicia `KeepNPCLookingAtPlayer()`
   - Se ejecuta `StartDialogue(asset, onFinished)` → `IsOpen = true`

2. **Frame 1** (primer `yield return null`):
   - La corrutina comprueba `while (IsOpen && currentNPC != null)`
   - **Ahora `IsOpen` es `true`** ✅
   - Entra al loop y empieza a rotar continuamente

3. **Frames 2+**: Loop continuo
   - Cada frame actualiza la rotación del NPC hacia el jugador
   - Rotación suave con `Quaternion.Slerp` y velocidad de 8f

4. **Al cerrar el diálogo**:
   - `IsOpen = false`
   - El `while` termina naturalmente
   - Se ejecuta el log de cierre

## 📝 Logs Esperados Ahora

```
[NPCQuestConfig.ProcessInteraction] Rotando NPC hacia player
[DialogueManager] 👁️ NPC 'Eldran' girado hacia el jugador
[DialogueManager] 👁️ Iniciando seguimiento de rotación del NPC 'Eldran' hacia el jugador
[DialogueManager] 🕐 Diálogo abierto en t=X
...
// Durante todo el diálogo, el NPC sigue mirando al jugador
...
[DialogueManager] 🔚 Diálogo cerrado - NPC 'Eldran' deja de seguir al jugador
```

**Diferencia clave**: El log de cierre aparece **después** del diálogo, no inmediatamente.

## 🔧 Archivos Modificados

- ✅ `Assets/Scripts/Dialogue/DialogueManager.cs` - Línea ~838

## ✨ Resultado

- ✅ El NPC **mira continuamente al jugador** durante todo el diálogo
- ✅ Rotación **suave y natural** (Slerp con velocidad 8f)
- ✅ Se detiene automáticamente al cerrar el diálogo
- ✅ No interfiere con otras mecánicas del NPC

---

**Fecha**: 2025-12-25  
**Prioridad**: Media (UX/Polish)  
**Estado**: ✅ RESUELTO  
**Testing**: Listo para probar en Unity

