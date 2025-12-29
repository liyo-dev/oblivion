# FEATURE: Sistema de Iconos Visuales y Mejoras de Búsqueda del NPC

## 📋 Mejoras Implementadas

### 1. ✅ Cancelación de Ataque si Pierdes Línea de Visión
El NPC ahora cancela el ataque INMEDIATAMENTE si el jugador se esconde detrás de un obstáculo durante cualquier fase del ataque.

### 2. ✅ Sistema de Iconos Visuales sobre la Cabeza (usando script existente)
- **❓ Interrogación**: Cuando el NPC pierde de vista al jugador y lo busca
- **❗ Admiración**: Cuando el NPC encuentra/ve al jugador
- **✅ VENTAJA**: Usa el script `NPCAlertIconController` **que ya existía**, solo añadidos nuevos prefabs

### 3. ✅ Comportamiento de Búsqueda Configurable
- **Búsqueda Activa**: El NPC se mueve buscando al jugador
- **Búsqueda Pasiva**: El NPC se queda quieto esperando (opción configurable)

### 4. ✅ Feedback Natural y Consistente
- Muestra admiración al inicio del combate
- Muestra interrogación cuando pierde de vista
- Muestra admiración cuando encuentra al jugador de nuevo

## 🎯 Problema 1 Resuelto: Cancelación Durante Ataque

### ANTES ❌
```
NPC empieza ataque
Player se esconde detrás del muro
NPC continúa ataque
NPC dispara (atraviesa muro o falla)
NPC termina ataque
NPC luego detecta que no hay visión
```

### AHORA ✅
```
NPC empieza ataque
Player se esconde detrás del muro
❌ CheckLineOfSight() → false
✅ Ataque CANCELADO inmediatamente
✅ Transición a SEARCHING
✅ Icono de interrogación aparece
```

### Implementación Técnica

**Verificaciones añadidas en State_Attack():**

```csharp
IEnumerator State_Attack()
{
    // ✅ 1. Verificar al INICIO
    if (!_hasLineOfSight)
    {
        Debug.Log("❌ Ataque cancelado - Sin línea de visión");
        _currentState = CombatState.SEARCHING;
        yield break;
    }
    
    // Windup
    yield return new WaitForSeconds(Random.Range(0.2f, 0.5f));
    
    // ✅ 2. Verificar DURANTE el windup
    if (!_hasLineOfSight)
    {
        Debug.Log("❌ Ataque cancelado durante windup");
        _currentState = CombatState.SEARCHING;
        yield break;
    }
    
    // Ejecutar animación
    _rawAnimator.Play(chosenAttack.animationState);
    
    if (!settings.spawnProjectileViaAnimEvent)
    {
        yield return new WaitForSeconds(settings.fireDelaySeconds);
        
        // ✅ 3. Verificar ANTES de disparar
        if (!_hasLineOfSight)
        {
            Debug.Log("❌ Disparo cancelado - Jugador se escondió");
            _currentState = CombatState.SEARCHING;
            yield break;
        }
        
        SpawnProjectile(chosenAttack.slotIndex);
    }
    
    yield return new WaitForSeconds(0.5f);
    
    // ✅ 4. Verificar DESPUÉS del ataque
    if (!_hasLineOfSight)
    {
        Debug.Log("❌ Jugador se escondió después del ataque");
        _currentState = CombatState.SEARCHING;
        yield break;
    }
    
    // Continuar combate...
}
```

## 🎨 Problema 2 Resuelto: Sistema de Iconos Visuales

### Script Existente: NPCAlertIconController.cs ✅

**¡Usamos el script que ya existía!** Solo se extendió para soportar nuevos tipos de iconos.

#### Características (ya existentes):

1. **Canvas WorldSpace** que siempre mira a la cámara
2. **Animaciones de bounce** configurables
3. **Duración configurable** del icono
4. **Posición automática** sobre el NPC
5. **Sistema de prefabs** reutilizables

#### Mejoras Añadidas:

**Nuevos Prefabs Configurables:**
```csharp
[Header("Prefabs de Iconos")]
[SerializeField] private GameObject alertIconPrefab;        // ❗ Alerta (ya existía)
[SerializeField] private GameObject questionIconPrefab;     // ❓ Interrogación (NUEVO)
[SerializeField] private GameObject exclamationIconPrefab;  // ❗ Admiración (NUEVO)
```

**Nuevos Métodos Públicos:**
```csharp
// Icono de alerta original (ya existía)
public void ShowAlert(float duration = -1f)

// ❓ Interrogación - Buscando al jugador (NUEVO)
public void ShowQuestion(float duration = -1f)

// ❗ Admiración - ¡Encontró al jugador! (NUEVO)
public void ShowExclamation(float duration = -1f)

// Ocultar icono actual (ya existía)
public void HideAlertIcon()
```

#### Configuración en Inspector:

```
NPCAlertIconController Component:
├── Prefabs de Iconos (ASIGNAR AQUÍ)
│   ├── Alert Icon Prefab: Prefab con icono ❗ (alerta)
│   ├── Question Icon Prefab: Prefab con icono ❓ (interrogación) ← NUEVO
│   └── Exclamation Icon Prefab: Prefab con icono ❗ (admiración) ← NUEVO
│
└── Configuración por Defecto
    ├── Icon Offset: (0, 2.5, 0) - Altura sobre el NPC
    ├── Icon Duration: 2.0s (cuánto dura visible)
    ├── Animate Bounce: ✓ (animación bounce)
    ├── Bounce Amplitude: 0.2
    └── Bounce Speed: 3.0
```

**✅ VENTAJA**: Solo necesitas crear/asignar los **prefabs de iconos**. Todo el código de manejo, animación y posicionamiento **ya está hecho**.

## 🎮 Problema 3 Resuelto: Búsqueda Configurable

### Nueva Configuración en NPCCombatBrain.Settings:

```csharp
[Header("Search Behavior")]
[Tooltip("Si está activado, el NPC se mueve activamente buscando. Si no, se queda quieto.")]
public bool activelySearchForPlayer = true;

[Tooltip("Si no busca activamente, cuánto tiempo espera antes de abandonar")]
public float passiveSearchDuration = 5f;
```

### Comportamiento según Configuración:

#### activelySearchForPlayer = TRUE ✅ (Búsqueda Activa)

```
Jugador se esconde
    ↓
NPC pierde visión
    ↓
❓ Icono de interrogación
    ↓
Animación de búsqueda
    ↓
NPC hace movimientos aleatorios:
  - Cerca de última posición conocida
  - Cada 2-4 segundos un nuevo punto
  - Radio configurable (searchMovementRadius)
    ↓
[A] Encuentra jugador → ❗ Admiración → Retoma combate
[B] searchDuration agotado → Abandona o vuelve al origen
```

#### activelySearchForPlayer = FALSE ❌ (Búsqueda Pasiva)

```
Jugador se esconde
    ↓
NPC pierde visión
    ↓
❓ Icono de interrogación
    ↓
Animación de búsqueda
    ↓
NPC se QUEDA QUIETO en su posición:
  - NO hace movimientos
  - Solo espera y observa
  - Duración: passiveSearchDuration
    ↓
[A] Jugador vuelve a aparecer → ❗ Admiración → Retoma
[B] passiveSearchDuration agotado → Abandona combate
```

## 🎬 Flujos Completos

### Escenario 1: Jugador Se Esconde Durante Ataque

```
1. NPC en estado ATTACK
2. Preparando disparo (windup)
3. ✅ Jugador se esconde detrás de muro
4. ✅ CheckLineOfSight() → false
5. ✅ Ataque CANCELADO
6. ✅ Transición a SEARCHING
7. ❓ Icono de interrogación aparece
8. Animación de búsqueda
9. [Búsqueda activa] NPC se mueve buscando
10. Jugador sale del escondite
11. ✅ CheckLineOfSight() → true
12. ❗ Icono de admiración aparece
13. Animación de alerta
14. Retoma combate (EVALUATE)
```

### Escenario 2: NPC Detecta al Jugador Inicialmente

```
1. Jugador entra en rango de detección
2. NPC cambia a CombatState
3. ❗ Icono de admiración aparece
4. Animación de alerta
5. SetBattleMode(true)
6. Inicia FSM de combate
```

### Escenario 3: Búsqueda Pasiva - NPC Se Rinde

```
1. Jugador se esconde
2. NPC pierde visión
3. ❓ Icono de interrogación
4. NPC se queda quieto (NO se mueve)
5. Espera passiveSearchDuration (ej: 5s)
6. No encuentra al jugador
7. Icono de interrogación desaparece
8. [returnToOriginAfterSearch = true]
   → Vuelve a posición inicial
9. Sale de modo combate
10. Vuelve a IdleState/PatrolState
```

### Escenario 4: Búsqueda Activa - Encuentra al Jugador

```
1. Jugador se esconde
2. NPC pierde visión
3. ❓ Icono de interrogación
4. NPC se mueve a punto aleatorio A
5. Reproduce animación de búsqueda
6. NPC se mueve a punto aleatorio B
7. ✅ Durante movimiento ve al jugador
8. ❗ Icono de admiración aparece
9. Animación de alerta
10. Retoma combate inmediatamente
```

## 🔧 Integración en NPCCombatBrain

### Nuevas Variables:

```csharp
NPCAlertIconController _alertIconController; // Sistema de iconos (usa prefabs)
```

### Inicialización:

```csharp
public void Initialize(NPCBehaviourManagerV2 manager)
{
    // ...código existente...
    
    // Buscar componente de iconos si existe
    _alertIconController = _manager.GetComponent<NPCAlertIconController>();
    if (_alertIconController == null)
    {
        Debug.LogWarning("⚠️ NPCAlertIconController no encontrado");
    }
}
```

### Uso en BeginCombat():

```csharp
public void BeginCombat()
{
    // ...configuración inicial...
    
    // ✅ Mostrar icono de admiración - ¡Te vi!
    if (_alertIconController != null)
    {
        _alertIconController.ShowExclamation();
    }
    
    // ...resto del código...
}
```

### Uso en State_Searching():

```csharp
IEnumerator State_Searching()
{
    // ✅ Mostrar interrogación al inicio
    if (_alertIconController != null)
    {
        _alertIconController.ShowQuestion();
    }
    
    // ...bucle de búsqueda...
    
    // Si encuentra al jugador:
    if (_hasLineOfSight)
    {
        // ✅ Mostrar admiración
        if (_alertIconController != null)
        {
            _alertIconController.ShowExclamation();
        }
        
        // Reproducir alerta
        if (_animator != null)
        {
            _animator.PlayAlert();
        }
        
        yield return new WaitForSeconds(1.0f);
        _currentState = CombatState.EVALUATE;
        yield break;
    }
    
    // Si agota tiempo:
    if (_alertIconController != null)
    {
        _alertIconController.HideAlertIcon();
    }
}
```

## 📝 Configuración Requerida en Unity

### 1. Setup del NPC GameObject:

```
NPC_Boy_Pirate
├── NPCBehaviourManagerV2 (existente)
├── NPCCombatBrain (existente)
│   └── Settings
│       ├── Obstacle Layer Mask: Default ✓
│       ├── Search Duration: 15
│       ├── Search Movement Radius: 5
│       ├── Return To Origin After Search: ✓
│       └── Search Behavior:
│           ├── Actively Search For Player: ✓ (o ✗)
│           └── Passive Search Duration: 5
│
└── NPCAlertIconController (YA EXISTÍA - solo configurar)
    ├── Prefabs de Iconos:
    │   ├── Alert Icon Prefab: [Asignar prefab ❗]
    │   ├── Question Icon Prefab: [Asignar prefab ❓] ← NUEVO
    │   └── Exclamation Icon Prefab: [Asignar prefab ❗] ← NUEVO
    │
    └── Configuración por Defecto:
        ├── Icon Offset: (0, 2.5, 0)
        ├── Icon Duration: 2.0
        ├── Animate Bounce: ✓
        ├── Bounce Amplitude: 0.2
        └── Bounce Speed: 3.0
```

### 2. Crear Prefabs de Iconos:

Los prefabs deben contener:

```
QuestionIconPrefab (ejemplo)
├── Canvas (WorldSpace)
│   ├── Render Mode: World Space
│   ├── Width: 100
│   ├── Height: 100
│   └── Scale: (0.01, 0.01, 0.01)
│
└── IconImage (Image)
    ├── Sprite: Icono ❓
    ├── Preserve Aspect: ✓
    ├── Color: White (1, 1, 1, 1)
    └── RectTransform: Centrado
```

**✅ TIP**: Duplica el prefab de alerta existente y cámbialo el sprite para crear los nuevos iconos rápidamente.

### 3. Sprites Recomendados:

- **Alert Icon** (❗): Ya existe
- **Question Icon** (❓): Crear o importar sprite de interrogación
- **Exclamation Icon** (❗): Puede ser el mismo que Alert o una variante

## 🎯 Logs de Debug Esperados

### Cuando el Jugador Se Esconde:

```
[CombatBrain:Boy_Pirate] ❌ Disparo cancelado - Jugador se escondió durante animación
[CombatBrain:Boy_Pirate] 🔍 INICIANDO BÚSQUEDA
[NPCAlertIcon:Boy_Pirate] ❓ Mostrando icono de interrogación (buscando)
[CombatBrain:Boy_Pirate] 🔍 Modo: BÚSQUEDA ACTIVA - Duración: 15s
[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda hacia: (X, Y, Z)
```

### Cuando Encuentra al Jugador:

```
[CombatBrain:Boy_Pirate] ✅ ¡Jugador encontrado! - Mostrando icono de admiración
[NPCAlertIcon:Boy_Pirate] ❗ Mostrando icono de admiración (¡encontrado!)
[NPCAnimator:Boy_Pirate] 🚨 PlayAlert() - Alerta activada
```

### Al Inicio del Combate:

```
[NPCAlertIcon:Boy_Pirate] ❗ Mostrando icono de admiración (¡encontrado!)
[CombatBrain:Boy_Pirate] FSM iniciado
```

## 🎮 Mejoras de Gameplay

1. **Feedback Visual Claro**: El jugador sabe instantáneamente cuándo el NPC lo está buscando (❓) o lo encontró (❗)

2. **Comportamiento Configurable**: Cada NPC puede tener personalidad diferente:
   - Agresivos → Búsqueda activa
   - Pasivos → Búsqueda pasiva (se rinden)

3. **Cancelación Inmediata**: El esconderse es útil tácticamente, el NPC reacciona inmediatamente

4. **Consistencia**: Los mismos iconos y animaciones en todas las situaciones (inicio, pérdida, encuentro)

5. **Naturalidad**: El comportamiento es predecible y lógico para el jugador

## 🔑 Ventajas del Sistema

| Aspecto | Antes ❌ | Ahora ✅ |
|---------|---------|---------|
| **Cancelación de ataque** | Termina ataque aunque no vea | Cancela inmediatamente |
| **Feedback visual** | Ninguno | Iconos ❓ y ❗ |
| **Búsqueda** | Solo un comportamiento | Activa o Pasiva (configurable) |
| **Al encontrar** | Sin feedback | Admiración + alerta |
| **Al inicio** | Sin feedback | Admiración |
| **Naturalidad** | Limitada | Alta |

---

**Fecha**: 28 de diciembre de 2024  
**Tipo**: Feature - Visual Feedback & Search Behavior  
**Estado**: ✅ COMPLETADO  
**Archivos Modificados**: 
- `NPCAlertIconController.cs` - Extendido para soportar iconos de interrogación y admiración
- `NPCCombatBrain.cs` - Integración del sistema de iconos y comportamiento de búsqueda configurable

