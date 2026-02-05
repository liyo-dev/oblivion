# ✅ SOLUCIÓN COMPLETA: Party Members en Quests

## 🎯 Problema Resuelto
Cuando un NPC se une al equipo como requisito de una quest, el step correspondiente **no se completaba automáticamente** al volver al NPC que dio la quest.

## ⭐ Solución Automática Implementada

### Para NPCs con `NPCQuestConfig` (TU CASO)

**✨ ¡Funciona automáticamente! No necesitas hacer nada.**

Ahora, cada vez que el jugador **habla con el NPC que dio la quest**, se verifica automáticamente:
- ✅ Items requeridos en el inventario
- ✅ **Party members requeridos en el equipo**
- ✅ Todos los steps se marcan automáticamente antes de evaluar la completitud

**Modificaciones realizadas**:
- `NPCQuestConfig.HandleQuestState()` → Llama a `CheckAndMarkPartyMemberRequirements()`
- Nuevo método que verifica si los NPCs están en el party y marca los steps correspondientes

## 📋 Checklist de Configuración

Para que funcione correctamente, asegúrate de:

### 1. En el NPC que se UNE al party (ej: Estela):
- [ ] Tiene componente `NPCPartyMember` (se agrega automáticamente al ejecutar JoinParty)
- [ ] Tiene `persistenceId` configurado en su `interactiveNarrativeConfig`
  - Ejemplo: `persistenceId = "Estela"`

### 2. En el QuestChainEntry (del NPC que DA la quest):
- [ ] Configurado `requiredPartyMembers` con:
  - `memberId`: Debe coincidir **exactamente** con el `persistenceId` del NPC
    - ✅ Correcto: `memberId = "Estela"` (mismo que persistenceId)
    - ❌ Incorrecto: `memberId = "estela"` (mayúsculas/minúsculas)
    - ❌ Incorrecto: `memberId = "NPC_Estela"` (nombre del GameObject)
  - `stepIndex`: Índice del step que se completa (empezando en 0)
    - Ejemplo: Si es el segundo step, `stepIndex = 1`

### 3. Configuración de la Quest:
```
Quest: "Encuentra a Estela"
Steps:
  0: "Ir al bosque" (trigger en zona)
  1: "Tener a Estela en el equipo" ← Este se completa automáticamente

requiredPartyMembers:
  - memberId: "Estela"           # ← Debe coincidir con persistenceId
    stepIndex: 1                  # ← Step que se completa
    stepConditionId: ""           # No necesario si stepIndex está configurado
```

## 🔍 Cómo Verificar que Funciona

### 1. Cuando el jugador ENCUENTRA a Estela:
Busca en Unity Console (opcional, evento reactivo):
```
[QuestManager] 👥 ===== OnPartyMemberJoined DISPARADO =====
[QuestManager] 👥 Miembro unido: persistenceId='Estela', gameObject='NPC_Estela'
[QuestManager] 👥 Quests activas: 1
```

### 2. Cuando el jugador VUELVE al NPC que dio la quest:
Busca en Unity Console (verificación automática):
```
[NPCQuestConfig] 🔍 Verificando 1 party members requeridos (party tiene 2 miembros)
[NPCQuestConfig] Verificando member 'Estela': ✅ EN PARTY
[NPCQuestConfig] ✅ Marcando step 1 de quest 'QUEST_FIND_ESTELA' como completado
```

Si ves "❌ NO EN PARTY", verifica:
- El `persistenceId` del NPC Estela
- El `memberId` en el `QuestChainEntry`
- Que Estela realmente esté en el party (usa el debug del PlayerParty)

## 🐛 Troubleshooting

### ❌ "NO EN PARTY" pero Estela está en el equipo
**Causa**: El `memberId` no coincide con el `persistenceId`

**Solución**:
1. Selecciona el NPC "Estela" en la escena
2. Busca su `NPCInteractiveNarrativeConfig` (en el NPCBehaviourManagerV2)
3. Anota el `persistenceId` exacto (ej: "Estela")
4. Abre el `NPCQuestConfig` del NPC que da la quest
5. En `requiredPartyMembers[0].memberId`, pon **exactamente** el mismo valor
6. Respeta mayúsculas/minúsculas

### ❌ "Step -1" o no encuentra el step
**Causa**: El `stepIndex` está mal configurado

**Solución**:
1. Abre el `QuestData` de tu quest
2. Cuenta los steps (empezando en 0)
3. Identifica cuál es el step de "tener a Estela en el equipo"
4. Pon ese índice en `requiredPartyMembers[0].stepIndex`

Ejemplo:
- Step 0: "Ir al bosque"
- Step 1: "Tener a Estela" ← `stepIndex = 1`

### ❌ No se completa la quest
**Causa**: El step se marca pero la quest no se completa

**Verificar**:
1. Que TODOS los steps estén completados (no solo el de party member)
2. Que el `completionMode` del `QuestChainEntry` sea correcto:
   - `CompleteOnTalkIfStepsReady`: Completa cuando todos los steps están listos
   - `AutoCompleteOnTalk`: Completa automáticamente al hablar
   - `Manual`: Requiere llamada manual a `CompleteQuest`

## 🎮 Flujo Completo de Ejemplo

1. **Jugador habla con el NPC Quest Giver**
   - Se inicia la quest "Encuentra a Estela"
   - Step 0: "Ir al bosque" (objetivo activo)

2. **Jugador va al bosque**
   - Trigger completa el Step 0 ✅
   - Aparece Estela

3. **Estela se une al party**
   - Evento `OnPartyMemberJoined` se dispara (verificación reactiva)
   - Si la quest está activa, se completa el Step 1 ✅
   - Si la quest NO está activa aún, se marca pendiente

4. **Jugador vuelve al NPC Quest Giver**
   - `NPCQuestConfig.HandleQuestState()` se ejecuta
   - **Verificación automática de party members** ← NUEVO
   - Se marca el Step 1 como completado ✅ (si no lo estaba)
   - Se evalúa si todos los steps están completados
   - Si todos están listos → Quest completada 🎉

## 📁 Archivos Modificados

### Principal:
- `Assets/Scripts/Behaviour NPC/Modules/NPCQuestConfig.cs`
  - `HandleQuestState()` → Agregada llamada a verificación
  - `CheckAndMarkPartyMemberRequirements()` → Nuevo método

### Secundarios (para debugging y alternativas):
- `Assets/Scripts/Quests/QuestManager.cs` → Logs mejorados
- `Assets/Scripts/Behaviour NPC/Modules/NarrativeChainEntry.cs` → Enum CheckPartyMembers
- `Assets/Scripts/Behaviour NPC/Modules/NPCInteractiveNarrativeExecutor.cs` → Handler CheckPartyMembers
- `Assets/Scripts/Quests/QuestPartyMemberChecker.cs` → Componente helper (creado)

---

## 🎉 Resumen

**Tu problema está solucionado automáticamente**. Cada vez que el jugador hable con el NPC que dio la quest, se verificará si tiene a Estela (o cualquier otro NPC) en el equipo y se completará el step correspondiente.

**Solo asegúrate de que el `memberId` coincida exactamente con el `persistenceId` del NPC.**

**Fecha**: 2025-02-05  
**Estado**: ✅ Implementado y funcionando  
**Requiere acción del usuario**: Solo configuración (checklist arriba)
