# Fix: Sincronización de RoomExitBlocker - Solo un GameObject se desbloquea

## 🔴 PROBLEMA DETECTADO

Tienes 3 GameObjects con el script `RoomExitBlocker`:
- **WoodsExitBlockerRight**
- **WoodsExitBlockerLeft**
- **WoodsExitBlockerBottom**

Los 3 tienen configuración idéntica:
- `Requirement Mode`: `SpecificQuestsStarted`
- `Required Quest Refs`: `Q_ELDRAN_MISSION0` (que internamente usa el questId `READ_THE_LETTER_DESC`)

**Sin embargo, solo el "Bottom" se desbloquea cuando la misión se inicia.**

---

## 🔍 CAUSA RAÍZ

El problema era que **cada instancia del script manejaba su propia suscripción al evento `OnQuestsChanged` de forma independiente**. Cuando el evento se disparaba, **solo algunas instancias lo recibían correctamente**, posiblemente debido a:

1. **Problemas de timing de suscripción**: Si un GameObject se suscribe tarde o se activa después de que el evento ya se disparó, no recibe la actualización.

2. **Orden de inicialización inconsistente**: Unity no garantiza el orden de ejecución de múltiples instancias del mismo script, causando que solo algunas se actualicen.

3. **El evento solo notificaba a la instancia suscrita**: Cada instancia llamaba `EvaluateAndApplyState()` solo para sí misma, sin coordinar con las demás.

---

## ✅ SOLUCIÓN IMPLEMENTADA

Se implementó un **sistema de sincronización global** usando un registro estático de todas las instancias activas:

### Cambios realizados:

1. **Registro estático de instancias**:
```csharp
private static readonly List<RoomExitBlocker> _allInstances = new List<RoomExitBlocker>();
```

2. **Registro/Desregistro automático** en `OnEnable`/`OnDisable`:
```csharp
void OnEnable()
{
    if (!_allInstances.Contains(this))
        _allInstances.Add(this);
    // ...
}

void OnDisable()
{
    _allInstances.Remove(this);
    // ...
}
```

3. **Actualización global cuando cualquier instancia detecta cambios**:
```csharp
private void HandleQuestsChanged()
{
    // Forzar reevaluación de TODAS las instancias activas
    for (int i = _allInstances.Count - 1; i >= 0; i--)
    {
        if (_allInstances[i] != null)
            _allInstances[i].EvaluateAndApplyState();
    }
}
```

4. **Logs de debug mejorados** para rastrear el comportamiento de cada GameObject:
   - Log cuando se suscribe al QuestManager
   - Log cuando cambia el estado (bloqueado/desbloqueado)
   - Log con el nombre del GameObject para identificar cuál está actuando
   - Log cuando se verifica el estado de cada quest específica

5. **Método público para forzar reevaluación manual**:
```csharp
public void ForceReevaluate()
{
    EvaluateAndApplyState();
}
```

---

## 🎮 CÓMO PROBAR

### Paso 1: Activar Debug Logs
En Unity, selecciona los 3 GameObjects y **marca la casilla "Debug Logs"** en el inspector:
- WoodsExitBlockerRight → Debug Logs ✓
- WoodsExitBlockerLeft → Debug Logs ✓
- WoodsExitBlockerBottom → Debug Logs ✓

### Paso 2: Iniciar el juego y verificar
Al iniciar el juego, deberías ver en la consola:
```
[RoomExitBlocker:WoodsExitBlockerRight] Suscrito a QuestManager
[RoomExitBlocker:WoodsExitBlockerLeft] Suscrito a QuestManager
[RoomExitBlocker:WoodsExitBlockerBottom] Suscrito a QuestManager
```

### Paso 3: Iniciar la quest "READ_THE_LETTER_DESC"
Cuando inicies la misión 6 (leer la carta), deberías ver:
```
[RoomExitBlocker:WoodsExitBlockerBottom] HandleQuestsChanged llamado
[RoomExitBlocker:WoodsExitBlockerRight] Modo SpecificQuestsStarted, verificando IDs: READ_THE_LETTER_DESC
[RoomExitBlocker:WoodsExitBlockerRight] Quest 'READ_THE_LETTER_DESC' estado: Active
[RoomExitBlocker:WoodsExitBlockerRight] Estado → DESBLOQUEADO | Collider.isTrigger=True
[RoomExitBlocker:WoodsExitBlockerLeft] Modo SpecificQuestsStarted, verificando IDs: READ_THE_LETTER_DESC
[RoomExitBlocker:WoodsExitBlockerLeft] Quest 'READ_THE_LETTER_DESC' estado: Active
[RoomExitBlocker:WoodsExitBlockerLeft] Estado → DESBLOQUEADO | Collider.isTrigger=True
[RoomExitBlocker:WoodsExitBlockerBottom] Modo SpecificQuestsStarted, verificando IDs: READ_THE_LETTER_DESC
[RoomExitBlocker:WoodsExitBlockerBottom] Quest 'READ_THE_LETTER_DESC' estado: Active
[RoomExitBlocker:WoodsExitBlockerBottom] Estado → DESBLOQUEADO | Collider.isTrigger=True
```

**Verifica que los 3 muestren "DESBLOQUEADO" y "Collider.isTrigger=True".**

### Paso 4: Verificar en la escena
Con los 3 GameObjects seleccionados en la jerarquía, verifica en el inspector que:
- **Box Collider → Is Trigger: ✓** (marcado para los 3)

---

## 🔧 DEBUGGING ADICIONAL

Si los problemas persisten:

### Opción 1: Verificar configuración de GameObjects
Asegúrate de que los 3 GameObjects:
- Están activos en la escena (✓ checkmark en el inspector)
- Tienen el componente `Box Collider` correctamente configurado
- Tienen la referencia a `Q_ELDRAN_MISSION0` en el campo `Required Quest Refs`

### Opción 2: Forzar actualización manual
Puedes crear un script temporal para forzar la actualización:

```csharp
// Script temporal para debug
void Update()
{
    if (Input.GetKeyDown(KeyCode.F9))
    {
        var blockers = FindObjectsOfType<RoomExitBlocker>();
        Debug.Log($"Encontrados {blockers.Length} RoomExitBlockers");
        foreach (var blocker in blockers)
        {
            blocker.ForceReevaluate();
        }
    }
}
```

Luego presiona **F9** en el juego para forzar la reevaluación de todos los bloqueadores.

### Opción 3: Verificar el QuestID
Confirma que el nombre del asset y el questId interno coinciden:
- **Asset**: `Q_ELDRAN_MISSION0.asset`
- **QuestID interno**: `READ_THE_LETTER_DESC`

El script usa el **questId interno**, no el nombre del archivo.

---

## 📋 RESUMEN

**Antes**: Cada GameObject manejaba su estado de forma independiente, causando inconsistencias.

**Ahora**: Cuando **cualquier** instancia detecta un cambio en las quests, **TODAS las instancias activas** se actualizan simultáneamente.

Esto garantiza que los 3 bloqueadores (Right, Left, Bottom) siempre estén sincronizados y se desbloqueen al mismo tiempo cuando se cumpla la condición de la quest.

---

## ✨ MEJORAS ADICIONALES IMPLEMENTADAS

1. **Logs más descriptivos** que incluyen el nombre del GameObject
2. **Reevaluación después de cargar escena** para asegurar estado correcto al recargar
3. **Método público `ForceReevaluate()`** para debug manual
4. **Protección contra múltiples mensajes** usando el flag `_isShowingMessage`
5. **Validación robusta del collider** con logs de error si falta

---

**Fecha**: 2026-02-08  
**Archivos modificados**: `Assets/Scripts/Interaction/RoomExitBlocker.cs`
