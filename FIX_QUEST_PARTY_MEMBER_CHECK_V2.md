# FIX: Verificación de Party Members en Quests

## 📋 Problema Identificado

Cuando un NPC se une al equipo del jugador como parte de una quest que requiere ese NPC en el party, el step correspondiente de la quest **no se completaba automáticamente**.

### Escenario del Bug:
1. **Quest activa**: "Ve al bosque y encuentra a Estela"
2. **Step 1**: Llegar al bosque ✅ (se completa correctamente)
3. **Step 2**: Tener a Estela en el equipo ❌ (NO se completaba)
4. El jugador regresa con el NPC que dio la quest, pero la quest no se puede completar

### Posibles Causas:
1. **Timing del evento**: El evento `OnMemberJoined` se dispara pero la quest no está activa todavía
2. **ID del miembro no coincide**: El `memberId` configurado en la quest no coincide con el `persistenceId` o `gameObject.name` del NPC
3. **Evento no se dispara**: En algunos casos, el evento puede no dispararse correctamente
4. **Verificación no reactiva**: Al volver al NPC que dio la quest, no se vuelve a verificar si los requisitos se cumplen

## 🔧 Soluciones Implementadas

### ⭐ 1. **Verificación Automática en NPCQuestConfig** (PRINCIPAL)

**Esta es la solución principal y automática para la mayoría de los casos.**

Cuando el jugador habla con el NPC que tiene `NPCQuestConfig`, ahora se verifica automáticamente:
- ✅ Items requeridos en el inventario
- ✅ **Party members requeridos en el equipo** (NUEVO)
- ✅ Todos los steps se marcan automáticamente antes de evaluar si la quest se puede completar

**Modificaciones**:
- `NPCQuestConfig.HandleQuestState()`: Ahora llama a `CheckAndMarkPartyMemberRequirements()`
- Nuevo método `CheckAndMarkPartyMemberRequirements()`: Verifica los miembros del party y marca steps

**¿Cuándo se aplica?**
- Cuando el NPC que **da la quest** usa `NPCQuestConfig` (sin `NPCInteractiveNarrativeConfig`)
- Se ejecuta automáticamente cada vez que el jugador habla con el NPC
- No requiere configuración adicional

### 2. **Logs de Debugging Mejorados en QuestManager**
Se agregaron logs exhaustivos en el método `OnPartyMemberJoined` para identificar exactamente dónde falla la verificación:

```csharp
private void OnPartyMemberJoined(Game.NPC.NPCPartyMember member)
{
    // Logs detallados de:
    // - persistenceId y gameObject.name del NPC
    // - Quests activas disponibles
    // - Requisitos de party members de cada quest
    // - Resultado de cada comparación
    // - Steps encontrados y su estado
}
```

**Uso**: Revisa los logs de Unity Console para ver exactamente qué está pasando cuando un NPC se une al party.

### 2. **Método Público para Forzar Verificación**
Se agregó el método `ForceCheckPartyMembersForActiveQuests()` en `QuestManager`:

```csharp
public void ForceCheckPartyMembersForActiveQuests()
```

Este método:
- Verifica todas las quests activas
- Compara los miembros actuales del party con los requisitos
- Completa los steps correspondientes si se cumplen las condiciones
- Es **idempotente**: Se puede llamar múltiples veces sin problemas

**Uso**: Se puede llamar manualmente desde:
- Nodos de diálogo
- Scripts personalizados
- Eventos de Unity
- El nuevo componente `QuestPartyMemberChecker`

### 3. **Componente QuestPartyMemberChecker**
Nuevo componente que se puede agregar a NPCs que dan quests:

**Ubicación**: `Assets/Scripts/Quests/QuestPartyMemberChecker.cs`

**Características**:
- Se puede configurar para verificar automáticamente al iniciar diálogo
- Método público `CheckPartyMembersForQuests()` que se puede llamar desde UnityEvents
- Método `OnPlayerEnter()` para triggers de proximidad
- Debug mode para ver qué está haciendo

**Uso**:
```csharp
// Desde un trigger
void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Player"))
    {
        GetComponent<QuestPartyMemberChecker>()?.CheckPartyMembersForQuests();
    }
}

// Desde un UnityEvent
// Asignar CheckPartyMembersForQuests() en el Inspector
```

### 4. **Nueva Acción Narrativa: CheckPartyMembers**
Se agregó un nuevo tipo de acción en el sistema narrativo:

**Enum**: `NarrativeActionType.CheckPartyMembers`

**Uso en cadenas narrativas**:
```
1. Diálogo: "¿Encontraste a Estela?"
2. CheckPartyMembers (verifica automáticamente)
3. Diálogo: "¡Excelente! Completaste la misión"
```

Esta acción se ejecuta automáticamente en la secuencia narrativa y llama a `ForceCheckPartyMembersForActiveQuests()`.

## 🎯 Cómo Usar las Soluciones

### ✨ Si tu NPC usa NPCQuestConfig (SIN NPCInteractiveNarrativeConfig)

**¡No necesitas hacer nada!** La verificación es completamente automática.

La verificación se ejecuta automáticamente cada vez que el jugador habla con el NPC. Solo asegúrate de:
- [ ] Configurar correctamente los `requiredPartyMembers` en el `QuestChainEntry`
- [ ] El `memberId` coincide con el `persistenceId` del NPC que se une al party
- [ ] El `stepIndex` apunta al step correcto

### 🎭 Si tu NPC usa NPCInteractiveNarrativeConfig (CON sistema narrativo)

Tienes 3 opciones:

#### Opción 1: Agregar acción CheckPartyMembers en el diálogo del NPC
1. Abre el `NPCInteractiveNarrativeConfig` del NPC que da la quest
2. En la narrativa que se ejecuta cuando el jugador vuelve con el NPC
3. Agrega una acción de tipo `CheckPartyMembers` ANTES del diálogo de completar
4. Esto verificará automáticamente si los requisitos se cumplen

### Opción 2: Agregar componente QuestPartyMemberChecker
1. Selecciona el GameObject del NPC que da la quest
2. Add Component → `QuestPartyMemberChecker`
3. Activa `Check On Dialogue Start`
4. El componente verificará automáticamente cada vez que se inicie el diálogo

### Opción 3: Llamar manualmente desde código
```csharp
if (QuestManager.Instance != null)
{
    QuestManager.Instance.ForceCheckPartyMembersForActiveQuests();
}
```

## 🔍 Debugging

### Para NPCs con NPCQuestConfig:
Cuando el jugador habla con el NPC, busca en Unity Console:
```
[NPCQuestConfig] 🔍 Verificando 1 party members requeridos (party tiene 2 miembros)
[NPCQuestConfig] Verificando member 'Estela': ✅ EN PARTY
[NPCQuestConfig] ✅ Marcando step 1 de quest 'QUEST_FIND_ESTELA' como completado
```

### Para sistema reactivo (OnPartyMemberJoined):
Cuando el NPC se une al party, busca:
```
[QuestManager] 👥 ===== OnPartyMemberJoined DISPARADO =====
[QuestManager] 👥 Miembro unido: persistenceId='Estela', gameObject='NPC_Estela'
```

Compara estos IDs con el `memberId` configurado en el `QuestChainEntry` de tu quest.

### Paso 2: Verificar que la quest esté activa
```
[QuestManager] 👥 Quests activas: 1
[QuestManager] 👥 Verificando quest 'QUEST_FIND_ESTELA' (Estado: Active)
```

Si sale 0 quests activas, el problema es que la quest no está activa cuando el NPC se une al party.

### Paso 3: Verificar la coincidencia de IDs
```
[QuestManager] 👥 ✅ Match encontrado por persistenceId: 'Estela' == 'Estela'
```
o
```
[QuestManager] 👥 ❌ NO match: esperaba 'Estela', recibido persistenceId='estela' / gameObject='NPC_Estela'
```

Si no hay match, corrige el `memberId` en el `QuestChainEntry`.

### Paso 4: Verificar el step index
```
[QuestManager] 👥 FindStepIndex devolvió: 1
[QuestManager] 👥 Step encontrado en índice 1, completed=False
[QuestManager] 👥 🎉 ¡COMPLETANDO STEP 1 de quest 'QUEST_FIND_ESTELA'!
```

Si el step index es -1, el problema es la configuración del `stepIndex` o `stepConditionId`.

## ✅ Checklist de Configuración

Para que funcione correctamente:

- [ ] El NPC tiene componente `NPCPartyMember`
- [ ] El NPC tiene `persistenceId` configurado en `interactiveNarrativeConfig`
- [ ] La quest tiene un `PartyMemberRequirement` con el `memberId` correcto
- [ ] El `stepIndex` del requirement apunta al step correcto (empezando en 0)
- [ ] La quest está activa cuando el NPC se une al party
- [ ] (Opcional) El NPC que da la quest tiene acción `CheckPartyMembers` o componente `QuestPartyMemberChecker`

## 📝 Notas Adicionales

### Diferencia entre OnPartyMemberJoined y ForceCheck:
- **OnPartyMemberJoined**: Evento reactivo que se dispara automáticamente cuando un NPC se une
- **ForceCheckPartyMembers**: Método manual que verifica TODOS los miembros actuales del party

Si el evento reactivo falla por timing o configuración, el método manual es una red de seguridad.

### Idempotencia:
Ambos métodos son **idempotentes**: si un step ya está completado, no hace nada. Es seguro llamarlos múltiples veces.

### Performance:
`ForceCheckPartyMembers` es seguro de llamar incluso con muchas quests activas. Solo procesa quests que tienen `requiredPartyMembers` configurados.

## 🐛 Si el Problema Persiste

1. **Revisa los logs** con los filtros detallados implementados
2. **Verifica la configuración** en el Inspector:
   - `NPCInteractiveNarrativeConfig.persistenceId`
   - `QuestChainEntry.requiredPartyMembers[].memberId`
   - `QuestChainEntry.requiredPartyMembers[].stepIndex`
3. **Prueba manualmente** llamando a `ForceCheckPartyMembersForActiveQuests()` desde el código
4. **Usa el componente** `QuestPartyMemberChecker` como solución rápida
5. **Comparte los logs** completos para análisis más detallado

## 📁 Archivos Modificados/Creados

### Modificados:
- `Assets/Scripts/Behaviour NPC/Modules/NPCQuestConfig.cs` ⭐ **PRINCIPAL**
  - Nuevo método `CheckAndMarkPartyMemberRequirements()`
  - Modificado `HandleQuestState()` para llamar a la verificación automáticamente
  - Se ejecuta cada vez que el jugador habla con el NPC

- `Assets/Scripts/Quests/QuestManager.cs`
  - Logs mejorados en `OnPartyMemberJoined`
  - Nuevo método `ForceCheckPartyMembersForActiveQuests()`

- `Assets/Scripts/Behaviour NPC/Modules/NarrativeChainEntry.cs`
  - Nuevo enum: `NarrativeActionType.CheckPartyMembers`

- `Assets/Scripts/Behaviour NPC/Modules/NPCInteractiveNarrativeExecutor.cs`
  - Nuevo case en `ExecuteAction`
  - Nuevo método `ExecuteCheckPartyMembers()`

### Creados:
- `Assets/Scripts/Quests/QuestPartyMemberChecker.cs`
  - Componente auxiliar para verificación automática (NPCs con sistema narrativo)

## 🎮 Ejemplo de Configuración Completa

### Quest: "Encuentra a Estela"
```
Steps:
  0: Ir al bosque (trigger en zona)
  1: Tener a Estela en el equipo (party member check)

requiredPartyMembers:
  - memberId: "Estela"           // Debe coincidir con persistenceId del NPC
    stepIndex: 1                  // Step que se completa
    stepConditionId: ""           // No necesario si stepIndex está configurado
```

### NPC: "Estela"
```
NPCInteractiveNarrativeConfig:
  persistenceId: "Estela"         // ¡IMPORTANTE! Debe coincidir con memberId

Componentes:
  - NPCBehaviourManagerV2
  - NPCPartyMember (se agrega automáticamente al ejecutar JoinParty)
  - NPCInteractiveNarrativeExecutor
```

### NPC: "Quest Giver" (el que da la quest)
```
Narrativa cuando vuelves:
  1. Action: CheckPartyMembers   // ← NUEVO: Verifica antes del diálogo
  2. Action: Dialogue
     Conditions:
       - QuestStepCompleted: QUEST_FIND_ESTELA, step 1
     Text: "¡Bien hecho! Encontraste a Estela"
```

---

**Fecha**: 2026-02-05
**Versión**: 1.0
**Estado**: ✅ Implementado y probado
