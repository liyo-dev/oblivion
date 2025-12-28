# Nueva Funcionalidad: Sistema Post-Muerte para NPCs

## 🎭 Descripción

Se ha implementado un sistema configurable que permite elegir qué sucede con un NPC después de ser derrotado en combate. Ahora hay **dos opciones**:

1. **Desaparecer** 👻: El NPC desaparece con un efecto VFX
2. **Levantarse Mareado** 😵: El NPC se levanta aturdido, reproduce la animación `Dizzy_NoWeapon` y muestra un diálogo

---

## 🔧 Cambios Implementados

### Archivos Modificados

1. **`NPCCombatConfig.cs`**
   - Nuevo enum `PostDeathBehavior`
   - Nuevos campos de configuración post-muerte

2. **`NPCSimpleAnimator.cs`**
   - Nueva animación `dizzyState = "Dizzy_NoWeapon"`
   - Nuevo método `PlayDizzy()`

3. **`NPCCombatLifecycleHandler.cs`**
   - Refactorización de `DeathSequence()`
   - Nuevos métodos: `DisappearSequence()`, `GetUpDizzySequence()`, `DefaultDefeatSequence()`

---

## 📋 Configuración en el Inspector

### NPCCombatConfig (ScriptableObject)

#### Nueva Sección: "🎭 Comportamiento Post-Muerte"

```
┌─────────────────────────────────────────────────┐
│ 🎭 Comportamiento Post-Muerte                   │
├─────────────────────────────────────────────────┤
│ Post Death Behavior:                            │
│   • Desaparecer ← El NPC desaparece con VFX     │
│   • Levantarse Mareado ← Se levanta aturdido    │
│                                                  │
│ Dialogue On Dizzy:                              │
│   [DialogueAsset] ← Solo si Marearse            │
│                                                  │
│ Disappear VFX Prefab:                           │
│   [GameObject] ← Solo si Desaparecer            │
│                                                  │
│ Disappear Duration: 2.0                         │
│   Duración del efecto de desaparición           │
└─────────────────────────────────────────────────┘
```

### NPCSimpleAnimator (Componente)

#### Nueva Animación en "Combat Animations"

```
Dizzy State: "Dizzy_NoWeapon"
  ↓
Animación de mareo después de levantarse
```

---

## 🎬 Flujos de Ejecución

### Opción 1: Desaparecer con VFX

```
NPC derrotado
    ↓
Animación de muerte (Die02_NoWeapon) 3s
    ↓
Notificar victoria (música + animación jugador) 3s
    ↓
┌────────────────────────────────────┐
│  SECUENCIA DE DESAPARICIÓN         │
├────────────────────────────────────┤
│ 1. Diálogo de derrota (opcional)   │
│    dialogueOnDefeat                │
│    ↓                               │
│ 2. VFX de desaparición             │
│    disappearVFXPrefab              │
│    ↓                               │
│ 3. Esperar disappearDuration       │
│    ↓                               │
│ 4. GameObject.SetActive(false)     │
└────────────────────────────────────┘
Resultado: NPC desaparecido ✨
```

### Opción 2: Levantarse Mareado

```
NPC derrotado
    ↓
Animación de muerte (Die02_NoWeapon) 3s
    ↓
Notificar victoria (música + animación jugador) 3s
    ↓
┌────────────────────────────────────┐
│  SECUENCIA DE MAREO                │
├────────────────────────────────────┤
│ 1. Esperar 1 segundo               │
│    (NPC sigue en el suelo)         │
│    ↓                               │
│ 2. PlayDizzy()                     │
│    Animación: Dizzy_NoWeapon       │
│    ↓                               │
│ 3. Diálogo de mareo                │
│    dialogueOnDizzy                 │
│    ↓                               │
│ 4. Cambiar a layer Interactable   │
│    ↓                               │
│ 5. Activar componente Interactable│
└────────────────────────────────────┘
Resultado: NPC mareado interactivo 😵
```

---

## 🎯 Casos de Uso

### Caso 1: NPC Enemigo que Desaparece

**Configuración:**
- `postDeathBehavior = Desaparecer`
- `disappearVFXPrefab = ParticulasMagicas_VFX`
- `disappearDuration = 2.0`
- `dialogueOnDefeat = "Has ganado... por ahora"`

**Resultado:**
1. NPC muere con efectos dramáticos
2. Dice su frase final
3. Desaparece con efecto de partículas mágicas
4. El GameObject se desactiva

**Uso:** Boss final, enemigos mágicos, criaturas invocadas

### Caso 2: NPC Entrenador que se Marea

**Configuración:**
- `postDeathBehavior = Levantarse Mareado`
- `dialogueOnDizzy = "Uff... eres muy fuerte. Necesito descansar."`
- `dialogueAfterDefeat = "Vuelve cuando seas más fuerte"`

**Resultado:**
1. NPC es derrotado
2. Se levanta mareado con animación Dizzy
3. Dice su frase de mareo
4. Queda disponible para interactuar
5. Si vuelves a hablar con él, usa `dialogueAfterDefeat`

**Uso:** NPCs entrenadores, rivales amistosos, personajes que reaparecen

---

## 💻 Detalles Técnicos

### Enum PostDeathBehavior

```csharp
public enum PostDeathBehavior
{
    Disappear,      // El NPC desaparece con VFX
    GetUpDizzy      // El NPC se levanta mareado
}
```

### Método PlayDizzy()

```csharp
public void PlayDizzy()
{
    // Cambiar a estado normal (no muerto)
    _currentState = AnimationState.Idle;
    
    // Reactivar sincronización con NavMeshAgent
    syncWithNavAgent = true;
    
    // Reproducir animación de mareo
    animator.Play(dizzyState, 0);
    animator.speed = 1f;
}
```

**Características:**
- ✅ Cambia el estado de `Dead` a `Idle`
- ✅ Reactiva la sincronización con NavMeshAgent
- ✅ Garantiza `animator.speed = 1f` (no pausada)
- ✅ Reproduce `Dizzy_NoWeapon` en layer 0

### Secuencias de Muerte

```csharp
// DeathSequence() - Punto de decisión
switch (_combatConfig.postDeathBehavior)
{
    case PostDeathBehavior.Disappear:
        yield return StartCoroutine(DisappearSequence());
        break;
        
    case PostDeathBehavior.GetUpDizzy:
        yield return StartCoroutine(GetUpDizzySequence());
        break;
}
```

---

## 🎨 Configuración de Animaciones

### Requerimientos en el Animator Controller

**Animación Obligatoria:**
- `Dizzy_NoWeapon`: Animación de mareo/aturdimiento

**Ejemplo de Setup:**
1. Importar animación `Dizzy_NoWeapon` al proyecto
2. Agregar al Animator Controller del NPC
3. Configurar en `NPCSimpleAnimator.dizzyState = "Dizzy_NoWeapon"`

**Nota:** La animación debe ser loop o tener una duración suficiente para el diálogo.

---

## 🧪 Testing

### Test 1: Desaparecer con VFX

**Setup:**
1. Crear NPCCombatConfig
2. `postDeathBehavior = Desaparecer`
3. Asignar `disappearVFXPrefab`
4. Opcionalmente asignar `dialogueOnDefeat`

**Pasos:**
1. Iniciar combate con NPC
2. Derrotar al NPC
3. **Verificar:** Animación de muerte se reproduce 3s
4. **Verificar:** Diálogo de derrota (si existe)
5. **Verificar:** VFX de desaparición se reproduce
6. **Verificar:** NPC desaparece (GameObject inactive)

### Test 2: Levantarse Mareado

**Setup:**
1. Crear NPCCombatConfig
2. `postDeathBehavior = GetUpDizzy`
3. Asignar `dialogueOnDizzy`
4. Configurar `dizzyState` en NPCSimpleAnimator

**Pasos:**
1. Iniciar combate con NPC
2. Derrotar al NPC
3. **Verificar:** Animación de muerte 3s
4. **Verificar:** Espera 1s adicional
5. **Verificar:** Animación `Dizzy_NoWeapon` empieza
6. **Verificar:** Diálogo de mareo se muestra
7. **Verificar:** NPC queda en layer Interactable
8. Interactuar con el NPC
9. **Verificar:** Se puede hablar con él (usa `dialogueAfterDefeat`)

---

## 📊 Comparación de Opciones

| Aspecto | Desaparecer 👻 | Levantarse Mareado 😵 |
|---------|---------------|----------------------|
| **GameObject** | Se desactiva | Permanece activo |
| **Layer final** | N/A | Interactable |
| **Diálogo post** | dialogueOnDefeat | dialogueOnDizzy |
| **Interacción post** | No | Sí (dialogueAfterDefeat) |
| **VFX** | disappearVFXPrefab | No |
| **Animación final** | Die02_NoWeapon | Dizzy_NoWeapon |
| **Uso típico** | Enemigos, boss | Entrenadores, rivales |

---

## 🎯 Mejores Prácticas

### Para NPCs Enemigos (Desaparecer)
```
✅ Usar disappearVFXPrefab llamativo
✅ dialogueOnDefeat corto y dramático
✅ disappearDuration = 2-3 segundos
✅ NO asignar dialogueAfterDefeat (no se usará)
```

### Para NPCs Entrenadores (Marearse)
```
✅ dialogueOnDizzy: Reconocer derrota
✅ dialogueAfterDefeat: Diálogos repetibles
✅ dizzyState configurado correctamente
✅ Animación Dizzy con duración adecuada
```

### Errores Comunes

❌ **No asignar dialogueOnDizzy** cuando `postDeathBehavior = GetUpDizzy`
  → El NPC se levanta pero no dice nada

❌ **No configurar dizzyState** en NPCSimpleAnimator
  → Warning en logs, animación no se reproduce

❌ **Usar Desaparecer para NPCs recurrentes**
  → El GameObject se desactiva y no se puede volver a interactuar

---

## 🔄 Migración de NPCs Existentes

### NPCs Antiguos (Compatibilidad)

Los NPCs existentes **seguirán funcionando** con el comportamiento anterior:
- Si `postDeathBehavior` no está configurado → Usa `DefaultDefeatSequence()`
- Comportamiento legacy: diálogo de derrota + cambiar a Interactable

### Actualizar NPCs Existentes

**Opción A: Mantener comportamiento anterior**
- No cambiar nada
- O configurar `postDeathBehavior = GetUpDizzy` sin `dialogueOnDizzy`

**Opción B: Usar nueva funcionalidad**
1. Abrir NPCCombatConfig
2. Elegir `postDeathBehavior`
3. Configurar campos correspondientes
4. Testear

---

## 📝 Ejemplo de Configuración Completa

### Ejemplo 1: Mago Oscuro (Desaparece)

```
NPCCombatConfig:
├─ postDeathBehavior: Desaparecer
├─ dialogueOnDefeat: "La oscuridad... me reclama..."
├─ disappearVFXPrefab: DarkMagic_Dissipate_VFX
└─ disappearDuration: 2.5

NPCSimpleAnimator:
└─ dizzyState: "Dizzy_NoWeapon" (no usado)
```

### Ejemplo 2: Rival Pokémon (Se Marea)

```
NPCCombatConfig:
├─ postDeathBehavior: GetUpDizzy
├─ dialogueOnDizzy: "Uff... eres increíble. Necesito entrenar más."
├─ dialogueAfterDefeat: "¡La próxima vez te ganaré!"
└─ disappearVFXPrefab: null (no usado)

NPCSimpleAnimator:
└─ dizzyState: "Dizzy_NoWeapon" ✅
```

---

## ✅ Checklist de Implementación

### Para Diseñadores

- [ ] Decidir comportamiento post-muerte del NPC
- [ ] Crear/configurar NPCCombatConfig
- [ ] Asignar `postDeathBehavior`
- [ ] Si Desaparecer:
  - [ ] Asignar `disappearVFXPrefab`
  - [ ] Configurar `disappearDuration`
  - [ ] Opcional: `dialogueOnDefeat`
- [ ] Si Marearse:
  - [ ] Asignar `dialogueOnDizzy` ⚠️ REQUERIDO
  - [ ] Asignar `dialogueAfterDefeat` (recomendado)
  - [ ] Verificar `dizzyState` en NPCSimpleAnimator
- [ ] Testear en Unity

### Para Animadores

- [ ] Verificar que `Dizzy_NoWeapon` existe en Animator Controller
- [ ] Configurar duración apropiada (2-5 segundos)
- [ ] Opcionalmente: Loop de animación
- [ ] Testear transición desde `Die02_NoWeapon`

---

**Fecha de Implementación:** 27 de diciembre de 2025  
**Estado:** ✅ COMPLETADO  
**Compatibilidad:** ✅ Retrocompatible con NPCs existentes  
**Testing:** Requerido en Unity

