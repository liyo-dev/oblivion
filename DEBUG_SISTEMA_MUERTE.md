# 🔍 DEBUG: Sistema de Logs para Rastrear Problema de Muerte

## 📋 Problema Reportado

Después de los últimos cambios, la animación de muerte, dizzy y diálogo no se reproducen. El NPC se queda de pie quieto después de recibir daño.

## 🕵️ Logs de Depuración Añadidos

### 1. **NPCCombatLifecycleHandler.OnDamaged()**

```csharp
Debug.Log($"[Lifecycle] ⚔️ {name} recibió {amount} de daño - Vida: {_damageable.Current}/{_damageable.Max} - IsAlive: {_damageable.IsAlive}");
```

**Qué detecta:**
- ✅ Cuánto daño recibe el NPC
- ✅ Vida actual después del daño
- ✅ Si el NPC sigue vivo después del daño
- ✅ Si OnDamaged() se está llamando

### 2. **NPCCombatLifecycleHandler.OnDied()**

```csharp
Debug.Log($"[Lifecycle] 💀💀💀 OnDied() LLAMADO para {name} - _isProcessingDefeat: {_isProcessingDefeat}");
```

**Qué detecta:**
- ✅ Si OnDied() se está llamando cuando muere
- ✅ Si hay procesamiento duplicado
- ✅ Estado de la bandera _isProcessingDefeat

### 3. **Damageable.TakeDamage()**

```csharp
// Si ya está muerto
Debug.Log($"[Damageable:{name}] ⚠️ Ignorando daño - ya está muerto (Current: {Current})");

// Si está invulnerable
Debug.Log($"[Damageable:{name}] 🛡️ Ignorando daño - invulnerable hasta {_invulnerableUntil - Time.time:F2}s");

// Cuando la vida llega a 0
Debug.Log($"[Damageable:{name}] 💀 VIDA AGOTADA - Llamando a Die() (vida anterior: {oldHealth:F1})");
```

**Qué detecta:**
- ✅ Si el daño está siendo ignorado porque ya está muerto
- ✅ Si el daño está siendo ignorado por invulnerabilidad
- ✅ Si la vida realmente llega a 0
- ✅ Si Die() se está llamando

### 4. **Damageable.Die()**

```csharp
Debug.Log($"[Damageable:{name}] 💀💀💀 Die() llamado - Invocando OnDied (suscriptores: {OnDied?.GetInvocationList().Length ?? 0})");
Debug.Log($"[Damageable:{name}] OnDied invocado - destroyOnDeath: {destroyOnDeath}");
```

**Qué detecta:**
- ✅ Si Die() se está llamando
- ✅ Cuántos suscriptores tiene el evento OnDied
- ✅ Si hay algo escuchando el evento
- ✅ Valor de destroyOnDeath

## 🎯 Posibles Causas del Bug

Basándome en el flujo, el problema podría ser:

### Hipótesis 1: El NPC no está muriendo realmente
```
❓ La vida no llega a 0
❓ Está recibiendo poco daño
❓ La vida inicial es muy alta
```

**Logs esperados:**
```
[Lifecycle] ⚔️ Boy_Pirate recibió 50 de daño - Vida: 50/100 - IsAlive: True
[Lifecycle] ⚔️ Boy_Pirate recibió 50 de daño - Vida: 0/100 - IsAlive: False
[Damageable:Boy_Pirate] 💀 VIDA AGOTADA - Llamando a Die()
```

**Si no aparece "VIDA AGOTADA":** El NPC no está muriendo.

### Hipótesis 2: El evento OnDied no tiene suscriptores
```
❓ NPCCombatLifecycleHandler.Start() no se ejecutó
❓ El componente no existe en el GameObject
❓ La suscripción al evento falló
```

**Logs esperados:**
```
[Damageable:Boy_Pirate] 💀💀💀 Die() llamado - Invocando OnDied (suscriptores: 1)
[Lifecycle] 💀💀💀 OnDied() LLAMADO para Boy_Pirate
```

**Si suscriptores = 0:** El evento no tiene nadie escuchando.

### Hipótesis 3: El daño está siendo ignorado
```
❓ NPC está en invulnerabilidad permanente
❓ IsAlive retorna false antes de recibir daño
```

**Logs esperados:**
```
[Damageable:Boy_Pirate] 🛡️ Ignorando daño - invulnerable hasta 2.5s
```

### Hipótesis 4: DamageSequence está bloqueando la muerte
```
❓ _isInvulnerable se queda en true permanentemente
❓ _isProcessingDefeat se activa antes de tiempo
```

## 📝 Secuencia de Logs Esperada (Correcto)

Cuando un NPC muere correctamente, deberías ver esto en la consola:

```
1. [Lifecycle] ⚔️ Boy_Pirate recibió 50 de daño - Vida: 0/100 - IsAlive: False
2. [Damageable:Boy_Pirate] 💀 VIDA AGOTADA - Llamando a Die() (vida anterior: 50.0)
3. [Damageable:Boy_Pirate] 💀💀💀 Die() llamado - Invocando OnDied (suscriptores: 1)
4. [Damageable:Boy_Pirate] OnDied invocado - destroyOnDeath: False
5. [Lifecycle] 💀💀💀 OnDied() LLAMADO para Boy_Pirate - _isProcessingDefeat: False
6. [Lifecycle] 💀 Iniciando secuencia de muerte: Boy_Pirate
7. [Lifecycle] 💀 Animación de muerte iniciada inmediatamente después del slow-mo
8. [NPCAnimator:Boy_Pirate] 💀 PlayDeath() llamado - dieState: 'Die02_NoWeapon'
... (resto de la secuencia)
```

## 🔍 Qué Buscar en los Logs

### ✅ Si ves TODOS estos logs
→ El sistema está funcionando correctamente

### ❌ Si NO ves "[Lifecycle] ⚔️ recibió daño"
→ OnDamaged() no se está llamando
→ Problema: El evento OnDamaged no está suscrito

### ❌ Si ves daño pero NO ves "VIDA AGOTADA"
→ La vida no llega a 0
→ Problema: El NPC tiene demasiada vida o recibe poco daño

### ❌ Si ves "VIDA AGOTADA" pero NO ves "Die() llamado"
→ Die() no se ejecuta
→ Problema: Algo interrumpe la ejecución de Die()

### ❌ Si ves "Die() llamado" con "suscriptores: 0"
→ El evento OnDied no tiene listeners
→ Problema: NPCCombatLifecycleHandler no se inicializó correctamente

### ❌ Si ves "Die() llamado" pero NO ves "OnDied() LLAMADO"
→ El evento se invoca pero el handler no responde
→ Problema: El suscriptor fue removido o el objeto fue destruido

## 🎮 Instrucciones de Prueba

1. **Ejecuta el juego en Unity**
2. **Mata al NPC**
3. **Copia TODOS los logs de la consola relacionados con el NPC**
4. **Compara con la secuencia esperada**

Basándose en qué logs aparecen y cuáles no, podemos identificar exactamente dónde falla el sistema.

## 📊 Tabla de Diagnóstico Rápido

| Síntoma | Log Faltante | Causa Probable |
|---------|--------------|----------------|
| No recibe daño | `⚔️ recibió daño` | Evento OnDamaged no suscrito |
| Recibe daño pero no muere | `💀 VIDA AGOTADA` | Vida > 0 después del daño |
| Vida agota pero no ejecuta | `Die() llamado` | Ejecución interrumpida |
| Die() sin suscriptores | `suscriptores: 0` | Start() no ejecutado |
| Die() pero no OnDied | `OnDied() LLAMADO` | Suscriptor desconectado |
| OnDied pero no secuencia | `Iniciando secuencia de muerte` | _isProcessingDefeat = true |

---

**Siguiente Paso**: Ejecutar el juego, matar al NPC, y enviar los logs completos para identificar el problema exacto.

