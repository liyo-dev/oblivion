# 🔍 DIAGNÓSTICO: SpawnManager - Comportamiento Diferente Según Escena

**Fecha:** 11 Febrero 2026  
**Estado:** EN INVESTIGACIÓN

---

## 🐛 Problema Actual

### Síntomas REPORTADOS (verificar si aún ocurren)
- Cargar desde **Start** con JSON: ✅ Spawnea correctamente
- Cargar desde **Start** con Preset: ❓ **VERIFICAR**
- Cargar desde **MainWorld** con JSON: ✅ Spawnea correctamente  
- Cargar desde **MainWorld** con Preset: ❓ **VERIFICAR**

### ⚠️ IMPORTANTE: Logs Obsoletos

Los logs reportados (`[WorldBootstrap] 🧪 Inicializando sin GameBootService`) **NO EXISTEN** en el código actual.

**El código actual:**
- WorldBootstrap espera indefinidamente (hasta 1800 frames = 30s)
- NO hay fallback que spawnee en posición incorrecta
- Si GameBootService no está disponible en 30s, FALLA con error claro

**Esto significa:**
1. Los logs compartidos son de una versión anterior ❌
2. El problema puede haberse resuelto solo con los fixes recientes ✅
3. O el problema es diferente al reportado 🔍

---

## 🧪 TESTING NECESARIO AHORA

### Test Urgente 1: Verificar Problema Actual

```
1. Unity → Abrir Start.unity
2. GameBootService → Activar preset de testeo
3. Play
4. Ir a MainWorld
5. ¿El jugador está en la posición correcta? ✅/❌
```

### Test Urgente 2: Iniciar desde MainWorld

```
1. Unity → Abrir MainWorld.unity
2. GameBootService (en Start.unity) → Verificar preset activo
3. Play (AutoBootstrapOnPlay debería cargar Start)
4. ¿El jugador está en la posición correcta? ✅/❌
```

### Logs a Buscar (Código Actual)

```
[AutoBootstrapOnPlay] ⚡ Cargando Start.unity... (si inicia desde MainWorld)
[GameBootService] 🎮 GameBootProfile 'xxx' cacheado
[GameBootService] 📋 MODO TESTING - Usando bootPreset
[WorldBootstrap] ✅ GameBootService disponible después de X frame(s)
[SpawnManager] ✅ Anchor establecido desde profile: 'XXX'
[WorldBootstrap] 📍 Modo NORMAL - Anchor desde profile: 'XXX'
```

**SI NO VES ESTOS LOGS:** El problema es diferente al reportado.

---

## 🔎 Análisis - Arquitectura ACTUAL

### Flujo Esperado (Código Actual v2026-02-11)

```
[Cualquier Escena]
    ↓
AutoBootstrapOnPlay detecta que NO es Start
    ↓
Carga Start.unity aditivamente ANTES de PlayMode
    ↓
PlayMode inicia
    ↓
GameBootService.Awake() (Script Execution Order: -1000)
    ├─ Instance = this
    ├─ IsAvailable = true
    ├─ PrepareActivePreset() → ApplyPresetAsLoadedGame()
    └─ StartCoroutine(NotifyProfileReadyDelayed()) → espera 1 frame
    ↓
WorldBootstrap.OnEnable() (Order: 200)
    ├─ Suscribe a OnProfileReady
    └─ if (!IsAvailable) → WaitForGameBootServiceOrFallback()
        └─ while (!IsAvailable) yield return null; // Espera indefinida
    ↓
GameBootService dispara OnProfileReady (frame siguiente)
    ↓
SpawnManager.HandleProfileReady() (suscrito)
    └─ Establece anchor desde profile ✅
    ↓
WorldBootstrap.HandleProfileReady() (suscrito)
    └─ InitializeWorld() → Aplica NPC positions, teleporta player ✅
```

### ✅ Protecciones Actuales

1. **WorldBootstrap espera indefinidamente** (hasta 1800 frames)
2. **GameBootService tiene -1000 en execution order**
3. **OnProfileReady se dispara 1 frame después** de Awake
4. **NO hay fallback que use anchor hardcodeado**

### ❓ Si Hay Problema con Preset

**Posibles causas:**
1. AutoBootstrapOnPlay no está funcionando
2. Script Execution Order incorrecto
3. Preset tiene anchor vacío o incorrecto
4. Timing issue entre SpawnManager y WorldBootstrap

---

## 🧩 Hipótesis

### Hipótesis 1: GameBootService No Se Inicializa con Preset

**Verificar:**
```csharp
// GameBootService.cs → Awake()
if (ShouldBootFromPreset())
{
    // ¿Hay algún return early que evite la inicialización?
    // ¿Instance se asigna correctamente?
}
```

**Archivo a revisar:**
- `Assets/Scripts/Core/GameBootService.cs` líneas 80-100

---

### Hipótesis 2: WorldBootstrap Ejecuta ANTES que GameBootService

**Script Execution Order actual:**
```
GameBootService: -1000 (debería ejecutar PRIMERO)
WorldBootstrap: Default (0)
```

**Verificar en Unity:**
1. Edit → Project Settings → Script Execution Order
2. Confirmar que GameBootService tiene -1000

---

### Hipótesis 3: AutoBootstrapOnPlay No Funciona con Preset Activo

**Verificar:**
```csharp
// Assets/Editor/AutoBootstrapOnPlay.cs
// ¿Detecta correctamente que debe cargar Start?
```

**Test manual:**
1. Abrir MainWorld
2. Activar preset de testeo en Inspector de GameBootService
3. Play
4. Ver consola: ¿Aparece log de AutoBootstrapOnPlay?

---

## 🔬 Diagnóstico Paso a Paso

### PASO 1: Verificar que GameBootService se inicializa

**Agregar log temporal:**
```csharp
// GameBootService.cs → Awake()
void Awake()
{
    Debug.Log($"[GameBootService] 🚀 Awake iniciando - Instance null: {Instance == null}");
    
    if (Instance != null && Instance != this)
    {
        Debug.LogWarning("[GameBootService] ⚠️ Ya existe instancia - Destruyendo duplicado");
        Destroy(gameObject);
        return;
    }
    
    Instance = this;
    Debug.Log($"[GameBootService] ✅ Instance asignada - IsAvailable: {IsAvailable}");
    
    // ...resto del código
}
```

---

### PASO 2: Verificar orden de ejecución

**Agregar log temporal:**
```csharp
// WorldBootstrap.cs → Start()
IEnumerator Start()
{
    Debug.Log($"[WorldBootstrap] 🎬 Start() iniciando - Frame: {Time.frameCount}");
    Debug.Log($"[WorldBootstrap] GameBootService.IsAvailable: {GameBootService.IsAvailable}");
    
    // ...resto del código
}
```

---

### PASO 3: Verificar AutoBootstrapOnPlay

**Agregar log temporal:**
```csharp
// AutoBootstrapOnPlay.cs
[InitializeOnEnterPlayMode]
static void OnEnterPlayModeInEditor(EnterPlayModeOptions options)
{
    Scene activeScene = EditorSceneManager.GetActiveScene();
    Debug.Log($"[AutoBootstrapOnPlay] Escena activa: {activeScene.name}");
    
    if (activeScene.name != "Start")
    {
        Debug.Log($"[AutoBootstrapOnPlay] ⚡ Cargando Start.unity aditivamente...");
        EditorSceneManager.OpenScene("Assets/Scenes/Systems/Start.unity", 
                                     OpenSceneMode.Additive);
    }
}
```

---

## 🧪 Plan de Testing

### Test 1: Logs de Inicialización
```
1. Agregar logs temporales (arriba)
2. Abrir MainWorld
3. Activar preset en GameBootService
4. Play
5. Copiar TODOS los logs
6. Buscar:
   - [AutoBootstrapOnPlay]
   - [GameBootService] 🚀 Awake
   - [WorldBootstrap] 🎬 Start()
   - [WorldBootstrap] GameBootService.IsAvailable
```

### Test 2: Comparar Preset vs JSON
```
1. Desactivar preset (cargar JSON)
2. Play desde MainWorld
3. Copiar logs de inicialización
4. Comparar con logs del Test 1
5. Identificar diferencias
```

---

## 📋 Checklist de Verificación

### En Unity Editor:
- [ ] GameBootService existe en Start.unity
- [ ] GameBootService está en Script Execution Order (-1000)
- [ ] Start.unity está en Build Settings (index 0)
- [ ] AutoBootstrapOnPlay.cs existe en Assets/Editor/

### En GameBootService:
- [ ] Instance se asigna en Awake()
- [ ] IsAvailable devuelve true después de Awake()
- [ ] No hay return early que evite inicialización con preset

### En WorldBootstrap:
- [ ] Espera indefinidamente (sin timeout de 10 frames)
- [ ] Solo usa fallback si GameBootService NO existe después de 1800 frames

---

## 🎯 Siguiente Acción

**INMEDIATA:**
1. Agregar logs temporales en los 3 archivos mencionados
2. Testear desde MainWorld con preset activo
3. Copiar TODOS los logs de inicialización
4. Compartir logs para análisis

**OBJETIVO:**
Identificar exactamente por qué WorldBootstrap no detecta a GameBootService cuando se usa preset.

---

## 📝 Notas Importantes

### Recordatorio de Arquitectura

**NO debe haber diferencia entre JSON y Preset:**
```csharp
// El código es el MISMO:
activePreset = useTestPreset ? testingPreset : LoadFromJSON();
ApplyPreset(activePreset); // ← Mismo flujo para ambos
```

**Si hay diferencia en comportamiento:**
- NO es un problema del preset vs JSON
- ES un problema de TIMING de inicialización
- Probablemente relacionado con orden de ejecución de scripts

---

**Diagnóstico pendiente - Agregar logs y compartir resultados** 🔍
