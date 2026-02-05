# Fix: Verificación de Miembros del Equipo en Quests

## 🐛 Problema Identificado

Cuando una quest requiere que un NPC forme parte del equipo para completar un paso, **la verificación no funcionaba correctamente**.

### Causas Principales

1. **Comparación inconsistente de IDs**: El método `OnPartyMemberJoined` solo comparaba el `persistenceId` directamente, pero no consideraba el `gameObject.name` como fallback.

2. **Lógica restrictiva en `FindStepIndex`**: El método requería que `conditionId == null` para usar el `stepIndex` directamente, lo cual era demasiado restrictivo.

3. **Falta de logs detallados**: No había suficiente información de debug para diagnosticar el problema.

## ✅ Soluciones Implementadas

### 1. Mejorada la comparación de miembros en `OnPartyMemberJoined`

**Antes:**
```csharp
string persistenceId = member.NPCManager?.Configuration?.interactiveNarrativeConfig?.persistenceId;
if (string.IsNullOrEmpty(persistenceId)) persistenceId = member.gameObject.name;

if (memberReq.memberId == persistenceId)
{
    // ...
}
```

**Después:**
```csharp
string persistenceId = member.NPCManager?.Configuration?.interactiveNarrativeConfig?.persistenceId;
string gameObjectName = member.gameObject.name;

bool matches = false;
if (!string.IsNullOrEmpty(persistenceId) && memberReq.memberId == persistenceId)
{
    matches = true;
}
else if (memberReq.memberId == gameObjectName)
{
    matches = true;
}

if (matches)
{
    // ...
}
```

**Beneficio**: Ahora compara tanto con `persistenceId` como con `gameObject.name`, consistente con `CheckPartyMembersForQuest`.

### 2. Corregida la lógica de `FindStepIndex`

**Antes:**
```csharp
if (stepIndex >= 0 && stepIndex < rq.Steps.Length && conditionId == null)
{
    return stepIndex;
}
```

**Después:**
```csharp
if (stepIndex >= 0 && stepIndex < rq.Steps.Length)
{
    Debug.Log($"[QuestManager.FindStepIndex] Usando stepIndex directo: {stepIndex} para quest '{rq.Id}'");
    return stepIndex;
}
```

**Beneficio**: El método ahora prioriza correctamente el `stepIndex` sin requerir que `conditionId` sea null.

### 3. Añadidos logs detallados

Se añadieron logs en varios puntos críticos:

- ✅ `OnPartyMemberJoined`: Muestra `persistenceId` y `gameObject.name` cuando se une un miembro
- ✅ `CheckPartyMembersForQuest`: Muestra cuántos requisitos y miembros hay, y si se encuentran coincidencias
- ✅ `FindStepIndex`: Muestra qué método se usa para encontrar el step
- ✅ Logs de warning cuando no se encuentra un step o el `memberId` está vacío

## 🎯 Cómo Usar

### En el Inspector de Unity (NPCBehaviourManagerV2)

Para configurar un requisito de miembro del equipo en una quest:

1. Ve a **Configuration > Quest Config > Quest Chain**
2. Selecciona la quest entry correspondiente
3. En **Required Party Members**, añade un nuevo elemento:
   - **Member Id**: El `persistenceId` del NPC o su `gameObject.name`
   - **Step Index**: El índice del step (0, 1, 2...) que se completará
   - **Step Condition Id**: (Opcional) Si prefieres usar un ID de condición en lugar del índice

### Ejemplo de Configuración

**Opción 1: Usar Step Index (Recomendado)**
```
Member Id: "NPC_Sabio_Estelar"
Step Index: 1
Step Condition Id: (vacío)
```

**Opción 2: Usar Condition Id**
```
Member Id: "NPC_Sabio_Estelar"
Step Index: -1
Step Condition Id: "PARTY_NPC_Sabio_Estelar"
```

**Nota**: Si dejas ambos vacíos, se auto-generará el Condition Id como `PARTY_{memberId}`.

## 🔍 Testing

Para verificar que funciona:

1. **Activa la quest** que requiere un miembro del equipo
2. **Recluta al NPC** configurado en `requiredPartyMembers`
3. **Verifica en la consola**:
   ```
   [QuestManager] 👥 Miembro unido: persistenceId='NPC_Sabio_Estelar', gameObject='Sabio_Estelar'
   [QuestManager] ✅ Match encontrado por persistenceId: 'NPC_Sabio_Estelar' == 'NPC_Sabio_Estelar'
   [QuestManager.FindStepIndex] Usando stepIndex directo: 1 para quest 'quest_ejemplo'
   [QuestManager] 🎉 Completando step 1 de quest 'quest_ejemplo' por miembro 'NPC_Sabio_Estelar'
   ```

## 📝 Archivos Modificados

- ✅ **QuestManager.cs**:
  - Método `OnPartyMemberJoined` mejorado
  - Método `FindStepIndex` corregido
  - Método `CheckPartyMembersForQuest` con más logs

## 🎉 Resultado

Ahora la verificación de miembros del equipo funciona correctamente:

- ✅ Se detecta cuando un NPC se une al equipo
- ✅ Se completa automáticamente el step de la quest correspondiente
- ✅ Funciona tanto con `persistenceId` como con `gameObject.name`
- ✅ Prioriza correctamente `stepIndex` sobre `conditionId`
- ✅ Logs detallados para debugging

## 🚀 Fecha de Fix

**4 de febrero de 2026**
