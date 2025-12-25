# Plantilla de Jerarquía para PlayerHUDV2

Esta es la estructura recomendada para crear tu UI del HUD en Unity.

## 📐 Estructura Completa

```
Canvas (Screen Space - Overlay)
│   Canvas Scaler: 1920x1080 reference
│   Graphic Raycaster
│
└── PlayerHUD (GameObject vacío con PlayerHUDV2)
    │
    ├── HealthContainer
    │   ├── HealthBarBG (Image)
    │   │   └── HealthBarFill (Image - Fill Horizontal) ← healthFillImage
    │   └── HealthText (TextMeshProUGUI) ← healthText
    │
    ├── ManaContainer  
    │   ├── ManaBarBG (Image)
    │   │   └── ManaBarFill (Image - Fill Horizontal) ← manaFillImage
    │   └── ManaText (TextMeshProUGUI) ← manaText
    │
    └── MagicSlotsContainer
        ├── LeftSlot (GameObject)
        │   ├── SlotBG (Image - Fondo del slot)
        │   ├── SlotIcon (Image) ← leftMagicSlotImage
        │   ├── CooldownOverlay (Image - Fill Radial 360) ← leftCooldownOverlay
        │   └── CooldownText (TextMeshProUGUI) ← leftCooldownText
        │
        ├── RightSlot (GameObject)
        │   ├── SlotBG (Image)
        │   ├── SlotIcon (Image) ← rightMagicSlotImage
        │   ├── CooldownOverlay (Image - Fill Radial 360) ← rightCooldownOverlay
        │   └── CooldownText (TextMeshProUGUI) ← rightCooldownText
        │
        └── UpSlot (GameObject)
            ├── SlotBG (Image)
            ├── SlotIcon (Image) ← upMagicSlotImage
            ├── CooldownOverlay (Image - Fill Radial 360) ← upCooldownOverlay
            └── CooldownText (TextMeshProUGUI) ← upCooldownText
```

## 🎨 Configuración de Componentes

### Canvas
```
Component: Canvas
- Render Mode: Screen Space - Overlay
- Pixel Perfect: False
- Sort Order: 100 (para estar encima de todo)

Component: Canvas Scaler
- UI Scale Mode: Scale With Screen Size
- Reference Resolution: 1920 x 1080
- Screen Match Mode: Match Width Or Height
- Match: 0.5
```

### PlayerHUD (GameObject principal)
```
Component: RectTransform
- Anchors: Stretch Stretch (0,0) → (1,1)
- Pivot: 0.5, 0.5
- Position: 0, 0, 0
- Size Delta: 0, 0

Component: PlayerHUDV2
- ← Arrastra todas las referencias aquí
```

### HealthContainer
```
Component: RectTransform
- Anchors: Top Left (0,1) → (0,1)
- Pivot: 0, 1
- Anchored Position: (50, -50) // 50px desde esquina
- Width: 300
- Height: 40

Component: Horizontal Layout Group (Opcional)
- Spacing: 10
- Child Alignment: Middle Left
```

### HealthBarFill (Imagen IMPORTANTE)
```
Component: Image
- Source Image: Tu sprite de barra
- Image Type: Filled
- Fill Method: Horizontal
- Fill Origin: Left
- Fill Amount: 1.0 (se actualizará dinámicamente)
- Color: Rojo/Verde según tu diseño
- Preserve Aspect: False

Component: RectTransform
- Anchors: Stretch Stretch
- Size Delta: 0, 0
```

### ManaContainer
```
Component: RectTransform
- Anchors: Top Left
- Anchored Position: (50, -100) // Debajo de health
- Width: 300
- Height: 40
```

### ManaBarFill (Imagen IMPORTANTE)
```
Component: Image
- Source Image: Tu sprite de barra
- Image Type: Filled
- Fill Method: Horizontal
- Fill Origin: Left
- Fill Amount: 1.0
- Color: Azul según tu diseño
```

### MagicSlotsContainer
```
Component: RectTransform
- Anchors: Bottom Center (0.5, 0) → (0.5, 0)
- Pivot: 0.5, 0
- Anchored Position: (0, 50) // 50px desde abajo
- Width: 250
- Height: 80

Component: Horizontal Layout Group
- Spacing: 15
- Child Alignment: Middle Center
- Child Force Expand: Width: False, Height: False
```

### LeftSlot / RightSlot / UpSlot
```
Component: RectTransform
- Width: 70
- Height: 70

Component: Layout Element
- Preferred Width: 70
- Preferred Height: 70
```

### SlotIcon (Imágenes IMPORTANTES)
```
Component: Image
- Source Image: Icono del hechizo o Empty Slot
- Image Type: Simple
- Color: White (1, 1, 1, 1)
- Preserve Aspect: True
- Raycast Target: False

Component: RectTransform
- Anchors: Stretch Stretch
- Size Delta: -10, -10 (padding de 5px)
```

### CooldownOverlay (Opcional pero recomendado)
```
Component: Image
- Source Image: Círculo negro o sprite de overlay
- Image Type: Filled
- Fill Method: Radial 360
- Fill Origin: Top
- Fill Amount: 1.0 (empieza lleno)
- Color: Negro semi-transparente (0, 0, 0, 0.7)
- Clockwise: True

Component: RectTransform
- Anchors: Stretch Stretch
- Size Delta: 0, 0
```

### CooldownText (Opcional)
```
Component: TextMeshProUGUI
- Font: Tu fuente
- Font Size: 32
- Alignment: Center, Middle
- Color: Blanco
- Outline: Grosor 0.2, Color negro
- Auto Size: False

Component: RectTransform
- Anchors: Center
- Width: 40
- Height: 40
- Position: Centro del slot
```

### HealthText / ManaText (Opcional)
```
Component: TextMeshProUGUI
- Font: Tu fuente
- Font Size: 24
- Alignment: Middle Left
- Color: Blanco
- Text: "100/100"
- Auto Size: False

Component: RectTransform
- Anchors: Middle Right
- Pivot: 1, 0.5
- Width: 100
- Height: 30
```

## 🎯 Orden de Renderizado

Para que los overlays aparezcan encima:

```
SlotBG (orden 0)
  └── SlotIcon (orden 1)
      └── CooldownOverlay (orden 2)
          └── CooldownText (orden 3)
```

**Importante:** Los hijos se renderizan después de sus padres.

## 🎨 Assets Necesarios

### Sprites Mínimos:
1. **HealthBar_BG.png** - Fondo de barra de vida
2. **HealthBar_Fill.png** - Fill de vida (rojo/verde)
3. **ManaBar_BG.png** - Fondo de barra de maná
4. **ManaBar_Fill.png** - Fill de maná (azul)
5. **Slot_BG.png** - Fondo de slot de magia (cuadrado/círculo)
6. **Slot_Empty.png** - Icono cuando el slot está vacío
7. **Cooldown_Overlay.png** - Círculo negro para overlay de cooldown

### Configuración de Sprites en Unity:
```
Texture Type: Sprite (2D and UI)
Sprite Mode: Single
Pixels Per Unit: 100
Filter Mode: Bilinear
Compression: None (para UI) o Normal Quality
```

## ⚙️ Asignación de Referencias

Una vez creada la jerarquía:

1. Selecciona `PlayerHUD` (GameObject principal)
2. En el componente `PlayerHUDV2`:
   - Arrastra `HealthBarFill` → **Health Fill Image**
   - Arrastra `ManaBarFill` → **Mana Fill Image**
   - Arrastra `LeftSlot/SlotIcon` → **Left Magic Slot Image**
   - Arrastra `RightSlot/SlotIcon` → **Right Magic Slot Image**
   - Arrastra `UpSlot/SlotIcon` → **Up Magic Slot Image**
   - (Opcional) Arrastra overlays y textos
3. Click derecho en componente → **Validate Setup**
4. Debe decir: "✅ Todas las referencias críticas están asignadas"

## 🚀 Quick Start (Versión Mínima)

Si quieres probar rápido sin arte:

1. Crea solo las imágenes Fill (blancas)
2. Configúralas como Fill Type: Filled
3. Asigna solo las 5 referencias obligatorias
4. Testea con **Test Fill Amounts** en el Context Menu

Una vez funcione, reemplaza los sprites blancos por tu arte.

---

**Tip:** Puedes crear esta estructura una vez y guardarla como Prefab para reutilizar en otras escenas.

