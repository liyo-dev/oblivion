# 🌙 Bake Nocturno de Lightmaps - Alta Calidad

## ✅ Configuración Aplicada

He configurado MainWorldLightSettings.lighting para **MÁXIMA CALIDAD**:

### Cambios Realizados:

| Parámetro | Antes | Ahora | Mejora |
|-----------|-------|-------|--------|
| **Lightmap Max Size** | 64 | 2048 | 32x más detalle |
| **Direct Samples** | 32 | 256 | 8x más precisión |
| **Indirect Samples** | 512 | 4096 | 8x más calidad GI |
| **Environment Samples** | 256 | 2048 | 8x mejor skylight |
| **Bounces** | 2 | 4 | Doble rebotes luz |
| **Light Probe Multiplier** | 4 | 8 | Doble precisión probes |
| **Bake Resolution** | 20 | 40 | Doble resolución |
| **Compression** | High Quality | Uncompressed | Sin pérdida |
| **Filtering Mode** | Auto | Advanced | Mejor filtrado |
| **Denoiser** | Intel OIDN | Optix | Máxima calidad |
| **AO** | OFF | ON | Ambient Occlusion activado |
| **Mipmap Limits** | ON | OFF | Sin límites |
| **Gauss Radius Indirect** | 1 | 5 | Más suavizado |

---

## 🚀 Cómo Iniciar el Bake

### Opción 1: Automático (Recomendado)

1. En Unity: **Tools → Lighting → Start Production Bake**
2. Lee el diálogo y haz clic en **"Sí, Iniciar Bake"**
3. Unity comenzará el baking automáticamente

### Opción 2: Manual

1. Abre MainWorld.unity
2. Ve a **Window → Rendering → Lighting**
3. Asegúrate de que **Lighting Settings** esté asignado a "MainWorldLightSettings"
4. Abajo a la derecha, haz clic en **"Generate Lighting"**

---

## ⏱️ Tiempo Estimado

- **Escena pequeña:** 2-4 horas
- **MainWorld (grande):** 6-12 horas
- **Muy compleja:** 12-24 horas

Con esta configuración de alta calidad, espera que tome **toda la noche**.

---

## ⚙️ Configuración del PC

### Antes de Iniciar:

1. **Guarda todo tu trabajo** (Ctrl+S)
2. **Cierra otras aplicaciones** (libera RAM)
3. **Desactiva ahorro de energía:**
   - Panel de Control → Energía
   - Selecciona "Alto rendimiento"
   - "Suspender nunca" / "Apagar pantalla nunca"
4. **Desactiva actualizaciones automáticas de Windows:**
   - Configuración → Windows Update
   - Pausar actualizaciones por 7 días
5. **Conecta el PC a corriente** (no batería)
6. **Opcional:** Cierra Discord, Chrome, etc.

---

## 📊 Monitoreo del Progreso

### En Unity:

- Mira la barra de progreso abajo a la derecha
- Dice "Baking..." con un porcentaje
- Puedes minimizar Unity - seguirá trabajando

### Script de Monitoreo:

- **Tools → Lighting → Show Baking Progress** - Muestra % en consola

### Cancelar:

- **Tools → Lighting → Cancel Baking** - Cancela el proceso

---

## 🎯 Checklist Pre-Bake

- [ ] Configuración aplicada (ya hecho ✓)
- [ ] Trabajo guardado
- [ ] PC configurado para no dormir
- [ ] Actualizaciones Windows pausadas
- [ ] Otras apps cerradas
- [ ] PC conectado a corriente
- [ ] Bake iniciado

---

## 📝 Después del Bake

Al terminar (mañana):

1. Unity habrá terminado (desaparece barra de progreso)
2. Verás los nuevos lightmaps en la escena
3. **Guarda la escena** (Ctrl+S)
4. **Guarda el proyecto** (Ctrl+Shift+S)
5. Los lightmaps estarán en: `Assets/Scenes/Main World/MainWorld/`
6. Commit a Git si quieres guardar el resultado

---

## ⚠️ Problemas Comunes

### Si Unity se cierra:
- Reabre Unity
- Ve a Window → Rendering → Lighting
- Si el bake no terminó, reinicia

### Si el PC se apaga:
- Verifica configuración de energía
- Reinicia el bake

### Si tarda demasiado:
- Es normal con alta calidad
- Puedes ver progreso en console log

---

## 🎨 Calidad Resultante

Con esta configuración obtendrás:

✓ Sombras suaves y detalladas  
✓ Global Illumination realista  
✓ Ambient Occlusion preciso  
✓ Sin artifacts ni noise  
✓ Rebotes de luz naturales  
✓ Light probes de alta calidad  
✓ Sin compresión (máximo detalle)  

**Perfecto para builds de producción y trailers**

---

## 🚀 Comandos Rápidos

```
Iniciar:  Tools → Lighting → Start Production Bake
Cancelar: Tools → Lighting → Cancel Baking
Ver %:    Tools → Lighting → Show Baking Progress
```

---

**¡Listo para dejar el bake toda la noche!** 🌙

Inicia el bake antes de irte y mañana tendrás lightmaps de calidad profesional.

