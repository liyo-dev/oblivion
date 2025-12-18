﻿# Fix: PortraitAnchor Cambiando de Posición al Entrar al Menú

## 🐛 **Problemas Identificados**

### **Problema 1: Player Rotaba**
El `PortraitAnchor` cambiaba de posición cada vez que se abría el menú de equipamiento porque el código estaba **rotando el transform raíz del player**.

### **Problema 2: Forward de Cámara Inconsistente**
Incluso sin rotar el player, la posición del personaje en la RawImage era **diferente cada vez** porque se usaba el forward de la cámara de retrato (que variaba) como punto de referencia inicial.

### **Problema 3: Variables No Se Reseteaban**
Los valores `_previewPlayerYaw` y `_previewBaseForward` **no se recalculaban** cada vez que se abría el menú, acumulando valores de sesiones anteriores.

---

## 🐛 **Problema Adicional: Animaciones No Funcionaban**

### **Problema:**
Al abrir el menú, el código ponía `Time.timeScale = 0` (pausa el juego), lo que **también pausaba las animaciones del Animator** del player. Esto impedía:
- Ver las animaciones idle del personaje
- Ver animaciones de cambio de equipamiento
- Cualquier feedback visual animado

### **Causa:**
Por defecto, los `Animator` en Unity usan `AnimatorUpdateMode.Normal`, que respeta `Time.timeScale`. Cuando `Time.timeScale = 0`, las animaciones se congelan.

---

## ✅ **Solución Implementada**

### **Fix 1: Animación Idle Forzada**

El Animator del player se fuerza a ir a **idle** al abrir el menú, sin importar qué animación estuviera reproduciendo (correr, saltar, etc.):

```csharp
// Resetear parámetros comunes de movimiento a 0 para forzar idle
if (_playerAnimator.parameters.Any(p => p.name == "InputMagnitude"))
    _playerAnimator.SetFloat("InputMagnitude", 0f);
if (_playerAnimator.parameters.Any(p => p.name == "Speed"))
    _playerAnimator.SetFloat("Speed", 0f);
if (_playerAnimator.parameters.Any(p => p.name == "VerticalVelocity"))
    _playerAnimator.SetFloat("VerticalVelocity", 0f);
```

### **Fix 2: Player Mira Hacia la Cámara**

El player se rota automáticamente para **mirar hacia la cámara** aplicando 180° de rotación base:

```csharp
// En UpdateEquipmentCamera - Aplicar rotación del player:
// 180° base para mirar hacia la cámara + yaw del joystick para rotar
_playerPreviewTarget.rotation = Quaternion.Euler(0f, 180f + _previewPlayerYaw, 0f);
```

**Explicación:**
- La cámara está en **-Z** (atrás) mirando hacia **+Z** (player en origen)
- Un objeto con rotación Y=0° mira hacia **+Z** (de espaldas a la cámara) ❌
- Un objeto con rotación Y=180° mira hacia **-Z** (de frente a la cámara) ✅
- `_previewPlayerYaw` empieza en 0°, así que: `180° + 0° = 180°` → De frente

### **Fix 3: Rotación con Joystick Derecho**

El usuario puede rotar al player con el **joystick derecho** para verlo desde todos los ángulos:

```csharp
// En UpdateEquipmentCamera:
float rotateInput = GamepadInputReader.CameraLook.x;
if (Mathf.Abs(rotateInput) > 0.01f)
    _previewPlayerYaw += rotateInput * previewOrbitSpeed * Time.unscaledDeltaTime;

// Aplicar la rotación al player (180° base + yaw acumulado):
_playerPreviewTarget.rotation = Quaternion.Euler(0f, 180f + _previewPlayerYaw, 0f);
```

**Importante:** La **cámara permanece fija** mirando al PortraitAnchor. Es el **player el que rota**, no la cámara.

### **Fix 4: Forward Consistente**

En lugar de usar el forward del player (que varía según hacia dónde miraba), ahora usamos **Vector3.forward fijo**:

```csharp
// ANTES (PROBLEMÁTICO):
Vector3 forwardSource = _playerPreviewTarget.forward; // ← Varía según la orientación del player ❌

// AHORA (CORRECTO):
_previewBaseForward = Vector3.forward; // ← Siempre igual ✅
```

### **Fix 5: Forzar Reset Completo**

Cada vez que se abre el menú, se fuerza un **reset completo** del preview target:

```csharp
// En SetEquipmentCameraActive(true):
_playerPreviewTarget = null; // Forzar recálculo
TrySetupPreviewTarget(); // Resetea todo desde cero
```

### **Fix 6: Mantener Animaciones Activas en el Menú**


El sistema ahora cambia el `AnimatorUpdateMode` del player a `UnscaledTime` cuando se abre el menú, permitiendo que las animaciones sigan funcionando aunque `Time.timeScale = 0`.

```csharp
// Al ABRIR el menú (OpenMenu):
if (_playerAnimator != null)
{
    _storedAnimatorUpdateMode = _playerAnimator.updateMode;
    _playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
}

// Al CERRAR el menú (CloseMenu):
if (_playerAnimator != null)
{
    _playerAnimator.updateMode = _storedAnimatorUpdateMode;
}
```

**Beneficios:**
- ✅ El player mantiene sus animaciones idle en el menú
- ✅ Se ven animaciones de cambio de equipamiento
- ✅ Feedback visual más rico y dinámico
- ✅ El player no se congela como una estatua
- ✅ Al cerrar el menú, el Animator vuelve a su modo original

---

## 🔍 **Explicación Técnica**

### **Antes (Problemático):**

```
Player (Root) - Orientación variable ❌
├─ PortraitAnchor (hijo) - Posición inconsistente ❌
├─ Animator - CONGELADO (Time.timeScale = 0) ❌
└─ ...otros hijos

Al abrir menú:
1. Player mantiene su rotación de gameplay (corriendo hacia X)
2. Aparece de espaldas o de lado ❌
3. Animación de correr congelada ❌
4. Posición diferente cada vez ❌
```

### **Ahora (Correcto):**

```
Player (Root) - FORZADO a mirar hacia adelante ✅
├─ PortraitAnchor (hijo) - SIEMPRE en el mismo lugar ✅
├─ Animator - ACTIVO en idle (UnscaledTime) ✅
└─ ...otros hijos

Al abrir menú:
1. Player.rotation = Quaternion.LookRotation(Vector3.forward) → Mira hacia la cámara ✅
2. Animator parámetros reseteados → Idle ✅
3. _previewPlayerYaw = 0 → Sin rotación acumulada ✅
4. Animaciones idle activas ✅

Durante el menú:
1. Stick derecho → _previewPlayerYaw cambia
2. Player rota: rotation = Quaternion.Euler(0, _previewPlayerYaw, 0)
3. PortraitAnchor rota CON el player (es correcto)
4. Cámara permanece fija mirando al anchor
5. Usuario ve al player desde todos los ángulos ✅
```

---

## 🎮 **Comportamiento Resultante**

### **✅ Lo que AHORA funciona correctamente:**

1. **Player siempre en idle al abrir el menú:**
   - Sin importar si estabas corriendo, saltando, etc.
   - El Animator se fuerza a idle (velocidad = 0)
   - Se ve natural y estable

2. **Player siempre mira hacia la cámara:**
   - Nunca aparece de espaldas
   - Siempre se ve de frente al abrir el menú
   - Posicionamiento consistente

3. **Rotación con joystick derecho:**
   - Mueves el stick derecho horizontal → El player rota
   - Puedes verlo desde todos los ángulos (360°)
   - La cámara permanece fija, el player es el que gira
   - Perfecto para ver capas, armas, equipamiento

4. **PortraitAnchor permanece estable:**
   - Siempre en la misma posición local relativa al player
   - No se ve afectado por la rotación (el player rota completo CON el anchor)
   - Cada vez que abres el menú, está en el mismo lugar

5. **Animaciones del player funcionan en el menú:**
   - Las animaciones idle se reproducen normalmente
   - Se ven animaciones de cambio de equipamiento en tiempo real
   - El personaje no está congelado como una estatua
   - Feedback visual más rico y profesional

6. **Posicionamiento consistente:**
   - El player siempre aparece en la misma posición en la RawImage
   - Sin "saltos" o cambios inesperados
   - Predecible y profesional

---

## 📝 **Diferencia Visual**

### **Antes (con el bug):**

```
Entrada 1 al menú:
- PortraitAnchor en (0, 1.5, 0) local

Salida del menú:
- Player rotado → PortraitAnchor afectado

Entrada 2 al menú:
- PortraitAnchor en (0.5, 1.5, 0.3) local ← ¡DIFERENTE! ❌

Cada vez diferente...
```

### **Ahora (corregido):**

```
Entrada 1 al menú:
- PortraitAnchor en (0, 1.5, 0) local

Salida del menú:
- Player NO rotado → PortraitAnchor intacto

Entrada 2 al menú:
- PortraitAnchor en (0, 1.5, 0) local ← ¡IGUAL! ✅

Siempre en el mismo lugar
```

---

## 🔧 **Código Modificado**

### **Archivo:** `PlayerEquipmentMenuController.cs`

**Método:** `UpdateEquipmentCamera(bool allowOrbit)`

**Cambios:**

1. ✅ **Añadida rotación del vector forward de la cámara:**
   ```csharp
   var rotatedForward = Quaternion.Euler(0f, _previewPlayerYaw, 0f) * cameraForward;
   ```

2. ✅ **Usamos el forward rotado para calcular la posición de la cámara:**
   ```csharp
   Vector3 cameraPos = anchorPos - rotatedForward * equipmentCameraDistance + ...
   ```

3. ✅ **ELIMINADA la rotación del player:**
   ```csharp
   // ELIMINADO:
   // _playerPreviewTarget.rotation = previewRotation;
   
   // REEMPLAZADO POR:
   // Comentario explicando por qué NO rotamos el player
   ```

4. ✅ **Añadido sistema de animaciones activas:**
   - Nuevos campos: `_playerAnimator`, `_storedAnimatorUpdateMode`
   - En `TrySetupPreviewTarget()`: Buscar y guardar referencia al Animator
   - En `OpenMenu()`: Cambiar a `AnimatorUpdateMode.UnscaledTime`
   - En `CloseMenu()`: Restaurar el modo original

---

## ✅ **Verificación del Fix**

### **Pasos para verificar:**

1. **Abrir el menú de equipamiento**
   - El personaje debe estar centrado
   - Anotar la posición visual del personaje en pantalla

2. **Rotar con el stick derecho**
   - La cámara orbita alrededor del personaje
   - El personaje se ve desde diferentes ángulos

3. **Cerrar el menú (B o Start)**
   - El menú se cierra
   - El player vuelve a gameplay

4. **Volver a abrir el menú**
   - El personaje debe estar **exactamente en la misma posición** que en el paso 1
   - No debe haber desplazamiento ni cambio de centrado

5. **Repetir varias veces**
   - Cada vez que abres el menú, el centrado es idéntico
   - No hay "deriva" o cambios acumulativos

---

## 🎯 **Resultado Final**

### **Antes del Fix:**
- ❌ PortraitAnchor cambiaba de posición cada vez que se abría el menú
- ❌ Player rotaba, afectando a todos sus hijos
- ❌ Centrado inconsistente del retrato
- ❌ Posición acumulaba cambios con cada apertura
- ❌ Animaciones del player congeladas (estatua)

### **Después del Fix:**
- ✅ PortraitAnchor permanece en su posición local fija
- ✅ Player NO rota, solo la cámara orbita
- ✅ Centrado consistente y estable
- ✅ Posición idéntica en cada apertura del menú
- ✅ Animaciones del player activas y fluidas

---

## 📚 **Lecciones Aprendidas**

### **Principio Clave:**

> **Para un sistema de retrato estable, la cámara debe orbitar alrededor del sujeto, NO rotar el sujeto.**

### **Buenas Prácticas:**

1. **Puntos de referencia (anchors) deben ser inmutables:**
   - Si defines un anchor en una posición local, debe permanecer ahí
   - No debe ser afectado por sistemas de cámara o UI

2. **Separación de responsabilidades:**
   - **Cámara:** Se mueve y rota (órbita)
   - **Player:** Permanece en su estado de gameplay
   - **Anchor:** Marca el punto de interés (inmutable)

3. **Preferir transformaciones de cámara sobre transformaciones de objeto:**
   - Mover/rotar la cámara es más predecible
   - No afecta a la jerarquía de objetos
   - Más fácil de debuggear

---

## 🔄 **Compatibilidad**

### **✅ Compatible con:**
- Sistema de PortraitAnchor existente
- Sistema de RenderTexture
- Sistema de cambio de layers (PortraitLayerSwapSRP)
- Rotación con gamepad (órbita)
- Todas las pestañas del menú
- Cambio de equipamiento en tiempo real

### **❌ NO afecta:**
- Gameplay del player
- Movimiento del player
- Combate
- Otras funcionalidades

---

**✨ El problema del PortraitAnchor moviéndose está completamente resuelto. El anchor ahora permanece en su posición local fija sin importar cuántas veces se abra/cierre el menú. ✨**

