# Sistema de Registro para NPCInteractiveNarrativeExecutor

## 📋 Resumen
Se implementó un sistema de registro centralizado para `NPCInteractiveNarrativeExecutor`, reemplazando las costosas llamadas a `FindObjectsByType()` con un patrón de registro estático optimizado, similar al sistema existente de `AnchorRegistry` y `NPCRegistry`.

## 🎯 Problema Resuelto
- **Antes**: Se usaba `Object.FindObjectsByType<NPCInteractiveNarrativeExecutor>()` en cada operación, lo cual es costoso en rendimiento
- **Después**: Los executors se auto-registran en un diccionario estático, permitiendo acceso O(1) por ID y O(n) para listar todos

## 📦 Archivos Creados

### `NPCInteractiveNarrativeRegistry.cs`
Registro estático centralizado que mantiene referencias a todos los executors activos.

**Características:**
- ✅ Auto-registro en `OnEnable` / `OnDisable`
- ✅ Búsqueda por `persistenceId` (O(1))
- ✅ Búsqueda por nombre del GameObject
- ✅ Listado de todos los executors registrados
- ✅ Limpieza automática de referencias null

**Métodos públicos:**
```csharp
// Registrar/des-registrar (automático desde executor)
Register(NPCInteractiveNarrativeExecutor executor)
Unregister(NPCInteractiveNarrativeExecutor executor)

// Búsqueda
GetById(string persistenceId) : NPCInteractiveNarrativeExecutor
GetByName(string npcName) : NPCInteractiveNarrativeExecutor
GetAll() : List<NPCInteractiveNarrativeExecutor>

// Utilidades
Clear()
GetDebugInfo() : string
AllById : IReadOnlyDictionary<string, NPCInteractiveNarrativeExecutor>
```

### `NPCNarrativeRegistryDebugger.cs`
Herramienta de debug con interfaz visual para testing y monitoreo.

**Características:**
- 🎮 GUI in-game con botones de control
- 🔍 Búsqueda por nombre y persistenceId
- 📊 Listado visual de todos los executors registrados
- 🎨 Visualización con Debug.DrawRay en la escena
- 🔗 Selección automática de GameObjects en el editor
- 📋 Context Menu para acceso rápido

**Uso:** Arrastra este componente a cualquier GameObject en la escena para activar las herramientas de debug.

## 🔧 Archivos Modificados

### `NPCInteractiveNarrativeExecutor.cs`
**Cambios:**
1. Agregado auto-registro en `OnEnable()`:
   ```csharp
   NPCInteractiveNarrativeRegistry.Register(this);
   ```

2. Agregado auto-des-registro en `OnDisable()`:
   ```csharp
   NPCInteractiveNarrativeRegistry.Unregister(this);
   ```

3. Agregado método público para acceder a la configuración:
   ```csharp
   public NPCInteractiveNarrativeConfig GetConfiguration()
   {
       return _config;
   }
   ```

### `NPCNarrativeStateManager.cs`
**Cambios:** Reemplazadas todas las llamadas a `FindObjectsByType()` con llamadas al registro:

1. `ResetAllNPCs()`:
   ```csharp
   // ANTES:
   var allExecutors = Object.FindObjectsByType<NPCInteractiveNarrativeExecutor>(
       FindObjectsInactive.Include, FindObjectsSortMode.None);
   
   // DESPUÉS:
   var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
   ```

2. `ResetNPC(string npcName)`:
   ```csharp
   // ANTES:
   var allExecutors = Object.FindObjectsByType<...>();
   foreach (var executor in allExecutors) {
       if (executor.name == npcName) { ... }
   }
   
   // DESPUÉS:
   var executor = NPCInteractiveNarrativeRegistry.GetByName(npcName);
   ```

3. `ClearAllSavedStates()`:
   ```csharp
   // ANTES:
   var allExecutors = Object.FindObjectsByType<...>();
   // Y luego GetComponent<NPCBehaviourManagerV2>()?.Configuration...
   
   // DESPUÉS:
   var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
   var config = executor.GetConfiguration();
   ```

4. `GetDebugInfo()`:
   ```csharp
   // Similar optimización
   var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
   var config = executor.GetConfiguration();
   ```

## ⚡ Mejoras de Rendimiento

| Operación | Antes | Después |
|-----------|-------|---------|
| Buscar por ID | O(n) con FindObjectsByType | O(1) con Dictionary lookup |
| Buscar por nombre | O(n) con FindObjectsByType | O(n) pero sin overhead de Unity |
| Listar todos | O(n) con FindObjectsByType | O(n) con List simple |
| Llamadas a Unity API | Múltiples por operación | 0 por operación |

**Estimación:** Reducción de ~80-95% en tiempo de ejecución para operaciones de búsqueda.

## 🔄 Patrón de Diseño
El sistema sigue el mismo patrón usado en el proyecto para:
- `AnchorRegistry` (para SpawnAnchors)
- `NPCRegistry` (para NPCBehaviourManagerV2)

**Ventajas del patrón:**
- ✅ Consistencia con el código existente
- ✅ Auto-gestión del ciclo de vida
- ✅ Sin necesidad de singleton/MonoBehaviour
- ✅ Thread-safe (solo se accede desde hilo principal de Unity)

## 📝 Uso

El sistema es completamente automático. Los `NPCInteractiveNarrativeExecutor` se registran automáticamente cuando se activan.

**Ejemplo de uso manual (avanzado):**
```csharp
// Buscar un executor específico por su persistence ID
var executor = NPCInteractiveNarrativeRegistry.GetById("npc_merchant_01");
if (executor != null) {
    executor.ResetState();
}

// Obtener todos los executors
var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
foreach (var exec in allExecutors) {
    Debug.Log($"Executor: {exec.name}");
}

// Debug info
Debug.Log(NPCInteractiveNarrativeRegistry.GetDebugInfo());
```

## ⚠️ Notas Importantes

1. **Auto-registro:** Los executors solo están en el registro mientras están activos (habilitados). Si se deshabilita un GameObject, se des-registra automáticamente.

2. **Timing:** El registro ocurre en `OnEnable`, por lo que el executor está disponible después de `Awake()` pero antes de `Start()`.

3. **Persistencia:** El registro es volátil (en memoria), no persiste entre sesiones. Esto es correcto ya que los executors son componentes de escena.

4. **Threading:** No thread-safe. Solo debe accederse desde el hilo principal de Unity (comportamiento normal para MonoBehaviours).

## ✅ Testing
Después de implementar estos cambios, verificar:
- [ ] Los NPCs se resetean correctamente al iniciar nueva partida
- [ ] `NPCNarrativeStateManager.GetDebugInfo()` muestra la info correcta
- [ ] No hay errores de NullReference al buscar executors
- [ ] El rendimiento mejoró (menos tiempo en Profiler para operaciones de búsqueda)

## 🎓 Lecciones Aprendidas
- Evitar `FindObjectsByType()` en operaciones frecuentes
- Usar registros estáticos para componentes que necesitan ser buscados regularmente
- Mantener consistencia con patrones existentes en el proyecto
- El auto-registro en OnEnable/OnDisable es más robusto que el registro manual

---
**Fecha de implementación:** 2025-12-24  
**Versión:** 1.0  
**Compatibilidad:** Unity 2021.3+

