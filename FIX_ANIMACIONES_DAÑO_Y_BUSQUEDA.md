# ✅ IMPLEMENTACIÓN: Sistema de Alternancia de Animaciones de Daño + Búsqueda al Huir

**Fecha**: 28/12/2024  
**Estado**: ✅ COMPLETADO

---

## 🎯 Funcionalidades Implementadas

### 1. ✅ Alternancia Aleatoria de Animaciones de Daño

**Problema**: Las animaciones de daño eran siempre las mismas, resultando repetitivo visualmente.

**Solución**: Sistema que alterna aleatoriamente entre múltiples animaciones de daño.

#### Archivos Modificados:

**NPCSimpleAnimator.cs**:
- Cambiado `string getHitState` → `string[] getHitStates`
- Array por defecto: `{ "TakeDamage", "TakeDamage_2" }`
- Método `PlayGetHit()` ahora selecciona aleatoriamente del array

**PlayerHealthSystem.cs**:
- Cambiado `string damageAnimationName` → `string[] damageAnimationNames`
- Array por defecto: `{ "TakeDamage", "TakeDamage_2" }`
- Nuevo método `GetRandomDamageAnimation()` para selección aleatoria
- Método `TakeDamage()` actualizado para usar selección aleatoria

#### Uso:

```csharp
// En Inspector - NPCSimpleAnimator
Get Hit States:
  - Element 0: "TakeDamage"
  - Element 1: "TakeDamage_2"
  - Element 2: "TakeDamage_3" (si existe)

// En Inspector - PlayerHealthSystem
Damage Animation Names:
  - Element 0: "TakeDamage"
  - Element 1: "TakeDamage_2"
```

**Comportamiento**:
- Cada vez que el NPC o Player recibe daño, se selecciona aleatoriamente una de las animaciones
- Si solo hay 1 animación en el array, siempre usa esa
- Si el array está vacío, muestra warning y no reproduce animación

---

### 2. ✅ Animación de Búsqueda al Huir

**Problema**: Cuando el NPC huye del jugador y se detiene, no había feedback visual si perdía de vista al jugador.

**Solución**: El NPC reproduce animación `SenseSomethingSearching_NoWeapon` cuando se detiene después de huir y el jugador no está en su campo de visión.

#### Archivos Modificados:

**NPCSimpleAnimator.cs**:
- Nuevo campo: `searchingState = "SenseSomethingSearching_NoWeapon"`
- Nuevo método: `PlaySearching()` - Reproduce animación de búsqueda

**NPCCombatBrain.cs**:
- Método `State_Reposition()` actualizado:
  - Al detenerse después de huir, verifica si el jugador está en campo de visión
  - Si NO está visible, reproduce animación de búsqueda
  - Espera 1.5s (duración de la animación) antes de volver a evaluar
- Nuevo método: `IsPlayerInFieldOfView()` - Detecta si el jugador está en el FOV

#### Flujo de Ejecución:

```
Jugador se acerca demasiado (dist < minSafeDistance)
    ↓
NPC entra en Estado REPOSITION (huida)
    ↓
NPC calcula dirección opuesta al jugador
    ↓
NPC corre alejándose (runSpeed)
    ↓
NPC llega a destino o timeout de 2s
    ↓
NPC se detiene (StopMove)
    ↓
┌─────────────────────────────────┐
│ ¿Jugador en campo de visión?   │
└─────────┬───────────────────────┘
          │
    ┌─────┴─────┐
    │           │
   SÍ          NO
    │           │
    │           ├─► PlaySearching() 🔍
    │           └─► Espera 1.5s
    │
    └─► Continúa sin animación
          │
          ▼
    Vuelve a Estado EVALUATE
```

#### Configuración del Campo de Visión:

El método `IsPlayerInFieldOfView()` usa el parámetro `fieldOfView` del `NPCCombatConfig`:

```yaml
# En Inspector - NPCCombatConfig
Field Of View: 160°  # Ángulo de visión

Ejemplos:
  - 90° = Visión frontal estrecha
  - 160° = Visión frontal amplia (recomendado)
  - 180° = Visión hemiesférica
  - 360° = Visión completa (ojos en la nuca)
```

**Cálculo**:
```csharp
// El jugador está en FOV si:
Vector3.Angle(npcForward, directionToPlayer) <= (fieldOfView / 2)

// Ejemplo con FOV = 160°:
// Jugador visible si está dentro de ±80° desde donde mira el NPC
```

---

## 📊 Comparación Antes/Después

### Animaciones de Daño

| Aspecto | Antes ❌ | Ahora ✅ |
|---------|---------|----------|
| **Variedad** | Siempre "TakeDamage" | Alterna entre múltiples |
| **Configuración** | String simple | Array de strings |
| **Selección** | Fija | Aleatoria |
| **Extensibilidad** | Difícil (cambiar código) | Fácil (agregar al array) |

### Comportamiento al Huir

| Aspecto | Antes ❌ | Ahora ✅ |
|---------|---------|----------|
| **Feedback Visual** | Ninguno | Animación de búsqueda |
| **Detección de Visión** | No existe | Usa FOV configurado |
| **Inmersión** | Baja | Alta |
| **Estado del NPC** | Ambiguo | Claro (está buscando) |

---

## 🎮 Testing

### Test 1: Alternancia de Animaciones de Daño

**NPC**:
1. Configurar 2-3 animaciones en `getHitStates` array
2. Atacar al NPC repetidamente
3. **Verificar**: Animaciones alternan aleatoriamente
4. **Ver logs**: `[NPCAnimator:Boy_Pirate] 💥 PlayGetHit() - Animación seleccionada: 'TakeDamage_2' (2 variantes disponibles)`

**Player**:
1. Configurar 2-3 animaciones en `damageAnimationNames` array
2. Recibir daño del NPC repetidamente
3. **Verificar**: Animaciones alternan aleatoriamente
4. **Ver logs**: `[PlayerHealthSystem] 💥 Reproduciendo animación de daño: 'TakeDamage' (2 variantes disponibles)`

### Test 2: Búsqueda al Huir

**Setup**:
- NPC con `minAttackDistance` bajo (ej: 2m)
- Player con combate activo

**Pasos**:
1. Acercarse al NPC hasta que huya (dist < minSafeDistance)
2. **Verificar**: NPC corre alejándose
3. Esperar a que el NPC se detenga
4. **Si player NO está en FOV**: 
   - NPC reproduce animación `SenseSomethingSearching_NoWeapon`
   - Ver log: `[CombatBrain:Boy_Pirate] 🔍 NPC se detuvo después de huir - Jugador fuera de vista, reproduciendo animación de búsqueda`
5. **Si player SÍ está en FOV**:
   - NPC NO reproduce búsqueda
   - Vuelve directamente a evaluar

**Variaciones**:
- Probar con diferentes `fieldOfView` (90°, 160°, 180°)
- Posicionarse detrás del NPC (fuera de FOV)
- Posicionarse al frente (dentro de FOV)

---

## ⚙️ Configuración en Unity

### Animaciones de Daño - NPC

```yaml
# NPCSimpleAnimator (Inspector)
Combat Animations:
  Get Hit States:
    Size: 2
    Element 0: "TakeDamage"
    Element 1: "TakeDamage_2"
    # Agregar más si existen:
    # Element 2: "TakeDamage_3"
```

### Animaciones de Daño - Player

```yaml
# PlayerHealthSystem (Inspector)
Animaciones:
  Damage Animation Names:
    Size: 2
    Element 0: "TakeDamage"
    Element 1: "TakeDamage_2"
```

### Búsqueda al Huir

```yaml
# NPCSimpleAnimator (Inspector)
Combat Animations:
  Searching State: "SenseSomethingSearching_NoWeapon"

# NPCCombatConfig (ScriptableObject)
Combat Stats:
  Field Of View: 160  # Ajustar según preferencia
```

**Asegurarse que la animación existe en Animator**:
- Abrir Animator Controller del NPC
- Verificar que existe estado "SenseSomethingSearching_NoWeapon"
- Si no existe, crear o usar un estado existente similar

---

## 🐛 Troubleshooting

### ❌ "No hay animaciones de daño configuradas"

**Causa**: Array de animaciones vacío

**Solución**:
- Abrir Inspector del NPC/Player
- Verificar que `Get Hit States` / `Damage Animation Names` tiene al menos 1 elemento
- Agregar "TakeDamage" como mínimo

---

### ❌ Animación de búsqueda no se reproduce

**Causa 1**: El jugador está en el campo de visión

**Solución**: Posicionarse detrás del NPC (fuera de los 160°)

**Causa 2**: El NPC no huyó suficientemente lejos

**Solución**: Ajustar `minSafeDistance` en Settings del CombatBrain

**Causa 3**: Estado de animación no existe

**Solución**:
- Verificar Animator Controller
- Confirmar que existe estado "SenseSomethingSearching_NoWeapon"
- O cambiar `searchingState` a un estado que exista

---

### ❌ NPC siempre reproduce búsqueda (incluso viendo al player)

**Causa**: `fieldOfView` muy bajo

**Solución**: Aumentar `fieldOfView` en NPCCombatConfig (recomendado: 160°)

---

## 📝 Logs de Debug

### Animaciones de Daño

```
# NPC recibe daño:
[NPCAnimator:Boy_Pirate] 💥 PlayGetHit() - Animación seleccionada: 'TakeDamage_2' (2 variantes disponibles)

# Player recibe daño:
[PlayerHealthSystem] 💥 Reproduciendo animación de daño: 'TakeDamage' (2 variantes disponibles)
```

### Búsqueda al Huir

```
# NPC huye y pierde de vista al jugador:
[CombatBrain:Boy_Pirate] 🔍 NPC se detuvo después de huir - Jugador fuera de vista, reproduciendo animación de búsqueda
[NPCAnimator:Boy_Pirate] 🔍 PlaySearching() - Buscando al jugador
```

---

## ✅ Checklist de Implementación

- [x] NPCSimpleAnimator - Array de animaciones de daño
- [x] NPCSimpleAnimator - Método `PlayGetHit()` con selección aleatoria
- [x] NPCSimpleAnimator - Campo `searchingState` agregado
- [x] NPCSimpleAnimator - Método `PlaySearching()` implementado
- [x] PlayerHealthSystem - Array de animaciones de daño
- [x] PlayerHealthSystem - Método `GetRandomDamageAnimation()` implementado
- [x] PlayerHealthSystem - `TakeDamage()` actualizado
- [x] NPCCombatBrain - `State_Reposition()` actualizado
- [x] NPCCombatBrain - Método `IsPlayerInFieldOfView()` implementado
- [x] Sin errores de compilación
- [x] Documentación creada

---

## 🎯 Estado Final

**Compilación**: ✅ Sin errores  
**Testing**: 🟡 Pendiente en Unity  
**Documentación**: ✅ Completa

---

## 📚 Archivos Modificados

1. `NPCSimpleAnimator.cs` - Alternancia de daño + animación de búsqueda
2. `PlayerHealthSystem.cs` - Alternancia de daño en player
3. `NPCCombatBrain.cs` - Lógica de búsqueda al huir

---

**Autor**: GitHub Copilot  
**Versión**: 1.0  
**Estado**: ✅ LISTO PARA TESTING

