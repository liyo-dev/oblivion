# 🚨 CAUSA RAÍZ IDENTIFICADA: Estela no está en la escena

## ❌ Problema Principal

**Estela solo puede unirse al party si está físicamente presente en la escena cargada.**

### Escenario del Bug:

1. **Escena A**: Estela está presente → la reclut as → guardas
2. **Escena B**: Cargas la partida → Estela NO está en esta escena → no puede unirse al party

## 🔍 Evidencia

### Sistema Actual de Restauración:

```csharp
PlayerParty.RestoreMembersFromIds(memberIds)
  └─> Busca NPC en NPCRegistry (solo NPCs en la escena actual)
  └─> Si no existe: marca como "pendiente"
  └─> Reintenta cada 2s... pero NUNCA encontrará un NPC que no está en escena
```

### Logs Esperados del Bug:

```
[PlayerParty] 🔄 Restaurando 1 miembros del party: [NPC_InteractiveNarrative_Config_Estela_b17a2d68]
[PlayerParty] NPCs registrados en la escena (5): [NPC1, NPC2, NPC3, NPC4, NPC5]  ← Estela NO está aquí
[PlayerParty] ❌ No se encontró NPC con ID: 'NPC_InteractiveNarrative_Config_Estela_b17a2d68' - marcado como pendiente
[PlayerParty] ⏳ 1 miembros pendientes. Update los reintentará cuando estén disponibles.

... (reintenta cada 2s indefinidamente) ...

[PlayerParty] 🔄 === RETRY PENDIENTES ===  1 miembros: [NPC_InteractiveNarrative_Config_Estela_b17a2d68]
[PlayerParty] 📋 NPCs registrados (5): [NPC1, NPC2, NPC3, NPC4, NPC5]  ← Sigue sin estar
[PlayerParty] ❌ No encontrado - sigue pendiente
```

## ✅ Soluciones Posibles

### Opción 1: Spawn Dinámico (RECOMENDADO)

Instanciar el prefab de Estela cuando no está en la escena.

#### Ventajas:
- ✅ Funciona en cualquier escena
- ✅ Los party members siempre están contigo
- ✅ Experiencia de juego consistente

#### Desventajas:
- ⚠️ Requiere recursos (prefab registry)
- ⚠️ Lógica adicional de spawn

#### Implementación:

```csharp
// En PlayerParty.cs

[Header("Party Member Prefabs")]
[SerializeField] private PartyMemberPrefabRegistry prefabRegistry;

private void RetryPendingMembers()
{
    // ...búsqueda normal...
    
    if (npcManager == null)
    {
        // NUEVO: Intentar spawnear desde prefab
        if (prefabRegistry != null)
        {
            var prefab = prefabRegistry.GetPrefabById(id);
            if (prefab != null)
            {
                var spawned = Instantiate(prefab, GetSpawnPosition(), Quaternion.identity);
                var partyMember = spawned.GetComponent<NPCPartyMember>();
                if (partyMember != null)
                {
                    partyMember.JoinParty();
                    Log($"✅ Spawneado y unido: {partyMember.DisplayName}");
                    continue;
                }
            }
        }
        stillPending.Add(id);
    }
}
```

**Archivo adicional**: `PartyMemberPrefabRegistry.cs`

```csharp
[CreateAssetMenu(menuName = "Party/Prefab Registry")]
public class PartyMemberPrefabRegistry : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string persistenceId;
        public GameObject prefab;
    }
    
    public List<Entry> entries;
    
    public GameObject GetPrefabById(string id)
    {
        return entries.FirstOrDefault(e => e.persistenceId == id)?.prefab;
    }
}
```

**Configuración**:
1. Crear asset `PartyMemberPrefabRegistry`
2. Añadir entrada: `"NPC_InteractiveNarrative_Config_Estela_b17a2d68"` → Prefab `_ESTELA`
3. Asignar al `PlayerParty` en escena

### Opción 2: DontDestroyOnLoad para Party Members

Hacer que los party members persistan entre escenas.

#### Ventajas:
- ✅ Simple de implementar
- ✅ No requiere registry de prefabs

#### Desventajas:
- ⚠️ Los NPCs persisten aunque no deberían estar en algunas escenas
- ⚠️ Puede causar conflictos si el NPC también está en la nueva escena
- ⚠️ Problemas con NavMesh entre escenas

#### Implementación:

```csharp
// En NPCPartyMember.cs o PlayerParty.cs

public void OnJoinedParty(PlayerParty party)
{
    // ...código existente...
    
    // NUEVO: Persistir entre escenas
    if (transform.parent == null)
    {
        DontDestroyOnLoad(gameObject);
    }
}

public void OnLeftParty()
{
    // ...código existente...
    
    // NUEVO: Eliminar persistencia
    // (opcional: destruir o devolver a la escena)
}
```

**Problema adicional**: Necesitas manejar conflictos si vuelves a la escena original.

### Opción 3: Restricción de Diseño (MÁS SIMPLE)

**Solución conservadora**: Solo permitir guardar en escenas donde todos los party members están presentes.

#### Implementación:

```csharp
// En SavePoint.cs

void DoSave(GameObject playerGo)
{
    // ...código existente...
    
    // NUEVO: Verificar party members
    var party = Game.NPC.PlayerParty.Instance;
    if (party != null && party.MemberCount > 0)
    {
        var allMembersInScene = true;
        foreach (var member in party.Members)
        {
            if (member == null || !member.gameObject.scene.isLoaded)
            {
                allMembersInScene = false;
                break;
            }
        }
        
        if (!allMembersInScene)
        {
            Debug.LogWarning("[SavePoint] ⚠️ No se puede guardar: algunos party members no están en la escena actual");
            // Mostrar mensaje al jugador
            return;
        }
    }
    
    // ...continuar guardado...
}
```

**Ventajas**:
- ✅ Muy simple
- ✅ Sin bugs de spawn/persistencia

**Desventajas**:
- ❌ Limitación artificial
- ❌ Mala experiencia de usuario

### Opción 4: Save Points Solo en "Hub" Scenes

Diseñar el juego para que solo puedas guardar en escenas centrales donde todos los party members están disponibles.

**Ejemplo**: Solo puedes guardar en la "Villa" o "Campamento", nunca en dungeons.

## 🎯 Recomendación

### Para Fix Rápido: Opción 3 (Restricción de Diseño)
Implementar validación en SavePoint para evitar guardar cuando party members están ausentes.

### Para Solución Completa: Opción 1 (Spawn Dinámico)
Crear el `PartyMemberPrefabRegistry` y sistema de spawn automático.

## 📝 Pasos Inmediatos

### 1. Confirmar el Diagnóstico

Ejecuta el juego con logs activos y verifica:

```
[PlayerParty] 📋 NPCs registrados en la escena (X): [lista]
```

**Si Estela NO está en la lista**: Confirmado, no está en la escena.

### 2. Verificación Manual

En el editor:
1. Carga la escena donde guardaste
2. Busca `_ESTELA` en la jerarquía → ✅ debería estar
3. Carga la escena donde intentaste cargar
4. Busca `_ESTELA` en la jerarquía → ❌ probablemente NO está

### 3. Solución Temporal

Mientras decides qué opción implementar:

**Workaround**: Solo guardar en escenas donde Estela está presente.

O bien:

**Quick Fix**: Añadir el prefab `_ESTELA` a TODAS las escenas jugables (oculto/desactivado inicialmente), para que siempre esté disponible.

```
Escena MainWorld:
  - _ESTELA (activo si es su spawn original, inactivo si no)

Escena Dungeon:
  - _ESTELA (inactivo, pero presente para restauración)
```

El sistema de restauración lo activará automáticamente cuando cargue la partida.

## 🔧 Implementación Recomendada del Quick Fix

### Paso 1: Preparar el Prefab

En `_ESTELA.prefab`:
- Asegúrate de que tiene un método para "activarse" correctamente
- Verificar que NPCBehaviourManagerV2 se registra en `OnEnable()`

### Paso 2: Añadir a Escenas

Para cada escena donde puedes guardar:
1. Arrastrar prefab `_ESTELA` a la escena
2. Posicionarlo fuera de la vista (ej: Y = -1000)
3. **Desactivarlo** si no es su spawn original
4. Renombrar a `_ESTELA_ForRestore` (para distinguir)

### Paso 3: Lógica de Activación

```csharp
// En NPCBehaviourManagerV2 o nuevo script

void OnEnable()
{
    // Si este NPC está desactivado pero debe unirse al party tras cargar...
    if (gameObject.name.Contains("ForRestore"))
    {
        // Esperar a que PlayerParty intente restaurar
        StartCoroutine(CheckIfShouldActivate());
    }
}

IEnumerator CheckIfShouldActivate()
{
    yield return new WaitForSeconds(1f);
    
    // Si PlayerParty tiene este ID pendiente, activarse
    var party = Game.NPC.PlayerParty.Instance;
    if (party != null && party._pendingMemberIds.Contains(GetPersistenceId()))
    {
        gameObject.SetActive(true);
        transform.position = party.GetFormationPosition(party.MemberCount);
    }
}
```

**Nota**: Esto requiere exponer `_pendingMemberIds` o crear un método público.

## 🎓 Lecciones Aprendidas

1. **Los sistemas de persistencia cross-scene son complejos**
2. **Los party members necesitan estar disponibles en todas las escenas jugables**
3. **El timing de inicialización es crítico**
4. **Siempre verifica que los NPCs estén en el NPCRegistry antes de intentar restaurarlos**

---

**Próximo paso recomendado**: Implementar el Quick Fix (añadir prefabs desactivados a escenas) mientras desarrollas la solución completa de spawn dinámico.

**Fecha**: 2026-02-06
**Prioridad**: 🔴 Alta
