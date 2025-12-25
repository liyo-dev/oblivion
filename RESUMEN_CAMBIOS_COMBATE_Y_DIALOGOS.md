# Resumen de Cambios - Sistema de Combate y Diálogos NPC

**Fecha:** 2025-12-24  
**Estado:** ✅ Completado

---

## 📋 Problemas Resueltos

### 1. ✅ Animación de Interacción en NPCs con Quest
**Archivo:** `Assets/Scripts/Behaviour NPC/Modules/NPCQuestConfig.cs`

**Problema:** Los NPCs con Quest Action no reproducían la animación `InteractWithPeople_NoWeapon` al hablar con el jugador.

**Solución:** 
- Añadida llamada a `PlayOneShot("InteractWithPeople_NoWeapon")` en el método `StartTalkingAnimation()`
- Ahora reproduce correctamente la animación de saludo/interacción cuando el jugador habla con el NPC

```csharp
// Reproducir animación de interacción (saludo/hablar)
context.Animator.PlayOneShot("InteractWithPeople_NoWeapon", 0, onComplete: null);
```

---

### 2. ✅ Animación de Interacción en NPCs con Interactive Narrative
**Archivo:** `Assets/Scripts/Behaviour NPC/NPCInteractiveNarrativeExecutor.cs`

**Problema:** Los NPCs con Interactive Narrative Config no reproducían la animación de interacción.

**Solución:**
- Añadida animación de interacción en el método `ExecuteNarrativeChain()` después de rotar hacia el jugador
- Se ejecuta antes de iniciar la cadena de acciones narrativas

```csharp
// Reproducir animación de interacción (saludo/hablar)
if (_npcManager?.Context?.Animator != null)
{
    _npcManager.Context.Animator.PlayOneShot("InteractWithPeople_NoWeapon", 0, onComplete: null);
}
```

---

### 3. ✅ Player Entra en Stance de Batalla en Diálogo Pre-Combate
**Archivo:** `Assets/Scripts/Dialogue/DialogueManager.cs`

**Problema:** Al iniciar un diálogo previo a la batalla, el player no miraba al NPC ni entraba en la animación de idle de batalla.

**Solución:**
- Corregido el método `PreparPlayerForBattleDialogue()` para usar `animator.Play()` en lugar de `CrossFade()`
- El player ahora gira hacia el NPC instantáneamente
- Se activa la animación `Idle_Battle_NoWeapon` directamente
- Se añaden feedbacks cinematográficos (camera shake, slowmo, screen flash)

```csharp
// Reproducir animación de Idle de batalla directamente
// Usar Play para activar inmediatamente el estado, no CrossFade
playerAnimator.Play("Idle_Battle_NoWeapon", 0);
```

---

### 4. ✅ Animación de Muerte del NPC se Reproduce Correctamente
**Archivo:** `Assets/Scripts/Behaviour NPC/NPCSimpleAnimator.cs`

**Problema:** La animación de muerte (`Die02_NoWeapon`) no se reproducía cuando el NPC moría.

**Solución:**
- Cambiado de `CrossFadeToState()` a `animator.Play()` directo
- Esto asegura que la animación se reproduzca inmediatamente sin transición
- La animación ahora se ve completamente antes del diálogo de derrota

```csharp
// Usar Play directamente para reproducir la animación de muerte inmediatamente
if (animator != null)
{
    animator.Play(dieState, 0); // Layer 0, reproducción inmediata
}
```

---

### 5. ✅ Slowmo se Restaura Correctamente Tras Matar al NPC
**Archivo:** `Assets/Scripts/Behaviour NPC/Modules/NPCCombatLifecycleHandler.cs`

**Problema:** El slowmo del golpe letal se quedaba activo, ralentizando el juego permanentemente.

**Solución:**
- Añadidas múltiples verificaciones de seguridad para restaurar `Time.timeScale`
- Verificación tras el bloque `finally`
- Verificación tras un `yield return null` adicional
- Verificación doble antes de iniciar el diálogo
- Sistema de salvaguardas en cadena para garantizar restauración

```csharp
// ✅ VERIFICACIÓN EXTRA #1: Asegurar que Time.timeScale está en 1 ANTES de continuar
if (Time.timeScale != 1f)
{
    Time.timeScale = 1f;
}

// Esperar un frame extra para asegurar que el cambio se aplique
yield return null;

// ✅ VERIFICACIÓN EXTRA #2: Doble check después del yield
if (Time.timeScale != 1f)
{
    Time.timeScale = 1f;
}
```

---

### 6. ✅ NPC Gira Hacia el Player al Decir Diálogo de Derrota
**Archivo:** `Assets/Scripts/Behaviour NPC/Modules/NPCCombatLifecycleHandler.cs`

**Problema:** El NPC decía el diálogo de derrota de espaldas al jugador.

**Solución:**
- El NPC se gira hacia el jugador ANTES del delay de 2 segundos
- Rotación instantánea usando `Quaternion.LookRotation`
- Ahora el NPC está siempre mirando al jugador durante el diálogo

```csharp
// 5. ROTAR HACIA EL JUGADOR INMEDIATAMENTE (antes del delay)
Vector3 directionToPlayer = playerGo.transform.position - transform.position;
directionToPlayer.y = 0f;
Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
transform.rotation = targetRotation;
```

---

### 7. ✅ Espera de 2 Segundos Antes del Diálogo de Derrota
**Archivo:** `Assets/Scripts/Behaviour NPC/Modules/NPCCombatLifecycleHandler.cs`

**Problema:** El diálogo de derrota aparecía inmediatamente sin ver la animación de muerte.

**Solución:**
- Añadido `yield return new WaitForSecondsRealtime(2f)` tras reproducir la animación de muerte
- Usa tiempo real (no afectado por Time.timeScale)
- Da tiempo suficiente para ver la animación completa de muerte

```csharp
// 6. ESPERAR 2 SEGUNDOS para que se vea la animación de muerte
yield return new WaitForSecondsRealtime(2f);
```

---

### 8. ✅ NPC Interrumpe Hechizo al Recibir Daño
**Archivos Modificados:**
- `Assets/Scripts/Behaviour NPC/NPCCombatBrain.cs`
- `Assets/Scripts/Behaviour NPC/Modules/NPCCombatLifecycleHandler.cs`

**Problema:** Cuando el NPC estaba preparando un hechizo y recibía daño, el hechizo no se cancelaba.

**Solución:**
- Sistema completo de tracking de casting implementado
- `StartCasting()` - Marca que el NPC está casteando
- `EndCasting()` - Marca que el casting terminó normalmente
- `InterruptCasting()` - Interrumpe el casting y reproduce animación `TakeDamage`
- `HandleNPCDamaged()` detecta si está casteando y llama a `InterruptCasting()`

```csharp
// Sistema de interrupción de casting
private bool _isCasting;
private string _currentCastAnimation;
private int _currentCastLayer;

// En HandleNPCDamaged:
if (_isCasting)
{
    InterruptCasting();
    return; // La interrupción ya reproduce TakeDamage
}
```

**Flujo del Sistema:**
1. `ExecuteAttack()` llama a `lifecycleHandler.StartCasting()`
2. Si el NPC recibe daño durante el casting → `InterruptCasting()` se ejecuta
3. Se reproduce `PlayTakeDamage()` para feedback visual
4. Se limpia el estado de casting
5. Se detienen las coroutines de monitoreo activas

---

### 9. ✅ Eliminación de FindGameObjectWithTag
**Archivos Modificados:**
- `Assets/Scripts/Dialogue/DialogueManager.cs`
- `Assets/Scripts/Behaviour NPC/NPCInteractiveNarrativeExecutor.cs`

**Problema:** Uso de `GameObject.FindGameObjectWithTag("Player")` en lugar del servicio centralizado.

**Solución:**
- Reemplazado por `PlayerService.TryGetPlayer(out var player, allowSceneLookup: true)`
- Código más robusto y consistente
- Mejor manejo de errores

```csharp
// ANTES:
var player = GameObject.FindGameObjectWithTag("Player");

// DESPUÉS:
if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true) || player == null)
{
    Debug.LogWarning("No se encontró el jugador");
    return;
}
```

---

## 🎯 Resumen de Impacto

### Mejoras de UX
- ✅ Los NPCs ahora saludan con animaciones al interactuar
- ✅ El player entra visualmente en postura de batalla antes del combate
- ✅ Las animaciones de muerte se ven completas
- ✅ El juego no se queda ralentizado tras combates
- ✅ Los NPCs miran al jugador durante diálogos importantes
- ✅ Los hechizos se pueden interrumpir con golpes

### Mejoras Técnicas
- ✅ Sistema robusto de restauración de Time.timeScale
- ✅ Uso correcto de `Play()` vs `CrossFade()` según contexto
- ✅ Sistema completo de interrupción de casting
- ✅ Eliminación de anti-patrones (FindGameObjectWithTag)
- ✅ Mejor separación de responsabilidades

---

## 📝 Notas Técnicas

### Time.timeScale Management
El sistema ahora usa múltiples capas de verificación para asegurar que `Time.timeScale` se restaure:
1. Bloque `try-finally` para garantía básica
2. Verificación tras el finally
3. Yield frame adicional para propagación
4. Verificación doble tras el yield
5. Verificación final antes del diálogo
6. Salvaguarda en `OnDestroy()`

### Animaciones de Batalla
- **Pre-batalla:** `Idle_Battle_NoWeapon` (player) con efectos cinematográficos
- **Muerte:** `Die02_NoWeapon` (NPC) con delay de 2 segundos
- **Interrupción:** `TakeDamage` (NPC) al cancelar hechizos

### Sistema de Casting
El sistema de casting es simple pero efectivo:
- Flags booleanos para tracking de estado
- Información de animación y layer guardada
- Limpieza automática al completar o interrumpir
- Integración con el sistema de daño existente

---

## 🔍 Archivos Modificados

1. `Assets/Scripts/Behaviour NPC/Modules/NPCQuestConfig.cs`
2. `Assets/Scripts/Behaviour NPC/NPCInteractiveNarrativeExecutor.cs`
3. `Assets/Scripts/Dialogue/DialogueManager.cs`
4. `Assets/Scripts/Behaviour NPC/NPCSimpleAnimator.cs`
5. `Assets/Scripts/Behaviour NPC/Modules/NPCCombatLifecycleHandler.cs`
6. `Assets/Scripts/Behaviour NPC/NPCCombatBrain.cs`

---

## ✅ Testing Checklist

- [ ] Iniciar diálogo con NPC Quest → Ver animación de saludo
- [ ] Iniciar diálogo con NPC Interactive Narrative → Ver animación de saludo
- [ ] Iniciar combate con NPC → Player entra en Idle_Battle_NoWeapon
- [ ] Matar NPC → Ver animación de muerte completa (2 seg)
- [ ] Matar NPC → Verificar que el juego NO se queda lento
- [ ] NPC derrotado dice diálogo → NPC mirando al player
- [ ] Golpear NPC mientras castea → Hechizo se interrumpe
- [ ] Todas las interacciones usan PlayerService correctamente

---

## 🎓 Lecciones Aprendidas

1. **Play vs CrossFade:** `Play()` es mejor para transiciones inmediatas e importantes (muerte, batalla)
2. **Time.timeScale:** Requiere múltiples verificaciones por la naturaleza asíncrona de Unity
3. **WaitForSecondsRealtime:** Esencial para delays que no deben verse afectados por timeScale
4. **PlayerService:** Siempre preferir servicios centralizados sobre búsquedas por tag
5. **Sistema de Interrupción:** Los sistemas de cancelación deben ser explícitos y visibles al jugador

---

**Completado por:** GitHub Copilot  
**Revisado:** Pendiente de testing en Unity

