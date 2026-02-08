# 📜 Guía de Configuración: Escena de Dedicatoria

## 🎯 Script Creado: `DedicationController.cs`

**Ubicación:** `Assets/Scripts/UI/DedicationController.cs`

---

## 🎬 Funcionalidad

El script gestiona una secuencia de dedicatoria con dos líneas de texto que aparecen y desaparecen suavemente antes de cargar el Menú Principal.

### Secuencia Automática:

1. ⏱️ **Espera 1 segundo** en pantalla negra
2. ✨ **Fade In** de la primera línea: *"A todas mis estrellas."*
3. ⏱️ **Espera 0.5 segundos**
4. ✨ **Fade In** de la segunda línea: *"A quienes hacen mi mundo un poco más brillante."*
5. ⏱️ **Espera 3 segundos** para lectura
6. 🌑 **Fade Out simultáneo** de ambas líneas
7. 🎮 **Carga el Menú Principal**

---

## 🛠️ Configuración en Unity

### Paso 1: Crear la Escena

1. Crea una nueva escena llamada **"Dedication"** o **"Intro"**
2. Guárdala en `Assets/Scenes/`

### Paso 2: Configurar el Fondo Negro

1. **Opción A - Usando UI Image:**
   - Crea un Canvas: `Right Click → UI → Canvas`
   - Dentro del Canvas: `Right Click → UI → Image`
   - Renombra la Image a "Background"
   - En el Inspector:
     - **Anchor Presets:** Stretch (ambos ejes)
     - **Color:** Negro (R:0, G:0, B:0, A:255)

2. **Opción B - Usando Cámara:**
   - Selecciona la Main Camera
   - En el Inspector, cambia **Background Color** a Negro

### Paso 3: Crear los Textos

1. **Primera Línea:**
   - En el Canvas: `Right Click → UI → Text - TextMeshPro`
   - Renombra a **"TextLine1"**
   - Configura el texto: *"A todas mis estrellas."*
   - Ajusta:
     - **Font Size:** 36-48 (según preferencia)
     - **Alignment:** Centro
     - **Color:** Blanco (R:255, G:255, B:255, A:255)
     - **Position:** Centro superior o centro de la pantalla

2. **Segunda Línea:**
   - En el Canvas: `Right Click → UI → Text - TextMeshPro`
   - Renombra a **"TextLine2"**
   - Configura el texto: *"A quienes hacen mi mundo un poco más brillante."*
   - Ajusta:
     - **Font Size:** 32-42 (ligeramente más pequeño que la primera)
     - **Alignment:** Centro
     - **Color:** Blanco (R:255, G:255, B:255, A:255)
     - **Position:** Debajo de TextLine1 (separación ~50-100 píxeles)

### Paso 4: Agregar el Script

1. Crea un GameObject vacío en la escena:
   - `Right Click → Create Empty`
   - Renombra a **"DedicationManager"**

2. Agrega el script:
   - Con DedicationManager seleccionado
   - En el Inspector: `Add Component → Dedication Controller`

3. **Asignar Referencias:**
   - **Text Line 1:** Arrastra el objeto TextLine1
   - **Text Line 2:** Arrastra el objeto TextLine2

4. **Configurar Tiempos (Opcional):**
   - **Fade Duration:** 1.5 segundos (por defecto)
   - **Reading Time:** 3 segundos (por defecto)
   - **Initial Delay:** 1 segundo (por defecto)
   - **Delay Between Lines:** 0.5 segundos (por defecto)

5. **Nombre de Escena:**
   - **Main Menu Scene Name:** "MainMenu" (asegúrate de que coincida con tu escena)

### Paso 5: Configurar Build Settings

1. Ve a `File → Build Settings`
2. Asegúrate de que ambas escenas estén agregadas:
   - **Escena 0:** Dedication (o Intro)
   - **Escena 1:** MainMenu
3. Si no están, arrastra las escenas desde la carpeta de Scenes

---

## ⚙️ Parámetros Configurables

| Parámetro | Valor por Defecto | Descripción |
|-----------|-------------------|-------------|
| **Fade Duration** | 1.5s | Duración del efecto fade in/out |
| **Reading Time** | 3s | Tiempo para leer antes del fade out |
| **Initial Delay** | 1s | Espera inicial en negro |
| **Delay Between Lines** | 0.5s | Espera entre la 1ª y 2ª línea |
| **Main Menu Scene Name** | "MainMenu" | Nombre de la escena a cargar |

---

## 🎨 Personalización Recomendada

### Tipografía:
- Considera usar una fuente elegante o manuscrita para un toque más emotivo
- Importa fuentes personalizadas desde `Assets/Fonts/`

### Colores:
- **Fondo:** Negro puro o azul oscuro espacial
- **Texto:** Blanco, dorado, o azul claro según la estética de tu juego

### Efectos Adicionales (Opcional):
- Agrega partículas de estrellas en el fondo
- Añade música suave de fondo
- Considera un brillo sutil en los textos

---

## 🔧 Funcionalidad Opcional: Saltar la Dedicatoria

El script incluye código comentado para permitir saltar la secuencia. Para habilitarlo:

1. Abre `DedicationController.cs`
2. En el método `Update()`, descomenta estas líneas:
   ```csharp
   if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
   {
       StopAllCoroutines();
       LoadMainMenu();
   }
   ```
3. Guarda el archivo

Ahora el jugador podrá presionar cualquier tecla o hacer clic para saltar directamente al menú.

---

## ✅ Checklist de Verificación

- [ ] Canvas creado con fondo negro
- [ ] TextLine1 creado y configurado
- [ ] TextLine2 creado y configurado
- [ ] DedicationManager creado
- [ ] Script DedicationController agregado
- [ ] Referencias asignadas en el Inspector
- [ ] Escena "MainMenu" existe y está en Build Settings
- [ ] Escena "Dedication" está en Build Settings (Index 0 si es la inicial)
- [ ] Textos están configurados con el contenido correcto
- [ ] Tiempos ajustados según preferencia

---

## 🎮 Pruebas

1. **Reproducir la escena** en el Editor
2. Verificar que:
   - ✅ La pantalla comienza en negro
   - ✅ La primera línea aparece suavemente
   - ✅ La segunda línea aparece después
   - ✅ Ambas desaparecen juntas
   - ✅ Se carga la escena MainMenu

3. **Ajustar tiempos** según necesites en el Inspector

---

## 🐛 Solución de Problemas

### "NullReferenceException" al iniciar:
- **Causa:** Referencias no asignadas
- **Solución:** Asegúrate de arrastrar TextLine1 y TextLine2 al script en el Inspector

### No carga el MainMenu:
- **Causa:** Nombre de escena incorrecto o escena no en Build Settings
- **Solución:** Verifica el nombre en `mainMenuSceneName` y añade la escena a Build Settings

### Los textos no desaparecen:
- **Causa:** Alpha inicial no establecido
- **Solución:** El script lo hace automáticamente, verifica que las referencias estén asignadas

### Fade demasiado rápido/lento:
- **Solución:** Ajusta `fadeDuration` en el Inspector (valores recomendados: 1.0s - 2.5s)

---

## 📝 Notas Adicionales

- El script utiliza **corrutinas** para manejar la secuencia temporal
- Todos los fades son suaves usando **Mathf.Lerp** implícito
- El código está documentado y organizado para facilitar modificaciones futuras
- Compatible con Unity 2020.3 y versiones superiores

---

**Creado:** 2026-02-05  
**Versión:** 1.0  
**Script:** `DedicationController.cs`
