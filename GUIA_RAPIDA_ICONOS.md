# ⚡ Guía Rápida: Configurar Iconos en Diálogos (Ya tengo los sprites)

## 📋 Pasos Rápidos (5 minutos)

### 1️⃣ Abrir la Herramienta TMP Sprite Importer

En Unity:
```
Window > TextMeshPro > Sprite Importer
```

### 2️⃣ Añadir tus Sprites

En la ventana que se abre:

1. **Source (Sprite/Texture)**:
   - Arrastra TODOS tus sprites de iconos aquí
   - O selecciónalos con el selector de objetos

2. **Sprite Data Source**: 
   - Selecciona `Sprite Asset`

3. **Character Sequence**: 
   - Deja `Unicode HEX (Default)`

### 3️⃣ Asignar Nombres a los Iconos

En la lista de sprites que aparece, **asigna nombres descriptivos** a cada uno:

**Botones de Gamepad:**
- `ButtonA`, `ButtonB`, `ButtonX`, `ButtonY`
- `LB`, `RB`, `LT`, `RT`
- `DpadUp`, `DpadDown`, `DpadLeft`, `DpadRight`
- `LeftStick`, `RightStick`
- `Start`, `Select`

**UI e Items:**
- `Heart` (vida)
- `Star` (maná)
- `Coin` (moneda)
- `Key` (llave)
- `Potion` (poción)
- `Sword`, `Shield`, `Chest`, etc.

💡 **IMPORTANTE**: Los nombres son **case-sensitive** (distinguen mayúsculas)

### 4️⃣ Guardar el Sprite Asset

1. Click en **"Save Sprite Asset"**

2. Guarda en esta ruta **EXACTA**:
   ```
   Assets/TextMesh Pro/Resources/Sprite Assets/DialogueIcons.asset
   ```

3. Nombre recomendado: `DialogueIcons`

### 5️⃣ Configurar en el DialogueManager

1. En Unity, ve a:
   ```
   Tools > Dialogue > Setup Icons
   ```

2. En la ventana que se abre:
   - **TMP Sprite Asset**: Arrastra el `DialogueIcons.asset` que acabas de crear
   - **Dialogue Body Text**: Arrastra el componente `TextMeshProUGUI` del diálogo
     - (Busca en la escena el GameObject del DialogueManager)

3. Click en **"✅ Configurar en DialogueManager"**

### ✅ ¡Listo!

Ahora puedes usar iconos en tus diálogos:

```
Pulsa <sprite name="ButtonA"> para saltar

Abre el inventario con <sprite name="DpadDown">

Tu vida <sprite name="Heart"> está baja
```

---

## 🎮 Ejemplo Completo

**Texto del diálogo:**
```
Hola aventurero. Para jugar:

- Usa <sprite name="LeftStick"> para moverte
- Pulsa <sprite name="ButtonA"> para saltar
- <sprite name="DpadDown"> abre el inventario
- <sprite name="LB"> y <sprite name="RB"> cambian de arma

¡Cuida tu vida <sprite name="Heart"> y tu maná <sprite name="Star">!
```

---

## 🔍 Verificar que Funciona

1. **Crear un diálogo de prueba**:
   - Click derecho en Project → `Create > Dialogue Asset`
   - Añade una línea con: `Pulsa <sprite name="ButtonA"> para continuar`

2. **Probar en el juego**:
   - Inicia el diálogo
   - Deberías ver el icono del botón A en lugar del texto

---

## 💡 Tips Rápidos

### Ver todos los iconos disponibles
```
Tools > Dialogue > Setup Icons
→ Ver Iconos Disponibles
```

### Cambiar color del icono
```
<sprite name="Heart" color=#FF0000>
```

### Cambiar tamaño del icono
```
<sprite name="ButtonA" size=150%>
```

### Usar índice en lugar de nombre
```
<sprite=0>  // Primer sprite
<sprite=1>  // Segundo sprite
```

---

## ❓ Problemas Comunes

### ❌ No veo el icono (sale un cuadrado)

**Solución**:
1. Verifica que el nombre coincide exactamente (mayúsculas/minúsculas)
2. Vuelve a `Tools > Dialogue > Setup Icons` y configura de nuevo
3. Comprueba que el Sprite Asset está en la ruta correcta

### ❌ El icono está muy pequeño

**Solución**:
```
<sprite name="ButtonA" size=150%>
```

### ❌ Quiero añadir más iconos después

**Solución**:
1. `Window > TextMeshPro > Sprite Importer`
2. Carga el asset existente con `Load Sprite Asset`
3. Añade los nuevos sprites
4. `Update Sprite Asset`

---

## 🚀 ¡A usar iconos!

Ya puedes mejorar todos tus diálogos con iconos visuales. Recuerda:

✅ Usa nombres descriptivos: `ButtonA`, `DpadDown`, `Heart`
✅ Guarda en `Assets/TextMesh Pro/Resources/Sprite Assets/`
✅ Configura una vez con `Tools > Dialogue > Setup Icons`
✅ Usa `<sprite name="Nombre">` en tus textos

---

**¿Necesitas ayuda?** Abre la guía completa: `GUIA_ICONOS_EN_DIALOGOS.md`

