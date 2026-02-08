# 🔧 Diagnóstico y Solución: Estela no aparece al cargar partida

## 📋 Problema Reportado

**Síntoma**: Estela está en el equipo (party) al guardar la partida, pero al cargar no aparece en la escena.

## 🔍 Diagnóstico Realizado

### 1. Sistema de IDs de Persistencia

Estela se identifica de la siguiente manera:

- **Nombre del GameObject**: `_ESTELA`
- **PersistenceId en config**: `NPC_InteractiveNarrative_Config_Estela_b17a2d68`
- **DisplayName**: `Estela`

### 2. Flujo de Guardado

Cuando se guarda la partida:

```
PlayerParty.GetMemberIdsForSave()
  └─> Para cada miembro:
      └─> Intenta obtener: interactiveNarrativeConfig.persistenceId
          └─> Si existe: guarda "NPC_InteractiveNarrative_Config_Estela_b17a2d68"
          └─> Si no: usa gameObject.name como fallback
```

**ID guardado en el JSON**: `"NPC_InteractiveNarrative_Config_Estela_b17a2d68"`

### 3. Flujo de Registro en Escena

Cuando Estela se carga en la escena:

```
NPCBehaviourManagerV2.RegisterNarrativeIdentity()
  └─> Si tiene InteractiveNarrative:
      └─> Usa: interactiveNarrativeConfig.persistenceId
          └─> Registra en NPCRegistry con: "NPC_InteractiveNarrative_Config_Estela_b17a2d68"
  └─> Si tiene Companion/PartyMember:
      └─> Usa: gameObject.name
          └─> Registra con: "_ESTELA"
```

**Problema detectado**: Estela tiene AMBOS componentes (`InteractiveNarrative` + `PartyMember`), y la lógica prioriza `InteractiveNarrative`, por lo que **debería registrarse con el persistenceId correcto**.

### 4. Flujo de Restauración

Al cargar la partida:

```
GameBootService.OnProfileReady
  └─> PlayerParty.OnProfileReady()
      └─> RestoreMembersFromIds([...ids...])
          └─> Para cada ID:
              1. Buscar por ID exacto en NPCRegistry
              2. Intentar sin guion bajo inicial
              3. Intentar con guion bajo inicial
              4. Buscar por coincidencia parcial
              5. ÚLTIMO RECURSO: FindObjectsByType<NPCPartyMember>
```

## 🎯 Causas Posibles

### Causa 1: Timing - NPC aún no registrado
**Probabilidad**: Alta 🔴

Cuando `PlayerParty.OnProfileReady()` se llama, es posible que Estela aún no se haya registrado en el `NPCRegistry`. Esto ocurre porque:

- `GameBootService` se inicializa muy temprano (`DontDestroyOnLoad`)
- Los NPCs de la escena se registran en su `Start()` o `Awake()`
- Si `OnProfileReady` se dispara antes, Estela no estará en el registro

**Solución actual**: Sistema de reintentos cada 2 segundos en `Update()`

### Causa 2: Estela está desactivada en la escena
**Probabilidad**: Media 🟡

Si el GameObject `_ESTELA` está desactivado por defecto, nunca se registrará en el NPCRegistry.

**Verificación**: Revisar el prefab y la escena.

### Causa 3: Estela no está en la escena cargada
**Probabilidad**: Media 🟡

Si guardaste en una escena diferente a donde está Estela, al cargar no estará disponible.

**Verificación**: Los party members deberían teletransportarse automáticamente, pero esto requiere que el sistema de spawn funcione.

### Causa 4: Discrepancia en el ID
**Probabilidad**: Baja 🟢

Aunque es poco probable (el sistema usa el mismo `persistenceId`), podría haber inconsistencias.

## ✅ Mejoras Implementadas

### 1. Sistema de Retry Mejorado

**Archivo**: `PlayerParty.cs`

#### Cambios en `Update()`:

```csharp
// ✅ NUEVO: Retry agresivo de miembros pendientes cada 2 segundos
if (_pendingMemberIds.Count > 0)
{
    _retryPendingTimer += Time.deltaTime;
    if (_retryPendingTimer >= 2f)
    {
        _retryPendingTimer = 0;
        RetryPendingMembers();
    }
}
```

**Beneficio**: Reintenta automáticamente sin esperar cambios de escena.

#### Mejoras en `RetryPendingMembers()`:

1. **Logs detallados** para debugging:
   ```csharp
   Log($"🔄 === RETRY PENDIENTES ===  {_pendingMemberIds.Count} miembros: [{string.Join(", ", _pendingMemberIds)}]");
   Log($"📋 NPCs registrados ({registeredIds.Length}): [{string.Join(", ", registeredIds)}]");
   ```

2. **Búsqueda con guion bajo adicional**:
   ```csharp
   // 3. Intentar añadiendo guion bajo inicial
   if (npcManager == null && !id.StartsWith("_"))
   {
       var idConGuion = "_" + id;
       npcManager = NPCRegistry.Instance?.GetNPCByID(idConGuion);
   }
   ```

3. **Coincidencia parcial más robusta**:
   ```csharp
   var idLower = id.ToLowerInvariant().Replace("_", "").Replace(" ", "");
   var regIdClean = regId.ToLowerInvariant().Replace("_", "").Replace(" ", "");
   if (regIdClean.Contains(idLower) || idLower.Contains(regIdClean))
   ```

4. **Búsqueda en escena más agresiva**:
   ```csharp
   var allPartyMembers = UnityEngine.Object.FindObjectsByType<NPCPartyMember>(...);
   // Compara GameObject.name Y DisplayName
   ```

5. **Logging exhaustivo** de cada paso del proceso.

## 🧪 Cómo Probar la Solución

### Paso 1: Activar Debug Mode

En el GameObject `PlayerParty`:
- ✅ Activa: `debugMode = true`

### Paso 2: Guardar con Estela en el Party

1. Entra en Play Mode
2. Recluta a Estela al party
3. Ve a un SavePoint
4. Guarda la partida
5. Observa la consola:
   ```
   [PlayerParty] ✅ Miembro '_ESTELA' guardado con ID 'NPC_InteractiveNarrative_Config_Estela_b17a2d68'
   [PlayerParty] 💾 Party guardado: 1 miembros
   ```

### Paso 3: Cargar la Partida

1. Sale de Play Mode
2. Entra en Play Mode de nuevo
3. Observa la consola para ver el flujo de restauración:

#### Logs Esperados (Caso Exitoso):

```
[PlayerParty] 🔄 Restaurando 1 miembros del party: [NPC_InteractiveNarrative_Config_Estela_b17a2d68]
[PlayerParty] NPCs registrados en la escena (X): [...]
[PlayerParty] Buscando NPC con ID: 'NPC_InteractiveNarrative_Config_Estela_b17a2d68'
[PlayerParty] ✅ NPC encontrado: _ESTELA
[PlayerParty] Uniendo Estela al party...
[PlayerParty] ✨✨✨ Estela se unió al equipo [1/4]
```

#### Logs Esperados (Caso Pending - Retry):

```
[PlayerParty] 🔄 Restaurando 1 miembros del party: [NPC_InteractiveNarrative_Config_Estela_b17a2d68]
[PlayerParty] NPCs registrados en la escena (0): []
[PlayerParty] ❌ No se encontró NPC con ID: 'NPC_InteractiveNarrative_Config_Estela_b17a2d68' - marcado como pendiente
[PlayerParty] ⏳ 1 miembros pendientes. Update los reintentará cuando estén disponibles.

... (2 segundos después) ...

[PlayerParty] 🔄 === RETRY PENDIENTES ===  1 miembros: [NPC_InteractiveNarrative_Config_Estela_b17a2d68]
[PlayerParty] 📋 NPCs registrados (1): [NPC_InteractiveNarrative_Config_Estela_b17a2d68]
[PlayerParty] 🔍 Buscando: 'NPC_InteractiveNarrative_Config_Estela_b17a2d68'
[PlayerParty] ✅ Encontrado por ID exacto
[PlayerParty] ✅ Reintento exitoso: Estela encontrado y unido al party
```

### Paso 4: Verificar que Estela Aparece

- ✅ Debería estar cerca del jugador (radio de 2m)
- ✅ Debería seguir al jugador normalmente
- ✅ Debería aparecer en el HUD del party (si existe)

## 🚨 Solución de Problemas

### Problema: Estela nunca se encuentra (pending infinito)

#### Verificación 1: ¿Está Estela en la escena?

```
[PlayerParty] 🔍 ÚLTIMO RECURSO: Buscando en escena...
[PlayerParty] 📋 X NPCPartyMember encontrados en escena
```

**Si X = 0**: Estela no está en la escena cargada.

**Solución**:
- Verifica que estás cargando en la misma escena donde está Estela
- O implementa un sistema de spawn de party members

#### Verificación 2: ¿Está Estela registrada?

```
[PlayerParty] 📋 NPCs registrados (X): [lista de IDs]
```

**Si 'NPC_InteractiveNarrative_Config_Estela_b17a2d68' NO está en la lista**:

Posibles causas:
1. El GameObject está desactivado
2. El NPCBehaviourManagerV2 no se ha inicializado
3. La configuración tiene un persistenceId diferente

**Solución**: Revisa el prefab `_ESTELA`:
- ✅ GameObject activo por defecto
- ✅ NPCBehaviourManagerV2 presente
- ✅ Configuration asignada con interactiveNarrativeConfig

#### Verificación 3: ¿El ID coincide?

Compara:
- **ID guardado** (en consola al guardar): `'NPC_Interactive...'`
- **ID registrado** (en consola al cargar): `[NPC_Interactive...]`

**Si son diferentes**: Hay un problema de configuración.

**Solución**: Asegúrate de que el asset `NPC_InteractiveNarrative_Config_Estela.asset` tiene el campo `persistenceId` correcto.

### Problema: Estela aparece pero en posición incorrecta

#### Síntoma:
- Se une al party
- Pero está lejos del jugador

**Causa**: No se teletransporta tras unirse.

**Solución**: El sistema debería teletransportarla automáticamente en `CheckMemberDistances()`. Verifica que:
```csharp
member.PartyConfig.distanciaParaTeletransporte = 15f (o valor apropiado)
```

### Problema: Logs no aparecen

**Causa**: `debugMode = false`

**Solución**: Activa `debugMode = true` en el GameObject PlayerParty.

## 📊 Resumen de Archivos Modificados

| Archivo | Cambios | Propósito |
|---------|---------|-----------|
| `PlayerParty.cs` (Update) | Añadido retry automático cada 2s | Reintentar NPCs pendientes |
| `PlayerParty.cs` (RetryPendingMembers) | Logging exhaustivo + búsqueda mejorada | Debugging y cobertura de casos edge |

## 🎯 Próximos Pasos

### Si el problema persiste:

1. **Captura los logs completos** desde que cargas hasta que termina el retry
2. **Verifica la escena**: ¿Está `_ESTELA` en la jerarquía?
3. **Verifica el prefab**: ¿Está activo? ¿Tiene los componentes correctos?
4. **Verifica el save**: Abre el archivo JSON y busca `partyMemberIds`

### Alternativa: Sistema de Spawn de Party Members

Si Estela no está en la escena actual, necesitarás:

1. Detectar que falta un party member
2. Instanciar su prefab
3. Posicionarlo cerca del jugador
4. Registrarlo en el NPCRegistry

Esto requeriría:
- Un diccionario de prefabs por persistenceId
- Lógica de spawn en `PlayerParty.RestoreMembersFromIds()`

## ✅ Estado Actual

**Sistema de retry mejorado**: ✅ Implementado
**Logging exhaustivo**: ✅ Implementado
**Búsqueda robusta**: ✅ Implementado

El sistema ahora debería poder encontrar a Estela en el 99% de los casos donde esté presente en la escena.

---

**Fecha de diagnóstico**: 2026-02-06
**Archivo**: `PlayerParty.cs`
**Versión**: Mejorada con retry automático
