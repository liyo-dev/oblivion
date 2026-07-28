# CLAUDE.md — El Sendero de las Estrellas

RPG de acción/aventura en Unity 6 (6000.4.6f1) + URP 17.4.0. Proyecto indie en solitario (Raúl Báez).
Toda la documentación técnica detallada está en **`TDD.md`** (fuente de verdad).

---

## 1. Entrada al proyecto

- **Escena de entrada:** `Assets/Scenes/Systems/Start.unity`
- `Start.unity` contiene todos los managers persistentes (`DontDestroyOnLoad`). Siempre debe estar cargada.
- Para testear cualquier escena directamente: abrir y dar Play. `AutoBootstrapOnPlay.cs` carga Start aditivamente de forma automática.
- **Script Execution Order crítico:**
  - `GameBootService` → -1000 (debe ejecutarse primero)
  - `PlayerService` → -900
  - `ServiceLocator` → -800
  - `WorldBootstrap` → +200

---

## 2. Reglas de código — no negociables

**Nunca en `Update` / `LateUpdate` / `FixedUpdate`:**
- `FindObjectOfType` / `FindObjectsByType` — usar registros (`ActiveCombatRegistry`, `PlayerParty`, etc.)
- `GetComponent` sin cachear — siempre en `Awake`
- `Camera.main` — cachear en `Awake`
- `new List<T>()` / `.ToList()` / `.Where(...).ToList()` — usar iteración directa sobre diccionarios
- `LayerMask.GetMask(...)` — cachear en `Awake`
- `animator.parameters` (getter) — cachear hashes en `Awake`
- `StartCoroutine(...)` sin comprobar si ya hay una corriendo — acumula corrutinas
- `SetActive(...)` sin guard de estado previo — dispara layout rebuilds

**VFX de un solo uso (impacto, explosión, despawn):** nunca `Instantiate(...); Destroy(fx, t)` directo — usar `VfxPoolService.Instance.Play(prefab, pos, rot, lifetime)` (`Core/Pooling/VfxPoolService.cs`). Ver TDD.md § 12 "Instancias y pools" para el patrón completo y la lista de sitios aún pendientes de migrar.

**Physics:**
```csharp
// NUNCA:
Collider[] hits = Physics.OverlapSphere(pos, radius);
// SIEMPRE:
private readonly Collider[] _buffer = new Collider[32];
int count = Physics.OverlapSphereNonAlloc(pos, radius, _buffer, layerMask);
```

**Logging:**
```csharp
// Todo Debug.Log de diagnóstico bajo:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
Debug.Log($"[Sistema] ...");
#endif
```

**Reflection:** no usar `System.Reflection` en código de runtime frecuente. Es lento y frágil en IL2CPP.

**Idioma:** comentarios, documentación y mensajes de commit **en español**.

---

## 3. Arquitectura clave

- **Multi-escena aditiva:** Start persiste siempre. Las demás escenas se cargan/descargan dinámicamente.
- **ServiceLocator** (`Core/ServiceLocator.cs`): punto de acceso a singletons globales. Preferir sobre referencias directas. Cachea tras la primera búsqueda.
- **ScriptableObjects como datos:** configuración de NPCs, quests, hechizos, presets. Nunca lógica en SOs.
- **Eventos C# (`Action<T>`)** para comunicación entre sistemas. No referencias directas entre managers.
- **`DontDestroyOnLoad` solo en managers de Start.** El resto pertenece a su escena.

**Patrón obligatorio en todos los singletons con estado estático:**
```csharp
#if UNITY_EDITOR
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics()
{
    _instance = null;
    OnMyStaticEvent = null;
}
#endif
```
Sin esto, las variables estáticas se contaminan entre sesiones de PlayMode en el editor.

**FSM de NPCs:**
```
NPCBehaviourManagerV2 → NPCBrain → NPCStateContext → INPCState
```
Archivos en `Assets/Scripts/Behaviour NPC/`.

**Stack de modos del jugador** (`PlayerActionManager`):
```csharp
PlayerActionManager.Instance.PushMode(ActionMode.Cinematic);
PlayerActionManager.Instance.PopMode(ActionMode.Cinematic);
```
El tope del stack determina el modo activo. Siempre hacer Pop para cada Push.

---

## 4. Invariantes del grafo narrativo — NO violar

El grafo narrativo (`MainNarrative.asset`) y sus runners son `DontDestroyOnLoad`. Estas reglas son críticas:

**Regla 1 — Test mode = vuelco EXACTO del bootPreset, sin mezcla con JSON.**
En `GameBootService.PrepareActivePreset()` modo testeo NUNCA leer el JSON. Solo `EnsureRuntimePresetFromTemplate(bootPreset)` + `ApplyPresetAsLoadedGame()`.

**Regla 2 — Al "cargar partida" en test mode, siempre recargar desde bootPreset.**
`GameBootService.ReloadTestPreset()` hace: (1) `hub.StopAllRunners()`, (2) `EnsureRuntimePresetFromTemplate(bootPreset)`, (3) `ApplyPresetAsLoadedGame()`. `MainMenuController.OnClickContinue()` lo llama en test mode.

**Regla 3 — `NarrativeRunner.StartFromStartNode` / `StartFromNode` deben llamar `StopExecution()` primero.**
Sin esto se acumulan runners paralelos que ejecutan nodos en la sesión incorrecta.

**Regla 4 — NO tocar `WaitQuestCompleteNode` ni la fork detection de `StartFromStartNode`.**
El `Advance()` ya maneja fork detection. El `WaitCustomEventNode` ya tiene su mecanismo `__event_{guid}_{key}_received`. Consultar TDD.md antes de tocar nodos narrativos.

**Regla 5 — `DefaultNarrativeSignals._raised` es el backup persistente de señales.**
`RaiseCustom` añade a `_pending` y `_raised`. `ResetState(preservePending:true)` preserva los dos. No eliminar `_raised`.

**Sobre presets de testing:** si un preset fue capturado después de que un trigger se activó, contiene el flag `__event_XXX_received = 1` en el blackboard. El runner saltará ese nodo. Para resetear, eliminar el flag manualmente del asset del preset.

---

## 5. Bugs conocidos activos (Mayo 2026)

Estos bugs están documentados en **TDD.md § 13**. No introducir más instancias del mismo patrón y no asumir que son accidentales.

### Críticos (todos resueltos — verificado Julio 2026)

El catálogo de Mayo 2026 quedó desactualizado; una pasada de optimización posterior ("Fase 2") corrigió C1-C6 sin volcarlo de vuelta a esta tabla. Antes de "arreglar" algo de aquí, comprobar primero el archivo actual.

- **C1** `Audio/AudioService.cs` — ya no usa `StopAllCoroutines()` en `PlayMusic`. Existe `StopMusicCoroutines()` dedicado que solo detiene las corrutinas de música por referencia explícita, sin tocar el pool SFX (comentario explícito en el código sobre por qué).
- **C2** `Core/SaveSystem.cs` — escritura atómica: escribe a `.tmp` y hace `File.Move` al path final. Un crash a mitad de escritura deja el save anterior intacto.
- **C3** `Quests/QuestManager.cs` — `OnInventoryItemAdded` usa `FindQuestChainEntry`, que consulta un índice cacheado (`_questChainIndex`) reconstruido solo cuando está `dirty` (cambio de escena), no en cada evento de inventario.
- **C4** `Attacks/MagicCaster.cs` — `Update()` itera el array estático `AllSlots` sin allocar.
- **C5** `Player/PlayerBattleModeController.cs` — `DetectEnemiesNearby()` usa `ActiveCombatRegistry.Count`, O(1).
- **C6** `Attacks/MagicProjectil.cs` — el `OverlapSphereNonAlloc` solo se llama al resolver un impacto, no en `Update`.

### Importantes (I1–I17)

Ver tabla completa en TDD.md § 13. **La mayoría ya está resuelta** (verificado Julio 2026): I2, I3, I4, I6, I7, I8, I11, I15, I16, I17. Genuinamente pendientes o sin verificar: I1 (grace period diálogos, UX), I5 (locks residuales en paths de error, impacto ≈0), I9 (reflection cacheada en `PlayerFlyingController`, solo 2 llamadas por vuelo, impacto ≈0), I10 (corrutina sin timeout en GameBootService), I12 (`FogZone.cs` ya no existe, confirmar dónde quedó esa lógica), I13 (`AdditiveSceneCinematic.PlayAndBlock`, correctness), I14 (`Time.timeScale` en SimpleCinematicDirector, probablemente mitigado pero no confirmado al 100%).

---

## 6. Dónde encontrar información

| Qué | Dónde |
|-----|-------|
| Arquitectura completa, API de sistemas | `TDD.md` |
| Quick start y estructura de carpetas | `README.md` |
| Configuración de NPCs, quests, hechizos | Assets SO en `_NPCs/`, `_QUEST/`, `_SPELLS/` |
| Localización (ES/EN) | `Assets/Resources/Localization/*.json` |
| Managers persistentes | `Assets/Scripts/Core/` |
| FSM de NPCs | `Assets/Scripts/Behaviour NPC/` |
| Grafo narrativo (runtime) | `Assets/NarrativeGraph/Runtime/` |
| Escenas de test | `Assets/Scenes/Test/` |
| Presets de testing | `Assets/_BootProfile/` |
| Debug visual en runtime | F3 (NPCs), F4 (panel general) |
