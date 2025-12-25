# PlayerHUDV2 - Setup Simplificado (Sin Textos)

## 📋 Versión Simplificada

Esta guía muestra cómo configurar el HUD **sin textos numéricos** y con **cooldowns solo radiales** (sin números).

## 🎨 Estructura de UI Mínima

```
Canvas (Screen Space - Overlay)
└── PlayerHUD (GameObject con PlayerHUDV2)
    ├── HealthBar
    │   ├── Background (Image - opcional)
    │   └── Fill (Image) ← healthFillImage
    │
    ├── ManaBar
    │   ├── Background (Image - opcional)
    │   └── Fill (Image) ← manaFillImage
    │
    └── MagicSlots
        ├── LeftSlot
        │   ├── SlotIcon (Image) ← leftMagicSlotImage
        │   └── CooldownOverlay (Image - Radial 360) ← leftCooldownOverlay
        │
        ├── RightSlot
        │   ├── SlotIcon (Image) ← rightMagicSlotImage
        │   └── CooldownOverlay (Image - Radial 360) ← rightCooldownOverlay
        │
        └── SpecialSlot
            ├── SlotIcon (Image) ← specialMagicSlotImage
            └── CooldownOverlay (Image - Radial 360) ← specialCooldownOverlay
```

## ⚙️ Configuración Rápida

### 1. Barras Fill (Vida/Maná)

**Configuración de las imágenes Fill**:
```
Image Type: Filled
Fill Method: Horizontal
Fill Origin: Left
Fill Amount: 1.0
```

### 2. Cooldown Overlays (Radiales)

**Configuración para efecto radial de cooldown**:
```
Image Type: Filled
Fill Method: Radial 360
Fill Origin: Top
Fill Amount: 1.0
Clockwise: ✓ (activado)
Color: Negro semi-transparente (R:0, G:0, B:0, A:180)
```

**Comportamiento**: El overlay comenzará lleno (negro) y se "vaciará" en sentido horario desde arriba.

### 3. Iconos de Slots

**Configuración de SlotIcon**:
```
Image Type: Simple
Preserve Aspect: ✓ (activado)
Color: Blanco
RaycastTarget: ✗ (desactivado)
```

## 📦 Referencias a Asignar

### ✅ OBLIGATORIAS (5 referencias mínimas)

1. **Health Fill Image** - Fill de barra de vida
2. **Mana Fill Image** - Fill de barra de maná
3. **Left Magic Slot Image** - Icono del slot izquierdo
4. **Right Magic Slot Image** - Icono del slot derecho
5. **Special Magic Slot Image** - Icono del slot especial

### ⭕ OPCIONALES (3 overlays de cooldown)

6. **Left Cooldown Overlay** - Overlay radial izquierdo
7. **Right Cooldown Overlay** - Overlay radial derecho
8. **Special Cooldown Overlay** - Overlay radial especial

**Nota**: Si no asignas los overlays, los cooldowns simplemente no se mostrarán visualmente (pero seguirán funcionando internamente).

## 🎯 Pasos de Setup

### Paso 1: Crear la UI
1. Crea la estructura de GameObjects e Images según el diagrama
2. Configura las imágenes Fill y Radial según especificaciones

### Paso 2: Agregar el Componente
1. Selecciona el GameObject `PlayerHUD`
2. Add Component → `PlayerHUDV2`

### Paso 3: Asignar Referencias
1. Arrastra **Health Fill** → campo `Health Fill Image`
2. Arrastra **Mana Fill** → campo `Mana Fill Image`
3. Arrastra los 3 **SlotIcon** → campos correspondientes
4. (Opcional) Arrastra los 3 **CooldownOverlay** → campos correspondientes

### Paso 4: Validar
1. Click derecho en el componente `PlayerHUDV2`
2. Selecciona **Validate Setup**
3. Verifica que diga: "✅ Todas las referencias críticas están asignadas"

### Paso 5: Testear
1. Entra en Play Mode
2. Verifica que las barras funcionen
3. Usa hechizos para ver los cooldowns radiales

## 🎨 Tips de Arte

### Barras de Vida/Maná
- **Tamaño recomendado**: 300x40 px
- **Formato**: PNG con transparencia
- **Gradient**: Opcional en el fill

### Cooldown Overlays
- **Forma**: Círculo perfecto
- **Color**: Negro semi-transparente
- **Tamaño**: Igual al SlotIcon (ej: 70x70 px)

### Iconos de Slots
- **Tamaño**: 64x64 o 128x128 px
- **Formato**: PNG con transparencia
- **Estilo**: Claro y reconocible

## 🔧 Configuración Visual

En el Inspector del `PlayerHUDV2`:

```
Available Color: Blanco (1, 1, 1, 1) - Hechizo disponible
Cooldown Color: Gris (0.5, 0.5, 0.5, 0.7) - En cooldown
No Mana Color: Rojo (1, 0.3, 0.3, 0.8) - Sin maná
Empty Slot Sprite: Sprite para slots vacíos
```

## ✅ Checklist Final

- [ ] 5 referencias obligatorias asignadas
- [ ] Barras configuradas como Fill Horizontal
- [ ] Overlays configurados como Fill Radial 360
- [ ] Validate Setup pasa sin errores
- [ ] Barras de vida/maná se actualizan en Play Mode
- [ ] Cooldowns se visualizan como radiales
- [ ] Color cambia cuando no hay maná

## 🎮 Comportamiento Esperado

### Barras
- **Vida**: Se llena/vacía horizontalmente de izquierda a derecha
- **Maná**: Se llena/vacía horizontalmente de izquierda a derecha

### Cooldowns Radiales
1. **Hechizo disponible**: Overlay oculto, icono blanco
2. **Uso del hechizo**: Overlay aparece lleno (negro)
3. **Durante cooldown**: Overlay se vacía en sentido horario desde arriba
4. **Cooldown terminado**: Overlay desaparece
5. **Sin maná**: Icono se vuelve rojo, overlay oculto

## 🐛 Troubleshooting

### "Las barras no se actualizan"
- Verifica que PlayerService.Player encuentra al jugador
- Verifica que el jugador tiene PlayerHealthSystem y ManaPool

### "Los cooldowns no se muestran"
- Asegúrate de haber asignado los CooldownOverlay (son opcionales)
- Verifica que sean tipo Filled → Radial 360

### "Los iconos no aparecen"
- Actualmente no se muestran iconos de hechizos
- Necesitas agregar campo `icon` a `MagicSpellSO` (ver documentación completa)

---

**Versión**: Simplificada (Sin Textos)  
**Fecha**: 2025-12-24  
**Estado**: ✅ Lista para usar

