# Arquitectura del Sistema de Inputs

## 📋 Resumen

El sistema de inputs está **centralizado** para evitar código disperso y conflictos. Todo el control de UI vs Gameplay se gestiona desde **PlayerInputManager**.

## 🏗️ Componentes Principales

### 1. **PlayerInputManager** (CEREBRO CENTRAL)
**Ubicación**: `Assets/Scripts/Core/PlayerInputManager.cs`
**Responsabilidades**:
- Mantiene la instancia única de `PlayerControls`
- Gestiona el cambio entre modo **UI** y modo **Gameplay**
- Usa un contador de referencias para soportar contextos anidados
- Se registra en `ServiceLocator` para acceso global

**Métodos clave**:
```csharp
// Cambiar a modo UI (deshabilita inputs de gameplay)
PlayerInputManager.Instance.PushUIMode();

// Restaurar modo Gameplay
PlayerInputManager.Instance.PopUIMode();

// Forzar modo Gameplay (solo para casos excepcionales)
PlayerInputManager.Instance.ForceGameplayMode();

// Consultar modo actual
bool isInUI = PlayerInputManager.Instance.IsInUIMode;
```

### 2. **GamepadInputReader** (LECTOR DE EVENTOS)
**Ubicación**: `Assets/Scripts/Core/GamepadInputReader.cs`
**Responsabilidades**:
- Lee eventos de input del `PlayerControls`
- Expone eventos estáticos para que otros scripts se suscriban
- **NO gestiona el estado UI/Gameplay** (eso lo hace PlayerInputManager)

**Uso típico**:
```csharp
void OnEnable()
{
    GamepadInputReader.OnInput += HandleInput;
}

void OnDisable()
{
    GamepadInputReader.OnInput -= HandleInput;
}

void HandleInput(GamepadInputReader.InputEvent input)
{
    if (input.Type == GamepadInputReader.InputEventType.Submit)
    {
        // Hacer algo
    }
}
```

### 3. **MenuManager** (COORDINADOR DE MENÚS)
**Ubicación**: `Assets/Scripts/UI/MenuManager.cs`
**Responsabilidades**:
- Mantiene registro de qué menús están abiertos
- Evita abrir múltiples menús simultáneamente
- Los menús se registran/desregistran usando `MenuManager.TryOpen()` y `MenuManager.Close()`

## 🔄 Flujo de Apertura de Menú

```
1. Usuario presiona botón de inventario (input de Gameplay)
2. PlayerEquipmentMenuController recibe el evento
3. Verifica MenuManager.TryOpen(MenuKind.Equipment)
4. Si es permitido, llama PlayerInputManager.PushUIMode()
5. Ahora solo funcionan inputs de UI
6. Al cerrar el menú, llama PlayerInputManager.PopUIMode()
7. Se restauran los inputs de Gameplay
```

## ⚠️ Reglas Importantes

### ✅ **HACER**:
- Usar `PlayerInputManager.PushUIMode()` al abrir cualquier menú/diálogo
- Usar `PlayerInputManager.PopUIMode()` al cerrar
- Suscribirse a `GamepadInputReader.OnInput` para leer eventos
- Usar `MenuManager.TryOpen()` antes de abrir un menú

### ❌ **NO HACER**:
- NO llamar directamente a `_controls.UI.Enable()` o `_controls.GamePlay.Disable()`
- NO crear instancias de `PlayerControls` manualmente
- NO gestionar el estado UI/Gameplay en scripts individuales
- NO abrir menús sin verificar `MenuManager.TryOpen()`

## 📦 Componentes Anidados

El sistema soporta contextos anidados gracias al contador de referencias:

```csharp
// Abrir inventario
PlayerInputManager.Instance.PushUIMode(); // refCount = 1

// Dentro del inventario, abrir diálogo
PlayerInputManager.Instance.PushUIMode(); // refCount = 2

// Cerrar diálogo
PlayerInputManager.Instance.PopUIMode(); // refCount = 1 (aún en UI)

// Cerrar inventario
PlayerInputManager.Instance.PopUIMode(); // refCount = 0 (vuelve a Gameplay)
```

## 🔧 Scripts Actualizados

Los siguientes scripts han sido actualizados para usar el sistema centralizado:

- ✅ `DialogueManager.cs` - Usa PushUIMode/PopUIMode
- ⚠️ `PlayerEquipmentMenuController.cs` - **PENDIENTE DE ACTUALIZAR**
- ⚠️ `QuestMenuManager.cs` - **PENDIENTE DE ACTUALIZAR**
- ⚠️ `PauseMenuController.cs` - **PENDIENTE DE ACTUALIZAR**
- ⚠️ `CreatorGamepadController.cs` - **PENDIENTE DE ACTUALIZAR**

## 🎯 Próximos Pasos

1. Actualizar todos los menús para usar el sistema centralizado
2. Eliminar clases internas `InputScope` y `InputActionMapScope` redundantes
3. Simplificar la gestión de menús para que MenuManager sea más efectivo
4. Documentar mejor los casos de uso especiales

## 📝 Ejemplo Completo

```csharp
public class MyMenuController : MonoBehaviour
{
    void OpenMenu()
    {
        // 1. Verificar que se puede abrir
        if (!MenuManager.TryOpen(MenuKind.MyMenu))
        {
            Debug.Log("No se puede abrir el menú ahora");
            return;
        }

        // 2. Cambiar a modo UI
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PushUIMode();

        // 3. Mostrar UI del menú
        ShowMenuUI();
    }

    void CloseMenu()
    {
        // 1. Restaurar modo Gameplay
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PopUIMode();

        // 2. Desregistrar del MenuManager
        MenuManager.Close(MenuKind.MyMenu);

        // 3. Ocultar UI del menú
        HideMenuUI();
    }
}
```

---

**Última actualización**: 2025-01-16
**Autor**: Sistema centralizado de inputs

