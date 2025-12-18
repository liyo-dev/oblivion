﻿# Implementación del Sistema de Preview del Player en Menú de Equipamiento

## 📋 Resumen de Cambios

Se ha implementado un sistema completo para mostrar el **player REAL** en tiempo real dentro del menú de equipamiento, visible en **TODAS las pestañas** (Inventario, Hechizos y Equipamiento), con capacidad de rotación solo en la pestaña de Equipamiento.

**✨ NUEVO:** El sistema ahora es **100% automático**. NO hay campo serializado para la cámara. La búsqueda se hace automáticamente usando el ServiceLocator.

---

## ✅ Cambios Implementados

### 1. **Nueva Clase: PortraitLayerSwapSRP.cs**

Ubicación: `Assets/Scripts/UI/PortraitLayerSwapSRP.cs`

**Función:** Gestiona el cambio temporal de layers del player para que la cámara de retrato SOLO renderice al player y nunca el mundo.

**Características:**
- Compatible con URP (Universal Render Pipeline)
- Usa eventos `RenderPipelineManager.beginCameraRendering` / `endCameraRendering`
- Cambia recursivamente las layers del player y sus hijos antes de renderizar
- Restaura automáticamente las layers originales después del render
- Usa la layer "UI_Portrait" (debe crearse en el proyecto)

---

### 2. **Modificaciones en PlayerEquipmentMenuController.cs**

#### a) **Campo Privado (NO serializado)**
```csharp
// Referencia a la cámara de retrato, encontrada automáticamente en el player
Camera _equipmentPreviewCamera;
```
**IMPORTANTE:** NO hay `[SerializeField]`. La cámara se busca automáticamente porque el player se instancia en otra escena.

#### b) **Método: FindPortraitCameraInPlayer()**
Busca automáticamente la cámara de retrato dentro del player usando el `PlayerService`:
1. Primero busca por **tag "PortraitCamera"** (recomendado)
2. Si no lo encuentra, busca por **nombre que contenga "Portrait"**
3. Muestra advertencias claras si no encuentra ninguna

#### c) **SetEquipmentCameraActive() - 100% AUTOMÁTICO**
- Busca automáticamente la cámara si `_equipmentPreviewCamera == null`
- No necesita asignación manual en el Inspector
- Logs informativos para debugging

#### d) **TrySetupPreviewTarget() - SIN Camera.main**
- **ELIMINADO:** `Camera.main` ya no se usa
- Solo usa `_equipmentPreviewCamera` o el forward del player
- Más limpio y predecible

#### e) **Otros cambios (sin modificaciones)**
- `OpenMenu()`: Activa cámara siempre al abrir
- `ShowTab()`: Mantiene cámara activa en todas las pestañas
- `LateUpdate()`: Órbita solo en pestaña de Equipamiento
- `UpdateEquipmentCamera(bool allowOrbit)`: Rotación condicional

---

## 🛠️ Configuración en Unity (Pasos Obligatorios)

### **Paso 1: Crear la Layer "UI_Portrait"**

1. Ve a **Edit > Project Settings > Tags and Layers**
2. En la sección **Layers**, busca un slot vacío (ej: User Layer 6)
3. Nómbrala exactamente: `UI_Portrait`

### **Paso 2: Crear el Tag "PortraitCamera"**

1. Ve a **Edit > Project Settings > Tags and Layers**
2. En la sección **Tags**, haz clic en el botón **+**
3. Añade un nuevo tag llamado exactamente: `PortraitCamera`

### **Paso 3: Configurar la Cámara de Preview (DENTRO DEL PLAYER)**

1. Navega al **prefab del Player** en el Project
2. Localiza la cámara que quieres usar para el retrato (tienes 2, elige la correcta)
3. Selecciona esa cámara
4. En el **Inspector**, arriba del todo, en el dropdown **Tag**, selecciona: `PortraitCamera`
5. En el componente **Camera**:
   - **Culling Mask**: Desmarcar todo excepto `UI_Portrait`
   - **Render Type**: Dejar como está
   - Asegúrate de que tenga un **Render Texture** asignado

### **Paso 4: Añadir el Componente PortraitLayerSwapSRP**

1. Selecciona el GameObject que tiene `PlayerEquipmentMenuController` (normalmente en la escena Start o MainMenu)
2. **Add Component** > busca `PortraitLayerSwapSRP`
3. Arrastrarlo al campo `Portrait Layer Swap` del `PlayerEquipmentMenuController`

### **Paso 5: ¡LISTO! No hay más configuración**

**NO** hay campo para asignar la cámara manualmente. El sistema la encuentra automáticamente cuando:
- Abres el menú de equipamiento por primera vez
- El player ya está instanciado en la escena

---

## 🎯 Búsqueda Automática de Cámara (100% Automático)

### **¿Cómo funciona?**

El sistema busca automáticamente la cámara de retrato cuando:
1. Se intenta activar la cámara (`SetEquipmentCameraActive(true)`)
2. `_equipmentPreviewCamera` es `null`
3. Busca en el player usando `PlayerService`

### **Orden de búsqueda:**

1. **Por Tag** (recomendado): Busca cámaras con tag `"PortraitCamera"` dentro del player
2. **Por Nombre**: Busca cámaras cuyo nombre contenga `"Portrait"` (case-insensitive)
3. **Advertencia**: Si no encuentra ninguna, muestra lista de cámaras disponibles

### **Logs en Consola:**

✅ **Éxito:**
```
[PlayerEquipmentMenuController] Cámara de retrato encontrada por tag: PortraitCamera
```

⚠️ **Advertencia (no encontrada):**
```
[PlayerEquipmentMenuController] Se encontraron 2 cámara(s) en el player, pero ninguna tiene tag 'PortraitCamera' o nombre 'Portrait'. Cámaras disponibles: MainCamera, UICamera
```

---

## 🎮 Comportamiento Esperado

### ✅ **Inventario y Hechizos (Tabs 0 y 1)**
- El player se ve en el RenderTexture
- El player está **quieto** (no rota)
- Solo se ve el player, **no el mundo**
- El equipamiento se actualiza en tiempo real

### ✅ **Equipamiento (Tab 2)**
- El player se ve en el RenderTexture
- El player **puede rotarse** con el stick derecho/CameraLook input
- Solo se ve el player, **no el mundo**
- El equipamiento se actualiza en tiempo real

### ✅ **Al Cerrar el Menú**
- Las layers del player se restauran completamente
- La rotación del player vuelve a su estado original
- El mundo vuelve a ser visible normalmente

---

## 🔍 Verificación y Troubleshooting

### **Problema: "No se encontró la cámara de retrato en el player"**

**Solución:**
1. Verifica que la cámara existe dentro del player (no como hermano)
2. Asegúrate de haberle puesto el tag `PortraitCamera`
3. O nómbrala con "Portrait" en el nombre (ej: "PortraitCamera", "Portrait Cam")
4. Comprueba que el player se está instanciando correctamente en la escena

### **Problema: El mundo se ve en el RenderTexture**

**Solución:**
1. Verifica que la cámara tenga Culling Mask = `UI_Portrait` únicamente
2. Verifica que la layer `UI_Portrait` exista en el proyecto
3. Verifica que `portraitLayerSwap` esté asignado en el Inspector
4. Comprueba la consola por errores de `[PortraitLayerSwapSRP]`

### **Problema: El player no se ve**

**Solución:**
1. Verifica que la cámara esté activa cuando el menú está abierto
2. Verifica que el RenderTexture esté asignado correctamente
3. Comprueba que el player existe en la escena
4. Verifica que la cámara tenga el tag `PortraitCamera` correcto

### **Problema: "Se encontraron 2 cámara(s)... Cámaras disponibles: X, Y"**

**Solución:**
Tienes dos cámaras en el player. El log te muestra cuáles son. Ponle el tag `PortraitCamera` a la correcta.

### **Problema: El player rota en todas las pestañas**

**Solución:**
- Esto NO debería pasar. El código protege `_previewPlayerYaw` con `if (allowOrbit)`
- Verifica que no hayas modificado `LateUpdate()` o `UpdateEquipmentCamera()`

---

## 📝 Notas Técnicas

### **¿Por qué NO hay SerializeField?**

**Razón:** El player es un prefab que se instancia en una escena diferente de donde está el `PlayerEquipmentMenuController`. Unity no puede serializar referencias entre escenas diferentes, por lo que cualquier asignación manual se perdería.

**Solución:** Búsqueda automática usando `PlayerService` y `ServiceLocator`, que mantienen referencias en runtime sin importar la escena.

### **¿Por qué NO usar Camera.main?**

**Razón:** `Camera.main` es la cámara principal del mundo (la que ve el jugador jugando). Usarla para calcular el `forwardSource` del preview causaría que la orientación del player en el menú dependiera de dónde esté mirando la cámara del mundo, lo cual es incorrecto.

**Solución:** Usar solo la cámara de retrato (`_equipmentPreviewCamera`) o el forward del player.

### **Arquitectura del Sistema:**

```
PlayerService (ServiceLocator)
    └─ Player GameObject (instanciado en escena)
        ├─ MainCamera (tag: MainCamera)
        ├─ PortraitCamera (tag: PortraitCamera) ← Esta se busca
        └─ [otros componentes]

PlayerEquipmentMenuController (en otra escena)
    ├─ Busca automáticamente usando PlayerService.TryGetPlayer()
    ├─ Itera GetComponentsInChildren<Camera>()
    └─ Filtra por tag "PortraitCamera" o nombre "Portrait"
```

### **Rendimiento:**

- **Búsqueda**: Solo se ejecuta una vez (cuando `_equipmentPreviewCamera == null`)
- **Impacto**: O(n) donde n = número de cámaras en el player (~2-5 típicamente)
- **Cacheo**: Una vez encontrada, se reutiliza durante toda la sesión

---

## ✅ Checklist Final

- [ ] Layer `UI_Portrait` creada en Project Settings
- [ ] Tag `PortraitCamera` creado en Project Settings
- [ ] Prefab del Player: cámara con tag `PortraitCamera` asignado
- [ ] Prefab del Player: cámara con Culling Mask = `UI_Portrait` únicamente
- [ ] Prefab del Player: cámara con RenderTexture asignado
- [ ] Componente `PortraitLayerSwapSRP` añadido en PlayerEquipmentMenuController
- [ ] Campo `portraitLayerSwap` asignado en `PlayerEquipmentMenuController`
- [ ] **NO** intentar asignar la cámara manualmente (no hay campo para ello)
- [ ] Prueba: Abrir menú → Ver log "Cámara de retrato encontrada por tag"
- [ ] Prueba: Abrir menú → Player visible, mundo invisible
- [ ] Prueba: Tab Inventario → Player quieto
- [ ] Prueba: Tab Equipamiento → Player rota con stick
- [ ] Prueba: Cerrar menú → Layers restauradas, mundo visible
- [ ] Prueba: Cambiar equipamiento → Se ve en tiempo real

---

## 🚀 Ventajas del Sistema Mejorado

### **Antes:**
- ❌ Tenías que arrastrar la cámara manualmente en el Inspector → IMPOSIBLE (player en otra escena)
- ❌ Usaba `Camera.main` como fallback → Orientación incorrecta del player

### **Ahora:**
- ✅ Solo necesitas poner el tag `PortraitCamera` una vez en el prefab del player
- ✅ Funciona automáticamente en todas las escenas
- ✅ Logs claros si algo falla
- ✅ No usa `Camera.main`, solo la cámara de retrato o el forward del player
- ✅ Arquitectura correcta para prefabs instanciados dinámicamente

---

## 📞 Soporte

Si encuentras algún problema:

1. **Revisa la consola** por logs de `[PlayerEquipmentMenuController]` y `[PortraitLayerSwapSRP]`
2. **Verifica el tag**: Debe ser exactamente `PortraitCamera` (case-sensitive)
3. **Comprueba la jerarquía**: La cámara debe estar dentro del prefab del player
4. **Verifica instanciación**: El player debe estar instanciado en la escena cuando abres el menú

---

**✨ Implementación completada con búsqueda 100% automática (sin SerializeField) ✨**

## ✅ Cambios Implementados

### 1. **Nueva Clase: PortraitLayerSwapSRP.cs**

Ubicación: `Assets/Scripts/UI/PortraitLayerSwapSRP.cs`

**Función:** Gestiona el cambio temporal de layers del player para que la cámara de retrato SOLO renderice al player y nunca el mundo.

**Características:**
- Compatible con URP (Universal Render Pipeline)
- Usa eventos `RenderPipelineManager.beginCameraRendering` / `endCameraRendering`
- Cambia recursivamente las layers del player y sus hijos antes de renderizar
- Restaura automáticamente las layers originales después del render
- Usa la layer "UI_Portrait" (debe crearse en el proyecto)

---

### 2. **Modificaciones en PlayerEquipmentMenuController.cs**

#### a) **Nuevo Campo Serializado**
```csharp
[SerializeField, Tooltip("Componente que gestiona el cambio temporal de layers para aislar al player del mundo")]
private PortraitLayerSwapSRP portraitLayerSwap;
```

#### b) **Nuevo Método: FindPortraitCameraInPlayer()**
Busca automáticamente la cámara de retrato dentro del player usando el `PlayerService`:
1. Primero busca por **tag "PortraitCamera"** (recomendado)
2. Si no lo encuentra, busca por **nombre que contenga "Portrait"**
3. Muestra advertencias claras si no encuentra ninguna

#### c) **SetEquipmentCameraActive() - MEJORADO**
- Ahora busca automáticamente la cámara si no está asignada manualmente
- Logs informativos para debugging

#### d) **Otros cambios (sin modificaciones)**
- `OpenMenu()`: Activa cámara siempre al abrir
- `ShowTab()`: Mantiene cámara activa en todas las pestañas
- `LateUpdate()`: Órbita solo en pestaña de Equipamiento
- `UpdateEquipmentCamera(bool allowOrbit)`: Rotación condicional

---

## 🛠️ Configuración en Unity (Pasos Obligatorios)

### **Paso 1: Crear la Layer "UI_Portrait"**

1. Ve a **Edit > Project Settings > Tags and Layers**
2. En la sección **Layers**, busca un slot vacío (ej: User Layer 6)
3. Nómbrala exactamente: `UI_Portrait`

### **Paso 2: Crear el Tag "PortraitCamera"**

1. Ve a **Edit > Project Settings > Tags and Layers**
2. En la sección **Tags**, haz clic en el botón **+**
3. Añade un nuevo tag llamado exactamente: `PortraitCamera`

### **Paso 3: Configurar la Cámara de Preview (DENTRO DEL PLAYER)**

1. Navega al **prefab/GameObject del Player** en la Hierarchy
2. Localiza la cámara que quieres usar para el retrato (hay 2, elige la correcta)
3. Selecciona esa cámara
4. En el **Inspector**, arriba del todo, en el dropdown **Tag**, selecciona: `PortraitCamera`
5. En el componente **Camera**:
   - **Culling Mask**: Desmarcar todo excepto `UI_Portrait`
   - **Render Type**: Dejar como está
   - Asegúrate de que tenga un **Render Texture** asignado

### **Paso 4: Añadir el Componente PortraitLayerSwapSRP**

#### Opción A: En el mismo GameObject que PlayerEquipmentMenuController (recomendado)
1. Selecciona el GameObject que tiene `PlayerEquipmentMenuController`
2. **Add Component** > busca `PortraitLayerSwapSRP`

#### Opción B: En el GameObject de la cámara (alternativa)
1. Selecciona el GameObject de la cámara de retrato dentro del player
2. **Add Component** > busca `PortraitLayerSwapSRP`

### **Paso 5: Configurar Referencias en PlayerEquipmentMenuController**

1. Selecciona el GameObject con `PlayerEquipmentMenuController`
2. En el Inspector, busca la sección **Cámara de equipamiento**
3. **Campo `Equipment Preview Camera`**: Puedes dejarlo **vacío** (se busca automáticamente) o asignarlo manualmente
4. **Arrastra** el componente `PortraitLayerSwapSRP` al campo `Portrait Layer Swap`

---

## 🎯 Búsqueda Automática de Cámara

### **¿Cómo funciona?**

El sistema ahora busca automáticamente la cámara de retrato cuando:
- El campo `equipmentPreviewCamera` está vacío en el Inspector
- Se intenta abrir el menú por primera vez

### **Orden de búsqueda:**

1. **Por Tag** (recomendado): Busca cámaras con tag `"PortraitCamera"` dentro del player
2. **Por Nombre**: Busca cámaras cuyo nombre contenga `"Portrait"` (case-insensitive)
3. **Advertencia**: Si no encuentra ninguna, muestra lista de cámaras disponibles

### **Logs en Consola:**

✅ **Éxito:**
```
[PlayerEquipmentMenuController] Cámara de retrato encontrada por tag: PortraitCamera
```

⚠️ **Advertencia (no encontrada):**
```
[PlayerEquipmentMenuController] Se encontraron 2 cámara(s) en el player, pero ninguna tiene tag 'PortraitCamera' o nombre 'Portrait'. Cámaras disponibles: MainCamera, UICamera
```

---

## 🎮 Comportamiento Esperado

### ✅ **Inventario y Hechizos (Tabs 0 y 1)**
- El player se ve en el RenderTexture
- El player está **quieto** (no rota)
- Solo se ve el player, **no el mundo**
- El equipamiento se actualiza en tiempo real

### ✅ **Equipamiento (Tab 2)**
- El player se ve en el RenderTexture
- El player **puede rotarse** con el stick derecho/CameraLook input
- Solo se ve el player, **no el mundo**
- El equipamiento se actualiza en tiempo real

### ✅ **Al Cerrar el Menú**
- Las layers del player se restauran completamente
- La rotación del player vuelve a su estado original
- El mundo vuelve a ser visible normalmente

---

## 🔍 Verificación y Troubleshooting

### **Problema: "No se encontró la cámara de retrato en el player"**

**Solución:**
1. Verifica que la cámara existe dentro del player (no como hermano)
2. Asegúrate de haberle puesto el tag `PortraitCamera`
3. O nómbrala con "Portrait" en el nombre (ej: "PortraitCamera", "Portrait Cam")
4. Si sigues teniendo problemas, asigna la cámara manualmente en el Inspector

### **Problema: El mundo se ve en el RenderTexture**

**Solución:**
1. Verifica que la cámara tenga Culling Mask = `UI_Portrait` únicamente
2. Verifica que la layer `UI_Portrait` exista en el proyecto
3. Verifica que `portraitLayerSwap` esté asignado en el Inspector
4. Comprueba la consola por errores de `[PortraitLayerSwapSRP]`

### **Problema: El player no se ve**

**Solución:**
1. Verifica que la cámara esté activa cuando el menú está abierto
2. Verifica que el RenderTexture esté asignado correctamente
3. Comprueba que el player existe en la escena
4. Verifica que la cámara tenga el tag `PortraitCamera` correcto

### **Problema: "Se encontraron 2 cámara(s)... Cámaras disponibles: X, Y"**

**Solución:**
Tienes dos cámaras en el player. El log te muestra cuáles son. Ponle el tag `PortraitCamera` a la correcta.

### **Problema: El player rota en todas las pestañas**

**Solución:**
- Esto NO debería pasar. El código protege `_previewPlayerYaw` con `if (allowOrbit)`
- Verifica que no hayas modificado `LateUpdate()` o `UpdateEquipmentCamera()`

---

## 📝 Notas Técnicas

### **¿Por qué usar Tag en lugar de asignación manual?**

1. ✅ **Flexibilidad**: Si el player cambia de prefab, no necesitas reasignar
2. ✅ **Claridad**: El tag identifica explícitamente la cámara de retrato
3. ✅ **Automático**: Funciona desde cualquier escena sin configuración extra
4. ✅ **Fallback**: Si prefieres, puedes asignar manualmente en el Inspector

### **Arquitectura del Sistema:**

```
PlayerService (ServiceLocator)
    └─ Player GameObject
        ├─ MainCamera (tag: MainCamera)
        ├─ PortraitCamera (tag: PortraitCamera) ← Esta se busca
        └─ [otros componentes]

PlayerEquipmentMenuController
    ├─ Busca automáticamente usando PlayerService.TryGetPlayer()
    ├─ Itera GetComponentsInChildren<Camera>()
    └─ Filtra por tag "PortraitCamera" o nombre "Portrait"
```

### **Rendimiento:**

- **Búsqueda**: Solo se ejecuta una vez (cuando `equipmentPreviewCamera == null`)
- **Impacto**: O(n) donde n = número de cámaras en el player (~2-5 típicamente)
- **Cacheo**: Una vez encontrada, se reutiliza durante toda la sesión

---

## ✅ Checklist Final

- [ ] Layer `UI_Portrait` creada en Project Settings
- [ ] Tag `PortraitCamera` creado en Project Settings
- [ ] Cámara dentro del player con tag `PortraitCamera` asignado
- [ ] Cámara con Culling Mask = `UI_Portrait` únicamente
- [ ] Cámara con RenderTexture asignado
- [ ] Componente `PortraitLayerSwapSRP` añadido (mismo GameObject o en cámara)
- [ ] Campo `portraitLayerSwap` asignado en `PlayerEquipmentMenuController`
- [ ] Prueba: Abrir menú → Ver log "Cámara de retrato encontrada por tag"
- [ ] Prueba: Abrir menú → Player visible, mundo invisible
- [ ] Prueba: Tab Inventario → Player quieto
- [ ] Prueba: Tab Equipamiento → Player rota con stick
- [ ] Prueba: Cerrar menú → Layers restauradas, mundo visible
- [ ] Prueba: Cambiar equipamiento → Se ve en tiempo real

---

## 🚀 Ventajas del Sistema Mejorado

### **Antes (Asignación Manual):**
- ❌ Había que arrastrar la cámara manualmente en el Inspector
- ❌ Si el prefab del player cambiaba, la referencia se perdía
- ❌ Cada escena necesitaba configuración individual

### **Ahora (Búsqueda Automática):**
- ✅ Solo necesitas poner el tag `PortraitCamera` una vez en el prefab del player
- ✅ Funciona automáticamente en todas las escenas
- ✅ Logs claros si algo falla
- ✅ Fallback: aún puedes asignar manualmente si prefieres

---

## 📞 Soporte

Si encuentras algún problema:

1. **Revisa la consola** por logs de `[PlayerEquipmentMenuController]` y `[PortraitLayerSwapSRP]`
2. **Verifica el tag**: Debe ser exactamente `PortraitCamera` (case-sensitive)
3. **Comprueba la jerarquía**: La cámara debe estar dentro del player, no como hermano
4. **Última opción**: Asigna la cámara manualmente en el Inspector del `PlayerEquipmentMenuController`

---

**✨ Implementación completada con búsqueda automática de cámara ✨**

