# ⚙️ Configuración de Input para Gamepad - Camera Targeting

## 🎮 Problema Común

El D-Pad de los mandos/gamepads NO siempre se detecta automáticamente como un eje en Unity. Necesitas configurarlo correctamente en el Input Manager.

## ✅ Solución: Configurar Input Manager

### Paso 1: Abrir Input Manager

```
Edit → Project Settings → Input Manager
```

### Paso 2: Verificar/Crear Eje "Horizontal"

Busca el eje **"Horizontal"** y verifica que tenga estas configuraciones:

#### Para D-Pad (Recomendado)
```
Name: Horizontal
Negative Button: left (o "dpad left")
Positive Button: right (o "dpad right")
Alt Negative Button: a (para teclado, opcional)
Alt Positive Button: d (para teclado, opcional)
Gravity: 3
Dead: 0.001
Sensitivity: 3
Snap: true
Type: Key or Mouse Button
```

#### Alternativa: Usar Joystick Axes

Si lo anterior no funciona, crea una nueva entrada:

```
Name: Horizontal
Type: Joystick Axis
Axis: 6th axis (Joysticks)  ← Este suele ser el D-Pad horizontal
Sensitivity: 1
Dead: 0.19
```

### Paso 3: Verificar en el Inspector

1. Ir a `CombatCameraTargeting` component
2. Verificar que `D Pad Horizontal Axis` = **"Horizontal"**
3. Si usas otro nombre, cambiarlo aquí

## 🔍 Testing del Input

### Script de Debug (Opcional)

Crea este script temporal para verificar que el D-Pad funciona:

```csharp
using UnityEngine;

public class GamepadDebug : MonoBehaviour
{
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            Debug.Log($"D-Pad Horizontal: {horizontal}");
        }
        
        // Probar todos los botones
        for (int i = 0; i < 20; i++)
        {
            if (Input.GetKeyDown((KeyCode)System.Enum.Parse(typeof(KeyCode), "JoystickButton" + i)))
            {
                Debug.Log($"Botón presionado: JoystickButton{i}");
            }
        }
    }
}
```

Agrégalo a cualquier GameObject y presiona botones en el mando para ver qué se detecta.

## 🎯 Mapeo de Botones (Xbox Controller)

| Botón Físico | KeyCode Unity | Número |
|--------------|---------------|--------|
| A (Verde) | JoystickButton0 | 0 |
| B (Rojo) | JoystickButton1 | 1 |
| X (Azul) | JoystickButton2 | 2 |
| Y (Amarillo) | JoystickButton3 | 3 |
| LB | JoystickButton4 | 4 |
| RB | JoystickButton5 | 5 |
| Back | JoystickButton6 | 6 |
| Start | JoystickButton7 | 7 |
| L-Stick Click | JoystickButton8 | 8 |
| R-Stick Click | JoystickButton9 | 9 |

### D-Pad (Depende del driver)
- **Opción 1**: Detectado como eje "6th axis" y "7th axis"
- **Opción 2**: Detectado como botones individuales ("dpad left", "dpad right", etc.)

## 🔧 Configuración Alternativa

Si el D-Pad NO funciona con el eje "Horizontal", puedes modificar `CombatCameraTargeting` para usar botones directos:

```csharp
// En lugar de Input.GetAxis("Horizontal")
bool dPadRight = Input.GetKeyDown(KeyCode.JoystickButton10); // D-Pad Right
bool dPadLeft = Input.GetKeyDown(KeyCode.JoystickButton11);  // D-Pad Left

if (dPadRight)
{
    SwitchToNextTarget();
}
else if (dPadLeft)
{
    SwitchToPreviousTarget();
}
```

> **Nota**: Los números de botón del D-Pad varían según el mando. Usa el script de debug para encontrar los correctos.

## 🎮 Configuración por Tipo de Mando

### Xbox 360/One Controller (Windows)
```
D-Pad Horizontal: 6th axis (Joysticks)
D-Pad Vertical: 7th axis (Joysticks)
Botón B: JoystickButton1
```

### PlayStation DualShock 4 (Steam Input)
```
D-Pad: Detectado como botones
D-Pad Left: JoystickButton7
D-Pad Right: JoystickButton5
D-Pad Up: JoystickButton4
D-Pad Down: JoystickButton6
Botón Circle: JoystickButton2
```

### Nintendo Switch Pro Controller
```
D-Pad: Generalmente detectado como eje
D-Pad Horizontal: 6th axis (Joysticks)
Botón B: JoystickButton0 (¡cuidado! el mapeo es diferente)
```

## ✅ Verificación Final

### Checklist

- [ ] Input Manager tiene el eje "Horizontal" configurado
- [ ] El eje responde al D-Pad del mando
- [ ] El script de debug muestra valores cuando mueves el D-Pad
- [ ] `CombatCameraTargeting` tiene el eje correcto en Inspector
- [ ] El botón B (u otro) está mapeado correctamente para cancelar lock

### Testing en Juego

1. Iniciar combate con múltiples enemigos
2. Presionar **D-Pad Right** → Debe cambiar al siguiente enemigo
3. Presionar **D-Pad Left** → Debe cambiar al enemigo anterior
4. Presionar **Botón B** → Debe cancelar el lock
5. Ver logs en consola (activar `Show Debug Logs` en Inspector)

## 🆘 Troubleshooting

### El D-Pad no responde
1. ✅ Verificar que el mando está conectado
2. ✅ Usar el script de debug para ver qué se detecta
3. ✅ Probar con "6th axis" en lugar de "Horizontal"
4. ✅ Verificar drivers del mando (actualizar si es necesario)

### Cambia de target continuamente
- El "Dead Zone" está muy bajo
- Aumentar `Dead` a 0.3 o más en Input Manager
- O aumentar el threshold en el código (actualmente 0.5f)

### Botón B no cancela el lock
- Usar el script de debug para encontrar el botón correcto
- Cambiar `cancelLockButton` en Inspector
- Probar con JoystickButton0, 1, 2, etc.

## 📝 Recomendaciones

### Usar New Input System (Opcional)

Si quieres un sistema más robusto, considera migrar a **Unity's New Input System**:

1. Mejor detección de mandos
2. Mapeo automático de D-Pad
3. Soporte multi-plataforma más consistente
4. Remapping en runtime

Sin embargo, el sistema actual con `Input.GetAxis()` funciona perfectamente si está bien configurado.

---

## 🎉 ¡Listo!

Ahora tu sistema de camera targeting funciona perfectamente con **gamepad/mando** usando:
- **D-Pad Right/Left** para cambiar de enemigo
- **Botón B** para cancelar lock
- **Detección automática** al entrar/salir de combate
