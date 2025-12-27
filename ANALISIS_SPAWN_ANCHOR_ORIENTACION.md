# 🔍 Análisis: Problema de Orientación en Spawn Anchors

**Fecha:** 2025-12-26  
**Estado:** Análisis completado - Solución en progreso

---

## 🎯 Problema Identificado

**Síntoma:** En un mismo SpawnAnchor, el Player y los NPCs terminan mirando en direcciones diferentes.

**Ejemplo Reportado:**
- **Player:** Mira hacia el forward del SpawnAnchor (correcto)
- **NPC:** Mira de perfil o en otra dirección (incorrecto)

---

## 🔬 Análisis de Causa Raíz

### Sistema Actual (2 Lógicas Diferentes)

#### 1. **TeleportService** (usado por Player)

```csharp
// TeleportService.cs - Líneas 128-139
if (sa.faceDoor)
{
    // Mirar hacia la puerta (forward del anchor)
    rot = Quaternion.LookRotation(anchor.forward, Vector3.up);
}
else
{
    // Mirar en dirección opuesta a la puerta (back del anchor)
    rot = Quaternion.LookRotation(-anchor.forward, Vector3.up);
}
```

**Comportamiento:**
- ✅ Respeta el `forward` del GameObject SpawnAnchor
- ✅ Aplica orientación inmediatamente al teletransportar
- ✅ Usa la opción `faceDoor` del SpawnAnchor

#### 2. **MoveToPoscionSequence** (usado por NPCs)

```csharp
// CinematicState.cs - Líneas 227-233
private void HandleArrival(Common.NPCStateContext context)
{
    // Girar 180° si está configurado
    if (_turnAroundOnArrival)
    {
        var newRotation = context.Transform.rotation * Quaternion.Euler(0, 180, 0);
        context.Transform.rotation = newRotation;
    }
}
```

**Comportamiento:**
- ❌ NO respeta el `forward` del SpawnAnchor
- ❌ Solo gira 180° relativos a la orientación actual del NPC
- ❌ NO usa la opción `faceDoor` del SpawnAnchor
- ⚠️ La orientación final depende de la orientación en la que el NPC llegó al destino

---

## 🐛 Causa del Bug

**El problema es que `MoveToPoscionSequence` NO consulta el SpawnAnchor** para saber hacia dónde debe mirar el NPC al llegar.

### Escenario Problemático:

```
SpawnAnchor GameObject:
├─ Position: (10, 0, 10)
├─ Forward: (0, 0, 1)  ← Mirando hacia el norte
├─ faceDoor: true
└─ anchorId: "Casa_Entrada"

Player teletransportado:
└─ Orientación: (0, 0, 1) ✅ Mira al norte (forward del anchor)

NPC moviéndose a esa posición:
├─ Llega desde el sur (mirando hacia el norte)
├─ turnAroundOnArrival = true
├─ Gira 180° → Ahora mira al sur ❌
└─ Resultado: NPC mira dirección opuesta al Player
```

---

## 🎯 Solución Propuesta

### Opción 1: Unificar Lógica (RECOMENDADA)

**Modificar `MoveToPoscionSequence` para que consulte el SpawnAnchor:**

```csharp
private void HandleArrival(Common.NPCStateContext context)
{
    // Buscar si el destino es un SpawnAnchor
    var nearbyAnchors = Physics.OverlapSphere(_targetPosition, 1f);
    SpawnAnchor anchor = null;
    
    foreach (var col in nearbyAnchors)
    {
        anchor = col.GetComponentInParent<SpawnAnchor>();
        if (anchor != null) break;
    }
    
    if (anchor != null)
    {
        // Usar la misma lógica que TeleportService
        if (anchor.faceDoor)
        {
            context.Transform.rotation = Quaternion.LookRotation(anchor.transform.forward, Vector3.up);
        }
        else
        {
            context.Transform.rotation = Quaternion.LookRotation(-anchor.transform.forward, Vector3.up);
        }
        context.Log($"[CinematicSequence] Orientación establecida desde SpawnAnchor '{anchor.anchorId}'");
    }
    else if (_turnAroundOnArrival)
    {
        // Fallback: comportamiento original si no hay anchor
        var newRotation = context.Transform.rotation * Quaternion.Euler(0, 180, 0);
        context.Transform.rotation = newRotation;
        context.Log("[CinematicSequence] Girado 180° (sin anchor)");
    }
}
```

**Ventajas:**
- ✅ Un único sistema de orientación
- ✅ Player y NPCs usan la misma referencia
- ✅ Compatible con código existente
- ✅ `turnAroundOnArrival` sigue funcionando como fallback

**Desventajas:**
- ⚠️ Requiere que los SpawnAnchors tengan colliders para detectarlos

---

### Opción 2: Pasar el Anchor como Parámetro

**Modificar constructor de `MoveToPoscionSequence`:**

```csharp
public MoveToPoscionSequence(
    MonoBehaviour owner, 
    Vector3 targetPosition, 
    float maxDuration = 15f, 
    bool turnAroundOnArrival = false, 
    float walkDisplayDuration = 999f,
    SpawnAnchor targetAnchor = null  // ← NUEVO
)
```

**Ventajas:**
- ✅ Más explícito
- ✅ No requiere colliders en el anchor

**Desventajas:**
- ❌ Requiere cambios en todos los lugares que llaman a MoveToPoscionSequence
- ❌ Más invasivo

---

### Opción 3: Búsqueda por ID

**Usar `SpawnAnchor.FindById()` si el destino es conocido:**

```csharp
public MoveToPoscionSequence(
    MonoBehaviour owner, 
    Vector3 targetPosition, 
    float maxDuration = 15f, 
    bool turnAroundOnArrival = false, 
    float walkDisplayDuration = 999f,
    string anchorId = null  // ← NUEVO
)
{
    _anchorId = anchorId;
    // ...
}

private void HandleArrival(Common.NPCStateContext context)
{
    if (!string.IsNullOrEmpty(_anchorId))
    {
        var anchor = SpawnAnchor.FindById(_anchorId);
        if (anchor != null)
        {
            // Aplicar orientación del anchor
            // ...
        }
    }
}
```

---

## 🎬 Plan de Implementación

**Recomendación: Opción 1 (Búsqueda espacial)**

### Pasos:

1. ✅ **Agregar collider a SpawnAnchors**
   - Todos los SpawnAnchors deben tener un Trigger pequeño
   - Esto ya debería existir para que el player pueda ser teletransportado

2. ✅ **Modificar `HandleArrival()` en `MoveToPoscionSequence`**
   - Buscar SpawnAnchor cerca del destino
   - Aplicar orientación del anchor si existe
   - Usar `turnAroundOnArrival` como fallback

3. ✅ **Testing**
   - Verificar que Player y NPCs miran en la misma dirección
   - Verificar que `turnAroundOnArrival` sigue funcionando en casos sin anchor

4. ✅ **Documentación**
   - Actualizar docs explicando el comportamiento unificado

---

## 📝 Notas Adicionales

### ¿Qué hace cada opción del SpawnAnchor?

```csharp
// SpawnAnchor.cs
public bool faceDoor = false;
```

- **`faceDoor = true`**: El personaje mira HACIA la puerta (forward del anchor)
  - Útil para: Entrar a un edificio, salir de una casa
  
- **`faceDoor = false`**: El personaje mira ALEJÁNDOSE de la puerta (back del anchor)
  - Útil para: Aparecer dentro de una casa mirando hacia adentro

### Diagrama Visual:

```
Caso: faceDoor = true (mirar hacia la puerta)
━━━━━━━━━━━━━━━━━━━━
🚪 Puerta
  ↑ forward del anchor
  👤 ← El personaje mira hacia arriba (hacia la puerta)


Caso: faceDoor = false (mirar alejándose de la puerta)
━━━━━━━━━━━━━━━━━━━━
🚪 Puerta
  ↑ forward del anchor
  👤 ← El personaje mira hacia abajo (alejándose de la puerta)
```

---

## ✅ Próximos Pasos

1. Implementar Opción 1 en `CinematicState.cs`
2. Testing en escena con Player y NPCs
3. Documentar el comportamiento unificado

---

**Estado:** Análisis completado - Listo para implementar solución

