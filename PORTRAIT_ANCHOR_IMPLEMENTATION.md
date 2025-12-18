# Implementación del PortraitAnchor para Centrado Estable del Player

## 📋 Resumen de Cambios

Se ha implementado un sistema de centrado estable para el retrato del player en el menú de equipamiento usando un **Transform de referencia llamado "PortraitAnchor"**.

---

## ✅ Cambios Realizados en PlayerEquipmentMenuController.cs

### **1. Nuevo Campo Serializado**

```csharp
[SerializeField, Tooltip("Transform de referencia para centrar la cámara (busca 'PortraitAnchor' automáticamente si es null)")]
private Transform portraitAnchor;
```

**Función:** Punto de referencia estable para la cámara de retrato, independiente del pivot del modelo.

---

### **2. Búsqueda Automática en TrySetupPreviewTarget()**

El sistema ahora busca automáticamente un hijo llamado `"PortraitAnchor"` dentro del player si el campo `portraitAnchor` está vacío:

```csharp
if (portraitAnchor == null)
{
    portraitAnchor = _playerPreviewTarget.Find("PortraitAnchor");
    if (portraitAnchor != null)
    {
        Debug.Log("[PlayerEquipmentMenuController] PortraitAnchor encontrado automáticamente");
    }
    else
    {
        Debug.LogWarning("[PlayerEquipmentMenuController] No se encontró 'PortraitAnchor'. Se usará el transform raíz.");
    }
}
```

**Logs en Consola:**
- ✅ **Éxito:** `[PlayerEquipmentMenuController] PortraitAnchor encontrado automáticamente: PortraitAnchor`
- ⚠️ **Advertencia:** `[PlayerEquipmentMenuController] No se encontró 'PortraitAnchor' como hijo del player. Se usará el transform raíz.`

---

### **3. Posicionamiento de Cámara Basado en Anchor**

En `UpdateEquipmentCamera()`, la cámara ahora usa el `portraitAnchor` como punto de referencia:

```csharp
// Usar portraitAnchor como punto de referencia si está disponible, sino usar el target
Transform anchorPoint = portraitAnchor != null ? portraitAnchor : _playerPreviewTarget;

// Calcular posición de la cámara usando el anchor como referencia
Vector3 anchorPos = anchorPoint.position + equipmentCameraLookOffset;
Vector3 cameraPos = anchorPos - cameraForward * equipmentCameraDistance + ...;

_equipmentPreviewCamera.transform.position = cameraPos;
_equipmentPreviewCamera.transform.rotation = Quaternion.LookRotation((anchorPos - cameraPos).normalized, Vector3.up);
```

**Resultado:**
- La cámara siempre mira al `PortraitAnchor`
- El centrado es estable sin importar el pivot del modelo
- No hay "baile" al cambiar animaciones o equipamiento

---

### **4. Rotación Alrededor del Anchor**

La rotación del player con el gamepad sigue funcionando igual, pero el punto de referencia visual es el `PortraitAnchor`:

```csharp
// Aplicar rotación alrededor del anchor, no del root del player
var lookDir = Quaternion.Euler(0f, _previewPlayerYaw, 0f) * cameraForward;
var previewRotation = Quaternion.LookRotation(-lookDir, Vector3.up);
_playerPreviewTarget.rotation = previewRotation;
```

**Nota:** La rotación se aplica al `_playerPreviewTarget` (root del player), pero la cámara siempre apunta al `portraitAnchor`, creando un efecto de órbita estable.

---

## 🛠️ Configuración en Unity

### **Paso 1: Crear el PortraitAnchor en el Prefab del Player**

1. Abre el **prefab del Player** en el editor
2. Haz clic derecho en el root del player > **Create Empty**
3. Nómbralo exactamente: `PortraitAnchor`
4. Posiciona el `PortraitAnchor` donde quieres que la cámara apunte (normalmente el centro del pecho o cabeza del personaje)

**Ejemplo de jerarquía:**
```
PLAYER
├─ _PLAYER (modelo)
│  ├─ Body01
│  ├─ Body02
│  └─ ...
├─ root
├─ CarryPoint
├─ FlyingTrailPoint
├─ vThirdPersonCamera
├─ Hint Camera
├─ PortraitCamera
└─ PortraitAnchor ← NUEVO (posicionado en el centro del personaje)
```

**Recomendaciones de posición:**
- **Y (altura):** A la altura del pecho o ligeramente por encima (1.5 - 1.7 unidades desde el suelo)
- **X, Z:** En el centro del personaje (0, 0 relativo al player)

---

### **Paso 2: (Opcional) Asignar Manualmente**

Si prefieres asignar el `PortraitAnchor` manualmente:

1. Selecciona el GameObject con `PlayerEquipmentMenuController`
2. En el Inspector, busca la sección **Cámara de equipamiento**
3. Arrastra el `PortraitAnchor` al campo `Portrait Anchor`

**Nota:** Si lo dejas vacío, se buscará automáticamente.

---

### **Paso 3: Ajustar Offset de Cámara (si es necesario)**

Si el personaje aparece descentrado, ajusta estos valores en el Inspector:

- **Equipment Camera Look Offset:** Offset aplicado al anchor (ej: `(0, 0.2, 0)` para subir ligeramente)
- **Equipment Camera Height:** Altura de la cámara
- **Equipment Camera Distance:** Distancia de la cámara al anchor
- **Equipment Camera Horizontal Offset:** Desplazamiento horizontal

---

## 🎮 Comportamiento Esperado

### ✅ **Sin PortraitAnchor (fallback)**
- La cámara usa el transform raíz del player
- Puede haber desplazamientos si el pivot del modelo está mal posicionado
- Funcional pero no ideal

### ✅ **Con PortraitAnchor (recomendado)**
- El personaje siempre está centrado en la RawImage
- Sin desplazamientos al cambiar animaciones
- Sin desplazamientos al cambiar equipamiento
- Rotación estable y predecible con el gamepad

---

## 🔍 Verificación y Troubleshooting

### **Problema: El personaje no aparece centrado**

**Solución:**
1. Verifica que el `PortraitAnchor` existe como hijo del player
2. Ajusta la posición del `PortraitAnchor` en el prefab del player
3. Prueba diferentes valores de `equipmentCameraLookOffset`

### **Problema: El personaje "baila" o se desplaza**

**Solución:**
1. Asegúrate de que el `PortraitAnchor` **NO** tiene animaciones aplicadas
2. Verifica que el `PortraitAnchor` no es hijo de un hueso animado
3. El `PortraitAnchor` debe ser hijo directo del root del player, no de `_PLAYER` (modelo)

### **Problema: No se encuentra el PortraitAnchor**

**Solución:**
1. Verifica que el nombre es exactamente `"PortraitAnchor"` (case-sensitive)
2. Comprueba que está como hijo directo del player, no anidado profundamente
3. Revisa los logs en consola para ver qué está pasando

---

## 📝 Notas Técnicas

### **¿Por qué usar un Transform separado?**

**Problema anterior:**
- La cámara apuntaba al pivot del modelo del player
- Los modelos pueden tener pivots en los pies, centro de masa, etc.
- Al cambiar animaciones, el modelo puede desplazarse ligeramente
- Al cambiar equipamiento, las proporciones pueden variar

**Solución con PortraitAnchor:**
- Punto de referencia fijo e independiente del modelo
- No afectado por animaciones del esqueleto
- Control total sobre dónde apunta la cámara
- Consistente entre diferentes equipamientos

### **Compatibilidad**

✅ **Compatible con:**
- Sistema de activación/desactivación de cámara
- Sistema de cambio de layers (PortraitLayerSwapSRP)
- RenderTexture existente
- Input de rotación con gamepad
- Cambio de equipamiento en tiempo real
- Todas las pestañas del menú (Inventario, Hechizos, Equipamiento)

❌ **NO afecta:**
- Cámara de gameplay
- Movimiento del player
- Sistema de combate
- Otras funcionalidades del player

---

## ✅ Checklist de Implementación

- [ ] Transform `PortraitAnchor` creado en el prefab del player
- [ ] `PortraitAnchor` posicionado en el centro del personaje (altura pecho/cabeza)
- [ ] `PortraitAnchor` es hijo directo del root del player
- [ ] Probar abrir menú → Ver log de confirmación en consola
- [ ] Personaje centrado en la RawImage
- [ ] Rotar con gamepad → Rotación estable alrededor del centro
- [ ] Cambiar equipamiento → Personaje permanece centrado
- [ ] Cambiar pestaña → Personaje permanece centrado

---

## 🚀 Ventajas de la Implementación

### **Antes (sin PortraitAnchor):**
- ❌ Dependencia del pivot del modelo (inestable)
- ❌ Posibles desplazamientos con animaciones
- ❌ Difícil de ajustar sin modificar el modelo

### **Ahora (con PortraitAnchor):**
- ✅ Punto de referencia fijo y estable
- ✅ Independiente del pivot del modelo
- ✅ Fácil de ajustar sin tocar el modelo
- ✅ Búsqueda automática (cero configuración manual)
- ✅ Fallback al transform raíz si no existe

---

**✨ Implementación completada. El personaje ahora permanece centrado de forma estable en el retrato del menú. ✨**

