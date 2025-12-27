# 🛡️ INSTRUCCIONES: Configurar Escudo Defensivo para NPCs

**Fecha:** 2025-12-26  
**Problema:** NPC no se protege con escudo  
**Causa:** Falta el componente `NPCShieldController`

---

## ✅ **SOLUCIÓN RÁPIDA**

### 1. Agregar NPCShieldController al NPC

1. **En Unity:**
   ```
   Hierarchy → Seleccionar NPC "Erika"
   Inspector → Add Component → NPCShieldController
   ```

### 2. Configurar el NPCShieldController

**Campos obligatorios:**

#### **Shield Prefab** (Obligatorio)
```
Assets/VFX/Shield/Shield_Prefab.prefab
```
(O el prefab del escudo que uses)

#### **Shield Anchor** (Recomendado)
```
Drag: Erika → LeftArm → Hand
```
(El transform donde aparecerá el escudo, normalmente la mano izquierda)

#### **Animaciones:**
```
defendAnimation: "Defend_NoWeapon"
defendHitAnimation: "DefendHit_NoWeapon"
upperBodyLayer: 1
```

#### **Duración:**
```
minDefendDuration: 2
maxDefendDuration: 5
```

#### **Layers a bloquear:**
```
blockLayerNames: 
  - "Projectile"
  - "PlayerProjectile"
```

---

### 3. Configurar el NPCCombatConfig

**En el ScriptableObject `NPC_Combat_Config_Erika`:**

```
useShield: ✅ true
shieldMinDuration: 2
shieldMaxDuration: 5
shieldCooldown: 8

preferShieldOverCover: ✅ true  (Para priorizar escudo sobre cobertura)
```

---

## 🔍 **VERIFICACIÓN**

### 1. Verificar que el componente está agregado

**Log esperado al iniciar combate:**
```
[NPCCombatBrain] ✅ Shield controller encontrado
```

**Si sale este log, hay problema:**
```
[NPCCombatBrain] ⚠️ useShield=true pero no hay NPCShieldController en Erika
```
→ **Solución:** Agregar el componente `NPCShieldController`

---

### 2. Verificar que se activa en combate

**Log esperado cuando activa escudo:**
```
[NPCCombatBrain] 🛡️ SIN MAGIA - Usando escudo para ganar tiempo
[NPCCombatBrain] 🛡️ ✅ ESCUDO ACTIVADO - Duración: 3.2s, Cooldown: 8.0s
[NPCShieldController] 🛡️ DEFENSA ACTIVADA - Duración: 3.2s
```

**Si no se activa, revisar:**
- ¿Tiene el componente NPCShieldController? → Verificar en Inspector
- ¿useShield está en true? → Verificar en Combat Config
- ¿El escudo está en cooldown? → Esperar 8 segundos

---

## 📋 **CHECKLIST**

### ✅ Componentes del NPC:
```
[ ] NPCBehaviourManagerV2
[ ] NPCSimpleAnimator
[ ] NavMeshAgent
[ ] Damageable
[ ] NPCShieldController  ← ⚠️ AGREGAR ESTE
```

### ✅ Configuración Combat Config:
```
[ ] useShield = true
[ ] shieldMinDuration = 2
[ ] shieldMaxDuration = 5
[ ] shieldCooldown = 8
[ ] preferShieldOverCover = true
```

### ✅ Configuración NPCShieldController:
```
[ ] shieldPrefab asignado
[ ] shieldAnchor asignado (mano izquierda)
[ ] defendAnimation configurado
[ ] blockLayerNames con "PlayerProjectile"
```

---

## 🎯 **COMPORTAMIENTO ESPERADO**

### Cuando el NPC NO tiene magia disponible:

1. **Opción A:** Activar escudo (si está disponible)
   ```
   - Se detiene
   - Mira al player
   - Activa escudo
   - Se queda quieto defendiendo 2-5 segundos
   - Espera a que se recarguen los cooldowns
   ```

2. **Opción B:** Buscar cobertura (si no tiene escudo o está en cooldown)
   ```
   - Busca un punto de cobertura
   - Se mueve hacia allí
   - Se queda quieto esperando cooldowns
   ```

3. **Opción C:** Quedarse quieto en guardia
   ```
   - Se detiene
   - Mira al player
   - Espera quieto (postura de duelo)
   ```

---

## 🐛 **DEBUGGING**

### Si el escudo no se activa:

#### 1. Verificar componente
```csharp
// En consola debería aparecer al iniciar combate:
[NPCCombatBrain] ✅ Shield controller encontrado
```

Si aparece este warning:
```
[NPCCombatBrain] ⚠️ No hay NPCShieldController en Erika
```
→ **AGREGAR COMPONENTE**

#### 2. Verificar configuración
```csharp
// Cuando intenta activar escudo:
[NPCCombatBrain] ⚠️ useShield está desactivado en config
→ useShield = true en Combat Config

[NPCCombatBrain] 🛡️ Escudo en cooldown: 5.2s
→ Esperar que termine el cooldown

[NPCCombatBrain] ⚠️ Ya está defendiendo con escudo
→ El escudo ya está activo
```

#### 3. Verificar prefab del escudo
```csharp
[NPCShieldController] ⚠️ shieldPrefab no asignado
→ Asignar el prefab del escudo en Inspector
```

---

## 📝 **NOTAS IMPORTANTES**

### Cooldown del escudo:
```
shieldCooldown: 8 segundos
```
- El NPC solo puede usar el escudo cada 8 segundos
- Esto evita spam de escudo
- Si todos los ataques están en cooldown Y el escudo también, buscará cobertura

### Duración del escudo:
```
Random entre shieldMinDuration y shieldMaxDuration
Por defecto: 2-5 segundos
```

### Comportamiento en combate:
```
1. ¿Puede atacar? → ATACA (prioridad máxima)
2. ¿Player muy cerca? → RETROCEDE
3. ¿Sin magia Y escudo disponible? → USA ESCUDO
4. ¿Sin magia Y sin escudo? → COBERTURA o QUIETO
```

---

## ✅ **RESULTADO ESPERADO**

Después de seguir estos pasos, el NPC debería:

1. ✅ Detectar cuando no puede atacar
2. ✅ Activar el escudo automáticamente
3. ✅ Quedarse quieto mientras se protege
4. ✅ Esperar a que se recarguen los cooldowns
5. ✅ Volver a atacar cuando tenga magia

**Logs esperados:**
```
[NPCCombatBrain] ⏳ SIN MAGIA - Esperando cooldowns
[NPCCombatBrain] 🛡️ SIN MAGIA - Usando escudo para ganar tiempo
[NPCCombatBrain] 🛡️ ✅ ESCUDO ACTIVADO - Duración: 3.5s, Cooldown: 8.0s
[NPCShieldController] 🛡️ DEFENSA ACTIVADA - Duración: 3.5s
[NPCShieldController] ✅ Escudo instanciado
```

---

**Estado:** ✅ Instrucciones completas  
**Siguiente paso:** Agregar NPCShieldController al prefab del NPC

