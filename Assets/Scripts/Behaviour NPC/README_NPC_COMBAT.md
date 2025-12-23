# Sistema de Combate NPC - Guía de Configuración

## ⚠️ IMPORTANTE: Configuración de Layer

**El GameObject del NPC DEBE estar en la Layer `Enemy`** para que funcione correctamente:

- ✅ **Targetable**: El script `Targetable` solo funciona en objetos con layer `Enemy`
- ✅ **Auto-targeting**: Los hechizos del jugador apuntan automáticamente al NPC
- ✅ **Marcador visual**: Se muestra el marcador de enemigo sobre el NPC

**Si el NPC está en la layer `Interactable`, el targeting NO funcionará.**

---

## 📋 Scripts Requeridos en el NPC

### 1. NPCBehaviourManagerV2 (Manager Principal)
- Controla la máquina de estados del NPC
- Configura el tipo de comportamiento (Interactive Narrative, Combat, Ambient, Quest)

### 2. NPCCombatBrain (Cerebro de Combate)
- Controla la lógica de combate del NPC
- Configura ataques, distancias, y comportamiento agresivo
- **Settings**:
  - `attackSlots`: Array de 3 ataques (Left, Right, Special)
  - Cada ataque tiene: prefab del proyectil, animación, daño, cooldown
  - `attackRangeMin` / `attackRangeMax`: Distancias de combate
  - `aggroDistanceSquared`: Distancia para volverse agresivo

### 3. Targetable (Auto-targeting)
- **REQUIERE Layer `Enemy`**
- Muestra el marcador visual sobre el NPC
- Permite que los hechizos del jugador apunten automáticamente

### 4. Damageable (Sistema de Vida)
- Gestiona la vida del NPC
- Eventos de daño, muerte, etc.
- Conecta con el sistema de salud visual

---

## 🎯 Configuración de Combate

### NPCInteractiveNarrativeConfig (ScriptableObject)

#### Combat Config:
```csharp
- alertMusicEvent: "Npc_Battle_Alert"
- alertIconPrefab: Prefab del icono de alerta (!)
- dialogueOnAlert: Diálogo antes del combate
- waitForAlertDialogue: Esperar a que termine el diálogo
```

#### Conditional Narratives:
Ahora **SOLO usamos Conditional Mode**:
- ✅ Si quieres condición: Configura `condition` con el tipo deseado
- ✅ Si NO quieres condición: Pon `condition = None`

**Eliminamos el modo "Simple"** porque era redundante.

---

## 🎬 Animaciones de Combate

### Durante Alerta (AlertState):
1. **Al detectar**: `Challenging_NoWeapon` (capa 1, peso 1)
   - Se reproduce en loop durante el diálogo
   - NPCSimpleAnimator se deshabilita temporalmente

2. **Después del diálogo**: `InteractWithPeople_NoWeapon` (capa 1)
   - Animación de transición
   - NPCSimpleAnimator se re-habilita

### Durante Combate (CombatState):
- Las animaciones de ataque se configuran en `NPCCombatBrain.attackSlots`
- Cada ataque tiene su propia animación
- Se reproduce en la capa 1 del Animator

---

## 🔫 Proyectiles de Enemigo

### EnemyProjectile.cs
- **Configuración**:
  - `speed`: Velocidad del proyectil
  - `lifetime`: Tiempo antes de autodestruirse
  - `usePhysicsMovement`: Usar Rigidbody para movimiento suave
  - `hitEffectPrefab`: Efecto visual al impactar

- **Detección de daño**:
  1. Busca el tag `Player`
  2. Busca el componente `PlayerHealthSystem`
  3. Busca la interfaz `IDamageable`

- **Shield detection**: Detecta `PlayerShieldController.ShieldMarker` y se destruye sin hacer daño

---

## 🚫 REGLAS DE CÓDIGO

### ❌ NO USAR `FindObject`
- **NUNCA** usar `FindObjectOfType`, `FindFirstObjectByType`, etc.
- Si necesitas buscar algo, **consultar primero**
- Usar referencias directas o ServiceLocator

### ✅ USO CORRECTO de Servicios
```csharp
// Correcto
var dm = DialogueManager.Instance;
var audio = AudioService.Instance;

// Incorrecto
var dm = FindObjectOfType<DialogueManager>(); // ❌ NUNCA
```

---

## 🐛 Troubleshooting

### El NPC no recibe targeting:
- ✅ Verificar que está en la layer `Enemy`
- ✅ Verificar que tiene el script `Targetable`
- ✅ Verificar que el prefab del marcador está asignado

### Las animaciones no se reproducen:
- ✅ Verificar que el Animator tiene las animaciones configuradas
- ✅ Verificar que `NPCCombatBrain.attackSlots` tiene las animaciones asignadas
- ✅ Verificar que el peso de la capa 1 es 1

### Los proyectiles no hacen daño:
- ✅ Verificar que el player tiene el tag `Player`
- ✅ Verificar que el player tiene `PlayerHealthSystem`
- ✅ Verificar que el proyectil tiene `EnemyProjectile` script
- ✅ Verificar que el collider del proyectil es trigger

### El diálogo no aparece:
- ✅ Verificar que `DialogueManager.Instance` existe
- ✅ Verificar que `combatConfig.dialogueOnAlert` está asignado
- ✅ Verificar que el player no está en otro modo (menú, etc.)

---

## 📝 Notas de Desarrollo

- **Layer Enemy**: Es fundamental para el sistema de targeting
- **No hay código basura**: Todo el código está limpio y documentado
- **Sin FindObject**: Usamos referencias directas y ServiceLocator
- **Conditional Only**: Solo usamos modo condicional (Simple eliminado)

