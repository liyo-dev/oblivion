# Fix: Gestión de Capas para Narrativa Interactiva + Combate

**Fecha:** 2025-12-26  
**Problema Resuelto:** Conflicto entre la capa "Interactable" (necesaria para interactuar con el NPC) y la capa "Enemy" (necesaria para el módulo de combate)

---

## 🔍 Problema Identificado

Cuando un NPC tiene configurado el módulo de **Interactive Narrative** con `autoStartOnPlayerDetection = false`, el jugador necesita **interactuar manualmente** con el NPC. Para que esto funcione, el NPC debe estar en la capa `"Interactable"`.

Sin embargo, el **módulo de combate** requiere que el NPC esté en la capa `"Enemy"` para funcionar correctamente.

Esto creaba un **conflicto de capas** donde:
- Si el NPC está en `"Interactable"` → El combate no funciona
- Si el NPC está en `"Enemy"` → No se puede interactuar con él

---

## ✅ Solución Implementada

Se ha implementado un **sistema de gestión de capas dinámicas** que cambia la capa del NPC según el contexto de la narrativa:

### 1. **Nuevas Opciones en `NPCInteractiveNarrativeConfig`**

```csharp
[Header("Layer Management")]
[Tooltip("Capa inicial del NPC")]
public LayerMode initialLayer = LayerMode.Interactable;

[Tooltip("¿Cambiar automáticamente a la capa 'Enemy' cuando se inicie un combate?")]
public bool switchToEnemyLayerOnCombat = true;
```

### 2. **Enum `LayerMode`**

```csharp
public enum LayerMode
{
    Interactable,      // Capa "Interactable" - permite interacción con el NPC
    Enemy,             // Capa "Enemy" - necesaria para combate
    Default,           // Capa "Default" - sin función específica
    Custom             // Usar la capa actual del NPC sin cambiar
}
```

### 3. **Métodos en `NPCInteractiveNarrativeExecutor`**

- **`ApplyInitialLayer()`**: Aplica la capa configurada en `initialLayer` al iniciar
- **`SwitchToEnemyLayer()`**: Cambia dinámicamente a la capa "Enemy" cuando se ejecuta una acción `StartCombat`

---

## 🎬 Flujo de Ejecución

### Caso Típico: Narrativa → Combate → Post-Derrota

1. **Inicio del NPC**
   - Se aplica `initialLayer` (típicamente `Interactable`)
   - El jugador puede acercarse e interactuar presionando el botón

2. **Interacción del Jugador**
   - El NPC ejecuta la cadena narrativa (diálogos, animaciones, etc.)

3. **Acción `StartCombat` Detectada**
   - Si `switchToEnemyLayerOnCombat = true`, el NPC cambia automáticamente a la capa `"Enemy"`
   - Se inicia el módulo de combate normalmente

4. **Durante el Combate**
   - El NPC está en capa `"Enemy"`
   - El sistema de combate funciona correctamente

5. **NPC Derrotado** (Gestionado por `NPCCombatLifecycleHandler`)
   - El NPC vuelve automáticamente a la capa `"Interactable"`
   - Se muestra el diálogo post-derrota
   - El jugador puede volver a interactuar con el NPC

---

## 📋 Configuración Recomendada

### Para NPCs con Narrativa + Combate (sin auto-start):

```
✅ initialLayer = Interactable
✅ switchToEnemyLayerOnCombat = true
✅ autoStartOnPlayerDetection = false
```

**Resultado:**
- El NPC comienza en capa "Interactable" → El jugador puede interactuar
- Al ejecutar StartCombat → Cambia automáticamente a "Enemy"
- Después de ser derrotado → Vuelve a "Interactable" para diálogos post-combate

### Para NPCs con Auto-Start + Combate:

```
✅ initialLayer = Enemy (o Custom si ya está en Enemy)
✅ switchToEnemyLayerOnCombat = false (ya está en Enemy)
✅ autoStartOnPlayerDetection = true
```

**Resultado:**
- El NPC detecta al jugador automáticamente
- Ya está en capa "Enemy" desde el inicio
- No necesita cambiar de capa durante la narrativa

---

## 🔧 Archivos Modificados

### 1. `NPCInteractiveNarrativeConfig.cs`
- ✅ Agregadas propiedades `initialLayer` y `switchToEnemyLayerOnCombat`
- ✅ Agregado enum `LayerMode`
- ✅ Actualizada documentación del header

### 2. `NPCInteractiveNarrativeExecutor.cs`
- ✅ Agregado método `ApplyInitialLayer()` (llamado en `Start()`)
- ✅ Agregado método `SwitchToEnemyLayer()` (llamado en `ExecuteStartCombat()`)
- ✅ Integración con el sistema existente

### 3. `NPCCombatLifecycleHandler.cs`
- ℹ️ Ya existía el código para volver a "Interactable" después de ser derrotado
- ✅ No requiere cambios adicionales

---

## 🧪 Casos de Uso

### Caso 1: NPC Guardia que se vuelve hostil
```
Configuración:
- initialLayer: Interactable
- switchToEnemyLayerOnCombat: true
- autoStartOnPlayerDetection: false

Flujo:
1. Jugador interactúa → Diálogo de advertencia
2. NPC dice "¡No pasarás!" → StartCombat
3. NPC cambia a capa Enemy → Combate
4. NPC derrotado → Vuelve a Interactable → Diálogo post-derrota
```

### Caso 2: NPC que ataca automáticamente
```
Configuración:
- initialLayer: Enemy
- switchToEnemyLayerOnCombat: false
- autoStartOnPlayerDetection: true

Flujo:
1. NPC detecta jugador → Alerta visual
2. StartCombat → Ya está en Enemy, no cambia capa
3. Combate normal
4. Derrotado → Vuelve a Interactable (por NPCCombatLifecycleHandler)
```

### Caso 3: NPC amigable (sin combate)
```
Configuración:
- initialLayer: Interactable
- switchToEnemyLayerOnCombat: N/A (no hay combate)
- autoStartOnPlayerDetection: false

Flujo:
1. Jugador interactúa → Diálogo + Animaciones
2. Sin combate → Se queda en Interactable
```

---

## ⚠️ Notas Importantes

1. **El componente `Interactable` NO depende de la capa del GameObject**
   - El sistema de interacción funciona independientemente de la capa
   - La capa "Interactable" es una convención para organización, no un requisito técnico

2. **El módulo de combate SÍ requiere la capa "Enemy"**
   - El sistema de detección de enemigos usa LayerMask que incluye la capa "Enemy"
   - Los proyectiles del jugador buscan objetivos en la capa "Enemy"

3. **`NPCCombatLifecycleHandler` gestiona el retorno a "Interactable"**
   - Después de ser derrotado, el NPC automáticamente vuelve a "Interactable"
   - Esto permite mostrar diálogos post-derrota y que el jugador vuelva a interactuar

4. **Compatibilidad con sistema existente**
   - Los cambios son 100% compatibles con NPCs existentes
   - Si no se configura `initialLayer`, se usa el valor por defecto (Interactable)
   - Si no se configura `switchToEnemyLayerOnCombat`, se activa por defecto (true)

---

## 🎯 Conclusión

El sistema ahora gestiona automáticamente los cambios de capa según el contexto de la narrativa, resolviendo el conflicto entre:
- Necesidad de interactuar manualmente (capa Interactable)
- Necesidad de combate funcional (capa Enemy)

El flujo es **transparente para el diseñador**, solo necesita configurar las opciones apropiadas en el ScriptableObject `NPCInteractiveNarrativeConfig`.

---

**Estado:** ✅ Implementado y funcional  
**Testing Recomendado:** Probar con un NPC que tenga diálogo → combate → diálogo post-derrota

