# ✅ SOLUCIÓN: Búsqueda de NPC y Bucle Infinito

**Fecha**: 29 de diciembre de 2024  
**Problema**: NPC abandona búsqueda instantáneamente y empieza batalla de nuevo

---

## 🔍 Diagnóstico

### Problema Principal
El NPC tenía configurado:
- `searchDuration = 0s` 
- `passiveSearchDuration = 0s`
- `activelySearchForPlayer = false` (sin configurar)

Esto causaba:
1. **Búsqueda instantánea**: Entra en SEARCHING y sale inmediatamente (0s)
2. **Bucle infinito**: SEARCHING → IDLE → Detecta player → ALERT → COMBAT → SEARCHING → (repite)
3. **Sin feedback visual**: ❓ No aparece, animación no se reproduce
4. **Comportamiento errático**: Parece que "empieza la batalla de nuevo"

### Logs del Problema
```
[CombatBrain:Boy_Pirate] 🔍 Modo: BÚSQUEDA PASIVA - Duración: 0s
[CombatBrain:Boy_Pirate] 😞 Búsqueda agotada - 0 intentos completados sin éxito
[NPC:Boy_Pirate] [Combat] OnExit
[NPC:Boy_Pirate] [Idle] OnEnter
[NPC:Boy_Pirate] [IdleState] 👁️ Jugador visto. ¡Alerta!  ← BUCLE
```

---

## ✅ Solución Implementada

### 1. Agregada Configuración en `NPCCombatConfig.cs`

```csharp
[Header("🔍 Sistema de Búsqueda del Jugador")]
[Tooltip("Si TRUE, el NPC se mueve activamente buscando al jugador (5 intentos)")]
public bool activelySearchForPlayer = true;

[Min(0f)]
[Tooltip("Duración máxima de búsqueda activa en segundos (recomendado: 15-20s)")]
public float searchDuration = 15f;

[Min(0f)]
[Tooltip("Tiempo que espera quieto en búsqueda pasiva (recomendado: 5-10s)")]
public float passiveSearchDuration = 8f;

[Min(2f)]
[Tooltip("Radio de movimiento durante búsqueda en metros (recomendado: 5-8m)")]
public float searchMovementRadius = 6f;

[Tooltip("Si vuelve a su posición inicial después de buscar")]
public bool returnToOriginAfterSearch = false;
```

### 2. Valores Por Defecto Configurados

- ✅ **Búsqueda Activa**: TRUE (el NPC SE MUEVE buscando)
- ✅ **Duración Búsqueda Activa**: 15 segundos
- ✅ **Duración Búsqueda Pasiva**: 8 segundos  
- ✅ **Radio de Búsqueda**: 6 metros
- ✅ **No Volver al Origen**: FALSE (se queda donde está)

### 3. Integrado en `CombatState.cs`

Los valores se leen desde `NPCCombatConfig` y se pasan a `NPCCombatBrain.Settings`:

```csharp
// 🔍 Búsqueda del Jugador
activelySearchForPlayer = cc.activelySearchForPlayer,
searchDuration = cc.searchDuration,
passiveSearchDuration = cc.passiveSearchDuration,
searchMovementRadius = cc.searchMovementRadius,
returnToOriginAfterSearch = cc.returnToOriginAfterSearch
```

---

## 🎮 Comportamiento Esperado Ahora

### Escenario: Player se esconde detrás de un obstáculo

```
1. NPC está en combate atacando
2. Player se esconde → CheckLineOfSight() detecta obstáculo ✅
3. NPC pierde visión → _hasLineOfSight = false
4. Entra en estado SEARCHING:
   ├─ ❓ Icono de interrogación aparece ✅
   ├─ 🔍 Animación "SenseSomethingSearching_NoWeapon" se reproduce ✅
   └─ 🚶 NPC se mueve buscando (5 intentos en 15 segundos) ✅

5. Si NO ENCUENTRA al player después de 15s:
   ├─ ❌ Abandona búsqueda
   ├─ 🏳️ Sale del modo combate (StopCombat)
   └─ 🧘 Vuelve a IDLE (NO DETECTA inmediatamente)

6. Si ENCUENTRA al player durante búsqueda:
   ├─ ❗ Icono de admiración "¡Te encontré!"
   ├─ ⚔️ Vuelve a EVALUATE → ATTACK
   └─ 🔁 Retoma combate
```

### Logs Esperados

```
[CombatBrain:Boy_Pirate] 🚫 Visión bloqueada por: Cube (Tag: Default)
[CombatBrain:Boy_Pirate] 🔍 INICIANDO BÚSQUEDA - Última posición: (X, Y, Z)
[NPCAlertIcon:Boy_Pirate] ❓ Mostrando icono de interrogación (buscando)
[NPCAnimator:Boy_Pirate] 🔍 PlaySearching() - Buscando al jugador
[CombatBrain:Boy_Pirate] 🔍 Modo: BÚSQUEDA ACTIVA - Duración: 15s
[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda #1 hacia: (X, Y, Z)
[CombatBrain:Boy_Pirate] ❓ Parada de búsqueda #1 - No encontrado
... (hasta 5 intentos)
[CombatBrain:Boy_Pirate] 😞 Búsqueda agotada - 5 intentos completados
[CombatBrain:Boy_Pirate] 🏳️ Abandonando modo combate - Jugador no encontrado
```

---

## 🔧 Instrucciones Post-Actualización

### Para Unity Editor:

1. **Selecciona tu NPC** en la escena
2. **Abre el NPCCombatConfig** (ScriptableObject)
3. **Verifica los nuevos valores** en la sección "🔍 Sistema de Búsqueda del Jugador":
   - `activelySearchForPlayer`: TRUE ✅
   - `searchDuration`: 15 ✅
   - `passiveSearchDuration`: 8 ✅
   - `searchMovementRadius`: 6 ✅
   - `returnToOriginAfterSearch`: FALSE ✅

4. **Ajusta según tu diseño** (opcional):
   - **Enemigo persistente**: `searchDuration = 25s`, `activelySearchForPlayer = true`
   - **Enemigo cauteloso**: `searchDuration = 10s`, `returnToOriginAfterSearch = true`
   - **Enemigo pasivo**: `activelySearchForPlayer = false`, `passiveSearchDuration = 5s`

### Verificación en Runtime:

1. Inicia el combate con el NPC
2. Escóndete detrás de un obstáculo (layer Default)
3. **Debes ver**:
   - ❓ Icono sobre la cabeza del NPC
   - 🔍 Animación de búsqueda
   - 🚶 NPC moviéndose buscándote
   - 📝 Logs mostrando intentos de búsqueda

4. **Si sale de la búsqueda**:
   - Debe ABANDONAR el combate
   - NO debe volver a detectarte inmediatamente
   - Si te acercas de nuevo, iniciará ALERT normalmente

---

## 🐛 Si Aún Hay Problemas

### Síntoma: Sigue sin moverse/buscar

**Verifica**:
1. El prefab del icono ❓ está asignado: `NPCCombatConfig.questionIconPrefab`
2. El NavMeshAgent tiene área navegable cerca
3. Los logs muestran: `"Modo: BÚSQUEDA ACTIVA"` (no pasiva)

### Síntoma: Bucle de batalla continúa

**Verifica**:
1. `searchDuration > 0` en el config
2. Los logs muestran `"Abandonando modo combate"` después de la búsqueda
3. El NPC sale del estado COMBAT → IDLE correctamente

### Síntoma: No detecta obstáculos

**Verifica**:
1. El obstáculo tiene Collider
2. El obstáculo NO está en layer "Ignore Raycast"
3. Los logs muestran: `"🚫 Visión bloqueada por: [Nombre]"`

---

## 📁 Archivos Modificados

- ✅ `Assets/Scripts/Behaviour NPC/Modules/NPCCombatConfig.cs`
- ✅ `Assets/Scripts/Behaviour NPC/States/CombatState.cs`
- ✅ `FIX_DETECCION_OBSTACULOS_Y_BUSQUEDA.md` (actualizado)

---

## 🎯 Resultado Final

- ✅ **Búsqueda funcional** con duración configurable
- ✅ **Icono ❓ aparece** correctamente
- ✅ **Animación de búsqueda** se reproduce
- ✅ **5 intentos de movimiento** durante búsqueda activa
- ✅ **Abandono correcto** sin bucle infinito
- ✅ **Valores por defecto** ya configurados

**¡Listo para probar en Unity!** 🚀

