# ⚙️ SETUP: Sistema de Colisión de Proyectiles en START

## 🎯 Sistema Persistente con ServiceLocator

El sistema de colisión de proyectiles ahora es un **servicio singleton** que:
- ✅ Persiste entre escenas (DontDestroyOnLoad)
- ✅ Se registra en el ServiceLocator
- ✅ Se inicializa automáticamente desde la escena START
- ✅ Está disponible globalmente durante toda la sesión

---

## 📋 PASOS DE INSTALACIÓN

### Paso 1: Crear el Asset de Configuración

1. En Unity, ve a la carpeta `Assets/Resources` (o crea esta carpeta si no existe)
2. Clic derecho → `Create > Game > Projectile Collision Settings`
3. Nómbralo: **`ProjectileCollisionSettings`**

### Paso 2: Configurar el VFX y Parámetros

1. Selecciona el asset `ProjectileCollisionSettings`
2. En el Inspector, configura:

```
┌─────────────────────────────────────────────┐
│ Projectile Collision Settings SO            │
├─────────────────────────────────────────────┤
│ VFX de Colisión                             │
│ ├─ Collision VFX: [Tu prefab de VFX]       │
│ └─ Vfx Lifetime: 2                          │
│                                              │
│ Fuerzas de Empuje                           │
│ ├─ Player Knockback Force: 8                │
│ └─ Npc Knockback Force: 8                   │
│                                              │
│ Efectos de Cámara                           │
│ ├─ Camera Shake Intensity: 0.5              │
│ └─ Camera Shake Duration: 0.3               │
│                                              │
│ Audio                                       │
│ └─ Collision SFX Key: "ProjectileClash"    │
└─────────────────────────────────────────────┘
```

### Paso 3: Añadir el Servicio a la Escena START

**IMPORTANTE**: Este servicio DEBE estar en la escena START para persistir entre escenas.

1. **Abre la escena START**: `Assets/Scenes/Systems/Start.unity`

2. **Crea un GameObject** para el servicio:
   - Clic derecho en Hierarchy → `Create Empty`
   - Nómbralo: **`ProjectileCollisionService`**

3. **Añade el componente**:
   - Con el GameObject seleccionado
   - En el Inspector, clic en `Add Component`
   - Busca: `ProjectileCollisionService`
   - Añádelo

4. **Configura el servicio**:
   - Arrastra el asset `ProjectileCollisionSettings` al campo `Settings`
   - Asegúrate de que `Persist Across Scenes` esté marcado (✓)

5. **Organiza la jerarquía** (opcional pero recomendado):
   ```
   Start Scene
   ├── [Managers]  (carpeta vacía para organización)
   │   ├── GameManager
   │   ├── QuestManager
   │   ├── DialogueManager
   │   ├── AudioService
   │   ├── PlayerService
   │   └── ProjectileCollisionService  ← NUEVO
   ```

6. **Guarda la escena**: `Ctrl + S`

---

## 🎮 Estructura Final en START

```
📦 Start.unity
 └── [Managers]
     ├── GameManager (DontDestroyOnLoad)
     ├── QuestManager (DontDestroyOnLoad)
     ├── DialogueManager (DontDestroyOnLoad)
     ├── AudioService (DontDestroyOnLoad)
     ├── PlayerService (DontDestroyOnLoad)
     └── ProjectileCollisionService (DontDestroyOnLoad) ✨ NUEVO
         └── Settings: ProjectileCollisionSettings
```

---

## ✅ Verificación del Setup

### Checklist de Configuración

- [ ] Asset `ProjectileCollisionSettings` creado en Resources
- [ ] VFX prefab asignado en el settings
- [ ] GameObject `ProjectileCollisionService` creado en START
- [ ] Componente `ProjectileCollisionService` añadido
- [ ] Settings asignado al componente
- [ ] `Persist Across Scenes` marcado
- [ ] Escena START guardada

### Test en Editor

1. **Inicia el juego desde START**
2. **Verifica la consola**:
   ```
   [ProjectileCollisionService] ✅ Sistema de colisión de proyectiles inicializado con settings
   [ProjectileCollisionService] VFX configurado: [NombreDeTuVFX]
   ```

3. **Cambia de escena** (carga cualquier escena de juego)
4. **Verifica que el servicio persiste**: No debe haber mensajes de "inicializado" de nuevo

5. **Prueba la colisión**:
   - Lanza un hechizo como jugador
   - Haz que un NPC lance un hechizo
   - Los proyectiles deberían colisionar con VFX

---

## 🔧 Configuración de Layers (REQUERIDO)

### Paso 1: Crear Layers

1. `Edit > Project Settings > Tags and Layers`
2. En la sección `Layers`, añade:
   - Layer 8 (o el siguiente disponible): **`Projectile`**
   - Layer 9 (o el siguiente disponible): **`ProjectileEnemy`**

### Paso 2: Asignar Layers a Prefabs

**Proyectiles del Jugador**:
1. Selecciona tus prefabs de proyectiles del jugador
2. En el Inspector, cambia el `Layer` a **`Projectile`**

**Proyectiles del Enemigo**:
1. Selecciona tus prefabs de proyectiles enemigos
2. En el Inspector, cambia el `Layer` a **`ProjectileEnemy`**

### Paso 3: Configurar Collision Matrix

1. `Edit > Project Settings > Physics`
2. Baja hasta `Layer Collision Matrix`
3. Asegúrate de que:
   - ✅ `Projectile` y `ProjectileEnemy` están marcados (pueden colisionar)
   - ❌ `Projectile` y `Projectile` NO marcado (proyectiles del jugador no colisionan entre sí)
   - ❌ `ProjectileEnemy` y `ProjectileEnemy` NO marcado (proyectiles enemigos no colisionan entre sí)

---

## 🎨 Configurar el VFX de Colisión

### Opción 1: VFX Existente
Si ya tienes VFX en tu proyecto, busca algo tipo:
- Explosion
- Magic Impact
- Energy Clash
- Spell Collision

### Opción 2: Crear VFX Rápido

1. `GameObject > Effects > Particle System`
2. Nómbralo: `ProjectileClashVFX`
3. Configuración básica:
   ```
   Main:
   - Duration: 0.5
   - Start Lifetime: 0.3
   - Start Speed: 8
   - Start Size: 0.3
   - Start Color: Gradient (Blanco → Naranja)
   
   Emission:
   - Bursts: 1 burst de 50 partículas en t=0
   
   Shape:
   - Sphere, Radio: 0.5
   ```

4. Convierte a Prefab
5. Asígnalo en el settings

---

## 🔊 Configurar Audio

En tu sistema de audio (AudioService o similar):
1. Añade un SFX con key: **`"ProjectileClash"`**
2. Asigna un audio de:
   - Explosión mágica
   - Impacto de energía
   - Choque metálico

---

## 📊 Acceso al Servicio en Código (Opcional)

Si necesitas acceder al servicio en código:

```csharp
// Obtener el servicio
var service = ProjectileCollisionService.Instance;

// Verificar si existe
if (service != null)
{
    var settings = service.GetSettings();
    // Usar settings...
}

// Actualizar configuración en runtime (avanzado)
service.UpdateSettings(nuevoSettings);
```

O a través del ServiceLocator:

```csharp
var service = ServiceLocator.Get<ProjectileCollisionService>();
if (service != null)
{
    // Usar el servicio...
}
```

---

## 🐛 Troubleshooting

### El servicio no persiste entre escenas
- ✅ Verifica que `Persist Across Scenes` esté marcado
- ✅ Verifica que el GameObject esté en la escena START
- ✅ Asegúrate de iniciar desde START, no desde otra escena

### No se muestra el VFX
- ✅ Verifica que el prefab esté asignado en el settings
- ✅ Verifica que el prefab tenga componentes visibles (Particle System, etc.)
- ✅ Revisa la consola para errores

### Las colisiones no se detectan
- ✅ Verifica que las layers estén creadas
- ✅ Verifica que los proyectiles tengan las layers correctas
- ✅ Verifica el Collision Matrix
- ✅ Revisa que los colliders sean triggers

### "Cannot resolve symbol ProjectileCollisionHandler"
- ✅ Espera a que Unity compile todos los scripts
- ✅ Cierra y vuelve a abrir Unity si es necesario

---

## ✅ Resumen

1. ✅ Crear `ProjectileCollisionSettings` asset
2. ✅ Configurar VFX y parámetros
3. ✅ Añadir `ProjectileCollisionService` a START
4. ✅ Asignar settings al servicio
5. ✅ Crear layers "Projectile" y "ProjectileEnemy"
6. ✅ Asignar layers a prefabs de proyectiles
7. ✅ Configurar Collision Matrix
8. ✅ Añadir audio "ProjectileClash"
9. ✅ Guardar escena START
10. ✅ Probar desde START

---

**¡El sistema está listo y persistirá entre todas las escenas del juego!** 🚀

