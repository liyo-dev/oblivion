# Fix: Party desaparece al volver a cargar desde menú principal

## 🔴 PROBLEMA DETECTADO

**Situación:**
1. ✅ Cargas una partida guardada → Estela está en el equipo → Todo funciona
2. ✅ Sigues jugando, guardas → Estela sigue en el equipo → OK
3. ❌ Sales al menú principal y vuelves a cargar la misma partida → **Estela NO está en el equipo**

**El JSON de guardado muestra que Estela debería estar en el party**, pero al recargar desde el menú, desaparece.

---

## 🔍 CAUSA RAÍZ

El problema era un **fallo en la lógica de restauración del party al cambiar de escena**:

### Flujo normal (primera carga):
1. `GameBootService.Awake()` carga el save y dispara `OnProfileReady` ✅
2. `PlayerParty.OnProfileReady()` lee `preset.partyMemberIds` desde el save ✅
3. `RestoreMembersFromIds()` busca los NPCs y los añade al party ✅
4. Si los NPCs no están todavía, se marcan como pendientes y `Update()` los reintenta ✅

### Flujo problemático (recarga desde menú):
1. Juegas con Estela en el party ✅
2. Sales al menú principal → **Los NPCs se destruyen** (no tienen `DontDestroyOnLoad`)
3. `PlayerParty` persiste (tiene `DontDestroyOnLoad`), pero `_members` tiene **referencias null** ❌
4. Cargas la partida otra vez → **`OnProfileReady` NO se dispara** (solo se dispara en `Awake()` inicial)
5. `PlayerParty.OnSceneLoaded()` se ejecuta, pero **NO limpiaba los miembros null ni reintentaba restaurar** ❌
6. Resultado: El party queda vacío ❌

**El problema**: `OnSceneLoaded()` **no verificaba si el party estaba vacío tras limpiar referencias null**, ni intentaba restaurar desde el preset activo.

---

## ✅ SOLUCIÓN IMPLEMENTADA

Se mejoró el método `PlayerParty.OnSceneLoaded()` para:

### 1. **Limpiar referencias null** tras cambio de escena:
```csharp
var nullCount = _members.RemoveAll(m => m == null);
if (nullCount > 0)
{
    Log($"🧹 Limpiados {nullCount} miembros null tras cambio de escena");
}
```

### 2. **Restaurar automáticamente** si el party quedó vacío pero hay IDs en el preset:
```csharp
if (_members.Count == 0)
{
    var profile = GameBootService.Profile;
    if (profile != null)
    {
        var preset = profile.GetActivePresetResolved();
        if (preset != null && preset.partyMemberIds != null && preset.partyMemberIds.Count > 0)
        {
            Log($"🔄 Party vacío tras cambio de escena - Restaurando {preset.partyMemberIds.Count} miembros desde preset");
            RestoreMembersFromIds(preset.partyMemberIds);
        }
    }
}
```

### 3. **Continuar con reintentos** de miembros pendientes:
```csharp
if (_pendingMemberIds.Count > 0)
{
    Log($"🔄 Nueva escena cargada, reintentando restaurar {_pendingMemberIds.Count} miembros pendientes...");
}
```

---

## 📊 ANTES vs DESPUÉS

| Escenario | Antes | Después |
|-----------|-------|---------|
| **Primera carga** | ✅ Funciona | ✅ Funciona |
| **Continuar jugando** | ✅ Funciona | ✅ Funciona |
| **Salir al menú → Recargar** | ❌ Party vacío | ✅ Party restaurado |
| **Cambiar escenas** | ⚠️ Referencias null | ✅ Limpieza automática |
| **NPCs pendientes** | ✅ Retry en Update | ✅ Retry mejorado |

---

## 🎮 CÓMO PROBAR

### Paso 1: Activar Debug Logs
Selecciona el GameObject `PlayerParty` en la jerarquía y marca **"Debug Mode"** en el inspector.

### Paso 2: Cargar una partida con Estela en el party
1. Inicia el juego y carga una partida guardada donde Estela ya esté en el equipo
2. Verifica en la consola:
   ```
   [PlayerParty] 🔄 Restaurando 1 miembros del equipo...
   [PlayerParty] ✅ NPC encontrado: _ESTELA
   [PlayerParty] Restauración completada. Miembros activos: 1, Pendientes: 0
   ```

### Paso 3: Salir al menú principal
1. Abre el menú con ESC
2. Selecciona "Salir al Menú Principal"
3. Verifica en la consola que los miembros se limpian:
   ```
   [PlayerParty] 🧹 Limpiados 1 miembros null tras cambio de escena
   ```

### Paso 4: Volver a cargar la misma partida
1. En el menú principal, haz clic en "Continuar"
2. Verifica en la consola:
   ```
   [PlayerParty] 🔄 Party vacío tras cambio de escena - Restaurando 1 miembros desde preset
   [PlayerParty] Restaurando 1 miembros del equipo...
   [PlayerParty] ✅ NPC encontrado: _ESTELA
   [PlayerParty] ✨✨✨ Estela se unió al equipo [1/4]
   ```

### Paso 5: Verificar en el juego
- Estela debería aparecer en la escena y seguir al jugador
- El UI del party debería mostrar su retrato
- Debería participar en combates

---

## 🔧 DEBUGGING ADICIONAL

Si Estela sigue sin aparecer después del fix:

### 1. Verificar el JSON de guardado
Abre el archivo de guardado (ubicado en `%AppData%/../LocalLow/[CompañíaTuya]/[NombreJuego]/save.json`):

```json
{
  "partyMemberIds": ["_ESTELA"],  // ← Debe aparecer aquí
  ...
}
```

Si `partyMemberIds` está vacío o es null, el problema está en la **escritura del save**, no en la carga.

### 2. Verificar que el preset tiene los IDs
En la consola, busca:
```
[GameBootProfile] 🤝 Party: 1 miembros a restaurar
```

Si dice `0 miembros`, el preset no se está cargando correctamente desde el save.

### 3. Verificar registro de NPCs
Activa `debugMode = true` en PlayerParty y busca:
```
[PlayerParty] NPCs registrados en la escena (X): [_ESTELA, _LIAM, ...]
```

Si `_ESTELA` NO aparece en la lista, el NPC no se está registrando correctamente en `NPCRegistry`.

### 4. Verificar que Estela tiene NPCPartyMember
En el GameObject `_ESTELA` en la jerarquía, debe tener el componente `NPCPartyMember` con:
- ✅ `Party Config` asignado
- ✅ `Auto Join On Start` desactivado (solo se une por script/quest)

---

## 🛠️ ARCHIVOS MODIFICADOS

- ✅ `Assets/Scripts/Behaviour NPC/PlayerParty.cs`
  - Método `OnSceneLoaded()` mejorado con limpieza y restauración automática

---

## 📝 NOTAS TÉCNICAS

### ¿Por qué `OnProfileReady` solo se dispara una vez?
`OnProfileReady` es un evento del `GameBootService` que se dispara **solo en `Awake()`** cuando el servicio se inicializa por primera vez. No se vuelve a disparar en cambios de escena porque el `GameBootService` persiste con `DontDestroyOnLoad`.

### ¿Por qué los NPCs se destruyen pero PlayerParty no?
- `PlayerParty` tiene `DontDestroyOnLoad` en su GameObject, así que persiste entre escenas
- Los NPCs son objetos normales de la escena MainWorld, así que se destruyen al cambiar al menú
- Cuando vuelves a MainWorld, los NPCs se spawnean de nuevo como **nuevas instancias**

### Sistema de retry robusto
El sistema ya tenía un mecanismo de retry en `Update()` que verifica cada 2 segundos si hay miembros pendientes. Esto maneja casos donde los NPCs tardan en spawnearse o registrarse.

El fix complementa esto asegurando que tras un cambio de escena, se **fuerza una restauración** si el party quedó vacío.

---

## ✨ MEJORAS IMPLEMENTADAS

1. **Limpieza automática** de referencias null tras cambio de escena
2. **Restauración automática** desde el preset cuando el party queda vacío
3. **Logs de debug detallados** para rastrear el flujo de restauración
4. **Sistema robusto** que combina restauración inmediata + retry automático

---

**Fecha**: 2026-02-08  
**Archivos modificados**: `Assets/Scripts/Behaviour NPC/PlayerParty.cs`  
**Relacionado con**: Sistema de guardado/carga, persistencia de party members
