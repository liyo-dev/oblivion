# FIX: Interrogación durante giro en combate activo

## 🎯 Problema Identificado

El NPC mostraba el icono de interrogación (❓) cuando se giraba durante el combate activo y perdía momentáneamente la visión del jugador, incluso cuando sabía perfectamente que el jugador estaba detrás de él.

### Escenario del Bug:
1. NPC está combatiendo contra el jugador
2. NPC se gira por alguna razón (NavMesh, reposicionamiento, etc.)
3. Pierde línea de visión momentáneamente (jugador queda fuera del FOV)
4. Sistema entra en modo SEARCHING
5. ❌ **Muestra interrogación** aunque estaban combatiendo hace 1 segundo

Esto no tiene sentido narrativo: el NPC sabe dónde está el jugador porque estaban peleando.

---

## ✅ Solución Implementada

### 1. Sistema de "Memoria de Combate Reciente"

Se agregó un sistema que detecta si el NPC perdió la visión del jugador hace poco tiempo:

```csharp
// 🔥 Memoria de combate reciente para evitar interrogación innecesaria
private const float RECENT_COMBAT_THRESHOLD = 3f; // Si perdió visión hace menos de 3s, NO mostrar interrogación
private bool _wasInRecentCombat => (Time.time - _lastSeenTime) < RECENT_COMBAT_THRESHOLD;
```

**Lógica:**
- `_lastSeenTime` se actualiza cada frame cuando hay línea de visión
- Si perdió visión hace **menos de 3 segundos** → Es "combate reciente"
- Si perdió visión hace **más de 3 segundos** → Es búsqueda real (jugador se escondió)

### 2. Modificación del Estado SEARCHING

El estado de búsqueda ahora diferencia entre dos casos:

#### Caso A: Combate Reciente (giro momentáneo)
```csharp
bool showQuestionMark = !_wasInRecentCombat;

if (showQuestionMark)
{
    // Mostrar interrogación + animación de búsqueda
    _alertIconController.ShowQuestion(...);
    _animator.PlaySearching();
    yield return new WaitForSeconds(1.5f);
}
else
{
    // 🔥 NO mostrar interrogación, búsqueda silenciosa
    Debug.Log($"Combate reciente detectado - Búsqueda sin interrogación");
}
```

#### Caso B: Búsqueda Real (jugador escondido)
- Se muestra interrogación normalmente
- Se ejecuta animación de búsqueda completa
- NPC realmente no sabe dónde está el jugador

### 3. Comportamiento en las Paradas de Búsqueda

Las paradas durante búsqueda activa también respetan la memoria de combate:

```csharp
if (showQuestionMark)
{
    // Búsqueda completa con interrogación y animación
    _alertIconController.ShowQuestion(...);
    _animator.PlaySearching();
    yield return new WaitForSeconds(2.0f);
}
else
{
    // Búsqueda rápida sin animaciones largas
    yield return new WaitForSeconds(0.5f);
}
```

---

## 🎮 Comportamiento Final

### Escenario 1: Giro durante Combate (NUEVO ✅)
1. NPC está combatiendo (última visión hace 0.5s)
2. NPC se gira y pierde visión momentáneamente
3. Entra en SEARCHING
4. ❌ **NO muestra interrogación** (sabe que el jugador está ahí)
5. Busca más rápido sin animaciones pesadas
6. Al recuperar visión: ✅ Muestra admiración y vuelve a combate

### Escenario 2: Jugador se Esconde (ORIGINAL ✅)
1. NPC está combatiendo
2. Jugador se esconde detrás de obstáculo
3. Pasan 3+ segundos sin visión
4. ✅ **Muestra interrogación** (no sabe dónde está)
5. Ejecuta animación de búsqueda completa
6. Si lo encuentra: ✅ Muestra admiración

### Escenario 3: Pérdida Momentánea durante Ataque (NUEVO ✅)
1. NPC prepara un ataque especial
2. Animación de ataque lo gira ligeramente
3. Pierde visión 1 frame
4. ❌ **NO muestra interrogación** (está atacando)
5. Recupera visión inmediatamente
6. Continúa combate fluidamente

---

## 🔧 Parámetros Configurables

### `RECENT_COMBAT_THRESHOLD` = 3f
- **Valor actual:** 3 segundos
- **Ajustable** según necesidades de diseño
- **Recomendaciones:**
  - 2s: Más estricto, interrogación más frecuente
  - 3s: Balance (ACTUAL) ✅
  - 5s: Más permisivo, casi nunca interrogación en combate

---

## 📊 Archivos Modificados

### `NPCCombatBrain.cs`
**Cambios:**
1. ✅ Agregada constante `RECENT_COMBAT_THRESHOLD`
2. ✅ Agregada propiedad `_wasInRecentCombat`
3. ✅ Modificado método `State_Searching()` - Líneas ~707-740
4. ✅ Modificado bloque de paradas de búsqueda - Líneas ~817-843

**Líneas totales afectadas:** ~40 líneas
**Compatibilidad:** 100% retrocompatible

---

## ✅ Testing Recomendado

### Test 1: Giro Rápido en Combate
- Entrar en combate con NPC
- Moverse rápidamente alrededor del NPC
- **Esperado:** NO debe aparecer interrogación durante los giros rápidos

### Test 2: Esconderse Tras Obstáculo
- Combatir con NPC
- Esconderse completamente tras obstáculo
- Esperar 3+ segundos
- **Esperado:** Debe aparecer interrogación después de 3s

### Test 3: Salir y Entrar de Cobertura
- Combatir con NPC
- Esconderse 1 segundo
- Salir de cobertura
- **Esperado:** NO debe aparecer interrogación, combate continúa

### Test 4: Ataques con Giros
- Provocar que NPC use ataques especiales
- Observar si la animación lo gira
- **Esperado:** NO debe aparecer interrogación durante el ataque

---

## 🎯 Resultado

El comportamiento ahora es **mucho más natural e inteligente**:
- ✅ El NPC no "olvida" al jugador instantáneamente
- ✅ Los giros durante combate no rompen la inmersión
- ✅ Las interrogaciones solo aparecen cuando tienen sentido narrativo
- ✅ La búsqueda en combate reciente es más ágil y responsiva

---

## 📝 Notas Técnicas

- El sistema usa `_lastSeenTime` que ya existía pero no se aprovechaba
- No se agregaron nuevos campos serializados (sin cambios en Inspector)
- Compatible con todos los NPCs existentes
- Sin impacto en rendimiento (solo una comparación de tiempo)

---

**Fecha:** 2025-12-29  
**Versión:** 1.0  
**Estado:** ✅ IMPLEMENTADO Y LISTO PARA TESTING

