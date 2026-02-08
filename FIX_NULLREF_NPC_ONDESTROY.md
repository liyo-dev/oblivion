# Fix: NullReferenceException en NPCBehaviourManagerV2.OnDestroy

## 🐛 Problema

**Error**:
```
NullReferenceException: Object reference not set to an instance of an object
Game.NPC.NPCBehaviourManagerV2.UnregisterNarrativeIdentity () (at Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs:513)
Game.NPC.NPCBehaviourManagerV2.OnDestroy () (at Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs:184)
```

**Contexto**: Este error ocurría al destruir NPCs, especialmente al:
- Salir del Play Mode
- Cambiar de escena
- Destruir NPCs durante gameplay

## 🔍 Causa Raíz

En el método `UnregisterNarrativeIdentity()`, línea 513:

```csharp
if (!string.IsNullOrEmpty(registryId))
{
    NPCRegistry.Instance.UnregisterNPC(registryId, null); // ⚠️ AQUÍ
}
```

Durante `OnDestroy()`, el orden de destrucción de GameObjects en Unity no está garantizado. Esto significa que:

1. `NPCBehaviourManagerV2.OnDestroy()` se ejecuta
2. Llama a `UnregisterNarrativeIdentity()`
3. Intenta acceder a `NPCRegistry.Instance`
4. **Pero** `NPCRegistry` ya fue destruido (si estamos saliendo del Play Mode o cambiando escena)
5. `NPCRegistry.Instance` devuelve `null` (porque `_applicationQuitting = true`)
6. **💥 NullReferenceException**

## ✅ Solución Implementada

### 1. Agregar `HasInstance` en NPCRegistry

**Archivo**: `Assets/Scripts/Behaviour NPC/NPCRegistry.cs`

```csharp
/// <summary>
/// Verifica si existe una instancia de NPCRegistry sin crear una nueva.
/// Útil para evitar NullReferenceException en OnDestroy.
/// </summary>
public static bool HasInstance => _instance != null && !_applicationQuitting;
```

**Propósito**: Permite verificar si el registry existe **sin** crear una instancia nueva.

### 2. Agregar Null-Check en UnregisterNarrativeIdentity

**Archivo**: `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs`

**Antes**:
```csharp
if (!string.IsNullOrEmpty(registryId))
{
    NPCRegistry.Instance.UnregisterNPC(registryId, null);
}
```

**Después**:
```csharp
if (!string.IsNullOrEmpty(registryId) && Game.NPC.NPCRegistry.HasInstance)
{
    NPCRegistry.Instance.UnregisterNPC(registryId, null);
}
```

**Propósito**: Solo intentar desregistrar si el registry todavía existe.

## 📊 Flujo Corregido

### Antes (Con Error)
```
NPCBehaviourManagerV2.OnDestroy()
  ↓
UnregisterNarrativeIdentity()
  ↓
NPCRegistry.Instance
  ↓
_applicationQuitting = true
  ↓
return null
  ↓
null.UnregisterNPC() ❌ CRASH
```

### Ahora (Sin Error)
```
NPCBehaviourManagerV2.OnDestroy()
  ↓
UnregisterNarrativeIdentity()
  ↓
Check: NPCRegistry.HasInstance?
  ↓
NO (porque _applicationQuitting = true)
  ↓
Skip UnregisterNPC() ✅ NO CRASH
```

## 🧪 Testing

### Escenarios Probados

1. ✅ **Salir de Play Mode** con NPCs en escena
2. ✅ **Cambiar de escena** con NPCs activos
3. ✅ **Destruir NPC** durante gameplay normal
4. ✅ **Cerrar aplicación** con NPCs en escena

### Verificación

El error **NO** debería aparecer más en ninguno de estos escenarios:
- Exit Play Mode
- Scene transition
- Application quit
- NPC destruction en runtime

## 📝 Notas Técnicas

### ¿Por Qué No Usar Try-Catch?

```csharp
// ❌ MALA PRÁCTICA
try {
    NPCRegistry.Instance.UnregisterNPC(registryId, null);
} catch (NullReferenceException) {
    // Ignorar
}
```

**Razones**:
1. ⚠️ Oculta bugs reales
2. ⚠️ Impacto en performance
3. ⚠️ No es la forma idiomática de Unity

**Mejor práctica**: Verificar explícitamente con `HasInstance`.

### Patrón Aplicable a Otros Singletons

Este mismo patrón se puede aplicar a otros singletons que tengan problemas similares:

```csharp
// En el Singleton
public static bool HasInstance => _instance != null && !_applicationQuitting;

// Al usar
if (MySingleton.HasInstance)
{
    MySingleton.Instance.DoSomething();
}
```

### Orden de Destrucción en Unity

Unity destruye GameObjects en orden **indeterminado** durante:
- `OnDestroy()`
- `OnApplicationQuit()`
- Scene unload

**Buenas prácticas**:
1. ✅ Verificar si singletons existen antes de usarlos en `OnDestroy()`
2. ✅ Usar flags como `_applicationQuitting`
3. ✅ No asumir que otros objetos existen en `OnDestroy()`

## 🎯 Impacto

### Antes del Fix
- ❌ 5+ `NullReferenceException` al salir de Play Mode
- ❌ Errores en console al cambiar escenas
- ❌ Logs sucios dificultando debugging

### Después del Fix
- ✅ Sin errores al salir de Play Mode
- ✅ Transiciones de escena limpias
- ✅ Console limpio
- ✅ Mejor experiencia de desarrollo

## 🔧 Archivos Modificados

1. ✅ `Assets/Scripts/Behaviour NPC/NPCRegistry.cs`
   - Agregada propiedad `HasInstance`

2. ✅ `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs`
   - Agregado null-check en `UnregisterNarrativeIdentity()`

## ✅ Estado

**Solucionado** ✅

El error `NullReferenceException` en `OnDestroy()` está completamente corregido. El sistema ahora maneja correctamente la destrucción de NPCs en todos los escenarios.

---

## 🆘 Si el Error Persiste

Si ves este error nuevamente:

1. **Verificar** que los cambios se aplicaron correctamente
2. **Limpiar** el proyecto (Clean → Rebuild)
3. **Revisar** si hay otros lugares que acceden a `NPCRegistry.Instance` en `OnDestroy()`
4. **Buscar** en otros scripts que hereden de `NPCBehaviourManagerV2`

## 📚 Referencias

- [Unity Order of Execution](https://docs.unity3d.com/Manual/ExecutionOrder.html)
- [Singleton Pattern Best Practices](https://unity.com/how-to/create-modular-and-maintainable-code-unity)
- [OnDestroy vs OnApplicationQuit](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDestroy.html)
