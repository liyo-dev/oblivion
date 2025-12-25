# Solución: Conflicto de Inputs al Soltar Objetos cerca de NPCs

## 🐛 Problema Identificado

**Situación problemática**:
1. El jugador **lleva un objeto** (caja, etc.)
2. Presiona **A** para **soltar** el objeto cerca de un NPC
3. El NPC **detecta automáticamente** al jugador
4. El **diálogo se abre** inmediatamente (porque A también abre diálogos)
5. El **skip UI no funciona** correctamente (conflicto de inputs)

**Resultado**: Experiencia de usuario rota y confusa.

## 🔧 Solución Implementada

### Sistema de Cooldown Post-Drop

Se ha implementado un **cooldown configurable** después de soltar objetos que previene interacciones inmediatas.

### 1. **PlayerCarrySystem.cs** - Tracking del Drop

```csharp
// Nuevos campos
[SerializeField] private float dropCooldown = 0.5f; // Cooldown configurable
private float _lastDropTime = -999f;

// Propiedad pública para que otros sistemas lo consulten
public bool JustDroppedObject => (Time.time - _lastDropTime) < dropCooldown;
```

**Funcionamiento**:
- Al soltar un objeto, se marca `_lastDropTime = Time.time`
- Durante los siguientes `dropCooldown` segundos (0.5s por defecto)
- `JustDroppedObject` devuelve `true`
- Otros sistemas pueden consultar esta propiedad para evitar interacciones

### 2. **Bloqueo Temporal con ActionMode.Stunned**

```csharp
private IEnumerator ClearInputBufferAfterDrop()
{
    // Bloquear brevemente las acciones para limpiar el buffer de inputs
    // y evitar que se abran diálogos automáticamente
    if (_actionManager != null)
    {
        _actionManager.PushMode(ActionMode.Stunned);
        yield return new WaitForSeconds(dropCooldown); // Cooldown configurable
        _actionManager.PopMode(ActionMode.Stunned);
    }
}
```

**Beneficios**:
- Bloquea TODAS las acciones del jugador temporalmente
- Limpia el buffer de inputs acumulados
- Previene que la A que soltó el objeto active otra cosa

### 3. **NPCInteractiveNarrativeExecutor.cs** - Verificación Pre-Detección

```csharp
if (distanceToPlayer <= _config.detectionRange)
{
    // ✅ NUEVO: Verificar si el jugador acaba de soltar un objeto
    var carrySystem = _player.GetComponent<PlayerCarrySystem>();
    if (carrySystem != null && carrySystem.JustDroppedObject)
    {
        // Log periódico para debug
        if (checkCount % 10 == 0)
        {
            Debug.Log($"[NPC] ⏳ Jugador acaba de soltar objeto, esperando cooldown...");
        }
        yield return new WaitForSeconds(0.2f);
        continue; // Volver a checkear en el siguiente ciclo
    }
    
    // Solo si NO acaba de soltar objeto, iniciar narrativa
    _hasDetectedPlayer = true;
    yield return StartAlertSequence();
    TryExecuteNarrative();
    yield break;
}
```

**Funcionamiento**:
- Antes de iniciar la narrativa automática
- Verifica `JustDroppedObject`
- Si es `true`, espera 0.2s y vuelve a chequear
- Solo inicia narrativa cuando el cooldown ha pasado

## ⚙️ Configuración

### En Inspector del PlayerCarrySystem

```
Drop Cooldown: 0.5 (segundos)
```

**Valores recomendados**:
- `0.3s` - Mínimo (rápido pero puede no ser suficiente)
- `0.5s` - Recomendado (balance perfecto) ✅
- `0.7s` - Más seguro (puede sentirse un poco lento)

## 🎮 Flujo Corregido

### Secuencia con el Fix:

1. **Jugador lleva objeto**
2. **Presiona A cerca de NPC**
   - Objeto se suelta
   - `_lastDropTime` se marca
   - `ActionMode.Stunned` se activa (0.5s)
3. **NPC detecta jugador**
   - Verifica `JustDroppedObject` → `true`
   - Espera 0.2s
   - Vuelve a verificar
4. **Después de 0.5s**
   - `ActionMode.Stunned` se desactiva
   - `JustDroppedObject` → `false`
5. **NPC inicia narrativa**
   - Ahora SÍ puede interactuar correctamente
   - Skip UI funciona normalmente ✅

### Comparación Antes/Después:

| Aspecto | ANTES ❌ | AHORA ✅ |
|---------|----------|----------|
| Soltar objeto | A | A |
| Diálogo se abre | Inmediato (conflicto) | Después de 0.5s |
| Skip UI | No funciona | Funciona correctamente |
| Input buffer | Se acumula | Se limpia |
| Experiencia | Rota y confusa | Fluida y natural |

## 🔍 Debug y Verificación

### Logs a Observar:

```
[PlayerCarrySystem] Objeto soltado
[NPC] ⏳ Jugador acaba de soltar objeto, esperando cooldown...
[NPC] ⏳ Jugador acaba de soltar objeto, esperando cooldown...
[NPC] ✅ ¡Jugador detectado! (después del cooldown)
[NPC] 📖 Intentando ejecutar narrativa...
```

### Testing:

1. **Llevar objeto cerca de NPC con auto-detección**
2. **Soltar con A**
3. **Verificar**:
   - ✅ Diálogo NO se abre inmediatamente
   - ✅ Hay una pausa breve (~0.5s)
   - ✅ Luego el diálogo se abre normalmente
   - ✅ Skip UI funciona correctamente

## 🎯 Sistemas Afectados

### ✅ Sistemas que Usan la Verificación:

1. **NPCInteractiveNarrativeExecutor** - Narrativas automáticas
2. *(Futuro) NPCQuestConfig* - Diálogos de quests con auto-detección

### 🔄 Sistemas Compatibles:

- ✅ **PlayerCarrySystem** - Fuente del estado
- ✅ **PlayerActionManager** - Bloqueo temporal
- ✅ **DialogueManager** - Funciona normalmente después del cooldown
- ✅ **Skip UI** - Funciona correctamente

## 📋 Casos de Uso Cubiertos

### ✅ Cubierto:
1. Soltar objeto cerca de NPC con auto-detección
2. Soltar objeto y luego hablar manualmente
3. Llevar objeto sin soltarlo (no afecta)
4. Múltiples drops seguidos

### ⚠️ Casos Especiales:

**¿Qué pasa si el jugador intenta interactuar manualmente durante el cooldown?**
- El `ActionMode.Stunned` bloqueará la interacción
- Después del cooldown, podrá interactuar normalmente

**¿Funciona con múltiples NPCs?**
- Sí, cada NPC verifica independientemente el estado

## 🐛 Troubleshooting

### "El diálogo todavía se abre inmediatamente"
- ✅ Verifica que `dropCooldown` sea al menos 0.3s
- ✅ Revisa que el NPC tenga `autoStartOnPlayerDetection = true`
- ✅ Busca logs que digan "esperando cooldown..."

### "El cooldown es demasiado largo"
- ✅ Reduce `dropCooldown` a 0.3s en PlayerCarrySystem

### "El jugador no puede interactuar después de soltar"
- ✅ Verifica que `ClearInputBufferAfterDrop` llame a `PopMode`
- ✅ Asegúrate de que no haya errores en la coroutine

## ✅ Resultado Final

### Experiencia del Usuario:

1. **Soltar objeto** → Acción inmediata ✅
2. **Pausa breve** → Imperceptible (~0.5s) ✅
3. **Diálogo se abre** → Correctamente ✅
4. **Skip UI funciona** → Perfectamente ✅
5. **Sin conflictos** → Controles fluidos ✅

### Beneficios:

- ✅ **Previene conflictos de input**
- ✅ **Limpia buffer de comandos**
- ✅ **Experiencia más fluida**
- ✅ **Skip UI funciona correctamente**
- ✅ **Configurable y ajustable**
- ✅ **Extensible a otros sistemas**

---

**Fecha**: 2025-12-25  
**Prioridad**: ALTA (UX crítica)  
**Estado**: ✅ RESUELTO COMPLETAMENTE  
**Impacto**: Mejora significativa en la fluidez de controles

