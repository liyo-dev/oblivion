# AGENTS.md — El Sendero de las Estrellas

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
- **Start → MainMenu:** el GameObject `START_BootLoader` en `Start.unity` lleva el componente `BootLoader.cs` (`sceneToLoad: MainMenu`), que hace `SceneManager.LoadScene("MainMenu")` (no aditivo) en su `Start()`. No hay condición de carrera con `GameBootService`: todos los `Awake()` de la escena (incluido el de `GameBootService`, execution order -1000) se ejecutan antes que cualquier `Start()`, así que el arranque ya está resuelto cuando `BootLoader` dispara la carga. Los managers persistentes sobreviven por ser `DontDestroyOnLoad`. (Verificado Agosto 2026 — antes no estaba documentado aquí.)

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

**Distinguir personajes de geometría en raycasts/obstrucciones:** los personajes (player, NPCs, party members) no tienen una capa propia — todos viven en `Default` junto con la mayoría de la geometría estática del mundo (confirmado en `Prefabs/_LIAM.prefab`, todo a `m_Layer: 0`). Por eso la capa sola no sirve para que un raycast de "¿hay pared/puerta en medio?" ignore a los personajes. Usar `NPCSimpleAnimator` como marcador fiable: lo tiene el player y TODOS los NPCs (mismo criterio que ya usa `DialogueManager.IsActualNPC`), y ningún objeto de escenario (puertas, muebles, props). Patrón:
```csharp
Transform root = hit.collider.transform.root;
if (root.GetComponent<NPCSimpleAnimator>() != null) continue; // es un personaje, no una obstrucción
```
Ejemplo real: `PlayerParty.FindClearDialogueFormationPosition` — evita teletransportar a un party member al otro lado de una puerta cerrada al posicionarlo para un diálogo (bug: NPC hablando desde detrás de una puerta, cámara pegada a la hoja).

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

Ver tabla completa en TDD.md § 13. **La mayoría ya está resuelta** (verificado Julio 2026, más I10/I12/I13 verificados Agosto 2026): I2, I3, I4, I6, I7, I8, I10, I11, I13, I15, I16, I17. I12 no era un bug (renombrado a `AmbientZone`, solo quedaba un tooltip desactualizado, ya corregido). Genuinamente pendientes o sin verificar: I1 (grace period diálogos, UX), I5 (locks residuales en paths de error, impacto ≈0), I9 (reflection cacheada en `PlayerFlyingController`, solo 2 llamadas por vuelo, impacto ≈0), I14 (`Time.timeScale` en SimpleCinematicDirector, probablemente mitigado pero no confirmado al 100%).

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

---

## 7. Convivencia Interactive ↔ Grafo narrativo — política formal

El proyecto tiene **dos motores narrativos en paralelo**: `NarrativeGraph`/`NarrativeRunner` y el sistema legacy "Interactive" (`NPCInteractiveNarrativeExecutor` + `NPCInteractiveNarrativeConfig`/`ConditionalNarrative`/`NarrativeCondition`), más `NPCQuestConfig` para diálogo-por-estado-de-quest fuera del grafo. Un intento de unificarlos en un único sistema rompió el juego (Agosto 2026); con el proyecto tan avanzado, **no se intenta fusionarlos**. En su lugar:

- **`NPCInteractiveNarrativeExecutor` queda congelado.** No añadir `NarrativeActionType` nuevos ni NPCs nuevos a su catálogo (`ConditionalNarrative`/`NPCInteractiveNarrativeConfig`).
- **Todo NPC o quest nueva se construye en `NarrativeGraph`.** Usa los nodos ya existentes (`StartQuestNode`, `CompleteQuestStepsNode`, `PlayDialogueNode`, `WaitCustomEventNode`, etc.). El puente `NPCBrain.HandleInteraction()` ya emite `NPC_INTERACT_{persistenceId}` por `DefaultNarrativeSignals` en cada interacción, así que un grafo puede reaccionar a "hablar con NPC X" sin tocar el executor legacy.
- **Antes de dar por buena una entrega**, correr `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)` (`Assets/NarrativeGraph/Editor/Validation/CrossSystemNarrativeValidator.cs`). Avisa si la misma quest o el mismo evento custom está referenciado a la vez por el grafo y por el sistema Interactive sin estar enlazado — el mismo patrón que causó INC-020 (consumo duplicado de ítems de quest en dos sitios que no se conocían entre sí).
- Los NPCs existentes que ya funcionan con `NPCQuestConfig`/`NPCInteractiveNarrativeConfig` **no se migran** salvo que se toquen por otro motivo. No es deuda urgente, es una decisión de arquitectura aceptada.
