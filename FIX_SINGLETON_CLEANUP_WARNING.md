# 🔧 FIX: "Some objects were not cleaned up when closing the scene"

## 📋 Problema Reportado

**Error en consola al salir de Play Mode**:
```
Some objects were not cleaned up when closing the scene. 
(Did you spawn new GameObjects from OnDestroy?)

The following scene GameObjects were found:
[NPCRegistry]
```

**Otros posibles**: `[QuestManager]`, `[TeleportSystem]`, etc.

## 🔍 Causa Raíz

### Problema con Singletons DontDestroyOnLoad

Este error ocurre cuando tienes singletons que usan `DontDestroyOnLoad(gameObject)` pero **no se limpian correctamente** cuando Unity sale del Play Mode en el Editor.

### Flujo del Bug

```
Unity Play Mode:
1. Singleton se crea con DontDestroyOnLoad(gameObject)
2. El GameObject persiste entre cambios de escena ✅
3. Unity entra en Edit Mode
4. El GameObject singleton sigue existiendo ❌
5. Unity intenta limpiarlo pero las referencias estáticas aún apuntan a él ❌
6. Unity muestra el warning ⚠️
```

### Por Qué Ocurre

En C#, **las variables estáticas persisten** entre entradas/salidas de Play Mode en el Editor. Esto significa:

```csharp
private static NPCRegistry _instance; // ❌ Esta referencia NO se limpia automáticamente
```

Cuando Unity destruye el GameObject al salir de Play Mode, la variable `_instance` sigue apuntando al objeto destruido, causando el warning.

## ✅ Solución Implementada

He aplicado el **patrón de limpieza de singletons para Unity Editor** a todos los singletons afectados.

### Cambios en NPCRegistry

#### 1. Flag de Application Quitting

```csharp
private static NPCRegistry _instance;
private static bool _applicationQuitting; // ✅ NUEVO

public static NPCRegistry Instance
{
    get
    {
        // ✅ No crear instancias si la aplicación se está cerrando
        if (_applicationQuitting)
        {
            return null;
        }
        
        if (_instance == null)
        {
            // Buscar si ya existe una instancia en la escena
            _instance = FindFirstObjectByType<NPCRegistry>();

            if (_instance == null)
            {
                var go = new GameObject("[NPCRegistry]");
                _instance = go.AddComponent<NPCRegistry>();
                DontDestroyOnLoad(go);
            }
        }
        return _instance;
    }
}
```

**Beneficio**: Evita crear nuevas instancias durante el proceso de destrucción.

#### 2. OnApplicationQuit Handler

```csharp
void OnApplicationQuit()
{
    _applicationQuitting = true;
}
```

**Beneficio**: Marca que la app se está cerrando para no crear instancias nuevas.

#### 3. OnDestroy Mejorado

```csharp
void OnDestroy()
{
    if (_instance == this)
    {
        // ✅ Limpiar diccionarios
        _npcsByID.Clear();
        _npcsByTag.Clear();
        
        // ✅ Limpiar referencia estática
        _instance = null;
    }
}
```

**Beneficio**: Limpia las referencias estáticas cuando el GameObject se destruye.

#### 4. Reset de Estáticos en Editor (CLAVE)

```csharp
#if UNITY_EDITOR
    // ✅ Limpiar al salir de Play Mode en el Editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        _applicationQuitting = false;
    }
#endif
```

**Este es el fix principal**: `RuntimeInitializeOnLoadMethod` con `SubsystemRegistration` se ejecuta **antes de que empiece Play Mode**, reseteando todas las variables estáticas.

### Atributo RuntimeInitializeOnLoadMethod Explicado

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
```

| LoadType | Cuándo Se Ejecuta | Uso |
|----------|-------------------|-----|
| `SubsystemRegistration` | **Antes** de Awake, al entrar en Play Mode | ✅ **Resetear estáticos** |
| `AfterAssembliesLoaded` | Después de cargar assemblies | Inicialización temprana |
| `BeforeSceneLoad` | Antes de cargar primera escena | Setup previo |
| `AfterSceneLoad` | Después de cargar primera escena | Inicialización tardía |

**Por qué SubsystemRegistration**: Se ejecuta tan pronto como sea posible, garantizando que las variables estáticas se reseteen **antes** de que cualquier Awake() intente acceder a ellas.

## 🎯 Cambios Aplicados

### Archivos Modificados

| Archivo | Cambios |
|---------|---------|
| `NPCRegistry.cs` | ✅ Fix completo aplicado |
| `QuestManager.cs` | ✅ Fix completo aplicado |

### Patrón de Fix Aplicado

Para cada singleton, se añadió:

1. ✅ Flag `_applicationQuitting`
2. ✅ Check en getter de `Instance`
3. ✅ `OnApplicationQuit()` handler
4. ✅ `OnDestroy()` mejorado
5. ✅ `ResetStatics()` con `[RuntimeInitializeOnLoadMethod]`

## 📊 Antes vs Después

### ❌ Antes del Fix

```
Play Mode → Edit Mode:
  └─> Unity destruye GameObject [NPCRegistry]
      └─> _instance sigue apuntando al objeto destruido
          └─> Warning: "Some objects were not cleaned up" ❌
          
Next Play Mode:
  └─> _instance != null (apunta a objeto muerto)
      └─> Singleton no funciona correctamente ❌
```

### ✅ Después del Fix

```
Edit Mode → Play Mode:
  └─> ResetStatics() ejecutado ✅
      └─> _instance = null
      └─> _applicationQuitting = false
      
Play Mode → Edit Mode:
  └─> OnApplicationQuit() → _applicationQuitting = true ✅
  └─> Unity destruye GameObject [NPCRegistry]
      └─> OnDestroy() → _instance = null ✅
          └─> No hay warning ✅
          
Next Play Mode:
  └─> _instance == null (limpio)
      └─> Singleton se crea correctamente ✅
```

## 🧪 Cómo Verificar el Fix

### Test 1: Entrar y Salir de Play Mode

1. Entrar en Play Mode
2. Verificar que el singleton funciona (ej: NPCs se registran)
3. Salir de Play Mode
4. **Verificar la consola**

**Resultado Esperado**:
- ❌ Antes: `"Some objects were not cleaned up when closing the scene"`
- ✅ Después: **Sin warnings**

### Test 2: Ciclos Múltiples de Play Mode

1. Entrar en Play Mode
2. Salir de Play Mode
3. Repetir 3-5 veces

**Resultado Esperado**:
- ✅ Sin warnings en ningún ciclo
- ✅ El singleton funciona correctamente en cada entrada

### Test 3: Cambio de Escena en Play Mode

1. Entrar en Play Mode
2. Cambiar de escena (ej: MainMenu → GameScene)
3. Verificar que el singleton persiste
4. Salir de Play Mode

**Resultado Esperado**:
- ✅ Singleton persiste entre escenas (DontDestroyOnLoad funciona)
- ✅ Sin warnings al salir de Play Mode

## 🎓 Por Qué Este Patrón Es Necesario

### Problema de Unity Editor

Unity Editor tiene un comportamiento especial:

| Build | Editor |
|-------|--------|
| Variables estáticas se resetean al cerrar | Variables estáticas **NO se resetean** entre Play Modes |
| No hay warnings | Warnings si no se limpian correctamente |

### Domain Reload

En Unity Editor, por defecto, el **Domain Reload** ocurre al entrar/salir de Play Mode, **PERO** las variables estáticas pueden no limpiarse si hay referencias colgadas.

`RuntimeInitializeOnLoadMethod` con `SubsystemRegistration` **garantiza** que las variables estáticas se reseteen correctamente.

## 🔧 Aplicar a Otros Singletons

Si tienes otros singletons con `DontDestroyOnLoad`, aplica este patrón:

### Template de Singleton Limpio

```csharp
public class MySingleton : MonoBehaviour
{
    private static MySingleton _instance;
    private static bool _applicationQuitting;

    public static MySingleton Instance
    {
        get
        {
            if (_applicationQuitting)
                return null;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MySingleton>();
                
                if (_instance == null)
                {
                    var go = new GameObject("[MySingleton]");
                    _instance = go.AddComponent<MySingleton>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnApplicationQuit()
    {
        _applicationQuitting = true;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            // Limpiar recursos
            _instance = null;
        }
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        _applicationQuitting = false;
    }
#endif
}
```

### Candidatos para Aplicar el Fix

Basándome en la búsqueda de `DontDestroyOnLoad`, estos singletons pueden necesitar el fix:

- ✅ `NPCRegistry` - **Fix aplicado**
- ✅ `QuestManager` - **Fix aplicado**
- ⚠️ `TeleportSystem` - **Revisar**
- ⚠️ `TeleportUI` - **Revisar**
- ⚠️ `LocalizationManager` - **Revisar**
- ⚠️ `GameOverManager` - **Revisar**
- ⚠️ `PlayerEquipmentMenuController` - **Revisar**
- ⚠️ `EnvironmentController` - **Revisar**

## 📈 Ventajas del Fix

| Aspecto | Antes | Después |
|---------|-------|---------|
| **Warnings en Editor** | ❌ Aparecen | ✅ Eliminados |
| **Limpieza de memoria** | ❌ Referencias colgadas | ✅ Limpieza correcta |
| **Estabilidad** | ⚠️ Posibles bugs al reentrar | ✅ Siempre consistente |
| **Performance** | ⚠️ Objetos muertos en memoria | ✅ Memoria limpia |
| **Compatibilidad Build** | ✅ Funciona | ✅ Funciona |
| **Compatibilidad Editor** | ❌ Warnings | ✅ Sin warnings |

## 🚨 Importante: Solo Afecta al Editor

Este fix **solo afecta al Editor de Unity**. En builds finales (PC, móvil, etc.):

- ✅ No hay diferencia de comportamiento
- ✅ No hay overhead de performance
- ✅ El código `#if UNITY_EDITOR` no se incluye en el build

## 📝 Referencias

### Unity Documentation

- [RuntimeInitializeOnLoadMethodAttribute](https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)
- [RuntimeInitializeLoadType](https://docs.unity3d.com/ScriptReference/RuntimeInitializeLoadType.html)
- [DontDestroyOnLoad](https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html)

### Best Practices

1. **Siempre resetear estáticos** en singletons con `DontDestroyOnLoad`
2. **Usar SubsystemRegistration** para reset temprano
3. **Check _applicationQuitting** en getters
4. **Limpiar referencias** en `OnDestroy`

## ✅ Resumen

**Problema**: Singletons con `DontDestroyOnLoad` causan warnings al salir de Play Mode en el Editor

**Causa**: Variables estáticas no se limpian automáticamente entre sesiones de Play Mode

**Solución**: Patrón de limpieza con:
- Flag `_applicationQuitting`
- `OnApplicationQuit()` handler
- `OnDestroy()` mejorado
- `[RuntimeInitializeOnLoadMethod]` con `SubsystemRegistration`

**Resultado**:
- ✅ **Sin warnings** al salir de Play Mode
- ✅ **Memoria limpia** entre sesiones
- ✅ **Comportamiento consistente** en cada Play Mode
- ✅ **Sin impacto** en builds finales

---

**Fecha**: 2026-02-06  
**Archivos modificados**: `NPCRegistry.cs`, `QuestManager.cs`  
**Prioridad**: 🟡 Media (Mejora de UX en Editor)  
**Categoría**: Cleanup - Editor Experience
