# ✅ RENOMBRADO: Rangos de Combate con Nombres Claros

**Fecha:** 2025-12-26  
**Estado:** Implementado

---

## 🎯 **PROBLEMA RESUELTO**

Los campos tenían **nombres engañosos**:
- `meleeRange` → Realmente era **minDistance** (distancia mínima)
- `combatRange` → Realmente era **maxDistance** (distancia máxima)

Esto causaba confusión porque los nombres no reflejaban su propósito real.

---

## ✅ **CAMBIOS REALIZADOS**

### 1. **Nuevos Nombres de Campos**

```csharp
// ❌ ANTES (nombres confusos)
public float meleeRange = 2f;     // Tooltip: "Rango para ataques cuerpo a cuerpo"
public float combatRange = 8f;    // Tooltip: "Rango para ataques a distancia"

// ✅ AHORA (nombres claros)
public float minAttackDistance = 2f;  
// Tooltip: "⚠️ DISTANCIA MÍNIMA DE ATAQUE - El NPC retrocede si el jugador está MÁS CERCA que esto. 
//           Para magos: 4-5m. Para melee: 1.5-2m."

public float maxAttackDistance = 8f;  
// Tooltip: "⚠️ DISTANCIA MÁXIMA DE ATAQUE - El NPC se acerca si el jugador está MÁS LEJOS que esto. 
//           Para magos: 8-12m. Para melee: 3-5m. 
//           IMPORTANTE: Debe ser MAYOR que minAttackDistance o el NPC nunca atacará."
```

### 2. **Validación Agregada**

```csharp
if (minAttackDistance >= maxAttackDistance)
{
    errorMessage = "⚠️ CRÍTICO: Max Attack Distance debe ser MAYOR que Min Attack Distance, " +
                   "o el NPC NUNCA atacará.";
    return false;
}
```

Esto previene configuraciones inválidas como:
```
minAttackDistance: 2
maxAttackDistance: 2  ← ❌ ERROR: Rango de ataque = 0
```

### 3. **Compatibilidad Retroactiva**

Se mantienen las propiedades antiguas como `[Obsolete]` para no romper assets existentes:

```csharp
[System.Obsolete("Usar minAttackDistance en su lugar")]
public float meleeRange { get => minAttackDistance; set => minAttackDistance = value; }

[System.Obsolete("Usar maxAttackDistance en su lugar")]
public float combatRange { get => maxAttackDistance; set => maxAttackDistance = value; }
```

---

## 📊 **En el Inspector de Unity**

### Antes:
```
Ranges
├─ Detection Range: 3
├─ Combat Range: 2      ← ¿Qué significa esto?
└─ Melee Range: 2       ← ¿Y esto?
```

### Ahora:
```
Ranges
├─ Detection Range: 3
├─ Min Attack Distance: 2   ← ⚠️ Distancia MÍNIMA (retrocede si más cerca)
└─ Max Attack Distance: 10  ← ⚠️ Distancia MÁXIMA (se acerca si más lejos)
```

**Tooltips claros explican exactamente qué hace cada campo.**

---

## 🔧 **Archivos Modificados**

1. ✅ `NPCCombatConfig.cs` - Campos renombrados con tooltips claros
2. ✅ `CombatState.cs` - Actualizado para usar nuevos nombres
3. ✅ `NPCConfiguration.cs` - Actualizado
4. ✅ `IdleState.cs` - Actualizado
5. ✅ `WanderState.cs` - Actualizado
6. ✅ `NPC_Combat_Config_Erika.asset` - Actualizado con nuevos valores:
   - `minAttackDistance: 2`
   - `maxAttackDistance: 10`

---

## 🎮 **Valores Correctos para Erika**

```
Min Attack Distance: 2   ← Retrocede si estás más cerca
Max Attack Distance: 10  ← Se acerca si estás más lejos
Rango de ataque efectivo: 8 metros (de 2m a 10m)
```

Esto le da a Erika un rango apropiado para un **mago ranged**.

---

## ✅ **Verificación en Unity**

1. Abre Unity
2. Selecciona `NPC_Combat_Config_Erika`
3. En el Inspector verás:
   ```
   Min Attack Distance: 2
   Max Attack Distance: 10
   ```
4. Al pasar el mouse sobre cada campo, verás tooltips explicativos

---

## 🚨 **Prevención de Errores**

Si intentas configurar:
```
Min Attack Distance: 5
Max Attack Distance: 5  ← Iguales
```

Unity mostrará un **error de validación** explicando que Max debe ser mayor que Min.

---

**Estado:** ✅ COMPLETADO - Nombres claros + Tooltips + Validación + Compatibilidad

