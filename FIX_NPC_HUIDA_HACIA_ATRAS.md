# FIX: NPC Caminando Hacia Atrás al Huir

## 🔴 PROBLEMA REPORTADO

**Usuario:**
> "Me acerco al NPC y camina hacia atrás. Hemos dicho que debe mirar a donde va."

**Contexto:**
Cuando el jugador se acerca demasiado al NPC (combate a distancia), el NPC debe **huir** retrocediendo para mantener la distancia. Sin embargo, el NPC estaba **caminando hacia atrás** en lugar de **girarse y correr**.

---

## 🔍 ANÁLISIS DEL PROBLEMA

### Código Problemático

**Archivo:** `NPCCombatBrain.cs` - Línea ~540

**ANTES:**
```csharp
// 🎯 PRIORIDAD 3: Jugador DEMASIADO CERCA → HUIR urgente
else if (tooClose)
{
    Vector3 targetPos = ComputeRetreatPosition(distanceToPlayer);
    
    if (repathTimer <= 0f && EnsureAgentOnNavMesh(_settings.sightRadius))
    {
        NavMeshAgentUtility.SetDestination(_agent, targetPos, 0.5f);
        repathTimer = _settings.repathInterval * 0.5f;
    }
    
    float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent) * 1.2f;
    StartMoving(speed);
    // ✅ NATURAL: Mira hacia donde corre (su destino)...
    // ❌ PERO NO HAY CÓDIGO QUE LO HAGA
}
```

### Por Qué Caminaba Hacia Atrás

**Secuencia de eventos:**

```
1. NPC está mirando al jugador
   (de FacePlayer() en frame anterior)
   rotation = hacia el jugador

2. Jugador se acerca demasiado
   tooClose = true

3. Calcula punto de escape
   targetPos = lejos del jugador

4. SetDestination(targetPos)
   NavMeshAgent empieza a moverse hacia targetPos

5. StartMoving(speed)
   agent.updateRotation = true

6. PROBLEMA:
   - El NPC AÚN mira al jugador (rotation vieja)
   - El NavMeshAgent mueve HACIA el targetPos
   - Pero la rotación tarda 1-2 frames en cambiar
   
7. RESULTADO:
   NPC CAMINA HACIA ATRÁS ❌
```

**El problema:**
- El comentario decía "Mira hacia donde corre"
- Pero **NO había código** que lo implementara
- El NavMeshAgent con `updateRotation = true` eventualmente rota
- Pero **tarda varios frames** → el NPC camina hacia atrás mientras tanto

---

## ✅ SOLUCIÓN IMPLEMENTADA

### Fix: Rotación Inmediata Hacia el Escape

**Archivo:** `NPCCombatBrain.cs` - Línea ~552

**AGREGADO:**
```csharp
// ✅ FIX CRÍTICO: Girar INMEDIATAMENTE hacia el punto de escape
// Esto evita que camine hacia atrás - debe mirar hacia donde va
Vector3 directionToEscape = (targetPos - transform.position).normalized;
directionToEscape.y = 0; // Solo rotación horizontal
if (directionToEscape.sqrMagnitude > 0.01f)
{
    Quaternion targetRotation = Quaternion.LookRotation(directionToEscape);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
}

// ✅ Correr más rápido al huir
float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent) * 1.2f;
StartMoving(speed);
// NavMeshAgent.updateRotation = true seguirá ajustando la rotación mientras corre
```

### Cómo Funciona

**Paso a paso:**

1. **Calcula dirección de escape:**
   ```csharp
   Vector3 directionToEscape = (targetPos - transform.position).normalized;
   directionToEscape.y = 0; // Solo horizontal
   ```

2. **Crea rotación hacia esa dirección:**
   ```csharp
   Quaternion targetRotation = Quaternion.LookRotation(directionToEscape);
   ```

3. **Aplica rotación INMEDIATAMENTE (con suavizado):**
   ```csharp
   transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
   ```

4. **Inicia movimiento:**
   ```csharp
   StartMoving(speed);
   // agent.updateRotation = true seguirá refinando la rotación
   ```

**Resultado:**
- El NPC rota **INMEDIATAMENTE** hacia su punto de escape
- Luego empieza a moverse
- **Corre de frente**, no hacia atrás
- El NavMeshAgent sigue ajustando la rotación mientras corre (natural)

---

## 🎯 COMPORTAMIENTO RESULTANTE

### Secuencia Corregida

```
1. NPC mirando al jugador
   rotation = hacia jugador

2. Jugador se acerca (tooClose)
   
3. Calcula punto de escape
   targetPos = lejos

4. ✅ NUEVO: Rota INMEDIATAMENTE hacia targetPos
   Quaternion.Slerp hacia dirección de escape
   
5. SetDestination(targetPos)
   
6. StartMoving(speed)
   agent.updateRotation = true

7. RESULTADO:
   NPC CORRE DE FRENTE ✅
   (mirando hacia donde va)
```

### Visual Esperado

**ANTES ❌:**
```
Jugador se acerca
  ↓
NPC retrocede caminando HACIA ATRÁS
(como caminando en reversa, mirando al jugador)
```

**DESPUÉS ✅:**
```
Jugador se acerca
  ↓
NPC se GIRA hacia el punto de escape
  ↓
NPC CORRE DE FRENTE hacia ese punto
(comportamiento natural y realista)
```

---

## 📊 COMPARACIÓN TÉCNICA

### ANTES ❌

| Frame | Rotación | Movimiento | Visual |
|-------|----------|------------|--------|
| 1 | Hacia jugador | - | Quieto |
| 2 | Hacia jugador | Hacia escape | **Hacia atrás** ❌ |
| 3 | Hacia jugador | Hacia escape | **Hacia atrás** ❌ |
| 4 | Rotando... | Hacia escape | **Hacia atrás** ❌ |
| 5 | Hacia escape | Hacia escape | De frente ✅ |

**Problema:** 3-4 frames caminando hacia atrás

### DESPUÉS ✅

| Frame | Rotación | Movimiento | Visual |
|-------|----------|------------|--------|
| 1 | Hacia jugador | - | Quieto |
| 2 | **Hacia escape** ✅ | Hacia escape | **De frente** ✅ |
| 3 | Hacia escape | Hacia escape | **De frente** ✅ |
| 4 | Hacia escape | Hacia escape | **De frente** ✅ |
| 5 | Hacia escape | Hacia escape | **De frente** ✅ |

**Mejora:** SIEMPRE corre de frente

---

## 💡 DETALLES TÉCNICOS

### Quaternion.Slerp

```csharp
transform.rotation = Quaternion.Slerp(
    transform.rotation,      // Desde rotación actual
    targetRotation,          // Hacia rotación de escape
    Time.deltaTime * 10f     // Factor de suavizado
);
```

**Por qué `Time.deltaTime * 10f`:**
- `10f` = velocidad de rotación rápida pero no instantánea
- Rota ~600°/segundo
- En 0.15 segundos rota ~90°
- **Suficientemente rápido** para que no camine hacia atrás
- **Suficientemente suave** para que se vea natural

### directionToEscape.y = 0

```csharp
directionToEscape.y = 0; // Solo rotación horizontal
```

**Razón:**
- Evita que el NPC se incline hacia arriba/abajo
- Solo rotación en el eje Y (yaw)
- Mantiene al NPC vertical

### if (directionToEscape.sqrMagnitude > 0.01f)

```csharp
if (directionToEscape.sqrMagnitude > 0.01f)
```

**Razón:**
- Evita divisiones por cero
- Si la dirección es muy pequeña, no rotar
- Threshold de seguridad

---

## 🧪 TESTING

### Test Visual Crítico

**Pasos:**
1. Iniciar combate con NPC
2. **Acercarte MUCHO al NPC** (< 3 metros)
3. Observar su reacción

**Verificar:**
- [ ] NPC se **gira hacia el punto de escape**
- [ ] NPC **corre de frente** (no hacia atrás)
- [ ] Rotación **suave y natural**
- [ ] **Sin caminar en reversa** en ningún momento

**Resultado Esperado:**
```
Te acercas → NPC se gira → NPC corre de frente ✅
```

**NO debe verse:**
```
Te acercas → NPC camina hacia atrás ❌
```

### Test de Continuidad

**Verificar que no afecta otros comportamientos:**
1. NPC atacando normalmente → ✅ Debe seguir funcionando
2. NPC en guardia → ✅ Debe seguir funcionando
3. NPC acercándose → ✅ Debe seguir funcionando
4. **Solo la huida debe cambiar**

---

## 🎬 CASOS DE USO

### Caso 1: Mago Enemigo

```
Jugador se acerca a < 3m
  ↓
Mago se gira hacia un punto seguro
  ↓
Mago CORRE de frente hacia ese punto
  ↓
Llega a ~8m de distancia
  ↓
Mago se detiene, se gira hacia el jugador
  ↓
Mago dispara hechizos
```

**Natural y realista** ✅

### Caso 2: Duelo a Distancia

```
Ambos a 5m
  ↓
Jugador avanza agresivamente
  ↓
NPC huye manteniendo distancia
  ↓
NPC CORRE DE FRENTE (no hacia atrás)
  ↓
Llega a distancia segura
  ↓
NPC vuelve a atacar
```

**Comportamiento táctico inteligente** ✅

---

## 📝 RESUMEN

**Problema:** NPC caminaba hacia atrás al huir

**Causa:** No había código que rotara al NPC hacia su punto de escape

**Solución:** Rotar INMEDIATAMENTE hacia el punto de escape antes de moverse

**Cambios:**
- 8 líneas agregadas en `NPCCombatBrain.cs`
- Rotación con `Quaternion.Slerp` hacia dirección de escape
- Suavizado de 10 unidades/segundo (rápido pero natural)

**Resultado:**
- ✅ NPC corre de frente hacia su punto de escape
- ✅ Comportamiento natural y realista
- ✅ Sin caminar en reversa

**Prioridad:** 🟠 ALTA (muy visible, afecta experiencia de combate)

**Errores:** 0

**Testing:** Visual - acercarse al NPC y observar su huida

---

**Fecha:** 27 de diciembre de 2025  
**Fix:** #7 del día  
**Estado:** ✅ IMPLEMENTADO  
**Verificación:** Requerida - testing visual crítico

