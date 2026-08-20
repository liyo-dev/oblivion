# TDD — El Sendero de las Estrellas

**Motor:** Unity 6 (6000.5.4f1)  
**Pipeline:** URP  
**Input:** Unity Input System (nuevo) + Invector (movimiento base del jugador)  
**Última revisión:** 12 de agosto de 2026 (auditoría de seguimiento — ver § 19.4)

---

## Índice

1. [Quick Start para Desarrolladores](#1-quick-start)
2. [Arquitectura General](#2-arquitectura-general)
3. [Sistema de NPCs y Comportamiento](#3-sistema-de-npcs)
4. [Sistema del Jugador](#4-sistema-del-jugador)
5. [Sistemas Core](#5-sistemas-core)
6. [Audio](#6-audio)
7. [Quests](#7-quests)
8. [Diálogos](#8-diálogos)
9. [Guardado y Persistencia](#9-guardado-y-persistencia)
10. [Narrativa y Cinemáticas](#10-narrativa-y-cinemáticas)
11. [UI](#11-ui)
12. [Reglas de Rendimiento](#12-reglas-de-rendimiento)
13. [Bugs Conocidos Pendientes](#13-bugs-conocidos-pendientes)
14. [Troubleshooting](#14-troubleshooting)
15. [Análisis: Unificación del Grafo Narrativo](#15-análisis-unificación-del-grafo-narrativo-y-viabilidad-como-asset-vendible)
16. [Diseño: Cielo, Clima y Cielo Nocturno](#16-diseño-cielo-unificado-clima-dinámico-y-cielo-nocturno-temático-nubes-estrellas-arcoíris)
17. [Diseño: Refugio de NPCs bajo la Lluvia + Relaciones Sociales](#17-diseño-refugio-de-npcs-bajo-la-lluvia--relaciones-sociales-dinámicas)
18. [Checklist: Demo de Steam](#18-checklist-demo-de-steam)
19. [Auditorías](#19-auditorías)
20. [Convenciones de Documentación del Proyecto](#20-convenciones-de-documentación-del-proyecto)
21. [Diseño: Vestir MainWorld con el "look" de las demos de Quibli (árboles, hierba, rayos de sol, outline)](#21-diseño-vestir-mainworld-con-el-look-de-las-demos-de-quibli-árboles-hierba-rayos-de-sol-outline)

---

## 1. Quick Start

### Iniciar el proyecto

1. Abre Unity 6 (6000.5.4f1) o superior.
2. La escena de entrada es `Assets/Scenes/Systems/Start.unity`.
3. Todos los managers persistentes viven en Start.

### Probar una escena de mundo directamente

Puedes hacer Play desde cualquier escena (MainWorld, Woods, etc.) sin configuración manual:

- `Assets/Editor/AutoBootstrapOnPlay.cs` detecta automáticamente que no estás en Start y carga Start de forma aditiva antes de entrar en PlayMode.
- El sistema inicializa todos los managers y posiciona al jugador en el último anchor guardado (o el del preset de testing activo).

**Requisito:** Start.unity debe estar en Build Settings (posición 0).

### Entrada a MainMenu (BootLoader)

- **Start → MainMenu:** el GameObject `START_BootLoader` en `Start.unity` lleva el componente `BootLoader.cs` (`sceneToLoad: MainMenu`), que hace `SceneManager.LoadScene("MainMenu")` (no aditivo) en su `Start()`. No hay condición de carrera con `GameBootService`: todos los `Awake()` de la escena (incluido el de `GameBootService`, execution order -1000) se ejecutan antes que cualquier `Start()`, así que el arranque ya está resuelto cuando `BootLoader` dispara la carga. Los managers persistentes sobreviven por ser `DontDestroyOnLoad`. (Verificado Agosto 2026 — antes no estaba documentado aquí.)

### Dónde encontrar información

| Qué | Dónde |
|-----|-------|
| Arquitectura completa, API de sistemas, bugs, auditorías | Este documento (`TDD.md`) — fuente de verdad única |
| Portada del repo / overview para quien llega nuevo | `README.md` |
| Configuración de NPCs, quests, hechizos | Assets SO en `_NPCs/`, `_QUEST/`, `_SPELLS/` |
| Localización (ES/EN) | `Assets/Resources/Localization/*.json` |
| Managers persistentes | `Assets/Scripts/Core/` |
| FSM de NPCs | `Assets/Scripts/Behaviour NPC/` |
| Grafo narrativo (runtime) | `Assets/NarrativeGraph/Runtime/` |
| Escenas de test | `Assets/Scenes/Test/` |
| Presets de testing | `Assets/_BootProfile/` |
| Debug visual en runtime | F3 (NPCs), F4 (panel general) |

### Herramientas de desarrollo

- **F3** — debug visual de NPCs · **F4** — panel de debug general
- `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)` — valida que quests/eventos no estén referenciados a la vez por el grafo y por el sistema legacy (ver § 10 — Política formal: convivencia Interactive ↔ Grafo narrativo)

### Presets de testing vs partida guardada

`GameBootService` decide el origen de datos al iniciar:
- Si hay un `testingPreset` asignado en el inspector y el flag de testing está activo → usa el ScriptableObject.
- En caso contrario → carga el save JSON del disco.

El flujo de inicialización es idéntico en ambos casos; la única diferencia es el objeto de datos fuente.

---

## 2. Arquitectura General

### Filosofía

- **Multi-escena aditiva:** Start persiste siempre. Las escenas de mundo se cargan/descargan dinámicamente.
- **ServiceLocator:** punto de acceso a singletons globales. Preferible a referencias directas para sistemas que pueden no existir en todas las escenas.
- **ScriptableObjects como datos:** configuración de NPCs, quests, hechizos, presets de jugador. Nunca lógica.
- **Eventos C# (`Action<T>`)** para comunicación desacoplada entre sistemas.
- **`DontDestroyOnLoad` solo en managers de Start.** El resto de objetos pertenece a su escena.

### Escena Start — managers persistentes

| Manager | Archivo | Responsabilidad |
|---------|---------|-----------------|
| GameBootService | `Core/GameBootService.cs` | Inicialización global, profile activo |
| PlayerService | `Core/PlayerService.cs` | Referencia global al GameObject del jugador |
| QuestManager | `Quests/QuestManager.cs` | Estado y progreso de misiones |
| DialogueManager | `Dialogue/DialogueManager.cs` | Reproducción de diálogos |
| AudioService | `Audio/AudioService.cs` | Música y SFX con pools |
| SaveSystem | `Core/SaveSystem.cs` | Leer/escribir save JSON |
| GamepadInputReader | `Player/GamepadInputReader.cs` | Input centralizado con eventos |
| LocalizationManager | gestión de textos ES/EN |
| MenuManager | `UI/MenuManager.cs` | Control de qué menús están abiertos |

### Script Execution Order relevante

```
GameBootService:     -1000   ← debe ser el primero
PlayerService:        -900
ServiceLocator:       -800
[otros managers]:    default (0)
WorldBootstrap:       +200   ← espera a que los managers estén listos
```

Configurado en Project Settings → Script Execution Order.

### Flujo de inicialización (frame a frame)

```
Frame 0:  Start.unity carga (o AutoBootstrapOnPlay la carga aditiva)
Frame 0:  GameBootService.Awake() → lee preset/JSON, asigna IsAvailable = true
Frame 0:  WorldBootstrap.OnEnable() → suscribe a OnProfileReady
          Si IsAvailable ya es true → llama HandleProfileReady directamente
Frame 1:  GameBootService.NotifyProfileReadyDelayed() → dispara OnProfileReady
Frame 1+: SpawnManager, WorldBootstrap y demás reciben el evento y se inicializan
```

El delay de un frame en `NotifyProfileReadyDelayed` garantiza que todos los `OnEnable` del frame 0 hayan suscrito antes de que el evento se dispare.

### Estructura de carpetas

```
Assets/
├── Scenes/
│   ├── Systems/        ← Start, MainMenu, LoadingScreen
│   ├── Main World/     ← MainWorld y escenas de mundo
│   ├── Cinematics/     ← cinemáticas y prólogo
│   └── Test/           ← escenas de prueba
├── Scripts/
│   ├── Core/               ← GameBootService, ServiceLocator, PlayerService, SaveSystem
│   ├── Behaviour NPC/      ← FSM de NPCs
│   ├── NarrativeGraph/     ← grafo narrativo (nodos, runner)
│   ├── Narrative/          ← sistema legacy "Interactive"
│   ├── Quests/, Dialogue/, Audio/, Inventory/, Puzzle/, UI/, ...
├── NarrativeGraph/      ← assets runtime del grafo (MainNarrative.asset, etc.)
├── _BootProfile/        ← presets de testing (ScriptableObjects)
├── Resources/Localization/  ← JSON de localización (ES/EN)
└── Plugins/             ← Invector 3rd Person Controller, DOTween, etc.
```

### Stack técnico

- Unity 6 · URP 17.5
- Unity Input System 1.19 (input centralizado por eventos)
- Cinemachine 3.1.7 · Timeline 1.8.12 · AI Navigation 2.0.13
- Invector 3rd Person Controller (base de movimiento del jugador) · DOTween


---

## 3. Sistema de NPCs

### Arquitectura FSM

Cada NPC usa una FSM implementada en tres capas:

```
NPCBehaviourManagerV2          ← orquestador principal
  └── NPCBrain                 ← núcleo de la FSM, gestiona transiciones
        └── NPCStateContext    ← datos compartidos entre estados (transform, agent, refs cacheadas)
              └── INPCState    ← interfaz de cada estado
```

**Archivos clave:**
- `Behaviour NPC/NPCBehaviourManagerV2.cs`
- `Behaviour NPC/NPCBrain.cs`
- `Behaviour NPC/Common/NPCStateContext.cs`
- `Behaviour NPC/States/` — un archivo por estado

### Estados principales

| Estado | Clase | Cuándo activo |
|--------|-------|---------------|
| Idle | `States/IdleState.cs` | Por defecto, patrulla |
| FollowPlayer | `States/FollowPlayerState.cs` | Miembro del party siguiendo |
| Combat | `States/CombatState.cs` | Enemigo en combate |
| AllyCombat | `States/AllyCombatState.cs` | Aliado combatiendo |
| Dialogue | `States/DialogueState.cs` | Durante conversación |
| PostDefeat | (gestionado por `NPCCombatLifecycleHandler`) | Tras derrota |

### Módulos del NPC

Los módulos son componentes opcionales que `NPCBehaviourManagerV2` detecta y cachea en `Awake`. No todos los NPCs necesitan todos los módulos.

| Módulo | Responsabilidad |
|--------|----------------|
| `NPCCombatHandler` | Vida, recibir daño, coordinar con el atacante |
| `NPCCombatLifecycleHandler` | Secuencia de muerte → dizzy → post-defeat action |
| `NPCSimpleAnimator` | Animaciones (locomotion, ataque, muerte, diálogo) |
| `NPCInteractiveNarrativeExecutor` | Diálogos narrativos con condiciones |
| `NPCPartyMember` | Seguir al jugador como aliado del party |
| `NPCCombatTeam` | Coordinación de grupos de enemigos (líder + miembros) |

### Configuración vía ScriptableObjects

Cada NPC tiene asignado un `NPCConfiguration` (SO) que referencia:
- `NPCCombatConfig` — stats de combate, acciones post-derrota
- `NPCPartyConfig` — configuración como miembro del party (hechizos, posicionamiento en diálogos)
- `InteractiveNarrativeConfig` — árbol narrativo asociado
- `QuestChainConfig` — encadenamiento de misiones

### Post-defeat actions

Configurado en `NPCCombatConfig.postDefeatAction`:

| Valor | Comportamiento |
|-------|---------------|
| `FleeAndDisappear` | NPC huye y se destruye al llegar al destino |
| `StayAndRespawn` | Se queda en el suelo |
| `FollowPlayer` | Se une al party |
| `None` | Solo animación dizzy, sin más acción |

### Equipos de combate (NPCCombatTeam)

Cuando varios NPCs comparten un `NPCCombatTeam`:
- Un miembro es el líder (el primero en la lista), pero desde el rediseño del 15 ago 2026 el liderazgo ya NO determina quién gestiona el diálogo post-derrota: **cada NPC del equipo dice su propia frase de derrota y ejecuta su propia acción post-derrota en cuanto él mismo muere**, sin esperar a que caiga el resto (antes, un no-líder que moría primero se quedaba congelado hasta 2 minutos esperando al equipo).
- La celebración de victoria del jugador (`PlayerBattleModeController.PlayVictory`) y la restauración de la música de combate se disparan **una única vez**, cuando la muerte de un NPC completa la derrota de TODO el equipo (`teamMember.Team.IsTeamDefeated`) — sea o no el líder quien remate. Ver `NPCCombatLifecycleHandler.DeathRoutine()` (`shouldCelebrate`) y `HandleGetUpDizzy()` (restauración de música tras el diálogo).
- Quien remata al equipo es quien llama a `NotifyPostDefeatDialogueFinished()` (antes: siempre el líder).
- **Trampa conocida (ver INC-078, § 13):** `NPCTeamMember` no viene en el prefab — lo añade dinámicamente `NPCCombatTeam.Start()` (`AddComponent` + `SetTeam`). Cualquier componente que necesite `GetComponent<NPCTeamMember>()` debe resolverlo de forma perezosa (primer uso real), **nunca cachearlo en su propio `Awake()`**: en ese momento el componente todavía no existe, porque Unity ejecuta todos los `Awake()` de la escena antes que cualquier `Start()`.

### Fix crítico: NavMeshAgent caminando en sitio

Tras una muerte, el agent queda con `updatePosition = false`. Al reactivarlo para huida, hay que secuenciar:

```csharp
_agent.isStopped = false;
_agent.updatePosition = true;
_agent.Warp(transform.position);   // re-sincroniza con el NavMesh
_agent.SetDestination(targetPos);
_animator.TransitionToLocomotion();
```

Sin el `Warp`, `remainingDistance` queda en `Infinity` y la animación de caminar nunca produce movimiento real.

### Posicionamiento durante diálogos

Los miembros del party se posicionan automáticamente al iniciar un diálogo. Configurable por NPC en `NPCPartyConfig`:

- `posicionarseDuranteDialogos` — activar/desactivar
- `ladoPreferidoDialogo` — `Left` o `Right` relativo al jugador mirando al NPC
- `distanciaLateralDialogo` — metros de separación lateral (default 1.5)
- `offsetDelanteDialogo` — positivo = adelante, negativo = atrás (default -0.3)

Implementado en `DialoguePositionState.cs` (estado FSM temporal del party member durante el diálogo). Al cerrar el diálogo, cada miembro vuelve a `FollowPlayerState`.

---

## 4. Sistema del Jugador

### Controladores de movimiento

El movimiento base usa Invector (3rd person controller). Encima de eso hay controladores especializados que se apilan vía `PlayerActionManager`:

| Controlador | Archivo | Cuándo activo |
|-------------|---------|---------------|
| Normal | (Invector) | Estado base |
| Swimming | `PlayerSwimmingController.cs` | Al entrar en volumen de agua |
| Flying | `PlayerFlyingController.cs` | Modo vuelo (Estela) |
| Levitation | `PlayerLevitationController.cs` | Levitación mágica |

### PlayerActionManager — stack de modos

Gestiona qué el jugador puede hacer en cada momento mediante un stack de `ActionMode`:

```csharp
// Bloquear al jugador durante cinemática:
PlayerActionManager.Instance.PushMode(ActionMode.Cinematic);
// Al terminar:
PlayerActionManager.Instance.PopMode(ActionMode.Cinematic);
```

Modos principales: `Normal`, `Combat`, `Dialogue`, `Cinematic`, `Swimming`, `Map`, `GameOver`.

El tope del stack determina el modo activo. Esto permite que un diálogo en medio de un combate bloquee correctamente y al cerrarse restaure el estado de combate.

### Sistema de cambio de personaje activo

`ActiveCharacterSwapper.cs` permite controlar a Liam o Estela en lugar de Will.

Al cambiar:
1. Teleporta el controller al NPC objetivo.
2. Aplica apariencia visual vía `CharacterAppearanceRegistry`.
3. Cambia los hechizos del `MagicCaster`.
4. Oculta el NPC objetivo (el controller lo representa).
5. Instancia/destruye el NPC de Will según si es el activo o no.

Will nunca desaparece del mundo: cuando no es el personaje activo, existe como NPC aliado con IA.

**Bug histórico (invisibilidad tras separar el equipo, CERRADO 19 de agosto de 2026 — ver §13, INC-079 para el historial completo con logs reales):** a diferencia de Liam/Estela (NPCs que ya llevan frames renderizándose desde la carga de la escena), el NPC de Will se `Instantiate()` de cero en `SpawnWillNpc()`. El AABB de culling de cada `SkinnedMeshRenderer` se calcula a partir de la pose del rootBone; si ese primer cálculo ocurre antes de que el Animator corra un frame (o antes de que `ModularAutoBuilder` active las partes correctas) y cae fuera del frustum de la cámara real —lo normal, porque la cámara ya está mirando al personaje recién activado, no a Will—, Unity nunca lo considera "visible" una primera vez. Sin ese primer "visible", nada dispara un recálculo del AABB, así que nunca vuelve a considerarse visible: punto muerto. Resultado: Will invisible pero presente (colisiones/IA/Interactable intactos), indefinidamente, muy notorio al dejarlo quieto anclado en un puzle. Diagnóstico confirmado en el editor: seleccionar el NPC en la Hierarchy (que fuerza a Unity a leer `Renderer.bounds` para dibujar el gizmo de selección) lo hacía "reaparecer" al instante en el Game View — la firma exacta de un AABB de culling nunca refrescado. Las reafirmaciones de `renderer.enabled` que ya existían (`ReassertWillVisibilityNextFrames`, `DiagnoseAndHealWillVisibility`) no lo arreglaban porque el problema no era `enabled=false`, era el AABB de culling atascado. Primera ronda de fix en `SpawnWillNpc()`: `SkinnedMeshRenderer.updateWhenOffscreen = true` en todos los renderers (para que se sigan actualizando aunque se consideren no-visibles) + `Animator.Update(0f)` inmediato tras instanciar/aplicar apariencia (para que las bones ya estén en su pose real) + lectura forzada de `.bounds` por cada renderer (para forzar el recálculo del AABB con esa pose ya correcta, antes de que la cámara real haga su primer test de culling). Mismo trío repetido en `DiagnoseAndHealWillVisibility()` (heal periódico cada 0.5s) por si algún recálculo se pierde entre el spawn y ese tick. Rondas posteriores (documentadas en el código, no repetidas aquí en detalle) añadieron `allowOcclusionWhenDynamic = false` contra el Occlusion Culling horneado de `MainWorld.unity`, y el propio heal periódico que toggla `Renderer.enabled`. **Ninguna de esas rondas cerraba el bug del todo** — seguía reapareciendo porque nunca se tocó la causa de fondo: este rig corre el Animator en `Animator.CullingMode.CullUpdateTransforms`, así que mientras el renderer no se considera visible el Animator deja de mover huesos, y sin huesos moviéndose el AABB nunca se recalcula por mucho `updateWhenOffscreen`/`.bounds` forzado que se le eche encima — solo `Animator.Update(0f)` empujaba un frame puntual que no siempre bastaba antes del siguiente test de culling. **Cierre real (19 ago 2026, ver INC-079):** `cullingMode = AnimatorCullingMode.AlwaysAnimate` en los tres puntos que tocan el Animator de Will/Liam/Estela (`SpawnWillNpc()`, `WarpNpcToPosition()`, `DiagnoseAndHealWillVisibility()`) — saca al Animator del modo que causaba el interbloqueo de raíz, así que huesos y bounds se actualizan todos los frames sin depender de haber sido visible antes. Confirmado en juego por Raúl.

### MagicCaster y hechizos

Los hechizos se asignan por slot: `Left`, `Right`, `Special`. Cada `MagicSpellSO` define comportamiento (proyectil, AOE, buff).

Los cooldowns se gestionan en `MagicCaster` con un diccionario indexado por `MagicSlot`.

### Sistema de combate del jugador

`PlayerBattleModeController` detecta enemigos y activa el modo batalla:
- Usa `ActiveCombatRegistry` para saber si hay enemigos en combate (O(1)).
- `Physics.OverlapSphereNonAlloc` con buffer pre-alocado para la detección de área.
- Secuencia de victoria coordinada con `AudioService` y `NPCCombatLifecycleHandler`.

### Targeting

`PlayerTargeting` escanea enemigos a intervalos configurables (`updatesPerSecond`, default 10). No usa Update directamente sino `InvokeRepeating`. Filtra por capas para evitar queries innecesarias.

---

## 5. Sistemas Core

### ServiceLocator

`Assets/Scripts/Core/ServiceLocator.cs`

Punto de acceso global a componentes que no conviene referenciar directamente. Cachea los servicios tras la primera búsqueda.

```csharp
// Obtener un servicio (puede devolver null si no está en escena):
var health = ServiceLocator.Get<PlayerHealthSystem>();

// Con warnIfNotFound = false para servicios opcionales:
var health = ServiceLocator.Get<PlayerHealthSystem>(false);
```

### GameBootService

`Assets/Scripts/Core/GameBootService.cs`

Punto de entrada de todo el sistema de inicialización. Responsable de:
- Leer el perfil activo (preset de testing o JSON del save).
- Distribuir el perfil a todos los managers vía el evento `OnProfileReady`.
- Gestionar el perfil en memoria durante la sesión.

**Script Execution Order: -1000.** Debe ejecutarse antes que cualquier otro manager.

**Invariante importante:** `IsAvailable` pasa a `true` en `Awake`. Cualquier sistema que necesite el perfil puede hacer `while (!GameBootService.IsAvailable) yield return null;` de forma segura.

### Variables estáticas y PlayMode en Editor

Unity no resetea variables estáticas entre sesiones de PlayMode en el editor. Esto puede causar managers duplicados, eventos con múltiples suscriptores y estado corrupto.

**Patrón obligatorio en todos los singletons y managers con estado estático:**

```csharp
#if UNITY_EDITOR
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics()
{
    _instance = null;
    OnMyStaticEvent = null; // También resetear eventos estáticos
}
#endif
```

`SubsystemRegistration` se ejecuta antes de cualquier `Awake`, garantizando un estado limpio al entrar en PlayMode.

### PlayerService

`Assets/Scripts/Core/PlayerService.cs`

Referencia global al GameObject del jugador. Usado para acceder a componentes sin referencias directas. Prefiere `TryGetPlayer(out GameObject go)` sobre `Instance.gameObject` para evitar errores si el jugador no está en escena.

---

## 6. Audio

### AudioService

`Assets/Scripts/Audio/AudioService.cs`

Gestión centralizada de música y SFX con:
- Pool de `AudioSource` 2D y 3D (configurable en inspector).
- Stack de música (para batalla/victoria/minijuego y vuelta a ambiente).
- Ducking automático de SFX durante música.
- Crossfades configurables.

### API principal

```csharp
AudioService.Instance.PlayMusic(clipId, fadeSeconds);
AudioService.Instance.StopMusic(fadeSeconds);
AudioService.Instance.PlaySFX(clipId, volume, position);   // 3D
AudioService.Instance.PlaySFX2D(clipId, volume);           // 2D
AudioService.Instance.BeginBattleMusic(battleId);
AudioService.Instance.OnBattleWonRestoreMusic(battleId);
```

### Bug crítico — StopAllCoroutines mata el pool SFX

`PlayMusic()`, `StopMusic()` y `RestartMusicClipFromBeginning()` llaman a `StopAllCoroutines()`. Esto destruye las corrutinas `ReturnWhenDone` de los SFX activos: los `AudioSource` nunca se devuelven al pool y se acumulan GameObjects hijo con el tiempo.

**Solución pendiente:** reemplazar `StopAllCoroutines()` por corrutinas con referencias explícitas:

```csharp
private Coroutine _crossfadeRoutine;
private Coroutine _fadeOutRoutine;

// En lugar de StopAllCoroutines():
if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
_crossfadeRoutine = StartCoroutine(CrossfadeRoutine(...));
```

### Conversión de volumen (bug menor)

La conversión lineal→dB actual usa interpolación lineal entre -80 y 0, que no es perceptualmente uniforme. A 0.5 de volumen el oyente esperaría ~-6dB, pero obtiene -40dB. La conversión correcta es:

```csharp
float dB = linear01 <= 0f ? -80f : 20f * Mathf.Log10(linear01);
```

---

## 7. Quests

### QuestManager

`Assets/Scripts/Quests/QuestManager.cs`

Gestiona el ciclo de vida de las misiones: activación, progreso, completado, persistencia.

Cada quest tiene un `QuestData` (SO) con pasos (`QuestStep`) y requerimientos por paso (items, party members, wardrobe, combates, etc.).

El estado en runtime vive en `RuntimeQuest`, indexado por `questId`.

### Tipos de requerimientos por paso

- **Item:** recoger N unidades de un ítem.
- **ItemConsume:** entregar N unidades (se eliminan del inventario).
- **PartyMember:** tener un NPC concreto en el party.
- **Wardrobe:** desbloquear un ítem de wardrobe.
- **Custom:** flag narrativo gestionado manualmente.

### Persistencia de quests

Los flags de progreso se guardan en el perfil activo (`GameBootProfile`) como strings clave-valor. `QuestPersistenceBridge` se encarga de sincronizar el estado del `QuestManager` con el perfil al completar pasos y al guardar.

### Bug crítico — FindObjectsByType en hot path

`FindQuestChainEntry()` llama a `FindObjectsByType<NPCBehaviourManagerV2>()` cada vez que el jugador recoge un ítem o cambia wardrobe. Con 20+ NPCs en escena y 5 quests activas, cada recogida dispara 5 scans completos de escena.

**Solución pendiente:** construir un `Dictionary<string, QuestChainEntry>` en Start y actualizarlo cuando un NPC se registra/desregistra:

```csharp
// O(1) lookup en lugar de O(n) scan:
private readonly Dictionary<string, QuestChainEntry> _questChainIndex = new();
```

### LINQ con allocaciones en hot path

`OnInventoryItemAdded` y `OnWardrobeChanged` hacen `.Where(...).ToList()` sobre el runtime de quests en cada evento. Reemplazar con iteración directa sobre el diccionario:

```csharp
foreach (var kv in _runtime)
{
    if (kv.Value.State != QuestState.Active) continue;
    CheckItemForQuest(kv.Value, item, newTotal);
}
```

---

## 8. Diálogos

### DialogueManager

`Assets/Scripts/Dialogue/DialogueManager.cs`

Gestiona la reproducción de `DialogueAsset` (SO con líneas localizadas). Responsabilidades:
- Bloquear al jugador (`PlayerActionManager.PushMode(ActionMode.Dialogue)`).
- Typewriter de texto.
- Activar animaciones de habla en el speaker activo.
- Mostrar opciones de respuesta cuando corresponde.
- Posicionar party members vía `PlayerParty.PositionMembersForDialogue`.

### DialogueAsset

SO con lista de `DialogueLine`. Cada línea tiene:
- `speakerId` — nombre del speaker (se busca en NPC activo, party, o escena).
- `textKey` — clave de localización.
- `emotion` — estado emocional para el animator.

### Sistema de cámara multi-speaker

Cuando el speaker cambia, la cámara (Cinemachine) hace corte o interpolación al target correspondiente. Cada personaje tiene un `dialogueCharacterId` configurado en su `NPCPartyConfig`.

### Bug — grace period reseteado en cada línea

El cooldown de input de 0.3s (`_dialogueOpenedAt`) se resetea en cada llamada a `Next()`. Esto hace que tras cada línea el jugador tenga que esperar 300ms antes de poder avanzar, lo que se percibe como input no responsivo en textos cortos.

El grace period debería aplicarse solo al abrir el diálogo, no en cada avance.

### GetComponent en cada línea (rendimiento)

`ActivateSpeakerTalkAnimation()` llama a `GetComponent<NPCSimpleAnimator>()` y `GetComponent<Animator>()` por cada línea de diálogo. Estos deben cachearse en `StartDialogue()` donde ya se tiene el contexto del speaker.

---

## 9. Guardado y Persistencia

### SaveSystem

`Assets/Scripts/Core/SaveSystem.cs`

Serializa el `GameBootProfile` a JSON y lo escribe en `Application.persistentDataPath/save.json`.

**Bug crítico — escritura no atómica:** `File.WriteAllText` sobreescribe directamente el archivo. Si el proceso se interrumpe (crash, cierre de app) a mitad de escritura, el save queda corrupto y el jugador pierde la partida.

**Solución pendiente:**

```csharp
var tmpPath = SavePath + ".tmp";
File.WriteAllText(tmpPath, json);
if (File.Exists(SavePath)) File.Delete(SavePath);
File.Move(tmpPath, SavePath);
```

### GameBootProfile

SO serializable que contiene todo el estado del jugador:
- Posición y ancla de spawn (`currentAnchorId`).
- Flags de quests, narrativa y mundo.
- Inventario, wardrobe, hechizos equipados.
- Miembros del party activo.

El profile es la única fuente de verdad para restaurar el estado entre sesiones.

### IDs de persistencia de mundo

Los objetos que persisten estado entre cambios de escena (pickups, puertas, puzzles…) usan un flag string guardado en `preset.flags`. El ID de cada flag se genera **automáticamente** sin necesidad de configuración manual:

```
{escena}_{nombreObjeto}_{posX:F1}_{posY:F1}_{posZ:F1}
```

Todos estos scripts exponen un campo override **opcional**. Si está vacío, el ID automático se usa siempre.

| Script | Prefijo del flag | Campo override |
|--------|-----------------|----------------|
| `WorldPickup` | `PICKUP_` | `pickupIdOverride` |
| `ItemLockedDoor` | `DOOR_UNLOCKED_` | `persistenceId` |
| `ActivationCounter` | `ACTIVATION_COMPLETE_` | `persistenceIdOverride` |

**Regla:** nunca asignar ID manual salvo que el objeto pueda moverse o renombrarse. La posición en mundo (1 decimal) es suficientemente estable para todos los casos normales.

### Presets de testing

`PlayerPresetSO` es un profile con datos fijos para testing. Se asigna en el inspector de `GameBootService`. Al entrar en PlayMode con un preset, el juego inicializa exactamente ese estado sin tocar el save del jugador.

**Precaución con blackboards de preset:** si el preset fue capturado después de que un trigger narrativo ya se había activado, contendrá el flag `__event_XXX_received = 1`. Esto hace que el grafo narrativo salte ese nodo sin esperar. Para resetear una sección narrativa, eliminar manualmente ese flag del asset del preset.

---

## 10. Narrativa y Cinemáticas

### NarrativeRunner y grafo narrativo

El grafo narrativo de `MainNarrative.asset` es el director de la historia. Cada nodo representa una acción: iniciar diálogo, esperar evento, iniciar batalla, modificar estado del mundo, etc.

Los eventos entre el mundo y el grafo se comunican vía `DefaultNarrativeSignals`. El sistema soporta eventos "pendientes": si un trigger se activa antes de que el grafo tenga un listener suscrito, el evento se guarda en `_pending` y se consume cuando el grafo llega al nodo correspondiente.

**Invariante crítica:** `NarrativeAutoSetup.ResetForLoadedProfile()` usa `preservePending: true` para no limpiar eventos pendientes al cargar un perfil. Esto garantiza que triggers que se activan antes de la inicialización del grafo no se pierdan.

### SimpleCinematicDirector

`Assets/Scripts/Cinematics/SimpleCinematicDirector.cs`

Reproduce secuencias de cinemáticas definidas como listas de steps. Cada step puede incluir: animación, movimiento de cámara, audio, slow motion, subtítulos.

**Importante:** Si hay un step con `slowMotion = true` y se para la cinemática externamente con `StopCoroutine`, `Time.timeScale` queda en el valor reducido indefinidamente. `OnDestroy` lo restaura, pero un stop sin destroy no. El método `Stop()` debe garantizar la limpieza de `timeScale`.

**Audio en cinemáticas:** no usar `AudioSource.PlayClipAtPoint` (bypasa el AudioMixer y el pool). Usar `AudioService.Instance.PlaySFX`.

### Metodología de diseño de secuencias (patrón "obra de teatro")

Toda cinemática nueva (`XxxSequencer : CinematicSequencerBase`) se diseña **antes** de tocar código o el Editor, en tres bloques, como el guion técnico de una obra de teatro.

**Dónde vive el sequencer:** el patrón de escena aditiva dedicada (`Assets/Scenes/Cinematics/Aditivas/*.unity`) y Timeline (`Assets/Timeline/*.playable`) es **legacy** — sigue en el proyecto pero ya no se usa para cinemáticas nuevas. El patrón actual: el componente `XxxSequencer` se añade como GameObject directamente en la escena donde ocurre la acción (normalmente `MainWorld.unity`), junto a los actores/prefabs/shot-points ya colocados en esa misma escena. No hay `PlayableDirector`; el movimiento de cámara lo hace `CinematicCameraDriver` (`Cut`/`MoveTo`) sobre Transforms "shot point" colocados a mano. El disparo es una señal del grafo narrativo: `_signalIn` se escucha vía `DefaultNarrativeSignals` (vinculado normalmente a un `RaiseCustomEventNode` en `MainNarrative.asset`) y `_signalOut` se levanta al terminar para que el grafo continúe (normalmente con un `WaitCustomEventNode` esperándolo). Ejemplos reales de este patrón: `TabernaSequencer`, `MountainSequencer`, `LiamGolemSummonSequencer`, `EstelaAppearsSequencer`, `LiamCrystalBallSequencer`, `ReinoExitBanterSequencer` — todos viven en `MainWorld.unity`.

1. **Materiales** — todo lo que la secuencia necesita para existir: prefabs de actores ya colocados en la escena relevante, Transforms de cámara ("shot points"), `Volume` URP de post-proceso dedicado, clips de audio (voz/SFX/música), VFX a reutilizar vía `VfxPoolService` (nunca instanciar VFX de un solo uso sin pool — ver § 12). Se anota explícitamente qué ya existe en el proyecto (con ruta) y qué falta por crear o buscar.
2. **Actores** — quién participa y su rol *dramático* en la escena, no solo su nombre técnico: qué transmite, qué anima, desde dónde entra y sale, con qué intención de encuadre (quién domina el plano, quién es secundario).
3. **Fases** — la secuencia se trocea en fases con tiempos concretos (mm:ss). Cada fase especifica: **cámara** (encuadre/movimiento, fijo o `CinematicCameraDriver`), **animación** (qué estado del `Animator` y a qué velocidad), **tiempos** (inicio–fin), y qué pasa con **post-proceso/audio** durante esa fase.

Este desglose se escribe primero (como documento o mensaje) y solo después se traduce a `Co_Sequence()` en el `Sequencer`. Sirve de contrato de lo que hay que montar en el Editor (Timeline, Volumes, prefabs, VFX) sin depender de tener la escena abierta para razonar sobre la secuencia. Ejemplos ya construidos con esta estructura implícita en el código: `StarAwakeningSequencer`, `TabernaSequencer`, `LiamGolemSummonSequencer`.

### Invariantes críticas del grafo narrativo — NO violar

El grafo narrativo (`MainNarrative.asset`) y sus runners son `DontDestroyOnLoad`. Estas reglas son críticas:

**Regla 1 — Test mode = vuelco EXACTO del bootPreset, sin mezcla con JSON.**
En `GameBootService.PrepareActivePreset()` modo testeo NUNCA leer el JSON. Solo `EnsureRuntimePresetFromTemplate(bootPreset)` + `ApplyPresetAsLoadedGame()`.

**Regla 2 — Al "cargar partida" en test mode, siempre recargar desde bootPreset.**
`GameBootService.ReloadTestPreset()` hace: (1) `hub.StopAllRunners()`, (2) `EnsureRuntimePresetFromTemplate(bootPreset)`, (3) `ApplyPresetAsLoadedGame()`. `MainMenuController.OnClickContinue()` lo llama en test mode.

**Regla 3 — `NarrativeRunner.StartFromStartNode` / `StartFromNode` deben llamar `StopExecution()` primero.**
Sin esto se acumulan runners paralelos que ejecutan nodos en la sesión incorrecta.

**Regla 4 — NO tocar `WaitQuestCompleteNode` ni la fork detection de `StartFromStartNode`.**
El `Advance()` ya maneja fork detection. El `WaitCustomEventNode` ya tiene su mecanismo `__event_{guid}_{key}_received`. Consultar § 13 (Bugs Conocidos Pendientes) antes de tocar nodos narrativos.

**Regla 5 — `DefaultNarrativeSignals._raised` es el backup persistente de señales.**
`RaiseCustom` añade a `_pending` y `_raised`. `ResetState(preservePending:true)` preserva los dos. No eliminar `_raised`.

**Sobre presets de testing:** si un preset fue capturado después de que un trigger se activó, contiene el flag `__event_XXX_received = 1` en el blackboard. El runner saltará ese nodo. Para resetear, eliminar el flag manualmente del asset del preset.

### Política formal: convivencia Interactive ↔ Grafo narrativo

El proyecto tiene **dos motores narrativos en paralelo**: `NarrativeGraph`/`NarrativeRunner` y el sistema legacy "Interactive" (`NPCInteractiveNarrativeExecutor` + `NPCInteractiveNarrativeConfig`/`ConditionalNarrative`/`NarrativeCondition`), más `NPCQuestConfig` para diálogo-por-estado-de-quest fuera del grafo. Un intento de unificarlos en un único sistema rompió el juego (Agosto 2026); con el proyecto tan avanzado, **no se intenta fusionarlos**. En su lugar:

- **`NPCInteractiveNarrativeExecutor` queda congelado.** No añadir `NarrativeActionType` nuevos ni NPCs nuevos a su catálogo (`ConditionalNarrative`/`NPCInteractiveNarrativeConfig`).
- **Todo NPC o quest nueva se construye en `NarrativeGraph`.** Usa los nodos ya existentes (`StartQuestNode`, `CompleteQuestStepsNode`, `PlayDialogueNode`, `WaitCustomEventNode`, etc.). El puente `NPCBrain.HandleInteraction()` ya emite `NPC_INTERACT_{persistenceId}` por `DefaultNarrativeSignals` en cada interacción, así que un grafo puede reaccionar a "hablar con NPC X" sin tocar el executor legacy.
- **Antes de dar por buena una entrega**, correr `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)` (`Assets/NarrativeGraph/Editor/Validation/CrossSystemNarrativeValidator.cs`). Avisa si la misma quest o el mismo evento custom está referenciado a la vez por el grafo y por el sistema Interactive sin estar enlazado — el mismo patrón que causó INC-020 (consumo duplicado de ítems de quest en dos sitios que no se conocían entre sí).
- Los NPCs existentes que ya funcionan con `NPCQuestConfig`/`NPCInteractiveNarrativeConfig` **no se migran** salvo que se toquen por otro motivo. No es deuda urgente, es una decisión de arquitectura aceptada.


---

## 11. UI

### MenuManager

`Assets/Scripts/UI/MenuManager.cs`

Registro estático de qué menús están abiertos. Controla si se puede abrir un menú dado el estado actual.

```csharp
MenuManager.TryOpen(MenuKind.QuestLog);   // devuelve false si hay conflicto
MenuManager.Close(MenuKind.QuestLog);
MenuManager.AnyOpenExcept(MenuKind.HUD);  // comprueba si hay algo abierto
```

`AnyOpenExcept` se llama frecuentemente desde Update (en QuestMenuManager). Evitar allocaciones dentro de este método: no usar `new HashSet<>()` por llamada.

### PlayerHUDV2

`Assets/Scripts/UI/PlayerHUDV2.cs`

HUD principal: barras de HP y mana, slots de hechizos con cooldowns.

`UpdateMagicSlotCooldowns()` se llama en `Update`. Evitar `GetSpellForSlot` por frame si no hay cambio en el preset. Cachear el resultado y suscribirse a `MagicCaster.OnSpellEquipped` para invalidar.

`SetActive` en cooldown overlays dentro de Update puede disparar layout rebuilds. Usar guard de estado:

```csharp
if (overlay.activeSelf != shouldBeActive)
    overlay.SetActive(shouldBeActive);
```

### CollectiblePopupQueue

`Assets/Scripts/UI/CollectiblePopupQueue.cs`

Gestiona la cola de popups de coleccionables. 

**Nota:** el código actual usa `lock` sobre colecciones en código single-threaded. Los locks son innecesarios (no hay threads) y añaden overhead de Monitor. Pueden eliminarse con seguridad.

`Canvas.ForceUpdateCanvases()` en `PlayLifecycle` fuerza rebuild de todos los canvases activos. Reemplazar por `LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect)`.

### LanguageSelectPanel (selector de idioma de primer arranque)

`Assets/Scripts/UI/LanguageSelectPanel.cs` (Agosto 2026)

Panel que `MainMenuController` enseña tapando el menú normal la primera vez que se arranca una instalación — antes de eso, el jugador no puede ver Continuar/Nueva Partida/Ajustes. Sigue el mismo patrón `Show()`/`Close(silent)` que `SettingsMenuController`/`ControlsMenuController`, pero **no** se puede cerrar con Start/Cancel: solo se cierra eligiendo un idioma, igual que el selector de idioma de arranque de una consola.

**Criterio de "primera vez":** no es `SaveSystem.HasSave()` (eso es progreso de partida, se borra en cada Nueva Partida) sino `PlayerSettings.LanguageSelected`, un flag booleano nuevo en `player_settings.json` (preferencia de la instalación, independiente del save). Se pone a `true` con `PlayerSettings.MarkLanguageSelected()`, llamado aparte de `SetLanguage()` porque si el jugador elige el idioma que ya es por defecto ("es") `SetLanguage()` no escribiría nada a disco por sí solo (no-op cuando el locale no cambia) y el flag nunca se guardaría.

Cada botón lleva su propio texto rotulado a mano en su idioma ("Español"/"English") en el Editor — el panel no depende de que `LocalizationManager` haya cargado ningún catálogo. Array de opciones (`locale` + `Button`) en vez de dos botones fijos, para poder añadir más idiomas sin tocar código.

`MainMenuController.OnEnable()` decide mostrarlo (`languageSelectPanel != null && !PlayerSettings.LanguageSelected`) reutilizando la misma coreografía de suspensión/restauración que ya usa para Ajustes/Controles (`SuspendMainMenuInteraction`, `buttonPanel` oculto, `RestartArmAfterSettingsClose` al volver) — ver `MainMenuController.ShowLanguageSelectFirst()`. La animación de entrada del menú (`PlayIntro()`) se retrasa hasta que el jugador elige idioma, para que lo primero que vea sea el selector sin fundido.

**Construcción del panel:** `Assets/Scripts/Editor/LanguageSelectPanelBuilder.cs` (menú `El Sendero → Controles → Construir Selector de Idioma en MainMenu`) crea el GameObject en `MainMenu.unity` y cablea el Inspector por código, mismo patrón que `ControlsMenuSceneBuilder`/`MainMenuCreditsExitButtonsBuilder`: clona el panel de Ajustes como fondo (mismo estilo que el resto de sub-paneles) y clona el botón CONTROLES dos veces para "Español"/"English" (mismo fondo de cristal `RowGlassBG`). A diferencia de los botones que sí se traducen (Créditos/Salir), aquí se **elimina** el `LocalizedText` de cada clon en vez de reapuntar su clave — este panel se enseña antes de que haya idioma elegido, así que cada botón debe mostrar su idioma fijo siempre, nunca traducirse según el locale activo. Idempotente (reutiliza `PanelSelectorIdioma` si ya existe) y no toca el YAML de la escena a mano.

---

## 12. Reglas de Rendimiento

### Reglas no negociables

**No hacer en Update/LateUpdate/FixedUpdate:**
- `FindObjectOfType` / `FindObjectsByType` — O(n) sobre toda la escena. Usar registros (`ActiveCombatRegistry`, `PlayerParty`, etc.).
- `GetComponent` sin cachear — moverlo a `Awake`.
- `Camera.main` — busca por tag internamente. Cachear en `Awake` o suscribirse a un evento de cámara.
- `new List<T>()` o `.ToList()` — usa listas reutilizables o iteración directa.
- `StartCoroutine(...)` sin control de si ya hay una corriendo — puede acumular corrutinas.
- `SetActive` sin guard de cambio de estado — puede disparar layout rebuilds.
- `animator.parameters` (getter) — devuelve un array nuevo cada llamada. Cachear hashes en `Awake`.

**Physics queries:**
```csharp
// Nunca:
Collider[] hits = Physics.OverlapSphere(pos, radius);

// Siempre:
private readonly Collider[] _buffer = new Collider[32];
int count = Physics.OverlapSphereNonAlloc(pos, radius, _buffer, layerMask);
```

**LayerMask:**
```csharp
// Nunca en Update o métodos frecuentes:
LayerMask.GetMask("Enemy", "Boss");   // string lookup

// Cachear en Awake:
private LayerMask _enemyBossLayer;
void Awake() { _enemyBossLayer = LayerMask.GetMask("Enemy", "Boss"); }
```

**Reflection:** no usar `System.Reflection` en código de runtime frecuente. Es lento y frágil en IL2CPP. Si se necesita acceder a un miembro privado de Invector u otro plugin, crear una wrapper class o subclass.

### Distinguir personajes de geometría en raycasts/obstrucciones

los personajes (player, NPCs, party members) no tienen una capa propia — todos viven en `Default` junto con la mayoría de la geometría estática del mundo (confirmado en `Prefabs/_LIAM.prefab`, todo a `m_Layer: 0`). Por eso la capa sola no sirve para que un raycast de "¿hay pared/puerta en medio?" ignore a los personajes. Usar `NPCSimpleAnimator` como marcador fiable: lo tiene el player y TODOS los NPCs (mismo criterio que ya usa `DialogueManager.IsActualNPC`), y ningún objeto de escenario (puertas, muebles, props). Patrón:
```csharp
Transform root = hit.collider.transform.root;
if (root.GetComponent<NPCSimpleAnimator>() != null) continue; // es un personaje, no una obstrucción
```
Ejemplo real: `PlayerParty.FindClearDialogueFormationPosition` — evita teletransportar a un party member al otro lado de una puerta cerrada al posicionarlo para un diálogo (bug: NPC hablando desde detrás de una puerta, cámara pegada a la hoja).

### Convenciones de idioma

comentarios, documentación y mensajes de commit **en español**.

### Instancias y pools

- **`VfxPoolService`** (`Core/Pooling/VfxPoolService.cs`, Julio 2026): servicio global auto-creado (mismo patrón que `HudToastService`) para poolear VFX de un solo uso (impactos, explosiones, despawns). Sustituye el patrón `Instantiate(...); Destroy(fx, t)` repetido por todo el proyecto. Un pool por prefab (`ObjectPool<Transform>` internamente), devolución centralizada en un único `Update` (sin coroutines por instancia). Uso:
  ```csharp
  VfxPoolService.Instance.Play(prefab, position, rotation, lifetime);
  // Devuelve el Transform si hace falta ajustar escala u otros parámetros puntuales:
  Transform vfx = VfxPoolService.Instance.Play(prefab, pos, rot, 3f);
  if (vfx != null) vfx.localScale = Vector3.one * radio;
  ```
  Migrado ya en: `MagicProjectil.cs` (impactVFX/despawnVFX), `GolemBossAI.cs` (punchImpactVFX/shockwaveVFX/landingDustVFX — resueltos los 3 TODOs de pooling en la fase 3), `SlowMotionFireProjectile.cs`, `BattleOrb.cs`, `StarAwakeningSequencer.cs`, `DuoSpecialAttackSystem.cs` (vfxPrefab del ataque especial dúo), `NPCCombatLifecycleHandler.cs` (Agosto 2026 — `deathVFXPrefab`/`disappearVFXPrefab`/`disappearOnArrivalVFX`, los 7 sitios que hacían `Instantiate` suelto sin destruir nunca la instancia; era la causa de los anillos de VFX de muerte que quedaban flotando en el suelo indefinidamente tras terminar el combate, ver INC nuevo).
  **Pendiente de migrar** (mismo patrón `Instantiate`+`Destroy` o `Instantiate` suelto, candidatos directos): VFX de impacto/muerte en `ImpDemonAI.cs`, `EnemyProjectile.cs`, `LevitationTarget.cs`, `Damageable.cs`, `Burnable.cs`, `ChestInteractable.cs`, `WorldPickup.cs`, `PressurePlate.cs`, `UnlockTrigger.cs`. Migrar con el mismo patrón de arriba cuando se toquen esos archivos.
- Los proyectiles en sí (el `GameObject` que vuela, no solo su VFX de impacto) todavía usan `Instantiate/Destroy` directo (`MagicProjectileSpawner`, `ImpDemonAI`, `NPCCombatBrain`, `AllyCombatState`, `GolemBossAI` rocas). Pendiente: requiere pool con reset de estado (velocidad, target, `_ended`, buffers) — más delicado que un VFX y no migrado todavía.
- UI lists (ShopUI, QuestLogListUI, PlayerAbilitiesUI) que hacen Destroy+Instantiate en cada refresh deben migrar a pattern de reutilización: actualizar los elementos existentes, ocultar los sobrantes.
- Prefabs de escudo en `PlayerShieldController`: pre-instanciar en `Awake` y usar `SetActive` en lugar de `Instantiate/Destroy`.

### Debug.Log en producción

Cada `Debug.Log` con `$"..."` (string interpolation) genera allocaciones GC aunque el log esté vacío, porque la interpolación se evalúa antes de la llamada.

Regla: todos los logs de diagnóstico deben estar bajo:
```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
Debug.Log($"[Sistema] ...");
#endif
```

O mediante un flag de instancia: `[SerializeField] private bool debugMode;`

---

## 13. Bugs Conocidos Pendientes

Lista de issues identificados en la revisión de código de Mayo 2026, ordenados por severidad.
**Todos los críticos (C1-C6) están resueltos** (verificado Julio 2026) — el catálogo original quedó desactualizado tras una pasada de optimización posterior. Se dejan documentados abajo por referencia histórica y porque describen patrones a no reintroducir.

### Críticos — todos resueltos

| # | Sistema | Archivo | Descripción original | Estado |
|---|---------|---------|-----------------------|--------|
| ~~C1~~ | Audio | `AudioService.cs` | `StopAllCoroutines()` al cambiar música destruía corrutinas del pool SFX. Los AudioSource nunca se devolvían al pool. | **Resuelto.** Existe `StopMusicCoroutines()` dedicado que detiene solo las corrutinas de música por referencia explícita (`_crossfadeRoutine`, `_fadeOutRoutine`, etc.), nunca `StopAllCoroutines()`. Comentario explícito en el código sobre por qué. |
| ~~C2~~ | Save | `SaveSystem.cs` | Escritura no atómica: crash durante save corrompe el archivo. | **Resuelto.** `Save()` escribe a `SavePath + ".tmp"` y hace `File.Move` al path final tras borrar el anterior. Un crash a mitad de escritura deja el save previo intacto. |
| ~~C3~~ | Quests | `QuestManager.cs` | `FindObjectsByType` llamado en cada evento de inventario. O(n×m) por recogida de item. | **Resuelto.** `OnInventoryItemAdded` → `FindQuestChainEntry` consulta el índice cacheado `_questChainIndex` (`RebuildQuestChainIndex` solo corre cuando `_questChainIndexDirty`, es decir al cambiar de escena). |
| ~~C4~~ | Player | `MagicCaster.cs` | `new List<MagicSlot>(_slotCooldowns.Keys)` en `Update()` cada frame. | **Resuelto.** `Update()` ya itera el array estático `AllSlots` con `TryGetValue`, sin allocar. No queda ningún `new List<MagicSlot>(...)` en el archivo. |
| ~~C5~~ | Player | `PlayerBattleModeController.cs` | 3× `GetComponentInChildren` en `Update` por cada collider detectado. | **Resuelto.** `DetectEnemiesNearby()` ahora es `ActiveCombatRegistry.Count > 0` — O(1), sin `GetComponentInChildren` ni physics queries en `Update`. |
| ~~C6~~ | Player | `MagicProjectil.cs` | `Physics.OverlapSphereNonAlloc` en `Update` por cada proyectil en vuelo como fallback de colisión. | **Resuelto.** El `OverlapSphereNonAlloc` (comentado "OPTIMIZACIÓN FASE 2: NonAlloc") solo se ejecuta al resolver un impacto real (`ApplyDamageAndKnockback`), no en `Update()`. `Update()` solo mueve el proyectil y comprueba rango. |

> **Nota:** todo C1-C6 se dio por corregido en una pasada de optimización ("Fase 2") posterior al catálogo de Mayo 2026 que nunca se reflejó en esta tabla. Antes de asumir que un bug de esta lista sigue vivo, verificar el archivo actual — el catálogo puede estar desactualizado.

### Otros (no son bugs activos, documentados por contexto)

| # | Sistema | Archivo | Descripción |
|---|---------|---------|-------------|
| C7 | Save/Narrativa | `SavePoint.cs` + `NarrativeRunner.cs` | Condición de carrera: si el SavePoint se activa mientras un nodo narrativo está en progreso (ej: durante una cinemática), el blackboard puede guardarse antes de que `WaitCustomEventNode` escriba su flag `_received`. Al recargar, el runner reanuda desde `__currentNodeGuid` correctamente, pero si ese nodo ya fue procesado en la sesión anterior y el grafo avanzó, la cinemática no se repite — el runner continúa desde el nodo guardado. No es necesario ningún parche externo. |

### Importantes

Igual que con los críticos: verificado Julio 2026, la mayoría ya estaba resuelta (Fase 2). Se marcan con ~~tachado~~ los confirmados resueltos.

| # | Sistema | Archivo | Descripción | Estado (Jul 2026) |
|---|---------|---------|-------------|--------------------|
| I1 | Diálogo | `DialogueManager.cs` | Grace period (0.3s) reseteado en cada línea. Input parece no responder. | No verificado — es UX/correctness, no rendimiento. Revisar si se reporta. |
| ~~I10~~ | Core | `GameBootService.cs` | Corrutina `while (QuestManager.Instance == null) yield return null` sin timeout. | **Resuelto** (verificado Agosto 2026). El código actual no tiene ese bucle: `RestoreQuestsWhenReady` y `RestoreBlackboardsWhenHubReady` esperan con `WaitForSeconds(0.1f)` durante máximo 10 iteraciones (1s) y registran `Debug.LogError` si se agota el timeout. |
| I12 | Environment | `FogZone.cs` | Variables estáticas sin reset entre escenas. | **No es un bug — renombrado.** `FogZone.cs` no existe porque la lógica de zonas de música vive en `Assets/Scripts/Environment/AmbientZone.cs` (`MusicZoneId` ← `ambientPreset.musicZoneId`), ya consumida correctamente por `AudioService`. Solo quedaba un tooltip desactualizado en `AudioGraphProfile.AmbientZoneRule.zoneId` que mencionaba "FogZone" — corregido (Agosto 2026) para referenciar `AmbientZone`/`AmbientPreset`. |
| ~~I13~~ | Cinematics | `AdditiveSceneCinematic.cs` | `PlayAndBlock()` puede bloquearse indefinidamente si `Play()` retorna early. | **Resuelto** (verificado Agosto 2026). Ya tiene guardas explícitas: si `Play()` no inicia nada y no dispara `OnCinematicFinished`, la corrutina sale (`yield break`) sin bloquear; además hay un timeout de seguridad de 120s (`while (!finished && elapsed < 120f)`) que loguea warning y desuscribe si se agota. |
| ~~I2~~ | Diálogo | `DialogueManager.cs` | `GetComponent` múltiple por cada línea. Cachear en `StartDialogue`. | **Resuelto.** `_playerDialogueAnimator` cacheado y reutilizado por línea (comentario explícito en el código). |
| ~~I3~~ | UI | `MagicSlotsUI.cs` | `StartCoroutine(FlashSlotReady)` lanzado en Update cada frame que la condición es verdadera. Acumula corrutinas. | **Resuelto.** Guard `!slot.isFlashing` antes de lanzar la corrutina (comentario "guard against per-frame launch"). |
| ~~I4~~ | UI | `PlayerHealthUI.cs` | `AddListener` sin `RemoveListener` previo en `OnEnable`. Listeners duplicados tras Enable/Disable. | **Resuelto.** `OnDisable()` hace `RemoveListener` de ambos eventos antes de que `OnEnable()` vuelva a suscribir. |
| I5 | UI | `CollectiblePopupQueue.cs` | Locks innecesarios en código single-thread. `Canvas.ForceUpdateCanvases()` bloquea todos los canvases. | **Parcial.** El `ForceUpdateCanvases()` ya no está. Quedan 2 `lock()` pero solo en paths de error raros (prefab null / Instantiate falla) — impacto real ≈ 0. |
| ~~I6~~ | UI | `MenuManager.cs` | `new HashSet<MenuKind>(allowed)` en `AnyOpenExcept()` llamado desde Update. | **Resuelto.** Ahora es un `foreach` sobre `s_open` sin allocar. |
| ~~I7~~ | Shop | `ShopUI.cs` | Reflection para acceder a `currencyItem`. Exponer propiedad pública en ShopController. | **Resuelto.** Sin ninguna referencia a `System.Reflection`/`BindingFlags` en el archivo. |
| ~~I8~~ | Player | `PlayerActionManager.cs` | Reflection para leer `abilities.magic` (bool). Acceso directo. | **Resuelto.** Sin reflection en el archivo. |
| I9 | Player | `PlayerLevitationController.cs` | Reflection + boxing en `Update` para leer propiedades estáticas. | **Resuelto en ese archivo** (sin reflection). Existe reflection similar en `PlayerFlyingController.cs` (`CacheControllerLockFields`/`SetControllerLocks`, acceso a campos privados de Invector) pero cacheada y llamada solo 2×/vuelo (inicio/fin), no en `Update` — impacto real ≈ 0. |
| ~~I11~~ | Core | `SaveSystem.cs` | Escritura no atómica. Save puede corromperse. | **Resuelto** (duplicado de C2, ver arriba). |
| I14 | Cinematics | `SimpleCinematicDirector.cs` | `Time.timeScale` no se restaura si se para la cinemática con `StopCoroutine` sin destruir el objeto. | Probablemente mitigado: hay 5 puntos distintos que restauran `Time.timeScale = 1f` (incluida una ruta de cleanup con comentario "Restaurar tiempo"), pero no confirmado al 100% para todos los caminos de cancelación. |
| ~~I15~~ | Player | `PlayerCarrySystem.cs` | `Invoke` no cancelado en `OnDisable`. NullReferenceException si el GO se destruye durante el delay. | **Resuelto.** `OnDisable()` hace `CancelInvoke` de ambos invokes pendientes. |
| ~~I16~~ | Proyectiles | `MagicProjectil.cs` | Daño puede aplicarse dos veces por frame (OnTriggerEnter + CheckEnemyProximity en el mismo frame). | **Resuelto.** Guard explícito: `if (_lastDamagedCollider == col && _lastDamagedFrame == Time.frameCount) return;`. |
| ~~I17~~ | Inventory | `Inventory.cs` | `Resources.LoadAll<ItemData>("")` como fallback — carga todos los assets de Resources. Cachear resultado. | **Resuelto.** `_allItemsCache` solo se puebla una vez (lazy, `if (_allItemsCache == null)`) y se reutiliza después. |

### Bugs de terceros (motor / paquetes) — no son bugs del proyecto

| # | Sistema | Archivo | Descripción | Estado |
|---|---------|---------|-------------|--------|
| U1 | Diálogo | `DialogueManager.cs` / TextMeshPro (com.unity.ugui) | Bug de motor en `TMP_Text.SaveSpriteVertexInfo`: `NullReferenceException` al generar el mesh de un texto con `<sprite name="X">` cuando ese sprite se resuelve a través de la lista `fallbackSpriteAssets` (en vez de estar definido directamente en el sprite asset asignado al componente). `DialogueIcons.asset` (el sprite asset de `bodyText`) solo define `interactable_A` en su propia tabla; el resto de iconos usados en diálogo (`algas`, `boots`, `interactable_b/dpad/Joystick/lb/lt/rb/rt/x/y`, `lifePotion`, `start`) viven cada uno en su propio sub-asset fallback, así que cualquier línea con uno de esos iconos disparaba potencialmente el bug bajo word-wrap. Sin fix oficial de Unity (confirmado en el hilo de Unity Discussions "Nullreference in SaveSpriteVertexInfo": ni causa ni solución oficial, solo diagnóstico de que `m_currentSpriteAsset`/`spriteSheet` quedan null en el repaso interno). Verificado Agosto 2026. | **Mitigado (`DialogueManager.cs`, comentario junto a `TryForceMeshUpdate`).** **(0) raíz — DESACTIVADA 2026-08-12:** `PinSpriteTagsToExplicitAsset` reescribía `<sprite name="X">` a `<sprite="X" name="X">`, pero revisando el código fuente de TMP se confirmó que esa forma explícita NO recorre `fallbackSpriteAssets` (busca el asset por nombre en `MaterialReferenceManager`/`Resources`, que nunca lo encuentra para estos sub-assets) — rompía SIEMPRE el icono en vez de evitar el crash. Se dejó como no-op; los iconos vuelven a resolver por la vía de carácter de TMP (`SearchForSpriteByHashCode`, que sí recorre fallbacks y es la única que funciona con el montaje actual de `DialogueIcons.asset`). **(1)** `ProtectSpriteTagsFromWordWrap` envuelve cada tag en `<nobr>`. **(2) red de seguridad** — `TryForceMeshUpdate` captura la excepción si ocurre y hace `Debug.LogWarning` (no `LogError`, a propósito, para no disparar "Error Pause" en el Editor durante playtesting), dejando el diálogo en estado usable. **Límite conocido — CERRADO 2026-08-12:** la capa 2 solo cubría la llamada explícita a `ForceMeshUpdate()` al inicio de línea; cada asignación posterior a `bodyText.maxVisibleCharacters` (typewriter frame a frame en `TypeRoutine()`, y el camino sin typewriter en `ShowLine()`) dejaba el rebuild para el próximo pase automático de Canvas (`Canvas.SendWillRenderCanvases` → `TextMeshProUGUI.OnPreRenderCanvas`), que no pasa por nuestro código y no captura el NRE si salta ahí — exactamente el stack trace real reportado (sin ningún frame de `DialogueManager` en medio). Ahora esas asignaciones van seguidas de un `TryForceMeshUpdate()` explícito, así que el bug (si se dispara) queda dentro del try/catch en vez de como excepción no capturada. **Arreglo definitivo pendiente (requiere Editor de Unity):** mover el glyph de cada icono afectado a la tabla PROPIA de `DialogueIcons.asset` (como ya está `interactable_A`) en vez de dejarlo como sub-asset fallback. |

### Sesión Agosto 2026 — arreglos aplicados

Auditoría de conflictos entre Quests / grafo narrativo / FSM de NPCs, más limpieza de reflection y allocs encontrados por el camino. Cinco cambios aislados, sin tocar arquitectura:

- **`PlayerCarrySystem.cs` / `NPCItemDetector.cs`** — `NPCItemDetector.ForceStopCarrying()` usaba reflection (`GetField` sobre campos privados no públicos) para limpiar el estado de "cargando objeto" al entregar un ítem de quest a un NPC. Se añadió `PlayerCarrySystem.CancelCarrySilently()` (método público) y se eliminó la reflection.
- **`NPCQuestActionExecutor.cs`** — disparaba `onPostActionCompleted` vía `GetField` cuando el campo ya era un `UnityEvent` público en `QuestChainEntry`. Reflection innecesaria, eliminada.
- **`ProjectileCollisionHandler.ApplyKnockbackToNPC()`** — usaba `Physics.OverlapSphere` (alloc) + `LayerMask.GetMask` sin cachear para adivinar "el NPC más cercano" como instigador de un proyectil enemigo. Sustituido por `ActiveCombatRegistry.GetClosestCombatNPC(...)`, el mismo registro que ya usa el resto del proyecto (ver C5). Sigue sin ser un instigador real — para eso haría falta un campo de instigador en `EnemyProjectile` seteado en cada IA que dispara, pendiente si se necesita en el futuro.
- **`DuoSpecialAttackSystem.ApplyAoeDamage()`** — `Physics.OverlapSphere` → `OverlapSphereNonAlloc` con buffer reutilizable; `LayerMask.GetMask("Enemy","Boss")` cacheado en `Awake`.
- **`BranchBoolNode.cs`** — marcado `[Obsolete]`. Lee el valor del blackboard pero nunca lo usa para elegir una salida (siempre avanza); confirmado sin uso en ningún grafo del proyecto. `NarrativeGraphWindow` ya filtra tipos `[Obsolete]` al construir el menú "Añadir Nodo", así que desaparece de ahí sin tocar el editor.
- **INC-075 (`EstelaAppearsSequencer.cs`)** — la clave de flag "ya vista" que se añadió el 27-28/jul ("CINEMATIC_SEEN:ESTELA_APPEARS") no seguía la convención real del proyecto ("CINEMATIC_SEEN:Cinematic_{id}", la que ya usa `SimpleCinematicDirector` y la que llevan escrita los 9 `PlayerPreset_*.asset` existentes como "CINEMATIC_SEEN:Cinematic_EstelaAppears"). Al activar modo test contra cualquiera de esos presets (p.ej. `PlayerPreset_Taberna`, activado el 05/08), la clave nunca coincidía y `HasSequencePlayed()` daba `false` pese a que el preset sí registraba la cinemática como vista → arañas y guerreros reaparecían en `Awake()`. Se añadieron `CinematicSequencerBase.HasCinematicBeenSeen(id)`/`MarkCinematicAsSeen(id)` con la convención correcta y `EstelaAppearsSequencer` los usa ahora; se mantiene lectura de compatibilidad con la clave antigua por si algún save real (no preset) quedó escrito con ella entre el 27/jul y el 05/ago. Revisados también `TabernaSequencer`/`LiamGolemSummonSequencer` (únicos otros con flag `Cinematic_*` ya presente en los presets): ninguno de los dos desactiva actores de forma permanente, así que no necesitan el mismo mecanismo de ocultación — sus flags son solo marcadores narrativos.
- **INC-076 — jugador "pillado" tras fallar el Despertar de la Estrella (`AWAKEN_FAILED`).** En `MainNarrative_Cap1.asset`, el nodo `RaiseCustomEventNode` que emite `AWAKEN_START` (guid `0c0af6c1...`) hace fork en dos `WaitCustomEventNode`: uno espera `AWAKEN_DONE` (éxito), otro `AWAKEN_FAILED` (fallo). La rama de fallo vuelve a entrar en `WaitQuestCompleteNode`/`GiveInventoryItemNode` (no-ops porque la quest ya está completa y el ítem ya se dio) y de ahí de vuelta al mismo nodo fork, para reintentar la cinemática (`StarAwakeningSequencer`) — patrón ya reconocido en el código de `NarrativeRunner.RunSubGraph` (fork "revisitado en vivo", añadido para evitar el `StackOverflowException` que este mismo reintento causaba antes). Causa raíz de que el reintento se quedara "pillado" en vez de resetearse limpio: (1) `WaitCustomEventNode` marcaba su flag `__event_{guid}_{key}_received` en el blackboard como permanentemente `true` tras recibir el evento una vez — pensado solo para el resume tras recarga (ver `SavePoint`), pero al no limpiarse nunca, la segunda vuelta del bucle de reintento leía ese flag viejo y avanzaba sin esperar el evento real, disparando un reintento fantasma inmediato de `AWAKEN_START`. (2) Dos `StarAwakeningSequencer.Co_Sequence()` solapados llamaban ambos a `LockCinematic()` (dos `PushMode(ActionMode.Cinematic)`), pero `EndCinematic()` solo restaura una vez por el guard de `_cinematicLocked` → un `Push` se quedaba sin su `Pop` y el jugador perdía el control para siempre. **Arreglado** (Agosto 2026): `WaitCustomEventNode.Enter()` consume su propio flag al leerlo (lo deja en `false` en vez de dejarlo en `true` para siempre); `CinematicSequencerBase` ahora ignora la señal de entrada si ya hay una secuencia en curso (`_sequenceRunning`), garantizando `Push`/`Pop` siempre equilibrados pase lo que pase en el grafo; `NarrativeRunner` añade un contador de "generación" por nodo-fork (`_forkGeneration`, en memoria) para que las ramas de un fork que quedan obsoletas tras un re-fork en vivo se descarten en vez de duplicar el tramo de éxito (`UnlockAbilitiesNode` → `StartQuestNode` → `StartBattleNode`) si el evento de éxito llega igualmente para una rama abandonada. No se tocó `WaitQuestCompleteNode` ni la lógica de resume (`node == start`) de `StartFromStartNode`/`RelaunchForkBranches`.
- **INC-077 (16 ago 2026) — `AbilityUnlockPopupUI.cs` / `SceneBoundUI.cs` — el popup de desbloqueo de habilidad/hechizo dejó de verse.** Reportado específicamente para el desbloqueo del primer hechizo ("Bola Prisma") al final del Despertar de la Estrella. Causa raíz: `StarAwakeningSequencer.RaiseSignal(AWAKEN_DONE)` dispara síncronamente (ver nota en el propio archivo, línea ~341) `UnlockAbilitiesNode → StartQuestNode → StartBattleNode → BossIntroPresentation.PlayIntroduction()`, y esta última llama a `SceneBoundUI.BeginBossIntro()` **antes de su primer `yield`** — es decir, en el mismo frame en que `UnlockAbilitiesNode` acaba de disparar `UnlockService.OnSpellUnlocked` y `AbilityUnlockPopupUI.AnimateIn()` ya ha arrancado su animación de entrada. `BeginBossIntro()` añade (o reutiliza) un `CanvasGroup` en la raíz del propio GameObject `AbilityUnlockPopupUI` (distinto del `popupCanvasGroup` hijo que anima `AnimateIn()`) y lo funde a alpha 0 para ocultar toda la UI persistente — sin consultar en ningún momento el guard `ISceneBoundUIHideGuard`/`BlocksSceneHide()` que sí respeta `ApplySceneState()` para cambios de escena. Como el alpha de un `CanvasGroup` hijo se multiplica por el del padre, el popup quedaba invisible aunque su propia animación de entrada se ejecutara con normalidad. La comprobación existente `if (SceneBoundUI.IsBossIntroActive)` al principio de `ShowPopup()` no cubre este caso porque, en el momento en que se evalúa, la intro del boss todavía no ha empezado — empieza unas líneas de código (síncronas) después, dentro de la misma llamada. **Arreglado:** `BeginBossIntro()` ahora respeta `_hideGuard.BlocksSceneHide()` igual que `ApplySceneState()` — si el popup sigue en pantalla, no le toca el `CanvasGroup` y marca el fundido como pendiente (`_pendingBossIntroHide`); `ReapplySceneState()` (ya invocado por `AbilityUnlockPopupUI.HidePopup()` al terminar) aplica ese fundido pendiente si la intro del boss sigue activa, o no hace nada si ya terminó. `EndBossIntro()` limpia el flag pendiente sin tocar el `CanvasGroup` de esas instancias (nunca se llegó a fundir). No se tocó `AbilityUnlockPopupUI.cs` ni ningún prefab/escena — el guard ya estaba correctamente cableado (`AbilityUnlockPopupUI` implementa `ISceneBoundUIHideGuard` en el mismo GameObject), solo faltaba que `BeginBossIntro`/`EndBossIntro`/`ReapplySceneState` lo consultaran.
- **INC-078 (16 ago 2026) — `NPCCombatLifecycleHandler.cs` — en combate de dúo/grupo, la música y animación de victoria sonaban al derrotar al PRIMER enemigo (no al último), y la música de combate se restauraba (se quitaba) justo después de esa primera derrota en vez de esperar a que cayera todo el equipo.** Reportado por Raúl justo después del rediseño de combate en dúo del 15 ago 2026 (cada NPC dice su propia frase de derrota, ver bullet de arriba y sección 3 — Equipos de combate). Causa raíz: ese mismo rediseño incluyó el "FIX #25" — cachear `_teamMember = GetComponent<NPCTeamMember>()` una sola vez en `Awake()` para evitar 5 `GetComponent` sueltos (`OnDied`/`DeathRoutine`/`HandleGetUpDizzy`/`HandleMoveToAnchor`/`MoveTeamMembersToRandomPoints`). Pero `NPCTeamMember` no viene en el prefab: lo añade dinámicamente `NPCCombatTeam.Start()` (`AddComponent<NPCTeamMember>()` + `SetTeam(...)`), y Unity ejecuta **todos** los `Awake()` de la escena antes que **cualquier** `Start()`. En el instante de ese `Awake()` el componente todavía no existía en el GameObject, así que `GetComponent<NPCTeamMember>()` devolvía `null` — y como solo se asignaba una vez, quedaba `null` para siempre (nunca se refrescaba). Con `_teamMember` siempre `null`, `isInTeam` era siempre `false` para cualquier NPC de equipo, así que cada muerte se trataba como "combate terminado en solitario": `shouldCelebrate` en `DeathRoutine()` y la restauración de música en `HandleGetUpDizzy()` disparaban en el primer enemigo derrotado, no en el último de todos. **Arreglado:** se sustituyó el campo cacheado en `Awake()` por una propiedad `ResolvedTeamMember` que resuelve `GetComponent<NPCTeamMember>()` de forma perezosa — en el primer uso real, ya en combate y con el componente ya añadido por `NPCCombatTeam.Start()` — y lo cachea a partir de ahí. Los 5 puntos de uso ahora leen `ResolvedTeamMember` en vez del campo crudo `_teamMember`. No se tocó la lógica de `isLastTeamMember`/`shouldCelebrate` en sí (ya estaba bien escrita) — solo el dato de entrada (`teamMember`) que recibía, que llegaba mal por la carrera Awake/Start. **Trampa a no repetir:** ningún componente debe cachear `GetComponent<NPCTeamMember>()` (ni de ningún otro componente que `NPCCombatTeam` añada dinámicamente) en su propio `Awake()` — solo en `Start()` en adelante, o de forma perezosa.

- **INC-079 (19 ago 2026) — cierre definitivo de "Will invisible" (secuela final del bug documentado en § 4, "Sistema del Jugador"; `ActiveCharacterSwapper.cs`).** Reportado por Raúl con logs reales: `[ActiveCharacterSwapper] 🩺 Will NPC en encuadre pero SIN renderizar durante ~1s ... Aplicando autocuración` disparándose repetidamente al cambiar de personaje en ciertos puntos del mapa, pese a las tres rondas de fix previas (`updateWhenOffscreen`, lectura forzada de `.bounds`, `Animator.Update(0f)`, `allowOcclusionWhenDynamic=false` contra el Occlusion Culling horneado de `MainWorld.unity`, y el heal periódico `DiagnoseAndHealWillVisibility()` que toggla `Renderer.enabled`). Diagnóstico a partir del propio latido de diagnóstico 🫀 que ya emitía el heal: `rendererBounds` quedaba clavado en `center=(1561.96, 1001.40, 653.23)` mientras la posición real de Will era `(1414.76, 1001.01, 9.94)` — ~650 unidades de diferencia en Z, sin moverse ni un ápice entre latidos de 2,5s espaciados varios segundos. Eso descarta culling legítimo: es el AABB del `SkinnedMeshRenderer` completamente congelado, nunca recalculado desde el spawn. **Causa raíz real** (nunca tocada por las tres rondas anteriores, todas ellas parches reactivos sobre el síntoma): el rig de Will/Liam/Estela corre su `Animator` en `AnimatorCullingMode.CullUpdateTransforms` (valor por defecto del componente). Con ese modo, mientras el renderer no se considera "visible" el Animator deja de actualizar los huesos — y sin huesos moviéndose, el `SkinnedMeshRenderer` nunca recalcula sus bounds, así que nunca vuelve a considerarse visible: el mismo punto muerto que ya diagnosticaba el comentario original de `SpawnWillNpc()`, pero nunca resuelto de raíz. `Animator.Update(0f)` solo empujaba un frame puntual a través del interbloqueo; si ese frame no bastaba para que Unity marcara el renderer visible antes del siguiente test de culling, el interbloqueo se rearmaba de inmediato — de ahí que `updateWhenOffscreen` + `.bounds` forzado + el toggle de `Renderer.enabled` del heal periódico no lo cerraran del todo. **Arreglado:** `cullingMode = AnimatorCullingMode.AlwaysAnimate` en los tres puntos del Animator de Will/Liam/Estela — `SpawnWillNpc()` (`ActiveCharacterSwapper.cs:699`), `WarpNpcToPosition()` (`:1031`, cubre también a Liam/Estela residentes en escena que nunca pasan por `SpawnWillNpc()`) y el heal periódico `DiagnoseAndHealWillVisibility()` (`:383`, red de seguridad por si algo revierte el valor). Saca al Animator del modo que causaba el interbloqueo en vez de seguir empujando frames sueltos contra él: huesos y bounds se actualizan todos los frames, on-screen o no, así que no queda ningún estado "nunca visible" del que depender para salir del bucle. Coste real: 3-4 NPCs de equipo, no cientos — irrelevante en el profiler. **Confirmado por Raúl en juego (19 ago 2026): incidencia dada por solucionada.**

- **INC-080 (20 ago 2026) — bug "dos Estelas" al cargar partida — `PlayerPresetService.cs` (`IsSelectionCorrupted`).** Reportado por Raúl con captura de pantalla (dos modelos idénticos de Estela en el grupo que sigue a Will) y log completo de una carga real. El propio proyecto ya tenía nombrado y parcialmente mitigado este bug como "dos Estelas y un Will" en `ActiveCharacterSwapper.cs` (`EnsureHiddenNpcSuppressed`, red de seguridad cada 0.5s), pero esa mitigación solo cubre la mitad del problema: el NPC "oculto" (Liam/Estela) recuperando renderers por accidente mientras el controller lo representa. Este incidente es la otra mitad, nunca cubierta: el controller (el rig de Will, reutilizado para representar a quien esté activo) queda vestido con la apariencia de Estela **aunque el personaje activo sea Will**, mientras la Estela real del equipo sigue siendo un NPC aparte, visible y siguiendo a Will con normalidad — de ahí las dos Estelas simultáneas (más Will si además hay un `_willNpcInstance` de equipo en pantalla). **Causa raíz, con evidencia del log real:** `PlayerPresetService.InitializePresetService()` toma un "pre-snapshot" de Will (`CharacterAppearanceRegistry.SnapshotWillFromBuilderIfNeeded()`) leyendo lo que el builder muestra en ese instante del boot, ANTES de aplicar nada — pensado como referencia para detectar `preset.appearance` corrompido (`IsSelectionCorrupted()`). En la partida reportada ese pre-snapshot salió `[Body:Body09, Head:Head02_Female, Hair:Hair08, Eyes:Eye02, Mouth:Mouth02, Accessory:AC09_Ribbon]` — la apariencia de **Estela**, no la de Will (la causa de que el builder arrancara ya mostrando eso no se ha perseguido más allá — no hace falta para el fix: el bug real es que `IsSelectionCorrupted()` confiaba ciegamente en esa referencia sin validarla). Con la referencia (`willSnap`) ya corrompida, la selección REAL y correcta del preset (`[Body:Body05, Cloak:Cloak02, Head:Head01_Male, Hair:Hair01, Eyes:Eye01, Mouth:Mouth01]`, la apariencia auténtica de Will) "difiere del snapshot" en todas sus categorías — y como `Eyes:Eye01`/`Mouth:Mouth01` resultan ser partes genéricas que también figuran en la apariencia registrada de Liam, `IsSelectionCorrupted()` contó 2 coincidencias y marcó el preset (el único dato correcto de los dos) como "corrupto": log real `[PlayerPresetService] 🔍 2 partes de otro personaje detectadas en preset.appearance.` seguido de `⚠️ Corrupción detectada en preset.appearance. Aplicando snapshot existente del registry sin sobreescribir.` — es decir, se descartó la apariencia correcta de Will y se re-aplicó al builder la apariencia de Estela, dejándolo permanentemente vestido de Estela. **Arreglado:** `IsSelectionCorrupted()` ahora empieza comprobando que su propia referencia (`willSnap`) no se parezca ya a Estela/Liam (`LooksLikeOtherCharacter()`, nuevo helper) — si el pre-snapshot ya "es" otro personaje, no es un punto de comparación fiable y se omite la detección por completo, confiando en `preset.appearance` (que `SnapshotAppearanceToPreset()` solo escribe cuando el personaje activo es Will, así que por diseño debería llegar limpio). No se ha tocado la detección de corrupción en sí para el caso en que `willSnap` sí sea válido — sigue protegiendo contra el incidente original para el que se añadió. **Pendiente de confirmar por Raúl en juego** (no se ha podido reproducir en el Editor desde esta sesión) — si el pre-snapshot corrupto vuelve a aparecer con este fix puesto, el siguiente paso sería perseguir por qué el builder arranca mostrando la apariencia de Estela antes de que `PlayerPresetService` llegue a aplicar nada.

- **INC-081 (20 ago 2026) — "la cámara está en otro sitio" en diálogos grupales tras un teletransporte — `DialogueCinematicController.cs` (`StartCinematic`).** Reportado por Raúl con captura de pantalla (durante el diálogo grupal de los "arrestados", jugando con Estela activa: la cámara quedaba encajada contra un muro, viendo el grupo real a través de un hueco/puerta, en vez de encuadrar la sala donde de verdad transcurre la escena) y la descripción del patrón: "cuando controlamos a un personaje que no es Will, y vamos a una conversación grupal que transcurre en otro espacio — es decir, hay un teletransporte previo al diálogo grupal — la cámara se rompe". El archivo ya tenía un historial extenso (agosto 2026) de fixes para "cámara de grupo cuando el personaje activo no es Will" (filtrar `ActiveCharacterSwapper.HiddenNpc` del centro del grupo en `CalculateCameraPosition()`, `UpdateGroupFacing()`, el *breathing* de `LateUpdate()` y `FindSpeakerTransform()`) — todos esos filtros seguían correctos y no eran la causa aquí. **Causa raíz real, sin relación con el decoy:** el patrón narrativo típico es `NPCInteractiveNarrativeExecutor` encadenando `[Dialogue]` (p.ej. el guardia habla antes de encerrar al grupo) → `TeleportPlayer` (`ExecuteTeleportPlayer`, mueve solo a `PlayerService.Player`, el rig único — no hace `Physics.SyncTransforms` ni falta que le haga) → `[Dialogue]` grupal ya en la celda. `ExecuteDialogue()` pasa siempre `transform` (el propio GameObject del executor) como "NPC" a `DialogueManager.StartDialogue`, así que **el mismo transform de NPC dispara ambos diálogos encadenados**. `StartCinematic()` detecta diálogos encadenados dentro de un grace period de 0.2s (`chainDialogueGracePeriod`, para no parpadear cámara entre líneas consecutivas) comprobando `currentNPC == npc && currentPlayer == player` — con el mismo executor y el mismo rig, esa condición era CIEMPRE cierta entre el diálogo previo y el grupal, así que la rama de "reutilizar cinematográfica activa" se colaba siempre que el `TeleportPlayer` intermedio tardaba menos de 0.2s reales en ejecutarse (instantáneo si el `entry.teleportTransition` no cubre pantalla, o el resto de la corrutina no cede frames de sobra). Esa rama de reutilización solo actualizaba el índice de línea y aplicaba el plano de apertura — **nunca volvía a calcular `_groupCamBaseDir` (dirección de cámara), `_cachedGroupCenter`/`_groupLookAtTarget` (centro/mirada del grupo) ni volvía a llamar a `PlayerParty.PositionMembersForGroupDialogue()`**, y ni siquiera actualizaba `_isGroupConversation` si el diálogo previo no era grupal. Resultado: el diálogo grupal heredaba el montaje calculado para la posición/sala del diálogo ANTERIOR al teletransporte — de ahí la cámara "en otro sitio", literalmente calculada para una sala que ya no es la actual. Coincide con el patrón "solo con Will no se veía" simplemente porque las cadenas `[Diálogo] → TeleportPlayer → [Diálogo grupal]` del contenido actual ocurren en tramos donde el personaje activo es Liam/Estela, no porque hubiera una rama de código específica de Will (esta causa es independiente del `HiddenNpc`). **Arreglado:** se extrajo el bloque de montaje grupal (elegir `_groupCamBaseDir` vía `PickBestGroupCameraDirection`, `PositionMembersForGroupDialogue`, recalcular centro/mirada del grupo, orientar NPC/player/compañeros) a un método nuevo `SetupGroupConversationStaging()`, reutilizado tanto en el montaje completo como en la rama de diálogo encadenado: esta última ahora actualiza `_isGroupConversation = isGroupConversation` y, si el resultado es grupal, vuelve a llamar a `SetupGroupConversationStaging()` antes de aplicar el plano — sin perder la ventaja de no apagar/reencender la cámara dedicada (evita el parpadeo que la reutilización existía para evitar). Cubre tanto el caso "individual→grupal encadenado" como "grupal→grupal encadenado tras moverse" (p.ej. un diálogo grupal partido en dos con un `TeleportPlayer`/`Move` entre medias), ya que ahora siempre se recalcula si el resultado final es grupal, sin depender de si el tipo cambió respecto al diálogo anterior. **Pendiente de confirmar por Raúl en juego.**

**Herramienta nueva:** `Assets/NarrativeGraph/Editor/Validation/CrossSystemNarrativeValidator.cs` — menú `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)`. Recorre todos los `NarrativeGraph`, `NPCQuestConfig` y `NPCInteractiveNarrativeConfig` del proyecto vía `AssetDatabase` y avisa (no bloquea, no modifica nada) cuando: la misma quest está referenciada a la vez por un nodo del grafo (`StartQuestNode`/`CompleteQuestStepsNode`/etc.) y por `NPCQuestConfig.questChain` o `NarrativeCondition.targetQuest`; o el mismo evento custom es esperado por un `WaitCustomEventNode` y también usado por una `NarrativeCondition`/`ConditionalNarrative` del sistema Interactive. Es la red de seguridad para no repetir el patrón de INC-020 (estado duplicado sin vínculo entre los dos sistemas). Pensada para correr manualmente antes de una entrega, no en cada carga de escena.

**Política formal (ver también § 10 — Política formal: convivencia Interactive ↔ Grafo narrativo):** los dos motores narrativos (`NarrativeGraph` y `NPCInteractiveNarrativeExecutor`) siguen coexistiendo a propósito — un intento previo de unificarlos rompió el juego y el proyecto está demasiado avanzado para asumir ese riesgo ahora. `NPCInteractiveNarrativeExecutor` queda congelado: no se le añaden `NarrativeActionType` nuevos ni NPCs nuevos a su catálogo. Todo NPC o quest nueva se construye en `NarrativeGraph`. Antes de dar por buena una entrega, correr `El Sendero/Narrativa/Validar Interactive vs Grafo`.

---

## 14. Troubleshooting

### El jugador no spawnea en la posición correcta

**Síntomas:** posición errónea al iniciar desde una escena que no es Start.

**Checklist:**
1. `GameBootService` tiene Script Execution Order -1000 (Project Settings → Script Execution Order).
2. `Start.unity` está en Build Settings en posición 0.
3. `Assets/Editor/AutoBootstrapOnPlay.cs` existe.
4. Los logs muestran `[WorldBootstrap] GameBootService disponible después de N frame(s)` — si no aparece, el boot no llegó a WorldBootstrap.

**Si funciona desde Start pero no desde otra escena:**
`WorldBootstrap` tenía históricamente un timeout de 10 frames que causaba fallback con anchor hardcodeado. El código actual espera hasta 1800 frames (30s) y falla con error explícito. Verificar que no hay versión antigua del archivo.

### Triggers narrativos funcionan con JSON pero no con preset

**Causa habitual:** el preset fue capturado después de que el trigger ya se activó, por lo que contiene el flag `__event_XXX_received = 1`. El grafo narrativo lo interpreta como "ya procesado" y salta el nodo.

**Solución:** abrir el asset del preset en el Inspector, localizar el flag en el blackboard y eliminarlo.

**Causa alternativa:** el preset tiene `__currentNodeGuid` con `type` vacío. El sistema `SimpleBlackboard` infiere el tipo automáticamente ahora, pero versiones antiguas del preset podían tener este campo ignorado.

### Variables estáticas contaminadas entre sesiones de PlayMode

**Síntomas:** managers duplicados, eventos disparándose varias veces, valores incorrectos al hacer Play-Stop-Play.

**Solución:** añadir el patrón `ResetStatics` con `SubsystemRegistration` al manager afectado (ver sección 5 — Variables estáticas). Los managers más críticos ya lo tienen implementado.

### NPCs que caminan en sitio tras combate

`NavMeshAgent.updatePosition` queda en `false` tras la animación de muerte. Secuencia de restauración obligatoria: `updatePosition = true` → `Warp` → `SetDestination`. Ver sección 3 — Fix crítico.

### Música de batalla que no vuelve al ambiente

El stack de música de `AudioService` debe equilibrarse: cada `BeginBattleMusic` debe tener su correspondiente `OnBattleWonRestoreMusic` o `OnBattleAbortedRestoreMusic`. Si el stack se desbalancea, la música de ambiente no vuelve. Verificar que `NPCCombatLifecycleHandler` emite el evento correcto en todos los caminos de finalización de combate (victoria, huida, transición de escena).

### SFX que dejan de sonar / pool de audio agotado

Causa del bug C1 (StopAllCoroutines en PlayMusic). Hasta que se corrija, evitar cambios de música mientras haya muchos SFX activos. En builds de desarrollo, el pool hace log cuando se expande dinámicamente — si aparecen logs `[AudioService] SFX2D_dyn creado` con frecuencia, el pool se está agotando.

### Preset con datos de un estado incorrecto del juego

Al crear un preset para testing, capturarlo en el momento exacto del estado que quieres reproducir:
- **Antes** de que un trigger se active → el grafo esperará el trigger.
- **Después** de un evento → el flag `_received` estará en el blackboard y el grafo lo saltará.

Documentar en el nombre del asset el estado de juego que representa (ej: `Preset_AntesDeBatallaGolem`, `Preset_PostEstela_Unida`).

### Game Over y cinemáticas que se repiten al recargar

**Síntoma:** al morir, volver al menú y cargar partida, aparece una cinemática que ya se había visto.

**Causa esperada:** la última vez que se guardó con el SavePoint, el blackboard capturó el grafo en un estado anterior al de esa cinemática. Cargar la partida restaura ese estado exacto — el grafo vuelve al nodo donde estaba en el momento del guardado.

**Principio de diseño (no violar):**
- **Game Over / salir al menú = no se guarda nada.** El estado en memoria se descarta. Al recargar, el sistema lee el último JSON escrito por un SavePoint.
- **El save es la única fuente de verdad.** Nada en código debe compensar, inyectar eventos ni corregir el estado cargado.
- **El grafo narrativo se auto-restaura** vía `__currentNodeGuid` (reanuda desde el nodo exacto) y los flags `_received` (los `WaitCustomEventNode` ya procesados se saltan automáticamente).

**Si la cinemática se repite siempre aunque el SavePoint esté después de ella:** el SavePoint está guardando el estado antes de que el nodo `WaitCustomEventNode` escriba el flag `_received`. Solución: asegurarse de que el `WaitCustomEventNode` avanza al siguiente nodo antes de que el jugador llegue al SavePoint, o añadir un nodo intermedio que confirme el estado antes del guardado.

---

> **Nota (unificación de documentación, Agosto 2026):** las secciones 15-17 de abajo eran antes archivos `.md` sueltos en la raíz del proyecto (`ANALISIS_UNIFICACION_NARRATIVA.md`, `Diseno_Cielo_Nubes_y_Estrellas.md`, `Diseno_Refugio_Lluvia_y_Relaciones_NPC.md`). Se han movido aquí íntegros para que todo el proyecto se lea desde un único documento. Son documentos de **análisis/diseño**, no estado actual verificado del código como las secciones 1-14 — revisar su fecha y su propio estado de progreso interno antes de asumir que algo descrito ahí ya está implementado.
>
> **Segunda pasada (12 de agosto de 2026):** se ha hecho lo mismo con `STEAM_DEMO_CHECKLIST.md` y las tres auditorías (`AUDITORIA_CODIGO_2026-08-07.md`, `AUDITORIA_COMPLETA_ENTREGABILIDAD_2026-08-08.md`, `AUDITORIA_SISTEMAS_OBSOLETOS_2026-08-07.md`) — su contenido, sin duplicados, vive ahora integrado en las secciones 12, 18 y 19 de este documento, y esos 4 archivos ya no existen sueltos en la raíz. `AGENTS.md`/`CLAUDE.md` y `README.md` son distintos: son archivos que herramientas de IA y GitHub leen automáticamente, así que **siguen existiendo como archivos aparte** — se han recortado a un resumen corto con pointers a las secciones 1, 2, 10 y 12 de aquí en vez de duplicar el contenido completo (ver § 20).

---

## 15. Análisis: unificación del grafo narrativo y viabilidad como asset vendible

**Fecha:** Julio 2026
**Basado en:** lectura directa de `Assets/NarrativeGraph/`, `Assets/Scripts/Quests/`, `Assets/Scripts/Behaviour NPC/`, `Assets/Scripts/Attacks/`, `TDD.md` y `CLAUDE.md`.

---

### Resumen ejecutivo

El diagnóstico real es distinto al que probablemente esperabas. No os falta infraestructura de unificación — ya tenéis un hub multi-grafo funcionando en producción (`MainNarrative.asset` + `Secundary.asset`), un validador de grafos, un sistema de eventos con persistencia (`DefaultNarrativeSignals`), y el campo `chapter` ya existe en cada nodo. El problema de fondo es otro: **hay dos motores narrativos ejecutándose en paralelo**, no uno. `NarrativeGraph`/`NarrativeRunner` es uno. `NPCInteractiveNarrativeExecutor` (dentro de la FSM de NPCs, 1800+ líneas, con su propio enum de acciones, sus propias condiciones y su propio tracking de "ya se ejecutó") es el otro. Ambos hacen básicamente lo mismo — diálogo, quests, combate, fade de pantalla — con implementaciones independientes que no se conocen entre sí más que por el bus de eventos. Ese es el motivo estructural por el que "tocar una cosa rompe otra": no es fragilidad del grafo, es que hay dos fuentes de verdad para la misma historia.

Sobre el segundo punto: partir en subgrafos por capítulo no es un problema de diseño nuevo, es un patrón que **ya usasteis una vez** (la separación Main/Secundary) y que el propio motor soporta de forma nativa vía `NarrativeGraphHub` + eventos cruzados. Es mecánicamente sencillo; lo único que falta es tooling de búsqueda en el editor, que hoy no existe.

Sobre venderlo como asset: el núcleo del motor (`NarrativeRunner`, `NarrativeNode`, `SimpleBlackboard`, `INarrativeSignals`) es pequeño, limpio y genuinamente reutilizable. El problema es que 30 de los 34 tipos de nodo activos están atados a este juego concreto (Invector, vuestro `QuestManager`, singletons de UI propios). Es viable, pero implica separar "motor genérico" de "paquete de nodos de El Sendero" — y ese es justo el trabajo que además os conviene hacer para el objetivo interno.

---

### 1. Lo que ya tenéis (y no lo sabíais aprovechar del todo)

- **Multi-grafo real, no hipotético.** `NarrativeGraphHub` gestiona un array de `GraphSlot { label, graph, initialBlackboardValues }`, un `NarrativeRunner` por slot. Ya existen dos grafos (`MainNarrative`, `Secundary`) comunicándose por eventos custom (`RaiseCustomEventNode` en uno, `WaitCustomEventNode` en otro, casados por string key). El estado de guardado ya está indexado por `graphLabel`, así que el aislamiento de save-state por grafo ya funciona.
- **Validador de grafos ya escrito.** `NarrativeGraphValidator` detecta nodos huérfanos (BFS desde el start), GUIDs duplicados, campos vacíos en nodos de quest/evento, y cobertura de save-points. Se ejecuta automáticamente en `NarrativeGraphHub.ValidateGraphsForScene()`. Le falta una cosa importante para el split por capítulos: no valida que las claves de evento cruzadas entre grafos (`WaitCustomEventNode` esperando algo que ningún `RaiseCustomEventNode` de *otro* grafo dispara) tengan su contraparte.
- **Herramienta de timeline ya construida.** `NarrativeTimelineWindow` dibuja todos los grafos como pistas horizontales y detecta automáticamente las dependencias cruzadas por evento custom, con líneas discontinuas entre grafos. Es literalmente la visualización de "cómo quedaría partido en capítulos" — ya la tenéis, solo hay que usarla como referencia al planificar el split.
- **El campo `chapter` ya existe** en cada nodo y el editor (`NarrativeGraphWindow`) ya tiene un filtro por capítulo en la toolbar. Hoy es solo un filtro visual (atenúa nodos, no los oculta ni los separa en memoria) — es la prueba de que la intención de organizar por capítulos ya estaba ahí, solo falta llevarla a una separación real de assets.
- **`INarrativeSignals` es una interfaz limpia** con superficie reducida (Quest/Battle/Custom) y es el mejor punto de apoyo que tenéis para cualquier trabajo de desacople.

### 2. La causa real de la fragilidad

No es el tamaño del grafo. Son cuatro problemas concretos, verificados en código:

**Dos motores narrativos en paralelo.** `NPCInteractiveNarrativeExecutor` es un segundo intérprete de "historia" con su propio `NarrativeActionType` (Dialogue, Move, StartQuest, StartCombat, JoinParty, ScreenFade...) que duplica funcionalidad de nodos ya existentes en `NarrativeGraph` (`PlayDialogueNode`, `StartQuestNode`, `StartBattleNode`, `ScreenFadeNode`). Se dispara desde `NPCBrain.HandleInteraction()`, que literalmente comenta en el código "Sistema principal" (prioridad 2) vs "Sistema Legacy o Simple" (`questConfig.ProcessInteraction`, prioridad 3) — o sea, ya sabíais que había dos sistemas conviviendo.

**Cinco mecanismos de eventos incompatibles**, cada uno con semántica distinta de persistencia/pérdida:
1. `DefaultNarrativeSignals` — pub/sub con registro "sticky" (`_pending`/`_raised`), no pierde eventos si nadie escucha aún.
2. Eventos C# sueltos por manager (`QuestManager.OnQuestCompleted`, `DialogueManager.OnDialogueStarted`, `ActiveCombatRegistry.OnNPCEnteredCombat`) — fire-and-forget, si nadie escucha en el momento exacto, se pierde.
3. `UnityEvent` cableados desde el Inspector, duplicando los eventos C# de arriba para el mismo hecho (`NPCBehaviourManagerV2.onJoinedParty` + `OnJoinedParty` a la vez).
4. Flags en el blackboard como sustituto de evento (`__event_{guid}_{key}_received`) — necesario para el save/resume del grafo, pero es una cuarta semántica distinta.
5. Polling en vez de push: `NarrativeCondition.Evaluate()` relee `QuestManager.GetState()` cada vez en vez de suscribirse, y aun así mantiene su propio booleano `_customEventReceived` en paralelo.

**Estado duplicado — la misma verdad contada por varios sistemas.** Los casos más peligrosos que encontramos:
- "¿Ya se disparó el evento X?" se rastrea en tres sitios independientes: `DefaultNarrativeSignals._pending/_raised` (global), `NarrativeCondition._customEventReceived` (por NPC, no serializado), y el flag del blackboard del `WaitCustomEventNode` (por nodo, persistido). No hay ningún punto que los sincronice entre sí.
- "¿Ya se consumieron los ítems de esta quest?" tiene guardia de idempotencia en `QuestManager` (`_itemsConsumedForQuest`, con un comentario explícito sobre un bug real, INC-020) pero **no** en `NPCQuestConfig.ConsumeRequiredItems()`, que puede llegar al mismo resultado por otro camino sin esa protección. Es decir: el bug que ya arreglasteis una vez en un sitio, sigue vivo en el otro camino.
- `QuestManager.IsPartyMemberMatch`/`ExtractNarrativeBaseName` y `NPCQuestConfig.IsPartyMemberMatch`/`ExtractNarrativeBaseName` son **literalmente el mismo código copiado dos veces** en dos archivos distintos.
- Dos configs de NPC pueden referenciar la misma quest de forma independiente (`NPCQuestConfig.questChain[i].questData` y `NarrativeCondition.targetQuest`) sin ningún vínculo entre ambas — renombrar o cambiar una no avisa de que la otra quedó desincronizada.

**La interfaz `INarrativeSignals` existe pero se ignora fuera de los nodos.** Los 17 archivos de `Assets/Scripts/` que hablan con el grafo lo hacen todos contra el singleton concreto `DefaultNarrativeSignals.Instance`, nunca contra la interfaz. Y dentro de los propios nodos, `CompleteQuestStepsNode` y `StartBattleNode` se saltan la interfaz y llaman a `QuestManager.Instance` directamente (o, en el caso de `StartBattleNode`, usan reflection como último fallback contra un `MissionManager` que ya ni siquiera existe en el proyecto). Esto contradice la regla de CLAUDE.md sobre no usar reflection en runtime, y es la clase de deuda que hace que cualquier refactor de `QuestManager` pueda romper el grafo sin que el compilador avise.

### 3. Plan de unificación — sin romper lo que ya funciona

El orden importa: cada fase deja el juego jugable y no depende de que la siguiente se complete. Nada de esto toca las invariantes del grafo narrativo descritas en `CLAUDE.md` §4 (test mode, `StopExecution`, `WaitQuestCompleteNode`, etc.) — de hecho las refuerza, porque elimina las rutas alternativas que hoy las esquivan.

**Fase 1 — Cerrar las fugas de la interfaz (bajo riesgo, alto valor inmediato).**
Hacer que `CompleteQuestStepsNode` y `StartBattleNode` pasen exclusivamente por `ctx.Signals`/`IQuestService`, eliminando las llamadas directas a `QuestManager.Instance` y toda la reflection muerta contra `MissionManager`. Cambiar `QuestServiceAdapter.Offer()`/`Complete()` de reflection a llamadas directas tipadas (ya conocéis la API real de `QuestManager`, no hace falta la indirección). Esto no cambia comportamiento observable, solo elimina rutas frágiles. Se puede hacer nodo a nodo, probando cada uno con los presets de testing existentes.

**Fase 2 — Un único dueño por cada hecho duplicado.**
Decidir y documentar, por cada fila de la tabla de estado duplicado de la sección 2, cuál sistema es la fuente de verdad y cuál pasa a *leer* de ahí en vez de mantener su propia copia:
- "¿Evento X ya recibido?" → dueño: `DefaultNarrativeSignals`. `NarrativeCondition` deja de mantener `_customEventReceived` y pregunta a `Signals.HasReceived(key)` (añadir ese método si no existe).
- "¿Ítems de quest consumidos?" → dueño: `QuestManager._itemsConsumedForQuest`. `NPCQuestConfig.ConsumeRequiredItems()` deja de reimplementar la lógica y llama al método de `QuestManager`.
- Eliminar la duplicación literal de `IsPartyMemberMatch`/`ExtractNarrativeBaseName`: mover a una clase de utilidad compartida (`QuestMatchingUtils`) que ambos archivos referencien.

**Fase 3 — Migrar `NPCInteractiveNarrativeExecutor` al grafo, NPC a NPC.**
Esta es la fase que realmente "unifica en un único sitio", y es la más delicada, por eso va después de las anteriores y se hace de forma incremental. Cada `ConditionalNarrative` de un NPC es, en esencia, un mini-grafo lineal con condición de entrada. La migración natural es: por cada NPC, convertir su cadena de `NarrativeActionType` en un subgrafo pequeño dentro de `NarrativeGraph` (usando los nodos que ya existen: `PlayDialogueNode`, `StartQuestNode`, `StartBattleNode`, etc.), activado por el mismo evento custom que hoy dispara la ejecución legacy. Mientras dura la migración, ambos sistemas coexisten sin conflicto porque no comparten estado (una vez completada la fase 2, si comparten estado, será a través de la única fuente de verdad). Se retira `NPCInteractiveNarrativeExecutor` NPC por NPC según se van migrando y verificando en juego, no de golpe. Este orden es exactamente lo que pedís: nunca hay un momento en que "lo viejo" y "lo nuevo" estén ambos rotos a la vez.

**Fase 4 — Split en subgrafos por capítulo.**
Ver sección 4, es independiente de las fases 1-3 y se puede intercalar en cualquier momento porque el motor ya lo soporta.

### 4. Subgrafos por capítulo — cómo hacerlo con el motor actual

Mecánicamente es el mismo patrón que ya usasteis para separar `Secundary.asset` de `MainNarrative.asset`, así que no es territorio nuevo:

1. Usar el campo `chapter` que ya tienen los nodos (y el filtro que ya existe en `NarrativeGraphWindow`) para decidir los cortes. `NarrativeTimelineWindow` os da la vista de qué nodos ya se comunican entre sí por evento custom — esas fronteras naturales son los mejores puntos de corte, porque ya están desacoplados por evento en vez de por referencia directa de GUID.
2. Por cada nodo cuyo `output` apunte a un nodo que se va a mover a otro asset: sustituir esa arista por un par `RaiseCustomEventNode` (al final del capítulo origen) / `WaitCustomEventNode` (al principio del capítulo destino), con una clave de evento nueva y descriptiva (`CH2_START`, no reutilizar claves existentes). `NarrativeGraphValidator` detecta huérfanos automáticamente, así que corred la validación después de cada corte — es vuestra red de seguridad.
3. Cada capítulo nuevo necesita su propio `StartNode` y se registra como un `GraphSlot` nuevo en el objeto de `NarrativeGraphHub` en `Start.unity`.
4. Antes de romper nada, extended `NarrativeGraphValidator` para comprobar que toda clave usada en un `WaitCustomEventNode` tiene un `RaiseCustomEventNode` correspondiente en *algún* grafo del proyecto (hoy solo avisa genéricamente, no cruza contra otros assets — es un hueco real y barato de cerrar, y os hará falta en cuanto haya más de dos o tres grafos).

**El problema de "buscar algo es eterno" es un problema de tooling de editor, no de tamaño de asset**, y se puede resolver hoy sin esperar al split: `NarrativeGraphWindow` no tiene caja de búsqueda por texto/nombre de nodo — solo el filtro por capítulo (que atenúa, no oculta) y el minimapa por defecto de `GraphView`. Añadir una búsqueda por texto que centre y resalte nodos (barata de implementar sobre `GraphView`, es un patrón estándar) os dará alivio inmediato incluso antes de partir nada. Recomiendo hacerlo primero — es la mejora de coste más bajo y beneficio más inmediato de todo este análisis.

También merece la pena, de paso, unificar las dos paletas de colores/categorías de nodo que ya han divergido entre `NodeView.ExplicitPalette` (editor de grafo) y `TypeToCategory` (timeline window) — son dos mapas mantenidos a mano por separado y ya no coinciden en algunos tipos de nodo. Una única fuente (un atributo `[NarrativeNodeCategory("Combate")]` en cada clase de nodo, leído por reflection *solo en editor*, que ahí sí es aceptable) resolvería esto de raíz y de paso os prepara el terreno para el punto siguiente.

### 5. Viabilidad como asset reutilizable / vendible

Es realista, pero con una condición: lo que se vende no es "vuestro grafo narrativo", es el motor que hay debajo, separado de las decisiones específicas de este juego.

**Lo que ya es vendible tal cual (con limpieza menor):** `NarrativeRunner`, `NarrativeNode`, `NarrativeGraph` (ScriptableObject + `[SerializeReference]`), `SimpleBlackboard`, `NarrativeGraphHub`, `INarrativeSignals`, `NarrativeGraphValidator`, y el editor base (`NarrativeGraphView`, `NodeView`, minimapa, sistema de fork/join con guardado). Es un motor de grafo narrativo con save/resume robusto, multi-grafo, y validación — eso por sí solo ya tiene valor de mercado; hay poca oferta con soporte de save-state tan cuidado en el Asset Store.

**Lo que NO es vendible sin separar:** de los 34 tipos de nodo activos, ~30 dependen de clases concretas de este juego — Invector (`vThirdPersonCamera`, `vThirdPersonMotor` en `PlayerLockService`), vuestro `QuestManager`/`Inventory`/`WardrobeService`/`UnlockService`, singletons de UI propios (`LorePopupUI`, `DramaticTextOverlayUI`, etc.). La solución estándar en este tipo de asset (y la que usan los pocos competidores serios, tipo node-based dialogue systems) es dos paquetes: un **core** genérico con nodos abstractos (diálogo vía interfaz que el comprador implementa, espera-evento, condición, branch, sub-grafo) y un **paquete de ejemplo** con nodos concretos que sirven de plantilla — nunca los del propio juego.

**Otros obstáculos concretos antes de publicar, ya verificados en el código:**
- El tooling de editor está repartido entre `Assets/NarrativeGraph/Editor/` y `Assets/Editor/` (`NarrativeQuickTestWindow`, `NarrativeTimelineWindow`) — hay que consolidarlo en una sola carpeta de paquete antes de empaquetar como `.unitypackage` o paquete UPM.
- Hay acoplamiento por reflection entre herramientas del propio editor (`NodeView` busca `NarrativeGraphWindow` por reflection para el "Quick Test desde aquí"; `NarrativeAutoSetup` asigna un campo privado de `NarrativeRunner` por reflection en vez de usar el setter público que ya existe) — son arreglos rápidos y necesarios para que el código pase cualquier review de Asset Store.
- La estrategia de migración de esquema actual es "nunca romper compatibilidad, dejar las clases obsoletas vivas para siempre" (`MigrateDeliverNodes.cs` está vacío, 0 bytes — una migración abandonada). Funciona para un proyecto propio pero no escala a terceros: necesitaríais una migración real versionada (detectar versión del asset, aplicar transformación, no solo "clase marcada Obsolete y ya").
- `PlayerLockService` vive bajo `NarrativeGraph/Runtime/Services/` pero no tiene nada de narrativo — es un servicio de bloqueo de jugador con dependencia directa de Invector. Debería mudarse fuera del paquete narrativo antes de extraerlo.

**Ruta recomendada, en orden:**
1. Hacer las fases 1-2 de la sección 3 igualmente (limpiar reflection, interfaces consistentes) — es trabajo que necesitáis para vuestro propio juego y es exactamente el mismo trabajo que hace falta para poder vender el motor después. No es esfuerzo duplicado.
2. Cuando el core esté limpio, extraerlo como **paquete local UPM dentro del mismo proyecto** (`Packages/com.sendero.narrativegraph/`), consumido por vuestro juego como dependencia local. Esto os obliga a trazar la línea real entre "motor" y "nodos de El Sendero" sin todavía comprometeros a publicar nada, y la prueba de fuego es si el juego sigue funcionando igual tras la extracción.
3. Solo después de tener esa separación viviendo y probada en producción (vuestro propio juego), evaluar publicarlo. En ese punto el coste marginal de preparar la documentación y el paquete de ejemplo es bajo, porque el trabajo estructural ya estaría hecho para vuestras propias necesidades.

No lo pondría antes: intentar generalizar el motor para terceros antes de haber resuelto la Fase 3 (los dos motores narrativos en paralelo) sería generalizar sobre una base que vosotros mismos sabéis que tiene un problema de diseño sin resolver.

### 6. Qué haría primero, si tuviera que priorizar

Por coste/beneficio, en este orden: (1) búsqueda por texto en `NarrativeGraphWindow` — una tarde de trabajo, alivio inmediato al dolor de "buscar es eterno"; (2) Fase 1 (cerrar fugas de interfaz, quitar reflection muerta) — bajo riesgo, hace el sistema más predecible ya; (3) Fase 2 (unificar estado duplicado) — aquí es donde realmente deja de romperse una cosa al tocar otra; (4) Fase 4 (split por capítulos) — se puede hacer en paralelo a lo anterior en cuanto haga falta por tamaño; (5) Fase 3 (fusionar `NPCInteractiveNarrativeExecutor` en el grafo) — el cambio de mayor impacto pero también el más largo, hacedlo NPC a NPC y sin prisa; (6) extracción como asset — dejadlo para cuando el juego esté más cerca de salir, no antes.

---

### 7. Progreso — Julio 2026 (segunda pasada)

Hecho, verificado por referencias cruzadas en el código (no solo propuesto):

- **Búsqueda por texto**: añadida a `NarrativeGraphWindow` (caja de búsqueda + Enter/Shift+Enter para saltar entre coincidencias, resalta el nodo con borde dorado usando `FrameSelection()`).
- **Fase 1**: `CompleteQuestStepsNode` y `StartBattleNode` ya no llaman a `QuestManager.Instance` ni usan reflection — todo pasa por `ctx.Signals` (`INarrativeSignals.CompleteQuestStepByConditionId`, nuevo). `StartBattleNode` perdió ~150 líneas de reflection muerta contra un `MissionManager` que no existe en el proyecto (confirmado por búsqueda global). `QuestServiceAdapter.Offer()`/`Complete()` pasaron de `GetMethod(...).Invoke(...)` a llamadas tipadas directas.
- **Fase 2**: `DefaultNarrativeSignals` tiene ahora un registro durable `_everRaised`/`HasEverRaised(key)` (no se vacía al consumirse, a diferencia de `_pending`/`_raised`, que siguen intactos) — `NarrativeCondition` lo usa como respaldo de su caché local de "evento custom recibido", cerrando el caso donde una suscripción tardía perdía el evento en silencio. `IsPartyMemberMatch`/`ExtractNarrativeBaseName`, que estaban copiados letra por letra en `QuestManager.cs` y `NPCQuestConfig.cs`, ahora viven en `Assets/Scripts/Quests/QuestMatchingUtils.cs` y ambos delegan ahí. Se encontró y cerró una variante real (no hipotética) del bug INC-020: `NPCQuestConfig.HandleQuestState()` consumía los ítems requeridos directamente y sin guardia de idempotencia, y a continuación `FinishQuest()` llamaba a `QuestManager.CompleteQuest()`, que los consume otra vez (esta vez con guardia, pero tarde). Se quitaron las tres llamadas duplicadas y el método que reimplementaba el consumo.
- **Fase 4**: nueva herramienta de editor `Assets/NarrativeGraph/Editor/ChapterSplitWindow.cs` (`El Sendero/Narrativa/Dividir por Capítulo...`). `MainNarrative.asset` ya tiene 88 nodos reales etiquetados en 6 capítulos (Cap. 1–6), así que no hay que inventar dónde cortar. La herramienta extrae todos los nodos de un capítulo elegido a un `NarrativeGraph` nuevo, sustituye las aristas que cruzan la frontera por pares `RaiseCustomEventNode`/`WaitCustomEventNode` (en ambas direcciones), intenta asignar el `StartNode` del capítulo nuevo, corre `NarrativeGraphValidator` sobre los dos grafos resultantes y muestra un reporte. No toca el YAML a mano — opera en memoria sobre `NarrativeGraph.nodes` y deja que Unity serialice, igual que hace el propio editor de grafo. Deliberadamente NO registra el nuevo grafo como `GraphSlot` en el `NarrativeGraphHub` de `Start.unity` (eso requiere la escena abierta y una decisión de nombre/orden) ni asigna el Start cuando hay más de un punto de entrada — ambas cosas quedan como paso manual señalado en el reporte de la propia herramienta. Recomendación: haced commit antes de correrla (la propia herramienta lo pide con un diálogo de confirmación) y revisad el resultado visualmente antes de dar la separación por buena.
- **Fase 3 — hallazgo importante que cambia el plan**: añadí un puente aditivo y seguro (`NPCBrain.HandleInteraction()` ahora emite siempre `NPC_INTERACT_{persistenceId}` vía `DefaultNarrativeSignals`, sin condicionar ni cambiar el comportamiento existente) — es el mecanismo que le faltaba al grafo para poder reaccionar a "el jugador habló con el NPC X" sin pasar por `NPCInteractiveNarrativeExecutor`. Pero al buscar un NPC piloto sencillo para migrar de verdad (revisé los 12 `NPCInteractiveNarrativeConfig` del proyecto; el más simple con contenido real es `Victoria`, 2 líneas de diálogo condicionadas al estado de una quest, sin combate ni movimiento) me encontré con esto: el catálogo de nodos actual **no tiene forma de expresar "diálogo distinto según el estado de la quest, re-evaluado cada vez que hablas con el NPC"** — que es exactamente el patrón de `ConditionalNarrative`/`NPCInteractiveNarrativeExecutor.TryExecuteNarrative()` (re-chequea condiciones en cada interacción y elige la primera que aplica). El grafo es lineal/uno-de-una-vez por diseño; no hay un nodo tipo "branch por estado de quest" (`BranchBoolNode` existe pero está roto — según el análisis anterior, siempre avanza sin mirar el valor) ni un mecanismo de "esperar la próxima interacción y volver a evaluar" con bucle. Conclusión honesta: fusionar `NPCInteractiveNarrativeExecutor` en el grafo no es solo mover datos NPC a NPC como decía el plan original — primero hace falta construir 1-2 tipos de nodo nuevos (`BranchOnQuestStateNode` y algo como `WaitForNPCInteractionNode` con reentrada) antes de que la primera migración real tenga sentido. No lo he hecho a ciegas porque diseñar y verificar un nodo con lógica de bucle sin poder abrir el Editor y probarlo en juego es justo el tipo de cambio que no debería intentar sin que lo veas tú primero. Recomendación: cuando quieras seguir con esto, empezamos diseñando esos 1-2 nodos juntos (con Play Mode a mano para probarlos) antes de tocar el asset de ningún NPC real.

### 8. Progreso — Agosto 2026 (tercera pasada): vista unificada de Editor (grafo + diálogos + quests en una sola pantalla)

**Fecha:** 15 de agosto de 2026.
**Pregunta origen:** ¿se puede ver, para un capítulo dado, la línea del grafo actual con los diálogos de NPC y las quests integrados en una única interfaz gráfica — sin crear sistemas nuevos, sin reducir el tamaño del grafo — con el mínimo impacto de migración posible?

**Aclaración importante antes de nada — esto NO es el intento que rompió el juego.** El intento de unificación de Agosto 2026 documentado en §10 y §13 fusionaba **dos motores narrativos en tiempo de ejecución** (`NarrativeGraph`/`NarrativeRunner` vs `NPCInteractiveNarrativeExecutor`) — tocaba lógica de juego real, por eso rompió cosas. Lo que se pide aquí es distinto de raíz: **una vista de Editor** que muestre en un solo sitio contenido que ya existe y ya se ejecuta exactamente igual que hoy. Cero clases de `Assets/Scripts/` en tiempo de ejecución cambian. Todo el trabajo vive en `Assets/NarrativeGraph/Editor/` y `Assets/Editor/` (código `UNITY_EDITOR`-only, no compila en build). Es un proyecto con un perfil de riesgo completamente distinto al que ya falló, y no reabre esa decisión.

**Diagnóstico — por qué hace falta abrir 6-8 ventanas para leer una escena narrativa completa.** El proyecto no tiene un problema de "falta de herramientas": tiene el problema contrario. Ya existen, contando solo lo relevante para "leer/ver" la historia (no las de testing/migración):

| Herramienta | Qué muestra | Archivo |
|---|---|---|
| `NarrativeGraphWindow` | El grafo de nodos, editable, con filtro por capítulo (atenúa) y búsqueda por texto | `Assets/NarrativeGraph/Editor/NarrativeGraphWindow.cs` |
| `NarrativeTimelineWindow` | Todos los grafos como pistas horizontales, dependencias cruzadas por evento | `Assets/Editor/NarrativeTimelineWindow.cs` |
| `NarrativeCrossReferenceWindow` | Mapa de referencias cruzadas de TODOS los ScriptableObjects: eventos, diálogos, quests, desbloqueos | `Assets/Editor/NarrativeCrossReferenceWindow.cs` |
| `NarrativeFactBrowserWindow` | Vista plana de "todos los hechos" (blackboard, quests, señales, estado de jugador) — ya se llama a sí misma "vista unificada" pero solo de **estado runtime**, no de contenido narrativo | `Assets/Editor/NarrativeFactBrowserWindow.cs` |
| `NPCNarrativeCardWindow` | Ficha de un NPC: sus diálogos, eventos y quests referenciados | `Assets/Editor/NPCNarrativeCardWindow.cs` |
| `DialogueAssetEditor` | Un `DialogueAsset` como guion de cine (inspector a medida) | `Assets/Editor/DialogueAssetEditor.cs` |
| Inspector por defecto | Un `QuestData` (sin editor a medida) | — |

Cada una es una herramienta sólida y bien escrita — el problema no es la calidad, es que **para reconstruir mentalmente "qué pasa en el Capítulo 1"** hay que abrir varias a la vez y cruzar la información a mano: el grafo te da el orden de los nodos, pero un `PlayDialogueNode` solo muestra una referencia a un asset (hay que abrirlo aparte para leer el texto), y un `StartQuestNode`/`CompleteQuestStepsNode` solo muestra un **string** `questId` (ni siquiera hay una referencia clicable — hay que buscar a mano o abrir `NarrativeCrossReferenceWindow`/`NPCNarrativeCardWindow` para saber a qué `QuestData` corresponde y qué dice).

**Esto no es solo incomodidad — es la causa mecánica de una parte real de los bugs "de lo mismo".** Verificado contra el código, no es una hipótesis: `StartQuestNode.questId`, `CompleteQuestStepsNode.questId`/`stepConditionIds` y `NarrativeCondition.targetQuest` son todos strings sueltos, sin ningún vínculo estructural con el `QuestData` real ni entre ellos (confirmado en `Assets/NarrativeGraph/Runtime/Graph/NodeTypes/StartQuestNode.cs` y `CompleteQuestStepsNode.cs`, y ya señalado en la sección 2 de este mismo análisis: "dos configs de NPC pueden referenciar la misma quest de forma independiente... sin ningún vínculo entre ambas"). INC-020 y la variante cerrada en Fase 2 (§15.7) son exactamente esto: la misma quest tocada desde dos sitios que no se veían el uno al otro porque no había ninguna pantalla que los mostrara juntos. `CrossSystemNarrativeValidator` es la prueba de que ya sabéis esto — es un validador que existe específicamente para detectar cuando el grafo y el sistema Interactive tocan la misma quest sin saberlo. Una vista unificada no sustituye ese validador (que se queda, es la red de seguridad automática), pero ataca la causa un paso antes: si al diseñar un nodo de quest ya ves en pantalla el `QuestData` real que resuelve ese `questId` — su nombre, sus steps, qué otros nodos/NPCs lo tocan — la mayoría de estos desajustes se evitan antes de guardar, en vez de descubrirse al correr el validador o, peor, en producción.

**Propuesta de diseño — extender `NarrativeGraphWindow`, no crear una herramienta nueva.** El motor ya tiene todas las piezas: referencia tipada a `DialogueAsset` en `PlayDialogueNode` (trivial de previsualizar), campo `chapter` con filtro ya construido, y tres herramientas que ya hacen búsqueda de referencias por `AssetDatabase` de forma redundante entre sí. La pieza que falta no es más UI — es **un único índice compartido** y **un panel de contenido inline**, ambos aditivos:

1. **`NarrativeProjectIndex`** (nuevo, ~medio día) — clase estática editor-only en `Assets/NarrativeGraph/Editor/`. Construye una vez (bajo demanda, invalidada por `AssetPostprocessor` o botón "Refrescar") los diccionarios `questId → QuestData`, `DialogueAsset → nodos/NPCs que lo usan`, `npcId → NPCConfiguration`. Hoy `NarrativeCrossReferenceWindow`, `NPCNarrativeCardWindow` y `NarrativeFactBrowserWindow` construyen cada una su propio barrido de `AssetDatabase` por separado — consolidarlo en un único sitio no es solo para la vista nueva, también simplifica el mantenimiento de las tres ventanas existentes (pueden migrar a consumirlo cuando convenga, sin prisa, cada una es un cambio aislado). Cero riesgo: no se conecta a nada todavía.
2. **Panel de inspección enriquecido, dentro de `NarrativeGraphWindow`** (~1 día) — al seleccionar un nodo, un panel lateral (nuevo `VisualElement`, al lado del `extensionContainer` que ya existe, sin tocarlo) muestra:
   - `PlayDialogueNode` → el guion completo (reutilizando el mismo renderer que ya construye `DialogueAssetEditor` para el Inspector, como método estático compartido en vez de código duplicado) en vez de solo el campo de referencia.
   - `StartQuestNode` / `CompleteQuestStepsNode` / `OfferQuestNode` / `WaitQuestCompleteNode` → el `QuestData` resuelto vía `NarrativeProjectIndex`: nombre, descripción, lista de steps con el `conditionId` que ese nodo concreto está completando resaltado, y un aviso visual si el `questId` no resuelve a ningún asset (typo detectado al momento, no al correr el validador).
   - Nodo con `npcId` → mini-ficha del NPC (nombre, otros nodos/grafos que lo tocan), a partir del mismo trabajo ya hecho en `NPCNarrativeCardWindow`.
3. **Filtro de capítulo → modo foco real** (~2 horas) — hoy `_activeChapter` solo atenúa (`narrative-node--dimmed`), como ya señala la sección 4 de este análisis. Añadir un toggle "Ocultar en vez de atenuar" convierte el filtro ya existente en exactamente el "ver Capítulo 1 y solo Capítulo 1" que se pide, sin construir nada nuevo — es la puerta de entrada natural a "grafo + diálogos + quests de un capítulo, todo junto, sin ruido de los otros cinco".
4. **Opcional, más caro y no crítico para el objetivo pedido** (~1-2 días, evaluar después de 1-3): integrar `NarrativeTimelineWindow` como una segunda pestaña dentro de la misma ventana (`Grafo` / `Línea temporal`) en vez de ventana aparte. Es un cambio de packaging (mover el `VisualElement` que ya existe a una pestaña compartida), no una reescritura — pero al no ser parte de lo pedido explícitamente ("no digo de crear nuevos"), se dejaría para una segunda vuelta si el resultado de 1-3 no es ya suficiente.

**Lo que deliberadamente NO cambia con este plan** (para que quede explícito qué garantiza el "mínimo impacto"): ningún `NarrativeNode`/`NarrativeRunner`/`QuestManager`/`DialogueManager` en runtime; el formato serializado de `NarrativeGraph.asset` (`[SerializeReference]`); `NarrativeQuickTestWindow` ni `ChapterSplitWindow` (herramientas de propósito distinto, se quedan tal cual); `CrossSystemNarrativeValidator` (se mantiene como red de seguridad automática, la vista nueva es prevención, no sustituto). Todo el trabajo son `EditorWindow`/`VisualElement` nuevos o extendidos — no hay ninguna ruta por la que esto pueda romper una build o una partida guardada, a diferencia del intento de Agosto.

**Plan de migración, en fases aisladas y entregables** (cada fase deja el editor usable, ninguna depende de que la siguiente se haga):

| Fase | Qué | Riesgo | Coste aprox. |
|---|---|---|---|
| A | `NarrativeProjectIndex` (índice compartido, sin conectar a ninguna ventana aún) | Ninguno | 0.5 día |
| B | Panel lateral con guion de diálogo inline en `NarrativeGraphWindow`, driven por selección de nodo | Bajo (puramente aditivo) | 1 día |
| C | Panel lateral con datos de quest resueltos vía el índice, mismo mecanismo que B | Bajo | 1 día |
| D | Filtro de capítulo con modo "ocultar" además de "atenuar" | Bajo | 0.25 día |
| E (opcional) | Timeline como pestaña de la misma ventana | Bajo-medio (packaging, no lógica nueva) | 1-2 días |

**Respuesta directa: sí, es posible, y con impacto bajo real** — no solo percibido. La razón de fondo es que el riesgo del intento anterior venía de tocar **lógica que se ejecuta en producción** (dos intérpretes de historia compitiendo por el mismo estado); esto es una capa de **solo lectura enriquecida sobre datos que el Editor ya carga hoy**, construida encima de piezas que ya existen (`DialogueAssetEditor`, el barrido de `AssetDatabase` que ya hacen tres ventanas, el campo `chapter` y su filtro). Orden recomendado por coste/beneficio: A → B → C → D primero (2.75 días, cubre exactamente lo pedido: capítulo + grafo + diálogos + quests en una pantalla); E después, solo si tras probar A-D en el Capítulo 1 real seguís echando en falta la vista temporal integrada en el mismo sitio.

### 9. Corrección — 15 de agosto de 2026: bug "personaje flotando sobre la cama" en el prólogo, y auditoría de los puntos pendientes de esta conversación

**Contexto.** Tras una build de "Nueva Partida" aparecieron Estela y, en una sesión de Editor posterior, Will "volando"/desencajados sobre la cama en la escena de apertura (donde debería verse a Will dormido con normalidad). El patrón reportado — arreglar algo, romper otra cosa sin tocarla — es justo el síntoma que motivó esta pasada: en vez de parchear el síntoma puntual, se buscó la causa mecánica compartida.

**Causa raíz encontrada — AABB de culling atascado, no un problema de identidad de personaje.** El proyecto ya tenía documentado y parcheado (en `ActiveCharacterSwapper.SpawnWillNpc`/`EnsureWillNpcVisible`) un bug conocido: un `SkinnedMeshRenderer` calcula su bounds de culling una vez y no lo recalcula solo tras un cambio brusco de pose/posición o tras encenderse tras estar apagado — hasta que algo fuerza la lectura de `.bounds` (lo que hace el Editor al seleccionar el objeto en la Hierarchy, "curándolo" visualmente). El fix (forzar `Animator.Update(0f)` + lectura de `.bounds` + `updateWhenOffscreen = true`) solo estaba aplicado en esos dos sitios. `SleepTrigger.SetupSleep()` — que teletransporta al personaje activo a `bedPosition` y fuerza `Animator.Play("Sleeping_NoWeapon")` en el mismo frame en que `WorldBootstrap` acaba de aplicar su apariencia — nunca lo tenía, y es exactamente la combinación (cambio de apariencia + teleport + cambio de pose forzado) que dispara el bug. Encaja con los dos reportes del usuario: primero "Estela", luego "Will" — no son dos personajes distintos, es el mismo bug de renderizado con la apariencia que tocara en cada sesión.

**Fix aplicado — centralizado, no parcheado sitio a sitio.** En vez de añadir el fix solo en `SleepTrigger`, se añadió en el punto más central posible para que cubra automáticamente cualquier llamador futuro:

- `ModularAutoBuilder.RefreshRendererBoundsAfterAppearanceChange()` (nuevo método público) — hace el refresco de bounds sobre el propio builder.
- `CharacterAppearanceRegistry.ApplyAppearance()` lo llama siempre, justo después de `ApplySelection()`/`RestoreInitialSelection()` — así **todo** cambio de apariencia del personaje activo (boot, cambio de personaje, vestuario) queda cubierto de una vez, sin depender de que cada sitio nuevo recuerde replicarlo.
- Como refuerzo puntual (defensa en profundidad, no el fix principal): `SleepTrigger.SetupSleep()`/`WakeUp()` y `ActiveCharacterSwapper.WarpNpcToPosition()` también aplican el mismo refresco tras sus propios teleports — `WarpNpcToPosition` era un hueco ya identificado (Will podía quedarse invisible tras un warp cerca de un puzle) que seguía sin el fix; ahora lo tiene.

**Bug de datos real encontrado y corregido — paso de quest de Fuego Fatuo nunca se completaba.** Al auditar los `CompleteQuestStepsNode` marcados por el validador, se encontró uno (Cap. 6, "Completo el paso de la misión de inspeccionar") con `questId` vacío y `stepConditionIds: [FUEGOFATUO_DESC_02]`. Contrastado contra `Q_FUEGO_FATUO1.asset`: ese step existe, pero su `conditionId` real es `FUEGOFATUO_02` — `FUEGOFATUO_DESC_02` es el `descriptionId` (el texto), no el id de condición. `QuestManager.CompleteQuestStepByConditionId` compara por `conditionId` exacto y además corta antes si `questId` está vacío (`_runtime.TryGetValue(questId, ...)` falla con clave vacía) — este nodo no hacía nada al ejecutarse, silenciosamente. Corregido en `MainNarrative.asset` y, por consistencia, también en `MainNarrative_Cap6.asset` (huérfano, ver más abajo): `questId: FUEGOFATUO_1`, `stepConditionIds: [FUEGOFATUO_02]`.

**Auditoría de los demás puntos hablados en esta conversación — con evidencia, no solo diagnóstico:**

- **Minijuego de Estela, ruta de FAIL:** se temía que faltara el nodo que escucha el evento de fallo. Verificado en el grafo: **sí existe** — `WaitCustomEventNode` con `eventKey: MINIGAME_TAG_MINIGAME_01_FAILED` es una de las tres salidas del `StartTagMinigameNode` ("ESTELA FURIOSA"), junto a las rutas de victoria y aborto, y las tres reconvergen en el mismo `WaitQuestCompleteNode` ("LLEGAMOS A LA TABERNA"). No hacía falta ningún cambio.
- **Pregunta original sobre grafo vs. quests al reintentar un nodo ya completado:** confirmado en código (`WaitQuestCompleteNode.Enter()`) que comprueba `IsQuestCompleted()` **antes** de suscribirse a nada y avanza inmediatamente si la quest ya estaba completa — coherente con la idempotencia ya verificada en `QuestManager` (`StartQuest`/`CompleteQuest`/`MarkStepDone` comprueban estado antes de mutar). El patrón de "volver a un nodo que ya completó una quest" es seguro por diseño en ambos sistemas.
- **Avisos del validador `CompleteQuestStepsNode` sin pasos para `ELDRAN_MISSION5/6/9/12`:** revisados uno a uno contra los `QuestData` reales (`Q_ELDRAN_MISSION5/6/9/12.asset`) — todos los `stepConditionIds` en el grafo coinciden exactamente con `conditionId`s reales de esas quests. No son bugs; probablemente el validador está mirando el campo `steps` (heredado, sin usar) en vez de `stepConditionIds` (el que de verdad se usa). No se ha tocado nada aquí para no arriesgar sobre una hipótesis de herramienta de validación sin confirmar contra su propio código fuente — queda anotado para revisar el validador en frío, sin presión de build.
- **Assets de capítulo huérfanos (`MainNarrative_Cap1-6.asset`):** confirmado que el Hub de `Start.unity` solo referencia `MainNarrative.asset` + `Secundary.asset`; los 6 assets partidos por capítulo no están conectados a nada y no se ejecutan. Decisión tomada por precaución, no por omisión: **no se han registrado en el Hub ni se han borrado** en esta pasada — cualquiera de las dos acciones es una migración real con riesgo propio, y no es de las que rompía nada hoy con la build. Queda como tarea aparte para cuando no haya una decisión de continuidad del proyecto pendiente de por medio.
- **Posición de nodos nuevos en el grafo (creados siempre en `Vector2.zero`):** corregido en la pasada anterior de esta misma conversación (`NarrativeGraphWindow.GetViewCenter()`), ya entregado.

**Archivos tocados en esta pasada:** `Assets/Scripts/Characters/ModularAutoBuilder.cs`, `Assets/Scripts/Characters/CharacterAppearanceRegistry.cs`, `Assets/Scripts/Player/ActiveCharacterSwapper.cs`, `Assets/Scripts/World/SleepTrigger.cs`, `Assets/NarrativeGraph/MainNarrative.asset`, `Assets/NarrativeGraph/MainNarrative_Cap6.asset`.

> **ACTUALIZACIÓN (16 ago 2026) — el bug seguía, pero NO era SleepTrigger: causa raíz real encontrada y corregida en el motor de Invector.** Primer intento de esta fecha (Rigidbody kinematic en `SleepTrigger`) fue un tiro a ciegas sobre el archivo equivocado — revertido íntegramente sin dejar rastro, `SleepTrigger.cs` quedó igual que al cierre de la entrada de arriba (15 ago). El log real de repro (`InteractionDetector` → `Bed01_a03` → `PlayerAmbientActivityHandler`) mostró que la cama interactiva NO pasa por `SleepTrigger` en absoluto, sino por el sistema de `NPCWorldPoint` + `PlayerAmbientActivityHandler` (bancos/mesas/camas de uso libre, distinto del "Will forzado a dormir" narrativo que sí usa `SleepTrigger`).
>
> **Causa raíz real (verificada leyendo el código, no adivinada):** `vThirdPersonController.ControlAnimatorRootMotion()` (`Assets/Plugins/Invector-3rdPersonController_LITE/Scripts/CharacterController/vThirdPersonController.cs`) — llamado cada frame desde `vThirdPersonInput.OnAnimatorMove()` — hace `if (inputSmooth == Vector3.zero) transform.position = animator.rootPosition;` **sin comprobar `lockMovement`**. Este hueco ya estaba señalado en un comentario de `vThirdPersonMotor.ResetInputSmoothing()` (15 ago) y en `PlayerLockService.ApplyHardLock()`, pero solo se había mitigado ahí — no en la fuente. `PlayerAmbientActivityHandler.SnapToSeat()` bloquea con `_motor.lockMovement = true` pero deja `vThirdPersonInput` **activo** (a propósito, para que la cámara siga respondiendo mientras el jugador está sentado/dormido) — así que `OnAnimatorMove()` sigue disparándose cada frame. En cuanto `SuppressMoveInput` lleva `inputSmooth` a cero (unos pocos frames después de sentarse/tumbarse), esa línea pisa `transform.position` con el `animator.rootPosition` del Animator de la RAÍZ del jugador — un Animator distinto y separado del Animator del hijo "model" que reproduce visualmente `Sleeping_NoWeapon`/`Sit*_Loop` (ver el propio comentario de `SleepTrigger.SetupSleep()` sobre `GetComponentInChildren<Animator>`). Como nadie mueve el `rootPosition` de ESE Animator a la posición del asiento/cama, este resync automático devuelve al jugador a donde estaba de pie justo antes de sentarse — visualmente, "flotar" sobre la cama.
>
> **Fix aplicado — en la fuente común, no en cada llamador:** añadido `if (lockMovement) return;` al principio de `ControlAnimatorRootMotion()`, antes de la línea del resync. Cubre automáticamente tanto `PlayerAmbientActivityHandler` (bancos/mesas/camas interactivas) como `SleepTrigger` (Will forzado a dormir) y cualquier sistema futuro que use `lockMovement`, sin tener que parchear cada sitio. Archivo tocado: `Assets/Plugins/Invector-3rdPersonController_LITE/Scripts/CharacterController/vThirdPersonController.cs`. **Pendiente de confirmar en juego** — no hay forma de correr el Editor desde aquí; probar la cama de nuevo (y de paso bancos/otros `NPCWorldPoint`, ya que el fix es común a todos) y confirmar que ya no flota.

> **ACTUALIZACIÓN (16 ago 2026) — segundo bug distinto en la misma cama: Will se queda dormido para siempre, sin forma de levantarse ni de salir de la casa.** Reportado por Raúl tras interactuar con la cama interactiva (sistema `NPCWorldPoint` + `PlayerAmbientActivityHandler`, el mismo de la entrada de arriba — no `SleepTrigger`). No es el bug de "flotar" ya corregido arriba: aquí Will se tumba correctamente, pero ninguna entrada vuelve a despertarlo.
>
> **Causa raíz (verificada leyendo el código, no adivinada):** `PlayerAmbientActivityHandler.StartActivity()`/`SnapToSeat()` bloquea el movimiento directamente sobre el motor de Invector (`_motor.lockMovement = true` + CC desactivado) **sin llamar a `PlayerInputManager.PushUIMode()`** — a propósito, según su propio comentario ("así la cámara sigue disponible mientras el player está sentado o durmiendo"). Pero `OnCancel()`, la única vía para llamar a `StopActivity()`, está suscrito a `cancelAction`, un `InputActionReference` que apunta a `PlayerControls.UI.Cancel` (confirmado por guid en `_WILL.prefab`: el asset referenciado es `PlayerControls.inputactions`, y "Cancel" solo existe en el mapa `UI`, no en `GamePlay`). `PlayerInputManager.InitializeControls()` deja el mapa `UI` **deshabilitado** por defecto (`_controls.UI.Disable()`) y solo se habilita vía `PushUIMode()` — que aquí nunca se llama. Resultado: `cancelAction.action.performed` no se dispara jamás mientras el jugador está en la cama, `OnCancel()` no se ejecuta nunca, y como el CharacterController está desactivado y el motor bloqueado, no hay ninguna otra vía de salida — el jugador queda atrapado sin remedio, tenga el mando que tenga.
>
> Con mando, `GamepadInputReader.CancelPressed` ya tenía un fallback que leía `Gamepad.current.buttonEast` directo del hardware (sin depender de qué mapa esté activo), así que en teoría un jugador de mando podía despertar pulsando B/Círculo. En teclado no había ningún fallback equivalente para Escape — por eso el bloqueo se reproduce siempre en teclado y depende del método de entrada en mando.
>
> **Fix aplicado:**
> - `GamepadInputReader.CancelPressed` (`Assets/Scripts/Core/GamepadInputReader.cs`): añadido fallback de teclado (`Keyboard.current.escapeKey.wasPressedThisFrame`), mismo patrón que el fix ya existente en `YButtonPressedUI`/`YButtonPressed` de este mismo archivo.
> - `PlayerAmbientActivityHandler` (`Assets/Scripts/Player/PlayerAmbientActivityHandler.cs`): nuevo `Update()` que, mientras `_currentWorldPoint != null` y no se está en `ActionMode.Cinematic`, sondea `GamepadInputReader.CancelPressed` directamente (lectura de hardware, independiente del mapa de Input System activo) y llama a `StopActivity()`. La suscripción original a `cancelAction` se deja tal cual — inofensiva y redundante si el modo UI llegara a estar activo por algún otro motivo; `StopActivity()` ya es idempotente (`if (_currentWorldPoint == null) return;` al principio), así que no hay riesgo de doble ejecución si ambas vías coincidieran en el mismo frame.
>
> **Pendiente de confirmar en juego** — no hay forma de correr el Editor desde aquí; probar la cama (y de paso bancos/mesas, mismo `PlayerAmbientActivityHandler`) con teclado y con mando y confirmar que Escape/B despiertan a Will en ambos casos.

> **ACTUALIZACIÓN (16 ago 2026) — tercer bug, descubierto solo al arreglar el segundo: Will se levanta de la cama pero se queda con la animación de dormir puesta.** Tras el fix de arriba, Raúl confirmó que ahora sí puede levantarse (posición y control se restauran), pero el personaje sigue visualmente tumbado/dormido al andar. Es un bug distinto y preexistente que el bloqueo total anterior tapaba — con el jugador nunca llegando a completar `StopActivity()`, este segundo tramo del código no se había ejecutado nunca en la práctica.
>
> **Causa raíz (verificada leyendo el código, no adivinada):** `StopActivity()` → `ReturnToGroundAndUnlock()` (camino normal de salida cuando la actividad no tiene exit state propio — `GetActivityExitState()` devuelve `string.Empty` para `Sleep`/`Drink`/`Eat`: "sin animación de salida, vuelven directo a idle") teleporta de vuelta al suelo y llama a `RestoreCC()` y `PopMode(ActionMode.UsingWorldPoint)`, pero **nunca** le dice al Animator que abandone el estado en el que quedó (`Sleeping_NoWeapon`, reproducido en loop por `ActivityRoutine()` al entrar). El comentario del propio código ya documentaba el fix correcto en el sitio equivocado: `ForceStopActivityImmediate()` (usado solo desde cinemáticas como `TabernaSequencer`) sí llama a `ReturnAnimatorToLocomotion()` tras `RestoreCC()`, con un comentario explícito ("sin esto el Animator se queda congelado en el Sit*_Loop") — pero `ReturnToGroundAndUnlock()`, la ruta que de verdad usa el jugador al levantarse normalmente, no lo tenía.
>
> **Fix aplicado:** añadida la misma llamada a `ReturnAnimatorToLocomotion()` en `ReturnToGroundAndUnlock()`, justo después de `RestoreCC()` y antes de `PopMode()` — mismo orden que ya usa `ForceStopActivityImmediate()`. Para actividades con exit state propio (`Sit*`), `ReturnToGroundAndUnlock()` se llama *después* de que `PlayExitAndUnlock()` ya reprodujo el clip de salida completo, así que esta llamada añadida ahí es un refuerzo inofensivo (asegura que se acaba en locomoción pase lo que pase), no un cambio de comportamiento. Archivo tocado: `Assets/Scripts/Player/PlayerAmbientActivityHandler.cs`.
>
> **Pendiente de confirmar en juego** — probar de nuevo la cama y comprobar que, al levantarse, Will vuelve a la animación de andar/idle normal en vez de quedarse con la pose de dormir puesta.

---

## 16. Diseño: Cielo unificado, clima dinámico y cielo nocturno temático (nubes, estrellas, arcoíris)

**Proyecto:** El Sendero de las Estrellas
**Fecha:** 8 agosto 2026
**Estado:** Propuesta de diseño — pendiente de aprobación antes de implementar

> **NOTA (11 ago 2026):** Esta sección analizaba una implementación basada en el asset Quibli (shaders Quibli/Cloud3D, Quibli/Cloud2D, Quibli/Skybox). Quibli se eliminó por completo del proyecto ese día y CloudCoverSpawner.cs / DayNightCycle.cs se revertieron a su versión previa (nubes Low Poly Modular Terrain Pack, skyboxes por franja horaria) — revert quirúrgico (commit `81d65a9cc`) motivado por daño colateral en 30 materiales ajenos migrados sin querer al shader Quibli/StylizedLit, NO por un problema de esta sección en sí.
>
> **ACTUALIZACIÓN (13 ago 2026):** Quibli volvió al proyecto por otra vía mientras tanto (postprocesado recuperado y afinado, commits `1c2a3e006`..`0f2ba83db`), así que ya no es un asset ajeno al proyecto. La Parte A y la Parte C de esta sección se han implementado (ver "Estado de implementación" al final de la sección 16), recuperando y adaptando el trabajo de Quibli/Cloud3D y Quibli/Skybox que existía en el commit `bfb27c983` (previo al revert). La Parte B (`AmbientCloudDirector`, nubosidad ligera independiente de la lluvia) se ha dejado aparcada a propósito — decisión explícita de alcance, ver nota al final.


Punto de partida (tal cual lo has planteado): la mejora reciente de nubes quedó bien y dispara tres ideas más:

1. Un único skybox genérico + jugar con la luz para vender amanecer/día/atardecer/noche, sin tantas franjas como hay ahora.
2. Nubes que se instancian con más variedad de comportamiento: se nubla un poco, se va, vuelve, se pone negro y llueve — no solo "lluvia sí/no".
3. Cielo nocturno temático (coherente con el nombre del juego y con que las estrellas son el destino final, aunque metafórico): estrellas doradas cubriendo el cielo, estrellas fugaces, arcoíris tras la lluvia.

Todo lo citado abajo (rutas, clases, shaders, propiedades) está verificado leyendo el código y los assets reales del proyecto, no asumido.

---

### 0. Diagnóstico previo (por qué esto no es un simple "cambiar un material")

- **El ciclo actual tiene 7 franjas activas, no 4.** `Assets/Scripts/World/DayNightCycle.cs` define el enum `TimeOfDay` con 9 valores (`Morning, BrightMorning, AfterNoon, EarlyDusk, Sunset, Night, Midnight, Cloudy, HaloSky`), y el array `timeSettings[]` configurado en el propio script instancia **7** de ellos (`Cloudy` y `HaloSky` no se usan hoy en el ciclo, aunque existen como valores del enum y como materiales de skybox en disco). Cada franja trae su propio `Material skybox`.
- **Los skyboxes ya son "genéricos" en el sentido técnico que pides — el problema es que hay 9, no 1.** Comprobado en `Assets/Art/Day-Night Skyboxes/Materials/SkyNoon.mat`: el shader es `m_Shader: {fileID: 104, guid: 000...0f000...}`, que es el shader **built-in de Unity `Skybox/6 Sided`** (no un shader custom del asset pack). Este shader trae de fábrica las propiedades `_Tint` (color, ya usado hoy: `{r: 0.5, g: 0.5, b: 0.5, a: 0.5}`), `_Exposure` y `_Rotation`, además de las 6 texturas de cubemap. Es decir: **ya tienes el "skybox genérico de Unity" que pides** — el pack solo le pintó 9 sets de texturas distintos (uno por franja) y hoy `DayNightCycle` cambia el `Material` entero en cada transición en vez de tintar uno solo.
- **No existe ningún sistema de "nublado parcial".** `CloudCoverSpawner.cs` (`Assets/Scripts/World/CloudCoverSpawner.cs`) ya instancia un techo de nubes 3D reales (mallas con shader `Quibli/Cloud3D` o `Quibli/Cloud2D`, ver `Assets/Plugins/Quibli/Shaders/Cloud3D.shadergraph`) en una rejilla alrededor del jugador, con fade de alfa, pool (no vuelve a `Instantiate` en lluvias posteriores) y recentrado automático (fix INC-074). Pero está **enganchado 1:1 a los eventos de `DayNightCycle`**: `CloudsBuildingUp` (aparece) y `RainStopped` (desaparece). No hay ningún estado intermedio — o no hay nubes, o hay techo completo de tormenta. Lo que describes ("se nubla un poquito, se va, vuelve, se pone más negro y llueve") no existe todavía como concepto en el código.
- **El bug de la "línea negra entre dos nubes" — diagnóstico razonado, no confirmado visualmente.** No tengo forma de ver capturas del juego desde aquí, así que esto es una hipótesis fundamentada en el propio `BuildCoverIfNeeded()`, no una causa verificada. Candidatos, de más a menos probable:
  1. **Solape de mallas alfa-recortadas (`QuibliCloud3D`).** `heightJitter` (±12 unidades) y el `jitter` de posición (hasta 50% de `cellSize`) permiten que dos nubes vecinas se solapen en profundidad. El shader `Cloud3D` usa recorte por `_AlphaThreshold` (dithering), y donde dos mallas con recorte por dithering se solapan, los patrones de puntos de cada una pueden interferir y leerse como una línea/borde oscuro — más visible cuanto más perpendicular es el ángulo de solape.
  2. **Sin sombra propia ni recibida (`shadowCastingMode = ShadowCastingMode.Off`, `receiveShadows = false`, línea 306-307).** Esto se hizo a propósito (techo lejano, no merece el coste), pero significa que si la "línea negra" no es un artefacto de dithering sino de iluminación, no viene de sombras — hay que mirar en otro sitio (probablemente normales de la malla del Foliage Generator en el borde de unión, o el propio bake del asset de Quibli).
  3. **Rotación aleatoria en Y sin comprobar solape real.** `CloudRotation()` gira cada nube al azar sin comprobar si eso hace que su silueta invada la de la vecina más de lo esperado.
  - **Antes de tocar código de esto, lo más rentable es una captura o clip corto del momento exacto en que se ve la línea** (con el `[ContextMenu] Activar/Desactivar techo de nubes (debug)` de `CloudCoverSpawner` puedes reproducirlo a demanda). Con eso se puede diferenciar en 30 segundos si es un problema de solape geométrico (se arregla con más espaciado/menos escala máxima) o del shader Quibli en sí (se arregla en el material, o cambiando esas nubes concretas a modo `QuibliCloud2D` con billboard, que no tiene solape 3D real). Lo dejo como primer paso de la Parte B, no como algo que vaya a "arreglar a ciegas".
- **No existe ningún sistema de estrellas de cielo, estrellas fugaces ni arcoíris.** Verificado: no hay coincidencias en `Assets/` para nada tipo "starfield/shooting star/rainbow/arcoiris" salvo `Assets/Scripts/World/StarWorldLighting.cs` y `StarWorldFootprintPool.cs`, que son del **nivel final "mundo estelar"** (la metáfora final del juego) y no tienen relación con el cielo nocturno del mundo normal — de hecho `StarWorldLighting.OnEnable/Start` **desactiva `DayNightCycle` por completo** mientras esa escena esté cargada y lo reactiva al salir. Cualquier cosa que hagamos en las Partes A/C de este documento no debe tocar esa escena: sigue siendo un override total independiente, ya funciona así y no hay motivo para unificarlo.
- **Riesgo real y concreto de tocar el enum `TimeOfDay`:** hay 4 sitios más en el proyecto que referencian valores concretos del enum, y hay que auditarlos antes de reducir franjas (detalle en Parte A.3):
  - `Assets/Scripts/UI/TimeOfDayIndicator.cs` — un sprite de UI por cada periodo (`SpriteForPeriod`).
  - `Assets/Scripts/World/CampfireRestInteractable.cs` — `nightTarget = TimeOfDay.Night`, `dayTarget = TimeOfDay.Morning`, y comprueba `== Night || == Midnight` para "es de noche".
  - `Assets/Scripts/World/DayOnlyInspectionTrigger.cs` — misma comprobación `== Night || == Midnight`.
  - `Assets/NarrativeGraph/Runtime/Graph/NodeTypes/SetTimeOfDayNode.cs` — nodo de grafo narrativo con `targetTime` serializado; probablemente ya colocado en `MainNarrative.asset` con un valor concreto grabado como int.

---

### PARTE A — Cielo unificado: un solo material de skybox + 4 franjas horarias

#### A.1 Qué cambia conceptualmente

- Un **único `Material` de skybox** (shader `Skybox/6 Sided`, el mismo que ya usan todos los `.mat` actuales) se queda asignado a `RenderSettings.skybox` de forma permanente. Ya no se cambia la *referencia* al material en cada transición de franja.
- Lo que varía por franja/momento es: `_Tint` (color, ya soportado), `_Exposure` (brillo) y `_Rotation` (gira el cubemap — útil para que el sol "pintado" en la textura seggase aproximadamente la posición del `directionalLight`), combinados con lo que `DayNightCycle` ya hace hoy (color/intensidad/rotación de la luz direccional, ambiente, niebla).
- Reducir de 7 franjas activas a **4: Amanecer, Día, Atardecer, Noche**, tal como pides.

#### A.2 Piezas nuevas / modificadas

**1. `DayNightCycle.TimeOfDaySettings` — nuevos campos, sin tocar el enum:**

```csharp
[Header("Skybox único (tint/exposure/rotation)")]
public Color skyboxTint = new Color(0.5f, 0.5f, 0.5f, 0.5f); // _Tint del shader Skybox/6 Sided
[Range(0f, 8f)] public float skyboxExposure = 1f;             // _Exposure
[Range(0f, 360f)] public float skyboxRotation = 0f;           // _Rotation, sincronizado a ojo con sunRotationY
```

El campo `public Material skybox` existente se queda (para no romper el inspector de golpe) pero deja de usarse en el ciclo normal — se documenta como legacy/no usado, o se elimina en una segunda pasada una vez validado en juego.

**2. `DayNightCycle.Awake()` — instanciar el skybox en runtime:**

Punto importante de corrección técnica: `RenderSettings.skybox` **no** auto-instancia el material al asignarlo (a diferencia de `renderer.material`). Si mutamos `_Tint`/`_Exposure` directamente sobre el asset compartido en Play Mode, en el Editor eso **ensucia el `.mat` real** (se queda con el último valor tintado al salir de Play). Hay que crear una copia en memoria una vez:

```csharp
[SerializeField] private Material sharedSkyboxMaterial; // el ÚNICO asset de skybox, arrastrado en el Inspector
private Material _runtimeSkybox;
private static readonly int TintId = Shader.PropertyToID("_Tint");
private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
private static readonly int RotationId = Shader.PropertyToID("_Rotation");

void Awake()
{
    // ...código existente...
    if (sharedSkyboxMaterial != null)
    {
        _runtimeSkybox = new Material(sharedSkyboxMaterial); // instancia propia, nunca toca el asset
        RenderSettings.skybox = _runtimeSkybox;
    }
}

void OnDestroy()
{
    if (_runtimeSkybox != null) Destroy(_runtimeSkybox); // evitar leak del material en memoria
}
```

**3. `ApplySettingsImmediate` / `TransitionToSettings` — sustituir el swap de `Material` por mutar `_runtimeSkybox`:**

Donde hoy dice `RenderSettings.skybox = settings.skybox;`, pasa a:

```csharp
if (_runtimeSkybox != null)
{
    _runtimeSkybox.SetColor(TintId, settings.skyboxTint);
    _runtimeSkybox.SetFloat(ExposureId, settings.skyboxExposure);
    _runtimeSkybox.SetFloat(RotationId, settings.skyboxRotation);
    DynamicGI.UpdateEnvironment();
}
```

Y en la corrutina de transición (`TransitionToSettings`), estas tres se interpolan igual que `lightColor`/`ambientColor` ya se interpolan (Lerp de color y float, LerpAngle para la rotación) — mismo patrón, sin lógica nueva de por medio.

**Ojo con `ApplyStormSkybox()`/`RevertStormSkybox()`:** hoy cambian `RenderSettings.skybox` a `stormSkybox` durante la nubosidad/lluvia (opcional, ver comentario del propio campo: "recomendado dejarlo null si usas `CloudCoverSpawner`"). Con un único skybox instanciado, si se sigue queriendo ese oscurecimiento adicional del fondo lejano durante tormenta, se puede lograr **tinta/expone también el `_runtimeSkybox`** en vez de cambiar de material (mismo mecanismo, un `Lerp` más hacia un tinte de tormenta), en vez de la ruta actual de "cambiar a otro material o forzar `CameraClearFlags.SolidColor`". Se puede dejar la red de seguridad de cámara tal cual está (no lo toca esta propuesta), solo se sustituye la parte de "cambiar el asset de skybox" por "tintar el único skybox".

#### A.3 Reducir a 4 franjas sin romper lo que ya depende del enum

**Decisión recomendada: no tocar el enum `TimeOfDay` (dejar los 9 valores tal cual existen hoy).** Renombrar o eliminar miembros del enum desplaza los valores `int` subyacentes de todo lo demás, y eso es justo el tipo de cambio silencioso que rompe datos ya serializados (nodos del grafo narrativo, prefabs con `CampfireRestInteractable` configurado) sin que se note hasta que se juega esa escena en concreto — el mismo tipo de riesgo que ya os hizo abortar un intento de unificación de sistemas en agosto según `CLAUDE.md` §7.

En su lugar:

1. **El array `timeSettings[]` pasa de 7 a 4 entradas**, eligiendo qué miembro del enum representa cada franja nueva:
   - **Amanecer** → `TimeOfDay.Morning` (se queda igual, ya es la franja de entrada).
   - **Día** → `TimeOfDay.AfterNoon` (recomendado sobre `BrightMorning`: ya tiene mayor `lightIntensity` — 1.3 vs 1.4, similar — y `ambientIntensity` más alta; a confirmar a ojo en el editor cuál de las dos gustaba más como "look de día").
   - **Atardecer** → `TimeOfDay.Sunset` (recomendado sobre `EarlyDusk`: colores más saturados/dorados, más "atardecer" reconocible; `EarlyDusk` queda como transición intermedia que ya no hace falta si solo hay 4 franjas).
   - **Noche** → `TimeOfDay.Night` (recomendado sobre `Midnight`: `Midnight` es casi idéntica pero más oscura — se puede recuperar ese "más de noche" simplemente alargando la `duration` de `Night` en vez de mantenerla como franja aparte).
   - `BrightMorning`, `EarlyDusk`, `Midnight` quedan sin usar en el ciclo automático (igual que ya pasa hoy con `Cloudy`/`HaloSky`), pero **sin borrar del enum**.
2. **Auditar antes de dar por cerrado**, uno por uno:
   - `CampfireRestInteractable.cs` — `nightTarget`/`dayTarget` en cualquier prefab de hoguera ya colocado en escena: revisar que sigan apuntando a `Night`/`Morning` (que se mantienen), y simplificar el check `== Night || == Midnight` a solo `== Night` si `Midnight` deja de ser alcanzable por el ciclo automático (puede seguir siendo alcanzable manualmente vía `SetTimeOfDay`, así que no es obligatorio simplificar, solo limpieza opcional).
   - `DayOnlyInspectionTrigger.cs` — mismo check, misma nota.
   - `SetTimeOfDayNode.cs` — **buscar en `MainNarrative.asset` y cualquier otro grafo** si hay algún nodo `SetTimeOfDayNode` apuntando a `BrightMorning`, `EarlyDusk`, `Cloudy` o `HaloSky`. Si existe alguno, `SetTimeOfDay()` (línea 552 de `DayNightCycle.cs`) hace un `for` sobre `timeSettings[]` y si no encuentra el `TimeOfDay` pedido, solo hace `Debug.LogWarning` y **no pasa nada más** — no rompe, pero ese nodo narrativo dejaría de tener efecto silenciosamente. Hay que revisar el grafo a mano (o con el validador que ya usáis, `CrossSystemNarrativeValidator`, si aplica aquí) antes de dar la Parte A por completa.
   - `TimeOfDayIndicator.cs` — tiene un sprite de UI por periodo (`SpriteForPeriod`). Con 4 franjas activas hacen falta a lo sumo 4 sprites (amanecer/día/atardecer/noche); es trabajo de arte, no de código, pero hay que encargarlo.

---

### PARTE B — Nubes con más vida: cobertura parcial + fix de la costura

#### B.1 De binario a progresivo

**Nuevo concepto: `CloudCoverage` (float 0-1)**, en vez de solo "hay tormenta / no hay tormenta". 0 = cielo despejado, valores intermedios = nubes ligeras pasando, 1 = techo de tormenta completo (lo que ya existe hoy).

**Nuevo componente `Assets/Scripts/World/AmbientCloudDirector.cs`** (vive en la misma escena que `DayNightCycle`, se suscribe a sus eventos igual que hace hoy `CloudCoverSpawner`, sin referencias directas entre managers — mismo patrón arquitectónico que ya pide `CLAUDE.md` §3):

- Corrutina de fondo, solo activa cuando NO está lloviendo ya (`DayNightCycle.IsRaining == false`), que hace un paseo aleatorio lento de `CloudCoverage` entre 0 y un umbral "ligero" (p. ej. 0.4), con periodos de espera entre cambios — esto es literalmente el "se nubla un poquito, se va, vuelve" que describes.
- Si el paseo aleatorio supera un umbral alto (p. ej. 0.75) **y** toca el sorteo de lluvia de `DayNightCycle` (`rainChance`/`forceRain`, ya existente), se cede el control a `DayNightCycle.StartRain()` tal cual funciona hoy — no se duplica lógica de lluvia, `AmbientCloudDirector` solo maneja la parte "ambiental" de nubes ligeras, la tormenta de verdad la sigue llevando `DayNightCycle`.
- Expone un evento propio, `event Action<float> CloudCoverageChanged`, del que se suscribe `CloudCoverSpawner`.

**2. `CloudCoverSpawner.cs` — nuevo modo de cobertura parcial:**

Hoy `BuildCoverIfNeeded()` construye TODO el techo de golpe la primera vez que se nubla para tormenta. Para nubes ligeras hace falta menos densidad y sin el tinte de tormenta (`stormCloudColor`/`stormShadowAmount` se quedan a 0 mientras `CloudCoverage < umbralTormenta`). La forma más barata de lograrlo reutilizando el pool ya construido: en vez de animar solo el alfa de 0→1, animar también **cuántas de las nubes ya instanciadas están activas**, proporcional a `CloudCoverage` (p. ej. ordenar los renderers una vez por distancia al centro y activar/desactivar un porcentaje de la lista según la cobertura objetivo, en vez de las 300 de golpe). Así "se nubla un poco" se ve como pocas nubes sueltas, no como el techo completo con alfa bajo (que se leería como niebla, no como nubes dispersas).

#### B.2 Fix de la costura negra — plan en dos pasos

1. **Repro dirigida primero.** Usar el `[ContextMenu] Activar/Desactivar techo de nubes (debug)` de `CloudCoverSpawner` para forzarlo en el editor, capturar dónde aparece la línea (una nube que solapa contra otra, o algo del propio material Quibli). Sin esto, cualquier cambio de código es un tiro a ciegas sobre un shader de terceros (Quibli).
2. **Mitigaciones candidatas, de menor a mayor invasión** (se elige una vez confirmada la causa):
   - Reducir el solape: bajar `heightJitter` y/o el límite superior de `scaleRange` (hoy 1.5), o aumentar `cellSize` relativo al tamaño máximo de nube — menos solape geométrico entre mallas vecinas.
   - Cambiar las nubes más pegadas al jugador (las que más se notan) a `CloudShaderMode.QuibliCloud2D` con `billboard = true`: al ser quads que siempre miran a cámara, no hay intersección 3D real entre dos nubes, solo alfa-blend por profundidad — elimina la clase entera de artefacto de solape de mallas 3D, a cambio de perder el volumen 3D de `Cloud3D`.
   - Si el artefacto está en el propio material/shader de Quibli (dithering de `_AlphaThreshold`), revisar si el material del demo de Quibli (`Assets/Plugins/Quibli/Demos/Clouds/Clouds Materials`) tiene una variante o ajuste de suavizado de borde que el prefab actual no esté usando.

---

### PARTE C — Cielo nocturno temático: estrellas doradas, estrellas fugaces, arcoíris

Encaja bien con el nombre del juego y la relevancia narrativa de las estrellas — y técnicamente es casi una reutilización directa de lo que ya existe en `CloudCoverSpawner`, cambiando "techo de nubes en un plano" por "domo de estrellas sobre la cabeza del jugador".

#### C.1 `Assets/Scripts/World/NightSkyStarSpawner.cs` (nuevo)

Mismo patrón estructural que `CloudCoverSpawner` (rejilla + jitter + pool + fade de alfa vía `MaterialPropertyBlock`, sin `Instantiate`/`Destroy` repetidos), pero:

- Se ancla a un domo (posiciones sobre una esfera de radio fijo centrada en el jugador la primera vez, igual que el techo de nubes se ancla a un plano) en vez de a un plano horizontal.
- Se activa/desactiva por `DayNightCycle.TimeOfDayChanged` (aparece progresivamente entrando en `Noche`, se apaga entrando en `Amanecer`/`Morning`) en vez de por lluvia — evento distinto, mismo mecanismo de suscripción que ya usa `CloudCoverSpawner` con `CloudsBuildingUp`/`RainStopped`.
- Sprite/mesh pequeño con tinte dorado/cálido (en vez de blanco puro), para diferenciarlo visualmente de un cielo estrellado genérico y conectar con la estética de "sendero dorado" que ya mencionas para las nubes.
- Reutiliza el pool: construir una sola vez, ocultar con `SetActive(false)` durante el día, reactivar de noche — igual que ya hace `CloudCoverSpawner.DeactivateCover()`/reactivación en `HandleCloudsBuildingUp()`.

#### C.2 `Assets/Scripts/World/ShootingStarSpawner.cs` (nuevo)

Componente pequeño e independiente: mientras `DayNightCycle.CurrentTimeOfDay == Night` y `CloudCoverage` (de la Parte B) esté por debajo de un umbral (no tiene sentido una estrella fugaz con el cielo cubierto de nubes de tormenta), cada X-Y segundos aleatorios anima un objeto (un `TrailRenderer` simple o un `ParticleSystem` de un solo disparo) cruzando el domo en línea recta con fade in/out. Sin física, sin pool complejo — es un evento raro y barato, se puede permitir `Instantiate`/`Destroy` puntual sin herramientas de pooling dedicadas (a diferencia de VFX de combate, donde `CLAUDE.md` sí exige `VfxPoolService` por la frecuencia; aquí la cadencia es de minutos, no de golpes por segundo).

#### C.3 `Assets/Scripts/World/RainbowSpawner.cs` (nuevo)

Escucha `DayNightCycle.RainStopped`. Solo si `CurrentTimeOfDay` es una franja de luz suficiente (Amanecer/Día/Atardecer, no Noche — un arcoíris de noche no tiene sentido salvo que se quiera un efecto "lunar" deliberado, a decidir), instancia un arco (mesh curvo con degradado de color, o un `ParticleSystem` en forma de arco) posicionado en el lado opuesto al sol — se puede derivar directamente de `sunRotationY` de la franja actual, ya expuesto en `TimeOfDaySettings`. Fade in/out de unos 20-30s y se destruye (evento raro, no hace falta pool aquí tampoco).

---

### Archivos a crear/tocar (resumen)

| Parte | Acción | Archivo |
|---|---|---|
| A | Tocar | `Assets/Scripts/World/DayNightCycle.cs` (campos `skyboxTint/Exposure/Rotation`, instanciar `_runtimeSkybox` en `Awake`, sustituir swap de material por mutación en `ApplySettingsImmediate`/`TransitionToSettings`, reducir `timeSettings[]` a 4 entradas) |
| A | Auditar (sin tocar código necesariamente) | `Assets/Scripts/UI/TimeOfDayIndicator.cs`, `CampfireRestInteractable.cs`, `DayOnlyInspectionTrigger.cs`, `SetTimeOfDayNode.cs` y cualquier nodo en `MainNarrative.asset` que apunte a franjas retiradas del ciclo |
| A | Arte (no código) | Reducir/confirmar sprites de `TimeOfDayIndicator` a 4; elegir la textura de cubemap "genérica" definitiva para el único skybox |
| B | Crear | `Assets/Scripts/World/AmbientCloudDirector.cs` |
| B | Tocar | `Assets/Scripts/World/CloudCoverSpawner.cs` (modo de cobertura parcial, activar/desactivar % del pool) |
| B | Investigar antes de tocar | Repro visual de la costura negra (usar el `ContextMenu` de debug ya existente) |
| C | Crear | `Assets/Scripts/World/NightSkyStarSpawner.cs`, `Assets/Scripts/World/ShootingStarSpawner.cs`, `Assets/Scripts/World/RainbowSpawner.cs` |
| C | Arte | Mesh/sprite de estrella dorada pequeña, mesh o textura de arco iris, trail/partícula de estrella fugaz |

---

### Orden de implementación recomendado

1. **Parte A primero** (cielo unificado) — es la que más cambia la sensación general del juego con menos código nuevo, y conviene validarla antes de construir Partes B/C encima de un ciclo que todavía podría cambiar de forma. Empezar por la auditoría de enum (A.3) ANTES de tocar `DayNightCycle.cs`, para no descubrir a mitad de implementación que algún nodo narrativo dependía de una franja que se iba a retirar.
2. **B.2 (fix de costura) antes que B.1 (cobertura parcial).** El fix es aislado y de bajo riesgo una vez haya una captura de repro; construir la cobertura parcial encima de un shader/prefab con un artefacto visual conocido solo lo haría más difícil de diagnosticar después.
3. **Parte C al final**, como pulido — es nueva funcionalidad aislada (no modifica nada existente, solo añade componentes que escuchan eventos ya existentes de `DayNightCycle`), así que no bloquea ni depende de que A/B estén terminadas al 100%, pero tiene más sentido narrativo/visual una vez el cielo diurno y las nubes ya están en su forma final.

### Preguntas abiertas para validar antes de programar

- **A:** ¿confirmas el mapeo de franjas — Amanecer=Morning, Día=AfterNoon, Atardecer=Sunset, Noche=Night — o prefieres probar en el editor con BrightMorning/EarlyDusk/Midnight antes de decidir cuál de cada par se queda?
- **A:** ¿qué set de 6 texturas de cubemap quieres como el único skybox "genérico"? ¿Uno de los 9 ya existentes (¿cuál?) o uno nuevo/neutro?
- **B:** manda una captura o clip corto del momento en que se ve la línea negra entre nubes en cuanto puedas reproducirlo — es el paso que más acelera el fix real.
- **C:** ¿las estrellas fugaces y el arcoíris son puramente ambientales/decorativos en v1, o quieres algún gancho de gameplay/narrativa (p. ej. una quest o logro ligado a verlos)?
- **General:** ¿cuál de las tres partes quieres ver jugable primero?

---


### Estado de implementación (13 ago 2026)

Implementado en esta pasada (sesión Cowork, sin poder abrir el Editor — pendiente validar en Play Mode):

- **Parte A — cielo unificado:** `DayNightCycle.cs` instancia un único `sharedSkyboxMaterial` en runtime (recomendado: `Assets/Plugins/Quibli/Demos/City/Materials/City_Skybox.mat`, degradado azul claro→azul, "cielo liso") y anima `_Tint/_Intensity/_Exponent/_DirectionYaw/_DirectionPitch` por franja en vez de cambiar de `Material`. Se añadió la propiedad `_Tint` (Color) al shader `Quibli/Skybox` (`Assets/Plugins/Quibli/Shaders/Skybox.shader`) — no existía antes, hacía falta para poder pintar el mismo degradado de colores distintos por franja. El enum `TimeOfDay` NO se ha tocado (mismo razonamiento que ya recogía esta sección); en su lugar se han añadido atributos `[InspectorName(...)]` en español a cada valor (cosméticos, no afectan a la serialización) y el array `timeSettings[]` se ha reducido de 7 a **4** entradas: Amanecer=`Morning`, Día=`AfterNoon`, Atardecer=`Sunset`, Noche=`Night` — exactamente el mapeo que esta sección recomendaba en su día. Los deltas de `lightIntensity`/`ambientIntensity` entre las 4 franjas se han suavizado a propósito (rango comprimido, `transitionDuration` subido de 10s a 16s) porque el pedido explícito era "que la iluminación no cambie tanto que afecta mucho a las sombras".
- **Parte B — nubes:** solo se ha restaurado lo que ya existía antes del revert (`CloudCoverSpawner.cs` con `CloudShaderMode.QuibliCloud3D`, prefabs `QuibliRainCloud3D_1..4` recuperados de git desde el commit `cf6ca2002` a `Assets/Prefabs/VFX/`). El fix de la costura negra (B.2) y la cobertura parcial/`AmbientCloudDirector` (B.1) siguen **sin implementar** — decisión de alcance explícita: se pidió ceñirse a nubes como aviso previo a lluvia (lo ya probado), no un sistema ambiental nuevo.
- **Parte C — cielo nocturno:** implementado `Assets/Scripts/World/NightSkyStarSpawner.cs` (nuevo), domo de GameObjects reales (Quads con textura de punto de luz generada en memoria, sin asset importado) en vez de partículas o un truco de skybox, tema dorado. Se activa en `TimeOfDayChanged → Night`, se apaga en `→ Morning`, y también se oculta durante tormenta (`CloudsBuildingUp`/`RainStopped`) y en interiores. Estrellas fugaces y arcoíris (C.2/C.3) **no** se han implementado — fuera del alcance pedido ("amanecer, dia, atardecer, noche y lluvia FIN").

**Pendiente, solo se puede hacer desde el Editor de Unity (no se ha podido tocar desde esta sesión):**
1. Arrastrar `City_Skybox.mat` al campo `sharedSkyboxMaterial` de cada `DayNightCycle` en escena (MainWorld, Sendero, CandyLand, PlayerTest...).
2. Asignar `QuibliRainCloud3D_1..4` al array `cloudPrefabs` de cada `CloudCoverSpawner`.
3. Añadir el componente `NightSkyStarSpawner` en la misma escena/GameObject que ya tiene `DayNightCycle`/`CloudCoverSpawner`.
4. Playtest completo de un ciclo entero (Amanecer→Día→Atardecer→Noche→lluvia) para afinar a ojo los valores de `skyboxTint`/`skyboxIntensity`/`skyboxExponent` por franja (los valores actuales son un punto de partida razonado, no un resultado validado en juego) y el aspecto/densidad del domo de estrellas.

### Actualización (16 ago 2026) — estado real verificado en MainWorld, AmbientCloudDirector activado

Verificado desde una sesión Cowork (sin Editor) al investigar un aviso de "faltan nubes" en una captura de MainWorld: los 3 primeros puntos del checklist "Pendiente" de arriba (13 ago 2026) **ya estaban hechos** en `MainWorld.unity` — en algún momento posterior alguien completó esos pasos de Editor sin actualizar esta sección. Estado confirmado en el GameObject `DayNightSystem` de `MainWorld.unity`:

1. `sharedSkyboxMaterial` de `DayNightCycle` → `City_Skybox.mat` asignado. Confirmado.
2. `cloudPrefabs` de `CloudCoverSpawner` → **no** son `QuibliRainCloud3D_1..4` como decía el punto 2 del checklist, sino `Assets/Prefabs/Clouds/Cloud3D-MeshCarrier_Cloud_01/02/03.prefab` + `Cloud3D-Sphere.prefab`. Es decir, se hizo, pero con otro set de prefabs distinto al que esta sección recomendaba en su día.
3. `NightSkyStarSpawner` → presente en el mismo GameObject. Confirmado.

No verificado en esta pasada si `Sendero.unity`/`CandyLand.unity`/`PlayerTest` tienen los mismos 3 pasos aplicados — solo se ha mirado `MainWorld.unity`. Pendiente de revisión si se detecta el mismo problema ("cielo sin nubes") en esas escenas.

**Parte B (B.1, `AmbientCloudDirector`) — activada en MainWorld, ya no está aparcada.** El motivo original de aparcarla ("se pidió ceñirse a nubes como aviso previo a lluvia") ya no aplica: `Assets/Scripts/World/AmbientCloudDirector.cs` y `AmbientCloudDrifter.cs` existen completos en disco (creados el 15 ago 2026, un día después de la nota de "aparcada" — decisión de alcance revertida en algún punto sin dejar rastro escrito), pero el componente no estaba añadido a ninguna escena. Se ha añadido `AmbientCloudDirector` al GameObject `DayNightSystem` de `MainWorld.unity` (mismo GameObject que `DayNightCycle`/`CloudCoverSpawner`/`NightSkyStarSpawner`), con:

- `dayNightCycle` → referencia al `DayNightCycle` del mismo GameObject.
- `ambientCloudPrefabs` → `Assets/Prefabs/VFX/QuibliRainCloud3D_1..4.prefab`, tal cual recomienda el propio comentario del script (no los `Cloud3D-MeshCarrier_*` que usa `CloudCoverSpawner` para la tormenta — sets distintos a propósito, para que la nube suelta de buen tiempo no se confunda visualmente con el techo de tormenta).
- Resto de campos a los valores por defecto del script (pool de 6, altitud 70, radio de paso 90, etc. — sin playtesting propio en esta pasada, son los valores que el propio autor del script dejó como default).

**Pendiente de este cambio:**
- Playtest en el Editor: comprobar que las nubes sueltas cruzan el cielo con cadencia razonable y que el fundido de alfa funciona con el material real de `QuibliRainCloud3D_1..4` (`shaderMode` del `AmbientCloudDrifter` asume `QuibliCloud3D` por defecto — no verificado contra el shader real de esos prefabs en esta pasada, solo por nombre).
- B.2 (fix de costura negra en `CloudCoverSpawner`, técnica no implementada) sigue sin hacer.
- Repetir el mismo cableado en `Sendero.unity`/`CandyLand.unity` si se confirma que tienen el mismo problema de cielo sin nubes.

### Nota (16 ago 2026) — por qué `Tree.mat` (Fantasy_Kingdom_Pack) no usa degradado pintado tipo Rosal/Otoñal

Al pedir "árboles pintados" para el pueblo se evaluó reusar la misma técnica de `Foliage_RojoOtonal_Arbol.mat`/`Foliage_Rosal_Flor.mat` (degradado horneado en `_ShadingGradientTexture`), pero **no aplica aquí** por dos motivos técnicos, documentados para no repetir la investigación:

1. Esas dos materiales usan `Foliage.shadergraph` en modo `BILLBOARD_ROTATION_WHOLE_OBJECT` (con `Fill_Texture`/`Shape_Texture`, sin `_BaseMap`) — pensado para tarjetas billboard que miran siempre a cámara, no para una malla FBX real con tronco+copa como las de `Fantasy_Kingdom_Pack`. Cambiarle el shader a `Tree.mat` sin más se arriesgaba a un render roto (sin albedo real).
2. `Quibli/StylizedLit` (el shader que `Tree.mat` ya usa, con outline) sí tiene un "Height Gradient" propio (`DR_GRADIENT_ON`/`_GradientRamp`/`_GradientCenterY`/`_GradientSize`), pero es **en espacio de mundo** (`LibraryUrp/Lighting_DR.hlsl` línea ~41-46: compara `positionWS.y` contra `_GradientCenterY` fijo). Al ser `Tree.mat` un material compartido por árboles repartidos por todo el mapa a alturas de terreno distintas, un centro/tamaño de banda fijo solo quedaría bien en una franja de altura concreta y se vería mal (o inexistente) en el resto — no es la herramienta correcta para un material compartido a escala de mundo, solo tiene sentido en objetos con material propio no compartido (así lo usan `City_Pillar_1.mat`/`City_Building_Wall_9.mat` en la demo, cada uno con su propio material).

**Lo que sí se hizo:** se subió `_BaseColor` de gris neutro (`0.5, 0.5, 0.5`) a un verde natural (`0.42, 0.54, 0.4`) — multiplica el color de la textura existente hacia un verde más saturado/cálido sin depender de posición en mundo, así que se ve consistente en todos los árboles del pack estén donde estén. No es un degradado real (más plano que el efecto Rosal/Otoñal), pero es lo seguro dado que el material es compartido. Si se quiere un degradado real por árbol, la vía sería materiales de instancia (`MaterialPropertyBlock` o duplicar material) en vez de tocar el `Tree.mat` compartido — no hecho en esta pasada, pendiente de decidir si merece la pena.

## 17. Diseño: Refugio de NPCs bajo la lluvia + Relaciones sociales dinámicas

**Proyecto:** El Sendero de las Estrellas
**Fecha:** 4 agosto 2026
**Estado:** Propuesta de diseño — pendiente de aprobación antes de implementar

> **ACTUALIZACIÓN (14 ago 2026) — Parte A, decisión de "casas" revertida:** la Parte A (refugio de
> lluvia) está implementada (`NPCShelterPoint.cs`, `SeekShelterState.cs`, `ReturnFromShelterState.cs`,
> `NPCWeatherAwareness.cs`), pero **la idea original de abajo de que el NPC "desaparece" al llegar a
> la puerta de una casa (`SetActive(false)`) nunca llegó a implementarse** — se simplificó durante la
> construcción a que `TreeCanopy` y `HouseDoor` se comportan exactamente igual (el NPC se queda de
> pie o sentado, siempre visible). Ahora se decide explícitamente **no** implementar nunca ese
> "entrar en la casa": se descarta del diseño. En su lugar, el refugio en el pueblo se coloca bajo
> GO con techo (puestos de mercado, porches, tejadillos), igual de "exterior" que un árbol del
> bosque — nunca en la puerta de una vivienda. Renombrado en código: `NPCShelterType.HouseDoor` →
> `NPCShelterType.RoofedSpot` (el valor entero no cambia, solo la etiqueta, así que no hace falta
> tocar los `NPCShelterPoint` ya colocados en escena para que sigan siendo válidos).
>
> **Trabajo de nivel pendiente:** los 8 `NPCShelterPoint` ya colocados a día de hoy (4 en
> `Assets/Scenes/Worlds/MainWorld.unity`, 4 en `Assets/Scenes/Systems/MainMenu.unity`, todos con
> `shelterType: 1`) están físicamente puestos en puertas de edificio (uno detectado sobre
> `Building06_c07`). Hay que reubicarlos a mano sobre GO con techo reales del pueblo — esto es
> trabajo de editor, no de código, igual que ya advertía este documento más abajo sobre la
> colocación de puntos de refugio.

Decisiones ya tomadas contigo:
- ~~Refugio en casas = el NPC desaparece al llegar a la puerta (no hay interior real que visitar).~~
  **Descartado, ver actualización de arriba** — el refugio en el pueblo va en GO con techo, el NPC
  nunca desaparece.
- Relaciones dinámicas = persistentes desde la v1 (se guardan en el save).

Todo lo citado abajo (rutas, clases, métodos, líneas) está verificado leyendo el código real del proyecto, no asumido. El contenido original de la propuesta (incluida la idea de "casas" ya descartada) se deja tal cual por debajo como registro histórico de cómo se llegó a la decisión actual — no como estado vigente.

---

### 0. Diagnóstico previo (por qué esto no es trivial)

- **No existe ningún sistema de clima consultable por NPCs.** La lluvia vive en `Assets/Scripts/World/DayNightCycle.cs`, con eventos C# públicos (`event Action RainStarted`, `event Action RainStopped`, `bool IsRaining`), pero hoy **cero scripts de NPC están suscritos**. `DayNightCycle` no es un singleton (`FindAnyObjectByType`, sin `Instance`, no vive en `Start.unity`) — probablemente hay una instancia por escena de pueblo.
- **No existe el concepto de "punto de refugio"** (árbol, porche, puerta) en ningún tag/capa/registro. Hay que crearlo desde cero.
- **El sistema social existe en código, pero en la práctica no se nota nunca — verificado, no es solo "a medias".** `NPCSocialConfig` (SO), `NPCRelationship.cs`, `WanderState.CheckSocialEncounter()`, `NPCSocialEncounterState`, `NPCBehaviourManagerV2.TryAcceptSocialEncounter()` están correctamente enlazados (capas, colliders, componentes, todo revisado en el prefab real `TownNpc#1.prefab` y la escena `MainWorld.unity`). El problema es que **cuatro condiciones tienen que coincidir a la vez** para que un encuentro se dispare, y casi nunca coinciden:
  1. Solo hay **13 NPCs "ambientales" que vagan de verdad** en toda `MainWorld.unity` (10 `TownNpc#` + 3 `Guerrero#`), repartidos por un pueblo entero — poca densidad, pocas coincidencias de proximidad.
  2. El escaneo (`CheckSocialEncounter`) **solo corre mientras el NPC está caminando en `WanderState`**, nunca en `IdleState` ni sentado en un banco (`WalkToActivityState`), y solo cada 3 segundos.
  3. Cada intento tira un dado contra `personality.sociability` (0.8 en el arquetipo "Amigable", pero puede ser bajo en otros), y el NPC receptor tiene que estar libre de su propio `socialCooldown` (25-45s) en ese instante exacto.
  4. Bug de identidad confirmado: `Assets/_NPCs/Social/NPC_Social_Archetype_Friendly.asset` (y previsiblemente sus hermanos Reserved/Energetic/Lazy/Grumpy) tienen **`npcId: ''` (vacío)**. Esto no impide que el encuentro se dispare, pero hace que los 13 NPCs de relleno que comparten ese arquetipo sean indistinguibles entre sí a efectos de relación.
  Además, `relationships[]` es un array estático de diseño dentro del `ScriptableObject`, vacío en los 27 SO reales del proyecto — nunca se escribe en runtime, así que aunque un encuentro se dispare, jamás evoluciona a nada.
  **Conclusión:** no basta con "arreglar" la forja de relaciones (Parte B original) — sin subir la frecuencia/visibilidad de los encuentros, el arreglo sería invisible. Por eso lo que antes era la mejora opcional B.6 (radar de amistad + escaneo también en Idle) pasa a ser parte del núcleo de esta feature, no un pulido final.
- **Problema adicional no obvio**: varios NPCs de "relleno" (`TownNpc#1`, `TownNpc#5`, `TownNpc#10`, `Guerrero#1`...) **comparten el mismo asset `NPCSocialConfig`** (mismo `npcId`) porque reutilizan un arquetipo genérico. Si escribimos relaciones nuevas directamente en ese SO compartido, todos los NPCs que comparten arquetipo heredarían la misma relación con el mismo tercero — un bug de identidad, no solo de datos. La solución no puede tocar el SO en runtime.

---

### PARTE A — Refugio de NPCs bajo la lluvia

#### A.1 Alcance de la v1

- Cuando empieza a llover, los NPCs ambientales interrumpen lo que están haciendo (vagar, sentados en un banco) y caminan hacia el punto de refugio más cercano.
- Si el punto es un árbol/porche exterior: se quedan ahí parados (idle) hasta que pare de llover.
- Si el punto es una puerta de casa: caminan hasta la puerta y **desaparecen** (se desactiva el GameObject), simulando que han entrado. Cuando deja de llover, reaparecen en esa misma puerta y retoman su rutina.
- NPCs en combate, cinemática, diálogo o interactuando **no se ven afectados** — las prioridades de transición ya existentes (`IsInCinematic > IsInCombat > WasDefeatedInCombat > IsInteracting`) se respetan tal cual.
- NPCs "importantes" (mercaderes con puesto fijo, guardias apostados, NPCs con diálogo de quest activo) deben poder **desactivar** este comportamiento con un flag, para no romper su disponibilidad narrativa.

#### A.2 Piezas nuevas

**1. `Assets/Scripts/Behaviour NPC/Common/NPCWeatherAwareness.cs` (nuevo, clase estática)**

Evita que cada NPC haga su propio `FindAnyObjectByType<DayNightCycle>()` (caro si hay decenas de NPCs por escena). Un único punto de suscripción por escena, expuesto como evento estático barato:

```csharp
public static class NPCWeatherAwareness
{
    public static event Action RainStarted;
    public static event Action RainStopped;
    public static bool IsRaining { get; private set; }

    private static DayNightCycle _cycle;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _cycle = null;
        IsRaining = false;
        RainStarted = null;
        RainStopped = null;
    }
#endif

    // Llamado por WorldBootstrap (execution order +200) tras cargar cada escena aditiva,
    // y también desde NPCBehaviourManagerV2.Awake() como fallback idempotente.
    public static void Resubscribe()
    {
        if (_cycle != null)
        {
            _cycle.RainStarted -= OnRainStarted;
            _cycle.RainStopped -= OnRainStopped;
        }

        _cycle = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (_cycle == null) return; // escena sin ciclo día/noche (interiores, mazmorras)

        _cycle.RainStarted += OnRainStarted;
        _cycle.RainStopped += OnRainStopped;
        IsRaining = _cycle.IsRaining;
    }

    private static void OnRainStarted() { IsRaining = true;  RainStarted?.Invoke(); }
    private static void OnRainStopped() { IsRaining = false; RainStopped?.Invoke(); }
}
```

**Punto a decidir en implementación:** ¿quién llama a `Resubscribe()` tras cada carga aditiva de escena? Candidato natural: `WorldBootstrap.cs` (execution order +200, ya orquesta el mundo tras cargar). Si `DayNightCycle` no cambia de instancia entre escenas del mismo pueblo, una sola llamada en `Start()` de `WorldBootstrap` basta.

**2. `Assets/Scripts/Behaviour NPC/NPCShelterPoint.cs` (nuevo)**

Calcado deliberadamente de `NPCWorldPoint.cs` (mismo patrón: registro estático `OnEnable/OnDisable`, `TryFindNearest`, gizmos) para que cualquiera que conozca `NPCWorldPoint` entienda este de inmediato:

```csharp
public enum NPCShelterType { TreeCanopy, HouseDoor }

public class NPCShelterPoint : MonoBehaviour
{
    public NPCShelterType shelterType = NPCShelterType.TreeCanopy;
    public Transform interactionPoint;      // igual que NPCWorldPoint
    public int capacity = 3;                // TreeCanopy: varios NPCs caben bajo el mismo árbol
                                             // HouseDoor: normalmente 1 (o pocos) para no saturar

    private readonly List<Transform> _occupants = new(); // NO se allocan en Update, solo en TryOccupy/Release

    public bool IsFull => _occupants.Count >= capacity;
    public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;
    public Quaternion InteractionRotation => interactionPoint != null ? interactionPoint.rotation : transform.rotation;

    private static readonly List<NPCShelterPoint> _all = new();
    public static IReadOnlyList<NPCShelterPoint> All => _all;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _all.Clear();
#endif

    void OnEnable()  => _all.Add(this);
    void OnDisable() { _all.Remove(this); _occupants.Clear(); }

    public bool TryOccupy(Transform occupant)
    {
        if (IsFull || _occupants.Contains(occupant)) return IsFull ? false : true;
        _occupants.Add(occupant);
        return true;
    }

    public void Release(Transform occupant) => _occupants.Remove(occupant);

    public static bool TryFindNearest(Vector3 position, NPCShelterType? filter, float maxDist, out NPCShelterPoint result)
    {
        result = null;
        float bestSqr = maxDist * maxDist;
        foreach (var sp in _all)
        {
            if (sp == null || sp.IsFull) continue;
            if (filter.HasValue && sp.shelterType != filter.Value) continue;
            float sqr = (sp.InteractionPosition - position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; result = sp; }
        }
        return result != null;
    }
}
```

**3. `Assets/Scripts/Behaviour NPC/States/SeekShelterState.cs` (nuevo `INPCState`)**

Calcado del esqueleto de `WalkToActivityState.cs` (caminar → llegar → ocupar → esperar → liberar), con la rama especial de "desaparecer en la puerta":

- `OnEnter`: `NPCShelterPoint.TryFindNearest(pos, null, maxDist: ~25f, out point)`. Si no hay ninguno libre en rango, no forzamos nada raro: el NPC se queda en `IdleState` bajo la lluvia (mejor que un NPC vagando sin rumbo buscando algo que no existe). Si hay punto, `SetDestination` igual que `WalkToActivityState`.
- `OnUpdate`: igual que `WalkToActivityState` — comprobar `HasReachedDestination`. Al llegar:
  - `TreeCanopy` → `TryOccupy`, `StopMovement`, orientar hacia el tronco/interior de la copa, reproducir una animación idle existente (no hace falta animación nueva: basta con parar el movimiento y, opcionalmente, `Animator.PlaySocialGesture("Question01")` ocasional para dar vida — evaluar en producción si merece una animación dedicada de "resguardarse").
  - `HouseDoor` → tras un pequeño delay (0.6–1s, tiempo de "abrir la puerta"), desactivar el NPC: **guardar el punto de refugio en el contexto (`context.CurrentShelter`) y hacer `gameObject.SetActive(false)`**, respetando la regla de CLAUDE.md de no llamar `SetActive` sin comprobar el estado previo (guardar un bool `_hidden` y solo desactivar una vez).
- `CheckTransitions`: mismas prioridades que siempre (`IsInCinematic > IsInCombat > WasDefeatedInCombat > IsInteracting`) primero. Después: `if (!context.ShouldSeekShelter) return HandleReturn(context);` — donde `HandleReturn`:
  - Si el NPC está oculto (`HouseDoor`), lo reactiva en la posición de la puerta (`gameObject.SetActive(true)`, reposicionar con `Agent.Warp(shelterPoint.InteractionPosition)` para evitar el mismo bug INC-046 de agentes "flotando" documentado en `WalkToActivityState.OnExit`), libera el punto, transiciona a `WanderState`.
  - Si estaba en `TreeCanopy`, simplemente libera el punto y transiciona a `WanderState` desde donde esté parado.
- `OnExit`: liberar el `NPCShelterPoint` (`Release(context.Transform)`) siempre, por si se interrumpe a media transición (p.ej. entra en combate estando bajo el árbol).

**4. Cambios en `NPCStateContext.cs`**

Añadir junto a los campos `Pending Social*` ya existentes:

```csharp
public bool ShouldSeekShelter { get; set; }
public NPCShelterPoint CurrentShelter { get; set; }
```

**5. Cambios en `NPCBehaviourManagerV2.cs`**

En `Awake()` (junto al resto de inicialización): `NPCWeatherAwareness.Resubscribe()` si aún no se ha llamado esta escena (idempotente), y suscribirse a `NPCWeatherAwareness.RainStarted/RainStopped` para setear `_context.ShouldSeekShelter = true/false`. Desuscribir en `OnDestroy`.

Guard opcional por NPC: nuevo campo en `NPCAmbientConfig` (el módulo ya existente, mismo sitio que `enableWander`), p.ej. `public bool canSeekShelter = true;`. Vendedores con puesto fijo, guardias apostados, NPCs con diálogo de quest activo → desmarcar en el inspector. Este patrón replica exactamente cómo ya se controla `enableWander` hoy (`NPCConfiguration.enableWander`, línea 144 de `NPCConfiguration.cs`).

#### A.3 Integración con estados existentes

Añadir la misma comprobación en las tres puertas de entrada donde hoy también se comprueba `PendingSocialPartner`, con **menor prioridad que combate/cinemática/interacción pero mayor que "seguir vagando"**:

- `IdleState.CheckTransitions()`
- `WanderState.CheckTransitions()`
- `WalkToActivityState.CheckTransitions()` (un NPC sentado en un banco debe levantarse a refugiarse, igual que hoy se levanta si otro NPC quiere socializar con él)

Ejemplo de línea a añadir (idéntica forma a la ya existente para `PendingSocialPartner`):

```csharp
if (context.ShouldSeekShelter && context.CurrentShelter == null && config.canSeekShelter)
    return new SeekShelterState();
```

#### A.4 Casos límite a resolver en implementación

- **Rain empieza mientras el NPC ya está en `SeekShelterState` yendo hacia otro sitio** (imposible salvo bug, pero por seguridad `ShouldSeekShelter` solo dispara la transición si `context.CurrentShelter == null`, evitando reentradas).
- **Un NPC queda "atrapado" dentro de una casa si la escena se descarga/recarga con la lluvia aún activa**: al reactivar tras cargar, `NPCBehaviourManagerV2.Awake()` debe comprobar `NPCWeatherAwareness.IsRaining` y, si sigue lloviendo, mantenerlo oculto o (más simple y robusto) simplemente no ocultar nada al recargar — cada carga de escena empieza "fresca" reevaluando el estado real.
- **NPCs relevantes para el grafo narrativo**: si un `NarrativeRunner` espera interactuar con un NPC concreto y este se ha desactivado por la lluvia, la interacción fallaría silenciosamente. Mitigación: `canSeekShelter = false` en cualquier NPC con `narrativeID` asignado en `NPCRegistry` (se puede automatizar: si `NPCBehaviourManagerV2` tiene un `narrativeID` no vacío, forzar `canSeekShelter = false` salvo override explícito). Anotar esto como checklist antes de dar por cerrada la feature.
- **Colocación de puntos de refugio**: trabajo manual de nivel, no de código — colocar `NPCShelterPoint` bajo árboles y en puertas de casas de cada escena de pueblo. Ninguna automatización razonable para esto sin analizar la geometría de cada escena.

#### A.5 Archivos a crear/tocar (Parte A)

| Acción | Archivo |
|---|---|
| Crear | `Assets/Scripts/Behaviour NPC/Common/NPCWeatherAwareness.cs` |
| Crear | `Assets/Scripts/Behaviour NPC/NPCShelterPoint.cs` |
| Crear | `Assets/Scripts/Behaviour NPC/States/SeekShelterState.cs` |
| Tocar | `Assets/Scripts/Behaviour NPC/Common/NPCStateContext.cs` (2 propiedades nuevas) |
| Tocar | `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs` (suscripción a eventos de lluvia) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/IdleState.cs`, `WanderState.cs`, `WalkToActivityState.cs` (una línea cada uno en `CheckTransitions`) |
| Tocar | Módulo `NPCAmbientConfig` (nuevo flag `canSeekShelter`) |
| Tocar (posible) | `Assets/Scripts/Core/WorldBootstrap.cs` (llamada a `NPCWeatherAwareness.Resubscribe()`) |
| Trabajo de nivel | Colocar `NPCShelterPoint` en escenas de pueblo (árboles, puertas) |

---

### PARTE B — Relaciones sociales dinámicas entre NPCs

#### B.1 Qué se arregla exactamente

Hoy: `WanderState.CheckSocialEncounter()` (línea 308) llama `socialConfig.GetRelationshipWith(partnerId)`, que lee el array estático `relationships[]` del `ScriptableObject` — vacío siempre, así que todo resuelve a `Stranger`. El encuentro (`NPCSocialEncounterState`) es puramente cosmético: elige gestos según relación, pero **nunca la modifica ni la crea**.

Objetivo: que hablar repetidamente haga que dos NPCs concretos pasen de `Stranger` → `Acquaintance` → `Friend` → `BestFriend`, que eso se note (duración/gestos ya varían según el enum, así que el pago visual ya existe gratis), y que sobreviva a guardar/cargar.

#### B.2 Decisión de identidad (el problema no obvio — verificado, no es hipotético)

`npcId` vive en el `NPCSocialConfig` (SO). Comprobado en el asset real `Assets/_NPCs/Social/NPC_Social_Archetype_Friendly.asset`: **`npcId: ''` (vacío)**, y es el que usan los 13 NPCs de relleno que vagan en `MainWorld.unity` (`TownNpc#1-10`, `Guerrero#1-3`). Con `npcId` vacío, ni siquiera se puede aplicar la solución "compartir progreso entre figurantes" que había planteado como aceptable — un `npcId` vacío es indistinguible de "sin identidad", así que ninguno de los 13 NPCs que más vagan por el pueblo podría forjar ninguna relación tal cual está montado hoy. Dado que además son literalmente el grueso de la población ambiental de la escena, dejar esto sin arreglar deja la feature entera sin sujetos sobre los que demostrarse.

**Decisión recomendada para v1 (cambiada respecto a la primera versión de este documento):** no basta con "aceptar" el `npcId` compartido — hay que garantizar que **todo NPC con `NPCSocialConfig` asignado tenga una identidad única en runtime**, aunque el SO de personalidad sea compartido. Opción de menor riesgo: en `NPCBehaviourManagerV2.Awake()`, si `configuration.socialConfig.npcId` está vacío, generar y cachear un id estable derivado de algo que ya identifica a esa instancia concreta — candidato directo: `persistenceId` si existe (los NPCs narrativos ya lo tienen, ej. `NPC_Eldran`), y si no, `gameObject.name + "_" + GetInstanceID()` (estable durante la sesión, se re-genera cada partida pero eso es aceptable para relleno anónimo — lo importante es que sea único *entre* los 13, no que sea el mismo ID entre sesiones). Este id runtime-only vive en el propio `NPCBehaviourManagerV2`/`NPCStateContext` (nunca se escribe de vuelta al SO compartido), y es el que se usa como clave en `NPCRelationshipRegistry`, no el `npcId` crudo del SO.

Los NPCs con nombre propio (Eldran, Sofía...) siguen usando su `npcId` real del SO individual, que ya es único — sin cambios para ellos.

Esto se documenta como comentario explícito en el código (`NPCBehaviourManagerV2`) para que quien lo lea entienda por qué hay dos fuentes de identidad (SO para autoría, runtime-id para forja) y no se intente "simplificar" fusionándolas.

#### B.3 Registro runtime nuevo: `NPCRelationshipRegistry`

**`Assets/Scripts/Behaviour NPC/NPCRelationshipRegistry.cs` (nuevo, estático — mismo patrón que `ActiveCombatRegistry.cs`)**

No se puede escribir en el `ScriptableObject` compartido (corrompería a todos los NPCs del arquetipo). El estado dinámico vive aparte, indexado por el par de `npcId`:

```csharp
public static class NPCRelationshipRegistry
{
    private struct Bond
    {
        public int encounterCount;
        public float bondScore;          // 0-100, acumulado en encuentros completados
        public NPCRelationType? forgedType; // null = usar el valor autor (relationships[] del SO)
    }

    private static readonly Dictionary<(string, string), Bond> _bonds = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _bonds.Clear();
#endif

    private static (string, string) Key(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    // Se llama SOLO cuando el encuentro social se completa de forma natural
    // (NPCSocialEncounterState.OnExit, _timer >= _duration), nunca si se interrumpe por combate/cinemática.
    public static NPCRelationType RegisterEncounterCompleted(string idA, string idB, float avgFriendliness)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || idA == idB)
            return NPCRelationType.Stranger;

        var key = Key(idA, idB);
        _bonds.TryGetValue(key, out var bond);

        bond.encounterCount++;
        bond.bondScore += Mathf.Lerp(2f, 8f, avgFriendliness); // más simpáticos, vínculo crece más rápido

        // No promocionar relaciones ya marcadas como Rival/Enemy por diseño (esas son fijas, autor-only en v1)
        var authored = ResolveAuthored(idA, idB);
        if (authored != NPCRelationType.Rival && authored != NPCRelationType.Enemy)
        {
            bond.forgedType = bond.bondScore switch
            {
                >= 60f => NPCRelationType.BestFriend,
                >= 30f => NPCRelationType.Friend,
                >= 10f => NPCRelationType.Acquaintance,
                _      => bond.forgedType, // no degradar antes de tiempo
            };
        }

        _bonds[key] = bond;
        return Resolve(idA, idB);
    }

    // Resolución que reemplaza a socialConfig.GetRelationshipWith en los puntos de consulta:
    // 1) override runtime forjado, 2) valor autor del SO, 3) Stranger.
    public static NPCRelationType Resolve(string idA, string idB, Func<string, NPCRelationType> authoredLookup = null)
    {
        if (_bonds.TryGetValue(Key(idA, idB), out var bond) && bond.forgedType.HasValue)
            return bond.forgedType.Value;
        return authoredLookup != null ? authoredLookup(idB) : NPCRelationType.Stranger;
    }

    private static NPCRelationType ResolveAuthored(string idA, string idB) => NPCRelationType.Stranger; // ver B.4

    // Persistencia — ver B.5
    public static List<PlayerSaveData.NpcRelationshipEntry> ToSaveEntries() { /* ... */ return null; }
    public static void LoadFromSaveEntries(List<PlayerSaveData.NpcRelationshipEntry> entries) { /* ... */ }
}
```

*(Pseudocódigo de diseño — los detalles de `ResolveAuthored` se resuelven en implementación real pasando el `NPCSocialConfig` del NPC iniciador, ver B.4.)*

Umbrales (10/30/60) son un punto de partida, no un valor cerrado — tunable en producción jugando unas cuantas sesiones.

#### B.4 Punto de integración: `WanderState.CheckSocialEncounter()`

Cambio mínimo, línea 308 (`NPCRelationType relation = socialConfig.GetRelationshipWith(partnerId);`) pasa a:

```csharp
NPCRelationType relation = NPCRelationshipRegistry.Resolve(
    socialConfig.npcId, partnerId,
    otherId => socialConfig.GetRelationshipWith(otherId)); // fallback al valor autor del SO
```

Y en `NPCSocialEncounterState.cs`, añadir un `OnExit` (hoy no existe override, hereda el de `NPCStateBase`) que, **solo si el encuentro terminó de forma natural** (no interrumpido por combate/cinemática — comprobar `_timer >= _duration` antes de salir), llame:

```csharp
NPCRelationshipRegistry.RegisterEncounterCompleted(myId, partnerId, avgFriendliness);
```

`avgFriendliness` = promedio de `personality.friendliness` de ambos NPCs (se puede pasar por `context` o recuperar de `configuration.socialConfig` de ambos lados).

#### B.5 Persistencia (siguiendo el patrón exacto de `npcPositions`)

El proyecto ya tiene un patrón claro y probado para "estado runtime de NPCs que debe sobrevivir al save" — es literalmente `npcPositions`, presente en tres sitios que se mantienen sincronizados:

1. `PlayerSaveData.cs` — struct `NpcPosEntry` + `List<NpcPosEntry> npcPositions`.
2. `PlayerPresetSO.cs` — su propio `NpcPosEntry` espejo, que es el estado "vivo" durante la sesión.
3. `GameBootProfile.cs` — copia entre ambos en `SetRuntimePresetFromSave()` (~línea 214-222), en la construcción del save (~línea 364-370) y en la ruta de test mode (~línea 405-420).

Para relaciones, replicar exactamente esa estructura:

1. **`PlayerSaveData.cs`** — nuevo struct y lista:
   ```csharp
   [Serializable]
   public struct NpcRelationshipEntry
   {
       public string npcIdA;
       public string npcIdB;
       public NPCRelationType type;
       public int encounterCount;
       public float bondScore;
   }
   public List<NpcRelationshipEntry> npcRelationships = new();
   ```
   En `FromGameBootProfile()`: `d.npcRelationships = NPCRelationshipRegistry.ToSaveEntries();`
   En `ApplyToGameBootProfile()` (vía `SetRuntimePresetFromSave`): `NPCRelationshipRegistry.LoadFromSaveEntries(data.npcRelationships);`

2. **`PlayerPresetSO.cs`** — mismo struct espejo (`NpcRelationshipEntry`) + lista, igual que ya existe para `NpcPosEntry`.

3. **`GameBootProfile.cs`** — añadir el mismo bloque de copia ida/vuelta que ya existe para `npcPositions` en los 3 puntos citados arriba.

4. **Sanitización de saves antiguos**: en `SaveSystem.LoadFromPath()` (línea 76-82), añadir `data.npcRelationships ??= new List<PlayerSaveData.NpcRelationshipEntry>();` junto a las demás listas saneadas, para que saves guardados antes de esta feature no rompan al cargar.

**Respetar la Regla 1 de CLAUDE.md** (modo test = volcado exacto del `bootPreset`, sin mezclar con JSON): las relaciones deben fluir por el mismo camino que `npcPositions` en modo test (`EnsureRuntimePresetFromTemplate` + `ApplyPresetAsLoadedGame`), nunca leerse del JSON real en ese modo. Como se está clonando el patrón ya existente literalmente campo por campo, esto sale gratis si se sigue la plantilla — pero es el punto a verificar con más cuidado al implementar, porque romper esa regla afecta al grafo narrativo persistente completo, no solo a esta feature.

#### B.6 Por qué esto **no es opcional**: sin esto, el arreglo de B.1-B.5 sigue siendo invisible

En la primera versión de este documento esto estaba planteado como "mejora opcional". Al verificar los números reales (13 NPCs ambientales en total en `MainWorld.unity`, escaneo social solo en `WanderState`, ventana de 3s, doble dado de sociabilidad/cooldown — ver diagnóstico al inicio del documento), queda claro que **arreglar solo la forja de relaciones (B.1-B.5) no resuelve tu queja**: seguiría pasando casi nunca, solo que ahora "casi nunca" acumularía progreso en vez de no acumular nada. Estos dos puntos pasan a ser parte obligatoria de la v1, no un pulido posterior:

1. **Radar de amistad** (obligatorio): en `WanderState.OnEnter()` (donde hoy se decide si ir a un `NPCWorldPoint` o vagar), añadir una probabilidad (ponderada por `sociability`) de que el NPC intente *buscar específicamente* a un `Friend`/`BestFriend` conocido dentro de un radio ampliado (p. ej. `socialDetectionRange * 2.5`) en vez de solo detectar a quien pase cerca por azar. Necesita un pequeño registro espacial "dónde está cada NPC ahora" — un registro nuevo tipo `ActiveCombatRegistry` pero para NPCs ambientales (`NPCAmbientRegistry`: `Dictionary<string runtimeId, Transform>` actualizado en `OnEnable`/`OnDisable` de `NPCBehaviourManagerV2`, coste O(1)). Sin esto, con solo 13 NPCs en todo el pueblo, dos con vínculo fuerte pueden pasarse la partida entera sin volver a cruzarse por pura geografía aleatoria.
2. **Activar el escaneo social también en `IdleState`** (obligatorio): hoy solo ocurre mientras el NPC camina (`WanderState`), nunca en `IdleState` ni sentado en un banco. Dado que un NPC pasa una parte grande de su ciclo de vida en Idle/actividades, dejarlo fuera reduce las oportunidades reales a la mitad o menos. Coste: mover `CheckSocialEncounter()` a un método compartido en `NPCStateBase` y llamarlo también desde `IdleState.OnUpdate`.
3. **Subir `socialDetectionRange` y bajar `socialCooldown` en los arquetipos de relleno** (ajuste de datos, no de código): `NPC_Social_Archetype_Friendly.asset` hoy tiene 5m/25s. Con solo 13 NPCs en todo el pueblo, valores conservadores pensados para una escena más poblada dejan la feature muerta por pura estadística. Subir a ~8-10m y bajar cooldown a ~15-20s para los arquetipos de relleno (no necesariamente para NPCs nombrados, donde el ritmo pausado puede ser intencional).

Con los tres puntos, la combinación (más alcance, más ventanas de detección, búsqueda activa de amigos) es lo que convierte "existe en el código" en "se ve en la partida".

#### B.7 Archivos a crear/tocar (Parte B)

| Acción | Archivo |
|---|---|
| Crear | `Assets/Scripts/Behaviour NPC/NPCRelationshipRegistry.cs` |
| Crear | `Assets/Scripts/Behaviour NPC/NPCAmbientRegistry.cs` (registro espacial para el radar de amistad, B.6.1) |
| Tocar | `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs` (id runtime cuando `npcId` está vacío, ver B.2; registro/desregistro en `NPCAmbientRegistry`) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/WanderState.cs` (línea 308, resolución de relación; `OnEnter`, radar de amistad B.6.1) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/IdleState.cs` (activar escaneo social, B.6.2) |
| Tocar | `Assets/Scripts/Behaviour NPC/Common/NPCStateBase.cs` (extraer `CheckSocialEncounter` a método compartido) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/NPCSocialEncounterState.cs` (nuevo `OnExit`, registrar encuentro completado) |
| Tocar (datos) | `Assets/_NPCs/Social/NPC_Social_Archetype_*.asset` (subir `socialDetectionRange`, bajar `socialCooldown`, B.6.3) |
| Tocar | `Assets/Scripts/Player/PlayerSaveData.cs` (struct + lista + sanitización) |
| Tocar | `PlayerPresetSO.cs` (struct espejo + lista) |
| Tocar | `Assets/Scripts/Core/GameBootProfile.cs` (copia ida/vuelta, 3 puntos) |
| Tocar | `Assets/Scripts/Core/SaveSystem.cs` (línea ~76-82, sanitización de listas null) |
| Opcional (B.6) | `Assets/Scripts/Behaviour NPC/States/WanderState.cs`, `IdleState.cs`, nuevo registro espacial de NPCs |

---

### Orden de implementación recomendado

1. **Parte B primero** (relaciones) — es la que te da más rabia y el riesgo es menor (no toca movimiento/NavMesh, solo datos + un `OnExit`). Empezar por B.3-B.4 sin persistencia, jugar y validar que las relaciones evolucionan bien en una sesión. Añadir B.5 (persistencia) una vez el comportamiento en runtime se sienta bien — así no hay que retocar el guardado dos veces si cambian los umbrales.
2. **Parte A después** (refugio de lluvia) — más trabajo de nivel (colocar puntos manualmente) y más superficie de casos límite (NPCs narrativos, agentes de NavMesh). Construir `NPCShelterPoint` + `SeekShelterState` con solo `TreeCanopy` primero, verificar que el ciclo completo (lluvia → refugio → vuelta) funciona bien, y añadir `RoofedSpot` después. *(Histórico: aquí se planteaba añadir `HouseDoor` con `SetActive` — descartado, ver actualización al principio de la sección.)*
3. **B.6** (radar de amistad / idle social) al final, como pulido, una vez lo esencial de ambas partes esté verificado en el juego real.

### Preguntas abiertas para validar antes de programar

- Umbrales de `bondScore` (10/30/60) y velocidad de acumulación: valores de partida, se ajustan jugando.
- ¿Quién llama a `NPCWeatherAwareness.Resubscribe()` tras cada carga aditiva de escena? Propuesto `WorldBootstrap`, a confirmar mirando su `Start()` real.
- Lista de NPCs que deben quedar excluidos del refugio de lluvia (guardias apostados, vendedores con puesto fijo, cualquiera con `narrativeID` activo) — se puede generar automáticamente o requerir marcado manual en el inspector; recomendado automático con override manual.

### PARTE D — Ideas futuras: "vida artificial" de NPCs y del grupo (capturadas, sin diseñar aún)

**Fecha:** 14 agosto 2026. Lluvia de ideas de Raúl, anotada aquí tal cual para no perderla — **nada
de esto está diseñado en detalle todavía** (piezas de código, estados nuevos, assets necesarios),
es solo la intención de diseño de alto nivel. Antes de implementar cualquier punto de estos hace
falta pasar por el mismo proceso que la Parte A/B de arriba: diagnóstico del código real, piezas
nuevas, casos límite. Objetivo general: que los NPCs (y el propio grupo jugable) se sientan vivos
sin que el jugador lo perciba como "un sistema" — reacciones oportunistas a contexto, no eventos
anunciados.

**D.1 — NPCs sueltos del pueblo, socialización ambiental por aburrimiento**
Cuando no tienen nada mejor que hacer, los NPCs del pueblo se buscan entre ellos y se ponen a
hablar, de dos en dos o en grupo si hace falta. Conversaciones random añadidas a los literales de
diálogo existentes. Bocadillo de "hablando" sobre la cabeza mientras dura. Probablemente construido
sobre el sistema social ya existente pero verificado como invisible en la práctica (ver diagnóstico
al principio de esta sección, punto sobre `WanderState.CheckSocialEncounter`) — puede que sea la
misma pieza que arregla la Parte B de arriba, con una capa de UI (bocadillo) y contenido de diálogo
nuevo encima.

**D.2 — Idle del grupo jugable (Will/Liam/Estela) cuando el jugador no toca el mando**
Tras un tiempo sin input, el personaje activo (o los compañeros) rompen el idle genérico con
pequeñas escenas con personalidad:
- Estela puede sentarse en el suelo y decir que tiene hambre.
- Will se pone a buscar algo (rebuscar en algo, gesto de "dónde estará").
- Liam le dice a Estela que se levante del suelo y empiezan a discutir (una pareja de gestos/líneas
  encadenadas entre dos compañeros, no solo un personaje solo).
Necesita: detección de "sin input del jugador durante X tiempo", banco de micro-escenas por
personaje/pareja de personajes, y decidir si esto vive en el propio `PlayerActionManager` / stack
de modos o en un sistema aparte que se dispare cuando el modo activo es el de exploración normal.

> **ACTUALIZACIÓN (14 ago 2026):** primera pieza de D.2 implementada — solo la parte de Estela.
> Nuevo componente `EstelaIdleCommentary` (`Assets/Scripts/Behaviour NPC/EstelaIdleCommentary.cs`),
> a añadir manualmente al GameObject de Estela (mismo objeto que ya tiene `NPCPartyMember`/
> `NPCBehaviourManagerV2`). Mientras Estela sigue al jugador con normalidad (no en combate/
> cinemática/diálogo, no controlada por el jugador, equipo en modo Siguiendo, sobre NavMesh —
> es decir, no volando/nadando/escalando junto al jugador), cada 25-55s (configurable) dispara al
> azar uno de tres numeritos: comentario suelto sin dejar de andar, sentarse a decir que tiene
> hambre, o plantarse un rato delante del jugador quejándose de aburrimiento (bloqueando
> físicamente el paso). Las líneas de diálogo están en el inspector (arrays serializados), fáciles
> de ajustar/ampliar sin tocar código.
>
> Nota de implementación importante para cuando se aborde el resto de D.2 (Will, Liam, la
> discusión entre los dos): **no usar directamente `IdleState`** para estas escenas. `NPCPartyMember.
> Update()` sondea cada 0.5s y, si el `Brain` de un compañero en party está en un estado
> literalmente llamado `"Idle"` con el equipo en modo Siguiendo, lo vuelve a poner a seguir al
> jugador inmediatamente — deshaciendo cualquier numerito a media frase (mismo mecanismo que ya
> causaba el bug documentado ahí como "FIX INC-059" con el sentado de la taberna). La solución
> usada aquí es montar el numerito como una `CinematicSequence` corta dentro de `CinematicState`
> (vía `NPCBehaviourManagerV2.StartCinematicSequence`, el mismo mecanismo que ya usan las
> cinemáticas narrativas) — `CinematicState` pone `Context.IsInCinematic = true` mientras dura, así
> que el sondeo de `NPCPartyMember` lo ignora, y al terminar la secuencia vuelve a `IdleState` de
> forma normal, momento en el que ese mismo sondeo reanuda el seguimiento solo, sin código extra.
>
> Pendiente de D.2: Will (buscar algo) y Liam (decirle a Estela que se levante + discusión entre
> los dos, que requiere coordinar dos `NPCPartyMember` a la vez en vez de uno) — no implementados
> todavía. Tampoco se implementó el disparador "sin input del jugador durante X tiempo" que
> describe el punto original: esta primera pieza dispara por temporizador aleatorio mientras el
> grupo sigue al jugador (más simple, y encaja con cómo lo pidió Raúl la segunda vez: "de pronto"),
> no por detección real de mando quieto. Si se quiere ese disparador más adelante, no existe
> ninguna utilidad de "segundos desde el último input" reutilizable en el proyecto todavía — habría
> que construirla desde cero (comprobado explícitamente, ver investigación de esta misma fecha).

**D.3 — Reacciones ambientales puntuales al pasar cerca de un NPC**
Caminando por el pueblo, un NPC suelta un comentario al azar y Estela (u otro compañero) le
contesta "por la cara" — interacción no solicitada por el jugador, con bocadillos de frase sobre
ambas cabezas.

**D.4 — Comentarios de viaje / caminata larga**
De forma random, tras caminar un rato seguido, los compañeros pueden soltar comentarios, sonidos, o
adelantarse y proponer una carrera ("te echo una carrera de aquí a allí") — Raúl apunta que esto
encaja con Liam (Estela más bien no, según lo comentado).

**Nota de alcance:** D.1 y D.3/D.4 comparten infraestructura de bocadillos de diálogo sobre la
cabeza (UI nueva o reutilizable — comprobar si ya existe algo parecido, p.ej. en diálogo de quest o
en el sistema de iconos de NPC como `NPCAlertIconController`/`NPCPersistentIconController`) y de
contenido de líneas sueltas independientes del grafo narrativo principal. D.2 es más un sistema de
idle-behaviours del grupo jugable, más cercano a `PlayerActionManager`/`NPCPartyMember` que al FSM
de NPCs ambientales. Cuando se aborde esto en serio, tratar D.1-D.4 como iniciativas separadas con
su propio diagnóstico, no como una sola feature.

---

## 18. Checklist: Demo de Steam

> Trasladado desde `STEAM_DEMO_CHECKLIST.md` (unificación de documentación, 12 de agosto de 2026). Checklist operativo para publicar la demo en Steam, partiendo de cero (sin cuenta de Steamworks todavía).

Partiendo de cero (sin cuenta de Steamworks todavía). Orden real de dependencias: hay pasos que bloquean a otros (el fee bloquea la app, la app bloquea la demo, la store page bloquea el lanzamiento).

---

### Fase 1 — Cuenta y papeleo (Valve, no técnico)

- [ ] Tener una cuenta de Steam normal con **al menos $5 gastados** (requisito para poder crear cuenta de Steamworks).
- [ ] Crear la cuenta de socio en **partner.steamgames.com**.
- [ ] Rellenar el cuestionario fiscal (tax interview) y datos bancarios para poder cobrar.
- [ ] Verificar identidad cuando Steamworks lo pida.
- [ ] Pagar el **fee de Steam Direct: $100** por el juego base (no hace falta pagar otro fee para la demo, se cuelga de la misma app).
  - Es reembolsable una vez el juego alcance $1,000 de ingresos brutos ajustados.
- [ ] Tras el pago hay una **espera obligatoria de 30 días** antes de poder publicar nada. Valve la usa para verificar quién eres. **Esto es lo primero que deberías arrancar**, porque corre en paralelo a todo lo demás.

---

### Fase 2 — Crear la app y la app de demo

- [ ] En Steamworks, crear la **app del juego base** (aunque el lanzamiento completo esté lejos, la demo cuelga de esta app).
- [ ] Crear una **segunda app separada de tipo "Demo"**.
  - En su configuración general hay que introducir el **App ID del juego base** para enlazarlas.
  - Se crea un depot automáticamente para la demo (debería verse exactamente uno en la pantalla de depots).
- [ ] Decidir la store page de la demo: se puede configurar una página propia completa, o simplemente aportar assets para que la demo aparezca dentro de la store page del juego base. Para una demo temprana, lo normal es la segunda opción (más simple, menos mantenimiento).

---

### Fase 3 — Contenido de la demo (esto ya lo tienes decidido)

Ya que sabes qué escenas/tramo entra en la demo, solo queda:

- [ ] Confirmar que ese tramo es jugable de principio a fin sin dependencias de sistemas que aún no estén cerrados (guardado, quests, etc. — revisa `TDD.md` § 13 por si hay bugs conocidos que afecten justo a esas escenas).
- [ ] Añadir una pantalla o mensaje de "fin de la demo" al terminar el tramo (evita que el jugador se quede colgado o salga del contenido probado).
- [ ] Revisar que el flujo de arranque (`Start.unity` con los managers persistentes) funciona igual en un build standalone que en el editor — probar el build, no solo Play en el editor.

---

### Fase 4 — Store page (assets y textos)

- [ ] Al menos **5 capturas de pantalla**.
- [ ] Un **tráiler** (recomendado, casi obligatorio en la práctica para conversión de la página).
- [ ] Descripción corta y descripción larga del juego.
- [ ] Tags / categorías.
- [ ] Precio (o marcar como gratis si la demo se distribuye independiente, aunque normalmente la demo es gratis por definición y el juego base lleva su precio).
- [ ] Assets gráficos con tamaños exactos (Valve rechaza por 1-2 px de diferencia):
  - Header Capsule: 920×430 px
  - Small Capsule: 462×174 px
  - Main Capsule: 1232×706 px
  - Vertical Capsule: 748×896 px
  - Library Capsule: 600×900 px
  - Todos JPG o PNG, máx. 2 MB.
- [ ] Desde septiembre 2022 la capsule base solo puede llevar: arte del juego, nombre del juego, subtítulo oficial. Nada de puntuaciones, premios ni texto de marketing — si no, Valve penaliza visibilidad en tienda.
- [ ] **La store page debe estar publicada (visible) al menos 2 semanas antes de poder lanzar** cualquier build, incluida la demo.

---

### Fase 5 — Build técnico y subida (SteamPipe)

- [ ] Descargar el **Steamworks SDK** (ContentBuilder).
- [ ] Generar el build de Unity para Windows (mínimo; valorar Mac/Linux según alcance).
  - Revisar Player Settings: `companyName: Liyodev`, `productName: El Sendero de las Estrellas`, versión actual `0.1.0` — decidir si la demo lleva su propio número de versión visible.
- [ ] Configurar los scripts `.vdf` de `app_build` y `depot_build` con el App ID de la demo y el depot creado en Fase 2.
- [ ] Subir con `steamcmd` (o Web Upload / ZIP si se prefiere algo más simple para una primera subida).
- [ ] Probar el build subido desde una **branch privada de Steamworks** (no la `default`) antes de hacerlo público, para no exponer una demo rota.
- [ ] Cuando esté validado, mover a la branch pública.

---

### Fase 6 — Lanzamiento

- [ ] Confirmar que ya pasaron los 30 días de espera de Valve.
- [ ] Confirmar que la store page lleva ≥2 semanas visible.
- [ ] Publicar la demo (cambiar de "coming soon" / oculta a visible/jugable).
- [ ] Anunciar (redes, Discord, etc. — fuera del alcance técnico, pero es el paso que de verdad mueve wishlists).

---

### Notas

- El fee y la espera de 30 días son lo más largo del proceso y no dependen de ti una vez pagado — conviene arrancar la Fase 1 ya, en paralelo a pulir el tramo jugable de la demo.
- Los tamaños de capsule y las reglas de contenido cambian de vez en cuando; si tardas meses en llegar a la Fase 4, vale la pena revalidar contra la documentación oficial de Steamworks antes de subir el arte final.

**Fuentes:**
- [Steam Direct Fee — Steamworks Documentation](https://partner.steamgames.com/doc/gettingstarted/appfee)
- [Demos — Steamworks Documentation](https://partner.steamgames.com/doc/store/application/demos)
- [Store Graphical Assets — Steamworks Documentation](https://partner.steamgames.com/doc/store/assets/standard)
- [Testing On Steam — Steamworks Documentation](https://partner.steamgames.com/doc/store/testing)


---

## 19. Auditorías

> Esta sección reúne, sin alterar su contenido sustantivo, las auditorías realizadas sobre el proyecto (trasladadas aquí el 12 de agosto de 2026 desde sus archivos `.md` originales). Son informes **fechados**: retratan el estado del código en el momento en que se escribieron, no el estado actual — conviene leer cada uno con su fecha en mente y verificar contra el código real antes de asumir que un hallazgo sigue vigente. Las referencias que hacen a `CLAUDE.md`/`AGENTS.md` corresponden hoy a secciones de este mismo documento: los invariantes narrativos y la política de convivencia Interactive↔Grafo están en el § 10, y las reglas de código no negociables en el § 12.

### 19.1 Auditoría de código — 7 de agosto de 2026

**Fecha:** 7 de agosto de 2026 · **Ámbito:** 530 archivos C# (Assets/Scripts, Assets/NarrativeGraph, Assets/Editor) · **Método:** 5 revisiones paralelas por subsistema + barrido automático de patrones + verificación manual de los hallazgos críticos (todos los citados como críticos han sido comprobados línea a línea sobre el código actual).

---

#### Veredicto general

El proyecto está en buena forma. La disciplina micro es notable y muy por encima de lo habitual en un proyecto indie: cero `OverlapSphere` sin NonAlloc en todo el código, buffers cacheados, hashes de animator, `sqrMagnitude`, ResetStatics presente en la gran mayoría de singletons, y las correcciones C1–C6 y la pasada "Fase 2" están confirmadas en el código real. Los invariantes del grafo narrativo (CLAUDE.md §4) **se cumplen hoy**: test mode no mezcla JSON, `ReloadTestPreset` sigue la secuencia correcta, `StartFromNode/StartFromStartNode` llaman a `StopExecution()` primero, y `_raised` se preserva bien.

El punto débil no está en el rendimiento por frame (que está sano) sino en **el ciclo de vida de las interrupciones**: qué pasa cuando una corrutina que dejó estado global a medias (input bloqueado, timeScale alterado, renderers apagados, puertas cerradas) muere porque algo la interrumpe — muerte, cambio de escena, cinemática, desactivación del GameObject. Ese patrón se repite en al menos 8 sistemas distintos y es el origen de casi todos los críticos de abajo.

Hay 4 temas transversales que, arreglados una vez, eliminan familias enteras de bugs:

1. **`PushMode` sin refcount** — dos sistemas que empujan el mismo `ActionMode` se roban el Pop entre sí (detalle en C2). Un solo fix arregla conflictos diálogo↔cinemática↔victoria↔stun.
2. **`Time.timeScale` sin árbitro** — lo tocan al menos 4 actores sin coordinarse (hitstop, muerte, cinemáticas, OnDestroy de NPCs). Un servicio central con contador/baseline elimina el "slow-mo permanente" y el "pausa rota".
3. **Corrutinas que restauran estado al final sin `OnDisable` de seguridad** — knockback aéreo, secuencia de victoria, cast con carga, parpadeo de invulnerabilidad, fades. El patrón correcto ya existe en el propio proyecto (`PlayerFlyingController.OnDisable`, `CinematicSequencerBase.Co_SequenceGuarded`): replicarlo.
4. **Logging sin guarda `#if UNITY_EDITOR || DEVELOPMENT_BUILD`** en rutas calientes de combate — es la mayor penalización de rendimiento evitable en builds (cientos de allocs de string por segundo con varios NPCs peleando). Viola la propia regla §2 del proyecto.

---

#### CRÍTICOS — pueden colgar una partida en flujos normales de juego

##### C1. Reentrada en `DialogueManager.StartDialogue` → grafo narrativo colgado para siempre — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Dialogue/DialogueManager.cs:319` *(verificado)*

`StartDialogue` no comprueba `IsOpen`: sobrescribe `_current` y `_onEnd` **sin invocar el callback del diálogo anterior**. `PlayDialogueNode` (`NarrativeGraph/Runtime/Graph/NodeTypes/PlayDialogueNode.cs:81-89`) espera `while (!completed)` sobre ese callback. Escenario real: al completarse una quest reaccionan a la vez el grafo (siguiente `PlayDialogueNode`) y la post-action de `NPCQuestActionExecutor` (que también abre diálogo, con ventanas de 0.5 s en su chequeo de `IsOpen`). El que llega segundo pisa al primero → la rama del grafo queda bloqueada eternamente. Es el punto único de fallo donde convergen grafo, Interactive y post-actions.

**Fix:** si `IsOpen`, encolar o rechazar; y si se decide pisar, invocar el `_onEnd` anterior antes de sustituirlo.

##### C2. `PushMode` dedupe sin refcount roba el Pop entre sistemas — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Player/PlayerActionManager.cs:249-274` *(verificado; detectado independientemente por dos revisores)*

`if (Top == mode) return;` ignora el segundo Push del mismo modo, pero el segundo sistema hará su Pop igualmente y eliminará la entrada del primero. `Cinematic` lo usan DialogueManager, SleepTrigger, CinematicSequencerBase y PlayVictorySequence; `Stunned` lo usan AerialKnockback y PlayerCarrySystem. Escenarios reales: victoria de combate con diálogo abierto → input desbloqueado en mitad del diálogo; diálogo abierto durante cinemática → jugador controlable en mitad de la cinemática.

**Fix:** refcount por modo, o permitir entradas repetidas en la pila (quitar el early-return; el Pop ya elimina solo una instancia).

##### C3. Teleport a anchor inexistente → jugador sin input permanentemente — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Teleport/TeleportSystem.cs:211` + `Assets/Scripts/World/TeleportService.cs:99-116` *(verificado)*

`TeleportSequence` empuja `Cutscene`, deshabilita el input y espera `WaitUntil(() => transitionEnded)`, que depende de `OnTeleportEnded`. Pero `TeleportService.TeleportToAnchor` retorna temprano **sin emitir ningún evento** si `Inst` es null o el anchor no se encuentra (solo un LogWarning). Resultado: input muerto, fase Cutscene y `IsTeleporting=true` para siempre (bloquea además todos los teleports futuros).

**Fix:** emitir siempre `OnTeleportEnded` en los paths de fallo, más un timeout de seguridad en el `WaitUntil`.

##### C4. `NarrativeGraphStarter` restaura blackboards rancios en cada carga de escena → ítems duplicados (patrón INC-020) — CORREGIDO (ver §19.4.2)
`Assets/NarrativeGraph/Runtime/Integration/NarrativeGraphStarter.cs:98,159`

Restaura `preset.narrativeBlackboards` cada vez que una escena de gameplay se activa, pero ese snapshot solo se refresca al guardar en un SavePoint (`GameBootProfile.cs:715` ← `SavePoint.cs:168`). Secuencia normal: guardas → avanzas el grafo (recibes ítem vía `GiveInventoryItemNode`) → cambias de escena sin guardar → el blackboard retrocede al save: el flag `INV_GIVEN` desaparece pero el inventario no se revierte → **el nodo vuelve a entregar el ítem**. Diálogos sin `oneShotFlag` se repiten y el grafo se desincroniza del QuestManager.

**Fix:** capturar blackboards al preset en cada transición de escena, o restaurar solo una vez por sesión (tras load real), no en cada `Start()`.

##### C5. Interrumpir la cinemática de un NPC → corrutina zombie y secuenciadores colgados — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Behaviour NPC/States/CinematicState.cs:562` + `NPCBehaviourManagerV2.cs:655-659` *(verificado)*

`Cleanup()` (salida forzada del estado) detiene la corrutina y restaura avoidance, pero **no marca `IsCompleted = true`** (solo `CleanupAndComplete` lo hace). `WaitForSequence` hace `while (!seq.IsCompleted) yield return null;` → si el NPC sale de `CinematicState` a mitad de secuencia, esa espera gira para siempre y el `onComplete` no dispara. Y hay una vía fácil de provocarlo: `NPCCombatLifecycleHandler.OnDamaged` llama `ForceEnterCombat` **sin comprobar `IsInCinematic`** — golpear a un NPC durante una cinemática cuelga los secuenciadores que encadenan pasos vía `onComplete` (MountainSequencer, ReinoExitBanterSequencer). Relacionado: `CheckTransitions` de CinematicState tampoco mira `WasDefeatedInCombat` → NPC que muere en cinemática queda atrapado en el estado.

**Fix:** `IsCompleted = true` en `Cleanup()`, gate de `IsInCinematic` en `OnDamaged`/`ForceEnterCombat`, y prioridad `WasDefeatedInCombat → DeadState` en `CheckTransitions`.

##### C6. Hitstops solapados dejan el juego en cámara lenta permanente — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Core/Feedback/SimpleHitStopProvider.cs:18-29`

Cada `Co_HitStop` captura `original = Time.timeScale` al empezar y lo restaura al acabar, sin cancelar el anterior. Dos golpes en <0.2 s (trivial en combate): A captura 1.0, B captura el 0.1 que puso A → A restaura 1.0, B restaura 0.1 → **slow-mo permanente**. Además pelea con el menú de pausa y con `DeathCameraEffect` (que fuerza `timeScale = 1` incondicional al final, rompiendo una pausa activa). Misma familia: `NPCCombatLifecycleHandler.OnDestroy` fuerza `timeScale = 1` si no es 1 — descargar una escena con NPCs estando en pausa revierte la pausa.

**Fix:** árbitro central de timeScale (contador de efectos + baseline gestionado). Un solo servicio resuelve los 4 actores.

##### C7. Knockback aéreo interrumpido → input bloqueado para siempre — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Attacks/AerialKnockbackReceiver.cs:147-289`

`LaunchRoutine` empuja `Stunned`, deshabilita el controller y pone el Rigidbody kinemático; la restauración está al final de la corrutina y **no hay `OnDisable`**. Si el componente se desactiva a mitad del arco (~0.6 s) — cinemática con `ModeRule.disableComponents`, muerte, cambio de escena — quedan: `Stunned` pushed para siempre, controller deshabilitado, RB sin gravedad y `_isLaunching=true` (bloquea futuros knockbacks). El propio proyecto tiene el patrón correcto en `PlayerFlyingController.OnDisable` y `PlayerSwimmingController.OnDisable`.

**Fix:** `OnDisable` que restaure RB/controller/rootMotion y haga `PopMode(Stunned)`.

---

#### ALTOS — rompen sistemas concretos o corrompen estado en escenarios alcanzables

##### A1. `ActiveCombatRegistry` retiene enemigos destruidos → player atrapado en modo combate — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Attacks/ActiveCombatRegistry.cs:164` + `Player/PlayerBattleModeController.cs:311`

`Count` no limpia referencias fake-null. Un enemigo destruido sin `UnregisterNPC` (Destroy directo, descarga de escena aditiva — `ClearAll` solo se llama en GameOver) deja `Count>0` para siempre → Battle Mode + `ActionMode.Combat` permanentes (que además bloquea `Interact`). `InteractionDetector` ya se defiende con `CleanupDestroyedNPCs()`; los otros dos consumidores no. **Fix:** limpieza dentro de `Count` o auto-unregister en `OnDestroy` del NPC.

##### A2. `BossArenaController`: arena cerrada sin salida si el boss se destruye sin morir — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Rooms/BossArenaController.cs:585-591`

Si el boss desaparece sin pasar por `Damageable.OnDied` (killzone, despawn, limpieza externa), el path de emergencia solo hace `started=false`: no reabre puertas, no llama `UnlockArea()` ni `RestoreBattleDisables()`, ni cierra la música de batalla → jugador encerrado con música infinita y sin posibilidad de re-disparar el trigger. **Fix:** en ese path, restaurar puertas/área/disables y `AudioService.EndBattleById`.

##### A3. Pooling: devolución doble corrompe el pool y los parents destruidos lo agotan — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Core/Pooling/ObjectPool.cs:114-121` *(verificado)* + `VfxPoolService.cs:74-119`

`Return()` detecta la devolución doble pero **aun así hace push** → la misma instancia dos veces en la pila → dos `Get()` devuelven el mismo Transform. Y `VfxPoolService.Play` con `parent` externo: si el parent se destruye, el VFX muere con él pero `_inUse` del ObjectPool lo cuenta para siempre → tras `MaxPoolSizePerPrefab` (64) instancias muertas, ese VFX **deja de verse el resto de la sesión**. **Fix:** `if (!_inUse.Remove(obj)) return;` y, en la rama `instance == null` del Update del servicio, purgar también `_instancePool`/`_inUse`.

##### A4. Save corrupto arranca el juego en estado indefinido — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Core/GameBootService.cs:280` *(verificado)*

En el arranque normal, `_profile.LoadProfile(_saveSystem)` **ignora el valor de retorno**. Si el JSON está corrupto (cierre forzado a mitad de escritura), `LoadProfile` devuelve false y no hay fallback al `defaultPlayerPreset`: el juego arranca con el runtimePreset residual, sin HP/inventario/flags coherentes. **Fix (2 líneas):** `if (!_profile.LoadProfile(_saveSystem))` → rama del preset por defecto.

Relacionado (MEDIO): `SaveSystem.Save` hace `File.Delete` + `File.Move` *(verificado)* — hay una ventana sin ningún save en disco; usar `File.Replace`, o leer `save.json.tmp` como fallback en `Load()`. Y `PlayerSaveData` no tiene campo de versión de esquema: cualquier renombrado de campo hará que saves antiguos carguen en silencio con defaults. Añadir `saveVersion` antes de la demo de Steam.

##### A5. Señales narrativas sticky consumidas por el sistema equivocado — CORREGIDO (ver §19.4.2)
`Assets/NarrativeGraph/Runtime/Integration/DefaultNarrativeSignals.cs:350-361` + `NPCInteractiveNarrativeExecutor.cs:342-349`

`OnCustom` consume `_pending`/`_raised` en el momento de suscribirse, y el executor Interactive se re-suscribe durante la carga **antes** de que los runners restauren blackboards y suscriban sus `WaitCustomEventNode`. Una señal pendiente puede ser consumida por el executor (que luego la ignora por `singleUse`/preset) → el `WaitCustomEventNode` del grafo nunca la ve → grafo bloqueado. Es la versión runtime del conflicto que el `CrossSystemNarrativeValidator` solo detecta en editor. **Fix:** consumo por-suscriptor, o que el executor re-emita la señal cuando decide ignorarla.

##### A6. Ramas fork del grafo: `Exit()` nunca se llama y el estado de suscripción vive en el asset compartido — CORREGIDO (ver §19.4.2)
`Assets/NarrativeGraph/Runtime/Graph/NarrativeRunner.cs:327-457`

Las ramas fork hacen `Enter` de cada nodo pero jamás `Exit`; `StopExecution()` solo hace `Exit` del nodo del camino principal. Nodos en espera dentro de ramas (`WaitQuestCompleteNode._cb`, `StartBattleNode._onBattleWonCb`) quedan suscritos tras `StopAllRunners`/recarga — y esos campos viven en el `NarrativeNode` serializado del asset compartido, así que una re-entrada pisa `_cb` y el `Exit` posterior ya no puede desuscribir el callback viejo → **callbacks fantasma de sesiones muertas ejecutando side effects reales** (completar quests al ganar una batalla de la sesión nueva). **Fix:** rastrear los nodos activos por rama y hacer su `Exit` en `StopExecution`; mover el estado de suscripción a un diccionario por runner.

Relacionados en el mismo archivo: el resume de forks re-ejecuta el `Enter` del nodo fork en cada carga (si es `RaiseCustomEventNode`, re-emite la señal en cada load); y `RequireInventoryItemNode.HandleMissing` usa `ForceJumpToOutput` (mecanismo del camino principal) desde ramas → rama nunca marcada `__DONE__` y `__currentNodeGuid` corrupto; además con `consumeOnSuccess` + `completeQuestInstead` el ítem puede consumirse dos veces (el guard `_itemsConsumedForQuest` no cubre el consumo hecho por el nodo).

##### A7. `Transition.cs`: fuga de `sceneLoaded` + disparo prematuro con cargas aditivas — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Core/EasyTransitions/Scripts/Transition.cs:99`

Suscribe `SceneManager.sceneLoaded` y no existe `OnDestroy` que desuscriba (el objeto muere con `Destroy(gameObject, destroyTime)`). En un proyecto multi-escena **aditiva**, además, cualquier carga aditiva durante la espera dispara `OnSceneLoad` prematuramente (no filtra por `LoadSceneMode`). **Fix:** `OnDestroy` desuscribiendo + ignorar `mode == Additive`. En la misma familia: `TeleportService.cs:226` y `CinematicSequencerBase.cs:266-279` dejan handlers de `onTransitionCutPointReached` suscritos al TransitionManager persistente si la transición se interrumpe → la siguiente transición de cualquier sistema puede teleportar al jugador al destino antiguo o ejecutar `BeginCinematic()` de un sequencer destruido. Desuscribir en `OnDestroy`/finally.

##### A8. `DayNightCycle`: oscurecimiento por lluvia compuesto exponencialmente y luz clavada tras la lluvia — CORREGIDO (ver §19.4.2)
`Assets/Scripts/World/DayNightCycle.cs:379-386` *(verificado)*

`LateUpdate` lee `directionalLight.intensity` (ya oscurecida el frame anterior) y la vuelve a multiplicar cada frame — exactamente el bug que ya se corrigió para la niebla con `_baseFogDensity` (el comentario de las líneas 248-253 lo documenta), pero sin aplicar a la luz. En ~4 frames la luz cae al suelo (0.28) y al terminar la lluvia **se queda ahí** hasta la siguiente transición de periodo. **Fix:** cachear `_baseLightIntensity` igual que la niebla.

##### A9. `SimpleCinematicDirector`: estado global compartido entre instancias — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Cinematics/SimpleCinematicDirector.cs:214-240`

`OnDisable`/`OnDestroy` deciden con el flag **estático** `IsAnyCinematicPlaying`: si el director A reproduce y un director B (que nunca reprodujo) se desactiva por descarga de escena, B resetea el flag global, fuerza `timeScale=1` y cierra el override de A. La limpieza de interrupción además no restaura HUD/minimapa ni prioridad de cámara. Y `PlayRoutine` no está blindada con try/finally (a diferencia de `CinematicSequencerBase.Co_SequenceGuarded`, que sí lo está): una NRE deja flag global, HUD y cámara en estado de cinemática. El campo `lockPlayer` no se usa en ninguna parte. **Fix:** flag de instancia, rutina de restauración completa, y el patrón guarded de la clase base.

##### A10. Muerte y revive del player sin limpiar contexto — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Player/PlayerHealthSystem.cs:182-225, 363-408, 501-513`

`TakeDamage`/`Die` no comprueban Cinematic (un AoE residual puede matar al player en mitad de una cinemática y disparar el GameOver dentro de ella); `Die()`/`ReviveInternal` no tocan la pila de modos (morir con `Flying`/`Carrying` pushed los deja vivos de cara al respawn) ni conceden invulnerabilidad temporal al revivir. Y `InvulnerabilityFlashCoroutine` apaga renderers: si el GO se desactiva en el medio ciclo apagado, **el player queda invisible permanente** (nadie llama a `ResetDamageVisuals` al reactivar). **Fix:** god-frame en Cinematic, reset de pila en muerte/revive, `OnDisable → ResetDamageVisuals()`.

##### A11. Bosses: pasada de higiene propia — CORREGIDO (ver §19.4.2)
- `GolemBossAI.cs` — muerte a mitad de salto/embestida deja cadáver flotando (agente desactivado que `StopAgent` no puede parar) y `animator.speed` en 1.8; onda expansiva con `OverlapSphereNonAlloc` **sin layermask** y buffer de 32 en un mundo donde todo vive en `Default` → en zonas densas el player puede quedar fuera del buffer y no recibir daño; reflection en runtime (`GetMethod("Shake")`) cuando el propio archivo ya usa `FeedbackService.CameraShake`; `SetDestination` por frame en embestida.
- `ImpDemonAI.cs` — `PlayAnimation` hace `animator.Play(hash, layer, 0f)` **cada frame** sin guard → reinicia la animación en el frame 0 continuamente (animación congelada + coste). `Spider1AI.cs:387` tiene el guard correcto: portarlo. VFX de casteo/lluvia instanciados sin `Destroy` programado ni pool.
- `Spider1AI.cs` — `StopCoroutineSafe(AttackPlayer())` crea un enumerator nuevo y el helper está vacío: la "cancelación" no cancela nada; el daño se aplica aunque la araña esté en stun. `SetDestination` cada frame en persecución (y las arañas atacan en grupo).

##### A12. Swap de personaje sin gating por estado — CORREGIDO (ver §19.4.2)
`Assets/Scripts/Player/PartyControlManager.cs:102-119`

`HandleInput` solo comprueba `IsInUIMode`: se puede hacer swap en pleno vuelo/nado/carry/knockback, aplicando el controller a un personaje que quizá no tiene esa habilidad, con los modos aún en la pila. **Fix:** rechazar swap salvo `Top == Default || Combat`.

---

#### MEDIOS — deuda que conviene saldar, sin urgencia de hotfix

**M1. `_questChainIndex` depende de qué escena esté cargada** (`QuestManager.cs:947-963`): solo indexa NPCs activos de escenas cargadas. Si una quest se completa donde su NPC dueño no está cargado, no se consumen ítems y el autocompletado se omite en silencio; el paso 5 de `RestoreFromProfileFlags` en boot sufre lo mismo. Mover los `QuestChainEntry` a un catálogo SO independiente de escena.

**M2. `RestoreFromProfileFlags` dispara post-actions durante la carga** (`QuestManager.cs:642-647`): `CompleteQuest` emite `OnQuestCompleted` síncrono en plena restauración → los ejecutores lanzan diálogos/moves de NPC durante la carga. Añadir flag "restaurando".

**M3. `NPCQuestActionExecutor`** (`:60-72, 239/377`): sin reintento de suscripción si `QuestManager.Instance` era null en `Start` (queda sordo toda la sesión), y `_isExecutingPostAction` nunca se limpia si el GO se desactiva a mitad → todas las post-actions futuras ignoradas. Reintento + limpiar en `OnDisable`.

**M4. `AudioService`** — `ReturnWhenDone` usa `WaitForSeconds` **escalado**: en pausa (`timeScale=0`) las fuentes SFX no vuelven al pool y `Rent2D` crea `SFX2D_dyn` sin límite mientras dure la pausa; además aloca un `WaitForSeconds` nuevo por SFX (hot path: cada pisada). Usar `WaitForSecondsRealtime` o devolución por `!isPlaying` en un update centralizado. Y el fade-out de `StopLoopingSFX` no se cancela al rearrancar el mismo `loopId` → el loop nuevo se corta en seco cuando el fade viejo termina.

**M5. `FeedbackService.ScreenFade`** lanza corrutinas sin cancelar la anterior (N llamadas = N corrutinas escribiendo el mismo `img.color`) → pantalla que puede quedarse en negro según cuál termine última. Igual el shake de `TransformPivotCameraShakeProvider`: dos shakes solapados dejan offset residual permanente en el pivot de cámara.

**M6. `BossProgressPersistenceBridge` + test mode**: el bridge se desuscribe de `OnProfileReady` tras la primera aplicación; `ReloadTestPreset` re-invoca el evento confiando en él → tras derrotar un boss y recargar el preset de testeo, el boss sigue derrotado. Afecta solo al flujo de testeo, pero es justo el flujo que usas a diario.

**M7. `InteractionDetector.FindNearest`** (`:283`): el SphereCast de línea de visión acepta al candidato si golpea *cualquier cosa* (incluida una pared entre medias → interactuar a través de muros) y lo rechaza si no golpea nada (interactuable solo-trigger en espacio abierto nunca seleccionable). Además corre cada frame sin throttle (PlayerTargeting ya usa 10 Hz: aplicar lo mismo).

**M8. `MagicProjectil`**: `_cfg.hitLayers`/`collisionLayers` se leen del SO pero **nunca se usan** — el AoE golpea a cualquier `Damageable` en el radio, aliados del party incluidos; detección de arenas por `name.Contains("Arena")` (frágil + allocs por trigger). Y `MagicProjectileSpawner.Co_SpawnWithCharge`: si el spawner se desactiva durante la carga, el orbe queda pegado a la mano para siempre si `lifeTime==0`.

**M9. Estáticos sin `ResetStatics`** (violación del patrón obligatorio §3) — lista consolidada y verificada: `TeleportService` (instancia + 3 eventos + `_sTransitionInProgress`), `BossArenaController` (`s_arenaRegistry` + 2 eventos), `LevitationTarget` (2 eventos), `FeedbackService` (instancia + 5 providers), `PlayerService` (2 eventos), `PlayerSettings` (4 eventos), `MagicProjectileSpawner` (`OnPlayerAttacked`), `ProfileReadyDiagnostics` (todo su estado), `EnvironmentController` (su ResetStatics existe pero no limpia `OnInteriorEntered/Exited`), `AudioService.MuteNextBaseSceneMusic`. Solo afecta al editor sin domain reload, pero es exactamente el entorno en el que trabajas.

**M10. UI**: `MenuNavigator` hace `GetComponent<Button>` por frame y, si nada es seleccionable, `GetComponentsInChildren + Array.Sort` **cada frame**; `EquipmentView.SetVisible(false)` no desuscribe `OnWardrobeChanged` (asimetría con InventoryView, que sí lo hace) → refrescos fantasma de un canvas oculto; `TagMinigameController` asigna `timerText.text` interpolado cada frame (cachear el segundo entero).

**M11. `EnvironmentController`** suscribe `sceneLoaded`/`activeSceneChanged` con **lambdas anónimas** sin `OnDestroy` — imposibles de desuscribir jamás. `LevelExit` carga en modo **Single** (contra la arquitectura aditiva) y sin guard de re-entrada → doble carga posible. `WarmLightFlicker`: interrumpir el fade puede perder el parpadeo para siempre y su `Update` escribe la luz cada frame incluso sin flicker.

**M12. Vigilancia anti-movimiento duplicada en Idle**: `IdleState` fuerza `isStopped/ResetPath` cada frame y `NPCBehaviourManagerV2.LateUpdate` repite el mismo chequeo con comparación de **string** (`StateName == "Idle"`) por NPC y frame. Unificar en una sola vigilancia por intervalo y comparar por tipo.

**M13. Logging sin guardas en rutas calientes** — los focos gordos, por coste real en build: `NPCCombatBrain` (107 logs, 3 guardados), `NPCCombatLifecycleHandler` (103, cero), `GolemBossAI` (log por collider evaluado en cada golpe/onda), `ActiveCombatRegistry.GetClosestCombatNPC` (por-NPC, llamado cada 0.5 s todo el combate), `Damageable` (log en el path de daño ignorado — por frame en AoE), `QuestManager.OnPartyMemberJoined` (~25 por evento), `DefaultNarrativeSignals.RaiseCustom` (además hace `GetInvocationList()` — alloc — solo para el log), `BossArenaController.OnTriggerEnter` (cada collider en build), toda la secuencia de victoria de `PlayerBattleModeController`. Envolver en `#if UNITY_EDITOR || DEVELOPMENT_BUILD` (regla §2 propia).

---

#### BAJOS — apuntados para cuando toque pasar por ahí

`GamepadInputReader`: `InputSystem.onAfterUpdate += PollHardwareFallback` nunca se desuscribe (se acumula un registro por sesión de PlayMode) y `_controls` cacheado podría apuntar a un asset dispuesto si PlayerInputManager se recrea · `PlayerHealthSystem.cs:172`: `new Material(renderer.material)` duplica la instancia y ninguna se destruye (fuga por respawn) · `TransitionManager`: si un suscriptor del cut point lanza excepción, `runningTransition` queda en true para siempre (todas las transiciones futuras ignoradas) y su `Start()` es un poll infinito inútil cada 1 s · `PersistOnLoad`: singleton por **clase** — el segundo GameObject distinto con el componente se autodestruye en silencio · `ProjectilePoolManager.cs`: archivo vacío (0 bytes) — eliminar · `PlayerService` declara `[DefaultExecutionOrder(-600)]` pero CLAUDE.md documenta -900 — alinear · `ServiceLocator.TryGet` de un servicio ausente hace `FindAnyObjectByType` en cada llamada sin caché negativa — peligroso si alguien lo sondea por frame · `PlayerSettings.SaveToDisk` escribe a disco síncronamente en cada notch del slider de volumen · Executor Interactive: dos `ConditionalNarrative` del mismo NPC con la misma `customEventKey` → solo la primera recibe el evento (trampa de datos que el validador no cubre; sistema congelado, solo documentarlo) · `NarrativeGraphHub.RestoreBlackboards` no limpia runners sin snapshot (un grafo no empezado en el save conserva el progreso de la sesión anterior en memoria) y `RelaunchForkBranches` con GUID desaparecido (grafo editado tras el save) mata la rama en silencio en vez de relanzarla desde `branchStartGuid` · `Assets/t2.txt` y `Assets/test_delete_me.txt` — basura de pruebas en el raíz de Assets.

---

#### Lo que está bien (y merece decirse)

La gestión de corrutinas de música de `AudioService` (referencias explícitas, INC-056 documentado en código) está muy cuidada. `VfxPoolService` con un único Update centralizado es el patrón correcto. `PlayerInputManager` resuelve con elegancia un problema real del Input System (cambios de mapa diferidos a `onAfterUpdate`). `CinematicSequencerBase.Co_SequenceGuarded`, `TagMinigameController`, `Inventory/Shop` y `CloudCoverSpawner` están bien blindados. La FSM de NPCs (Brain/Context/States) es sólida, con throttling y NonAlloc bien aplicados. Los registros de NPCs (registro/desregistro en escenas aditivas) están correctos. Y los invariantes narrativos de §4 se cumplen íntegramente.

El patrón general es claro: la infraestructura reciente es de buena calidad; los sistemas más antiguos (TeleportService/System, BossArenaController, SimpleCinematicDirector, DayNightCycle, los tres bosses) arrastran los mismos problemas que el proyecto ya identificó y corrigió en otros sitios. No hace falta rediseñar nada: hace falta llevar los patrones buenos que ya existen a los archivos que se quedaron atrás.

#### Orden de ataque sugerido

1. **C2 (PushMode refcount)** — el fix más rentable: una tarde, elimina una familia entera de conflictos entre diálogo, cinemáticas, victoria y stun.
2. **C1 (reentrada DialogueManager)** — el soft-lock narrativo más probable en juego normal.
3. **C4 (blackboards rancios)** — duplicación de ítems con la secuencia guardar→avanzar→cambiar de escena; directo contra la demo.
4. **C6 + M4-parcial (árbitro de timeScale)** — un servicio pequeño, cierra 4 bugs.
5. **C3, C5, C7, A1, A2** — los "jugador/NPC bloqueado"; todos son fixes locales de pocas líneas.
6. **A4 (save corrupto, 2 líneas) + versionado del save** — antes de que haya saves de jugadores reales.
7. La pasada de logging (M13) y los bosses (A11) cuando toque optimizar la build.

### 19.2 Auditoría completa de entregabilidad — 8 de agosto de 2026

**Fecha:** 8 de agosto de 2026 · **Autor:** Claude (Cowork), a petición de Raúl · **Objetivo:** valorar si el proyecto está en condiciones de ser mostrado a un estudio/publisher o de salir como demo pública, y qué falta para ese nivel.

**Método:** esta auditoría no repite desde cero el trabajo de código ya hecho ayer (`AUDITORIA_CODIGO_2026-08-07.md` y `AUDITORIA_SISTEMAS_OBSOLETOS_2026-08-07.md`, 530 archivos revisados) — lo verifica puntualmente y lo integra. El foco de hoy es todo lo que esas dos auditorías **no** cubren: testing/QA, ajustes de rendimiento y FPS a nivel de Project Settings, preparación de build para tienda, higiene de repositorio y paquetes. Verifiqué en vivo contra el código y los `.asset` reales: `ProjectSettings.asset`, `QualitySettings.asset`, `GraphicsSettings.asset`, `DynamicsManager.asset`, `Packages/manifest.json`, `.gitignore`/`.gitattributes`, `EditorBuildSettings.asset`, `git log`, y releí `DialogueManager.cs`/`PlayerActionManager.cs` línea a línea para confirmar que dos de los bugs "críticos" de ayer siguen presentes hoy.

---

#### 0. Veredicto general

El código en sí está en **muy buen estado para un proyecto indie en solitario** — la auditoría de ayer ya lo dice y hoy lo confirmo: cero `OverlapSphere` sin `NonAlloc`, buffers cacheados, FSM sólida, pooling de VFX centralizado, sistema de guardado con escritura atómica. Eso no es lo que separa este proyecto de "nivel estudio" ahora mismo.

Lo que sí lo separa son tres cosas que no son bugs de código sino **ausencias de proceso**, y son las que una empresa grande mira primero:

1. **Cero tests automatizados.** El proyecto tiene instalados `com.unity.test-framework`, `test-framework.performance` y `testtools.codecoverage` en `manifest.json` — pero no existe ni un solo archivo de test (`*Test*.cs`, `.asmdef` de tests) en todo `Assets/`. Los paquetes están puestos pero nunca usados.
2. **El identificador de build sigue siendo el del template de Unity.** `applicationIdentifier` es literalmente `com.Unity-Technologies.com.unity.template.urp-blank` (Standalone/iOS) y `com.UnityTechnologies.com.unity.template.urp-blank` (Android). `projectName: Test`. Esto no es un detalle: si mañana se sube un build a Steam o a cualquier tienda tal cual está, sale con la identidad del blank template de Unity, no la del juego.
3. **No hay ningún proceso de verificación automatizado** (CI, build check, ni siquiera un test runner) — ni falta hace decirlo, dado el punto 1, pero es la razón estructural por la que las regresiones se detectan jugando manualmente y no antes de tocar código.

Ninguna de las tres es difícil de arreglar. Ninguna requiere rediseñar nada. Pero las tres son exactamente lo que un revisor externo (publisher, QA de una empresa grande, o un port house) señalaría en los primeros 10 minutos, antes incluso de mirar una línea de gameplay.

Por debajo de esto, el resto del proyecto — arquitectura, rendimiento por frame, higiene de Git — está genuinamente bien y no necesita una "limpieza de choque", solo rematar lo que ya está empezado.

---

#### 1. Bloqueadores reales para "entregable" (arreglar antes que nada)

##### 1.1 Identidad del proyecto sin configurar — **bloqueante para cualquier build público**
`ProjectSettings/ProjectSettings.asset`

```
applicationIdentifier:
  Android: com.UnityTechnologies.com.unity.template.urp-blank
  Standalone: com.Unity-Technologies.com.unity.template.urp-blank
  iPhone: com.Unity-Technologies.com.unity.template.urp-blank
projectName: Test
organizationId: luarbaz
templateDefaultScene: Assets/Scenes/SampleScene.unity   (vestigio, no afecta al build real)
metroPackageName: Test
metroApplicationDescription: Test
```

El proyecto nace de `com.unity.template.urp-blank` (`clonedFromGUID` + `templatePackageId` lo confirman) y esos campos nunca se tocaron. `companyName: Liyodev` y `productName: El Sendero de las Estrellas` sí están bien puestos — son los que se ven en la ventana del juego y en el `.exe` — pero el `applicationIdentifier` (bundle ID) es el que usan Steam, Google Play y Apple para identificar la app de forma única, y ahora mismo apunta al paquete de ejemplo de Unity. Si se sube así, choca de bruces con cualquier control de calidad de tienda.

**Fix:** Project Settings → Player → Other Settings → Identification. Poner algo tipo `com.liyodev.elsenderodelasestrellas` en las tres plataformas, y `projectName` a un nombre real. Es un cambio de 2 minutos, cero riesgo — pero es el que más "vergüenza empresa grande" causaría de los tres si se pasa por alto.

##### 1.2 Cero tests automatizados — infraestructura instalada, cero uso
`Packages/manifest.json` tiene:
```
"com.unity.test-framework": "1.7.0",
"com.unity.test-framework.performance": "3.5.0",
"com.unity.testtools.codecoverage": "1.3.0",
```
Búsqueda exhaustiva en los 445 `.cs` de `Assets/Scripts` (más `NarrativeGraph`, `Editor`): **cero** archivos de test, **cero** `.asmdef` de tipo Tests (de hecho cero `.asmdef` en todo el proyecto — ver §3). `playModeTestRunnerEnabled: 0` en Player Settings. `TDD.md`, el documento que el propio proyecto declara "fuente de verdad", no tiene ninguna sección de testing/QA en su índice (14 secciones: arquitectura, NPCs, jugador, sistemas core, audio, quests, diálogos, guardado, narrativa, UI, rendimiento, bugs, troubleshooting — ninguna de QA).

Esto no significa que el juego no se pruebe — claramente se prueba mucho a mano (los presets de `_BootProfile`, F3/F4 de debug, las escenas de `Test/` lo demuestran) — pero **cero de esa cobertura sobrevive de una sesión a otra**. Cualquier regresión se descubre jugando, no en segundos al guardar un archivo.

Dado que el árbitro de rendimiento y los bugs de ciclo de vida de corrutinas de la auditoría de ayer (C1–C7) son exactamente el tipo de bug que un test de EditMode/PlayMode atraparía en segundos (p. ej. "abrir dos diálogos seguidos no debe perder el callback del primero"), esto no es un "nice to have" de proceso — es la razón concreta por la que esa familia de bugs lleva tiempo sin detectarse.

**Recomendación realista (no "hacer TDD retroactivo de todo el juego", que no es viable para un dev en solitario):**
- Empezar por 5-8 tests de EditMode que cubran los invariantes ya documentados como "no negociables" en CLAUDE.md §4 (los del grafo narrativo) — son los que más cuestan un bug de producción si se rompen.
- Un test de PlayMode que reproduzca exactamente el escenario de C1 (`DialogueManager.StartDialogue` reentrante) y C2 (`PushMode` sin refcount) de la auditoría de ayer — ambos siguen presentes hoy (ver §2), y son perfectos como primer par de tests porque el bug y el fix ya están identificados.
- Activar `com.unity.testtools.codecoverage` en el runner para tener una cifra objetiva de cobertura, aunque empiece en un dígito bajo.

##### 1.3 Sin CI ni verificación automática de build
No hay carpeta `.github/` (ni ningún otro CI) en el repo. Ningún hook de pre-commit corre tests o valida compilación. El validador narrativo (`CrossSystemNarrativeValidator`, mencionado en CLAUDE.md §7) existe pero es una `MenuItem` manual del editor, no algo que corra solo. Para un proyecto en solitario esto es razonable hoy, pero es lo primero que pediría un equipo de QA externo antes de aceptar builds regulares: al menos un job que compile el proyecto (o corra las escenas de test) en cada push.

---

#### 2. Estado real de los críticos de la auditoría de ayer (verificado hoy, no asumido)

Releí `DialogueManager.cs` y `PlayerActionManager.cs` línea a línea contra el código actual (no contra la copia de ayer):

- **C1 (reentrada en `DialogueManager.StartDialogue`) — CONFIRMADO, sigue abierto hoy.** Línea 319-326: `StartDialogue` sobrescribe `_current`/`_onEnd` sin comprobar `IsOpen` (línea 92) ni invocar el `_onEnd` anterior. El fix propuesto ayer sigue siendo válido y no se ha aplicado.
- **C2 (`PushMode` sin refcount) — CONFIRMADO, sigue abierto hoy.** Línea 251: `if (Top == mode) return;` sigue ahí exactamente como se describió. `PopMode` (línea 259-274) sigue quitando una entrada del stack aunque el segundo `Push` haya sido ignorado por el early-return.

No relanzo el resto de la lista de ayer (sería redundante con `AUDITORIA_CODIGO_2026-08-07.md`, que ya la tiene priorizada con el "orden de ataque" al final). El dato importante es que esta auditoría se apoya en hallazgos reales de hace 24h, no en una foto vieja — y que el fix de más impacto (C2, refcount de `PushMode`) sigue siendo la tarea más rentable: una tarde de trabajo, elimina de un plumazo los conflictos diálogo↔cinemática↔victoria↔stun.

---

#### 3. Rendimiento y FPS — lo que no cubrió la auditoría de código

La auditoría de ayer ya cubrió el rendimiento *por frame* (Update/FixedUpdate, NonAlloc, GC). Esto es lo que falta a nivel de configuración de proyecto:

**Sin arquitectura de compilación (`.asmdef`).** Cero archivos `.asmdef` en todo el proyecto — 445 scripts compilan como un único `Assembly-CSharp` monolítico. No afecta al rendimiento en runtime, pero sí a la velocidad de iteración (cualquier cambio en un script recompila los 445) y es una práctica estándar en proyectos "AAA" que aquí falta por completo. Dividir al menos `Core`/`Runtime` de `Editor`/`Tests` sería el primer paso — y además es un prerrequisito real para poder tener tests de EditMode aislados (§1.2).

**Layer Collision Matrix sin configurar — `DynamicsManager.asset`:**
```
m_LayerCollisionMatrix: ffffffff... (todo colisiona con todo, el default de Unity)
```
El proyecto tiene capas dedicadas (`Enemy`, `Player`, `Projectile`, `ProjectileEnemy`, `Interactable`, `Floor`, `Obstacle`, `Climb`, `UI`, `Water`...) pero la matriz de colisión física nunca se personalizó: sigue siendo la que trae Unity por defecto, donde absolutamente todas las capas colisionan entre sí. Esto es tanto un tema de rendimiento (el motor de físicas evalúa pares de colliders que nunca deberían tocarse, p. ej. `UI` contra `Floor`) como de corrección — es la misma raíz del problema que CLAUDE.md ya documenta a mano ("los personajes no tienen capa propia, viven en `Default`"): el proyecto compensa con chequeos por componente (`NPCSimpleAnimator`) en tiempo de ejecución un problema que la matriz de colisión debería filtrar en el motor de físicas, gratis y antes de que el código de gameplay se entere. **Recomendación:** dar a los personajes una capa `Character` propia y configurar la matriz (p. ej. `Projectile` no debería colisionar con `ProjectileEnemy`, `UI` no debería colisionar con nada físico).

**Anti-aliasing desactivado en ambas calidades — `QualitySettings.asset`:**
```
name: Mobile → antiAliasing: 0
name: PC     → antiAliasing: 0
```
Con URP y MSAA/TAA disponibles, un proyecto que aspira a mostrarse con pulido visual "AAA" normalmente al menos ofrece una opción de AA en la calidad PC. Ahora mismo ninguna de las dos calidades la trae por defecto — puede ser intencional (rendimiento en gama baja), pero vale la pena una decisión explícita en vez de heredarlo del blank template.

**`vSyncCount: 0` en ambas calidades.** No hay VSync por defecto en ningún tier — coherente con dejar el framerate sin techo en manos de `Application.targetFrameRate` en código (no verificado aquí a nivel de script; merece una comprobación rápida de que existe un cap explícito, porque sin VSync ni target framerate el juego corre a la FPS que dé la GPU, con el consumo/calentamiento que eso implica en un build de demo).

**Puntos positivos que sí valen la pena mencionar (no solo hay que apuntar problemas):** `gcIncremental: 1` (GC incremental activado — reduce los picos de frame por recolección de basura, justo lo que un juego de acción/combate necesita), `m_MTRendering: 1` (renderizado multihilo activo), `m_BuildTargetBatching` con `m_StaticBatching: 1` para Standalone, y URP con SRP configurado correctamente (`m_CustomRenderPipeline` apuntando al asset URP en ambas calidades). La base de configuración de rendimiento está bien elegida donde importa; lo que falta es terminar de personalizar lo que aún trae el valor por defecto del template.

**Logging en rutas calientes (ya señalado ayer, lo confirmo como el hallazgo de rendimiento más rentable de arreglar antes de un build de demo):** `NPCCombatBrain` (107 logs, 3 guardados con `#if`), `NPCCombatLifecycleHandler` (103, cero guardados) son los focos gordos. Es una regla que el propio CLAUDE.md §2 ya exige ("todo `Debug.Log` bajo `#if UNITY_EDITOR || DEVELOPMENT_BUILD`") y que en estos dos archivos no se cumple — coste real en build de combates con varios NPCs.

---

#### 4. Higiene de proyecto y repositorio

**Git — en buen estado, sin acción urgente.** `.gitignore` cubre correctamente `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, builds y archivos temporales de análisis; ningún artefacto pesado colándose en el repo hoy (los `t2.txt`/`test_delete_me.txt` que señalaba la auditoría de sistemas obsoletos de ayer ya no están en `Assets/`, así que o se limpiaron o el hallazgo ya está resuelto). `.gitattributes` configura Git LFS correctamente para texturas, modelos, audio y vídeo, con normalización de line endings a LF y el merge driver de Unity YAML documentado. El historial de commits es activo y descriptivo (mezcla español/inglés, pero mensajes con sustancia, no "wip" sueltos). Único matiz: 8 de los 25 últimos commits llevan mensaje en inglés pese a que CLAUDE.md §2 pide "mensajes de commit en español" — inconsistencia menor, cero impacto funcional.

**Documentación — mayormente ejemplar, con una fecha que no cuadra.** Tener `CLAUDE.md`/`AGENTS.md`/`TDD.md`/`README.md` tan cuidados y actualizados es, honestamente, mejor práctica de la que tienen muchos estudios pequeños. El único hallazgo real: `TDD.md` se autodescribe como "Motor: Unity 2022.3+" y "Última revisión: Mayo 2026", pero el proyecto corre hoy sobre Unity 6000.5.4f1 y CLAUDE.md/README ya lo tienen actualizado a "Unity 6 + URP 17.5". Si `TDD.md` es la fuente de verdad declarada, vale la pena una pasada de actualización del encabezado — no cambia nada técnico, pero alguien externo que abra ese archivo primero se lleva una impresión de documentación desactualizada que no es real.

**Paquetes — dos candidatos a revisar, no a borrar a ciegas.** `manifest.json` incluye `com.unity.ads` (4.19.0) y `com.unity.analytics` (3.8.2). No pude confirmar desde aquí si se usan en código (requeriría grep de contenido sobre los 445 scripts, fuera del alcance de esta pasada), pero ninguno de los dos aparece mencionado en TDD.md/CLAUDE.md como sistema activo, y `com.unity.analytics` es el paquete legacy que Unity fue sustituyendo por Unity Gaming Services. Vale la pena una comprobación de 5 minutos (`grep -r "UnityEngine.Advertisements\|UnityEngine.Analytics"` sobre `Assets/`) antes de decidir si se quitan — cada paquete de más es superficie de mantenimiento y tiempo de import.

---

#### 5. Lo que ya está a nivel de estudio grande (para que quede dicho, no solo lo que falta)

- La disciplina de rendimiento por frame (NonAlloc, buffers cacheados, hashes de animator) está por encima de la media indie, confirmado independientemente hoy en los dos archivos que releí.
- El patrón `ResetStatics` para limpiar estado estático entre sesiones de PlayMode, aplicado en la mayoría de singletons, es exactamente el tipo de disciplina que evita bugs "solo en el editor" — la lista de excepciones (M9 en la auditoría de ayer) es corta y ya está identificada.
- El sistema de guardado con escritura atómica (`.tmp` + `File.Move`) es la práctica correcta para no corromper saves ante un crash.
- La decisión documentada de **no** fusionar los dos motores narrativos (CLAUDE.md §7) tras un intento fallido es exactamente la clase de decisión de arquitectura madura que un equipo grande valora — reconocer cuándo no tocar algo que funciona es tan importante como refactorizar.
- Convención de idioma, estructura de carpetas y nomenclatura consistentes en todo el proyecto.

---

#### 6. Plan de acción priorizado para "entregable"

1. **Identidad del build** (§1.1) — 2 minutos, cero riesgo, bloqueante para cualquier subida a tienda. Hacerlo ya, antes de generar ningún build de demo.
2. **C2 — refcount en `PushMode`** (§2) — una tarde, el fix de código más rentable de todo el informe de ayer.
3. **C1 — reentrada en `DialogueManager`** (§2) — el soft-lock narrativo más probable en una sesión de demo real.
4. **Layer Collision Matrix** (§3) — una tarde: crear capa `Character`, revisar la matriz. Beneficio de rendimiento y de corrección a la vez.
5. **Primeros 5-8 tests** (§1.2) — empezar por los invariantes narrativos de CLAUDE.md §4 y por reproducir C1/C2 como test antes de arreglarlos (así queda el test como red de seguridad permanente, no solo el fix puntual).
6. **Logging en rutas calientes de combate** (M13 de ayer / §3 aquí) — antes de cualquier build de rendimiento medido con perfilador.
7. Resto de críticos/altos de `AUDITORIA_CODIGO_2026-08-07.md` en el orden que ya proponía ese documento (C3, C4, C5, C6, C7, A1-A12).
8. Antes de la demo de Steam específicamente: todo lo de `STEAM_DEMO_CHECKLIST.md` sigue siendo el checklist correcto — esta auditoría no lo sustituye, lo complementa (el punto 1 de aquí, identidad del build, es un prerrequisito técnico que ese checklist da por hecho pero no verifica explícitamente).

Nada de esto exige parar el desarrollo ni rehacer sistemas. El patrón general, otra vez: la infraestructura y el código reciente son de buena calidad; lo que falta es peinar la configuración de proyecto heredada del template y cerrar el hueco de proceso (tests, CI) que hoy hace que cada verificación dependa de jugar a mano.

### 19.3 Addendum — Código y sistemas obsoletos — 7 de agosto de 2026

**Fecha:** 7 de agosto de 2026 · Complementa a `AUDITORIA_CODIGO_2026-08-07.md`. **Método:** cada hallazgo de este documento está verificado contra el proyecto real (no solo contra el código): para todo lo que se marca "sin uso" comprobé el GUID del script en `Assets/Scenes`, `Assets/Prefabs` y las carpetas de datos (`_NPCs`, `_QUEST`, `_DIALOGUES`, `_BootProfile`, `NarrativeGraph`, `Resources`) para confirmar que ningún GameObject ni asset lo referencia. Nada de esta lista es "probablemente muerto": o está confirmado muerto, o está confirmado vivo y se dice explícitamente.

---

#### 1. Archivos completamente vacíos — cero uso confirmado, borrado seguro

Estos 9 archivos `.cs` no contienen ninguna clase, están vacíos o son solo un BOM/espacios en blanco, y **ningún prefab, escena ni asset del proyecto referencia su GUID**. Son husks: restos de un refactor donde se movió o eliminó el contenido pero no se borró el archivo (y Unity, al no tener el `.meta` borrado, los mantiene compilando como archivos vacíos sin más).

| Archivo | Contenido |
|---|---|
| `Assets/Scripts/Behaviour NPC/Initialization/NPCInitializer.cs` | vacío — **ver nota especial abajo** |
| `Assets/Scripts/UI/LocalizedMessage.cs` | vacío |
| `Assets/Scripts/IA/AmbientAnimatorBridge.cs` | vacío |
| `Assets/Scripts/World/SaveGameService.cs` | vacío (solo BOM) |
| `Assets/Scripts/Editor/ProfileDiagnosticsEditorTools.cs` | vacío |
| `Assets/Scripts/VFX/BarrierScanURP.cs` | vacío |
| `Assets/Scripts/Core/Pooling/IPoolable.cs` | vacío |
| `Assets/Scripts/Core/Pooling/ProjectilePoolManager.cs` | vacío |

**Nota especial — duplicado real:** `Assets/Scripts/Behaviour NPC/Initialization/NPCInitializer.cs` (vacío) convive con `Assets/Scripts/Behaviour NPC/NPCInitializer.cs` (53 líneas, la clase real y en uso — sistema de inicialización de NPCs sin coroutines). Es el resto de una carpeta `Initialization/` que se dejó de usar cuando la clase se movió a la carpeta padre. La carpeta `Initialization/` no contiene nada más.

**Recomendación:** borrar los 8 archivos y sus `.meta`, y la carpeta `Initialization/` vacía resultante. Cero riesgo — no hay ninguna referencia que romper.

`SaveGameService.cs` merece una mención aparte: por el nombre, uno esperaría que sea "el" servicio de guardado — pero está vacío. El sistema de guardado real y activo es `Assets/Scripts/Core/SaveSystem.cs` (el auditado en el informe principal, con escritura atómica). No hay confusión posible en el código actual, pero si alguna vez tienes que tocar el guardado, el nombre puede despistar — otro motivo para borrarlo.

---

#### 2. Sistema completo escrito y nunca conectado: `NPCMovementController`

`Assets/Scripts/Behaviour NPC/Movement/NPCMovementController.cs`

Este archivo no está vacío — es una clase completa y cuidada (eventos `OnDestinationReached`/`OnMovementBlocked`/`OnMovementStarted`, `[RequireComponent(typeof(NavMeshAgent))]`, comentario de cabecera: *"Sistema centralizado de movimiento para NPCs. TODO el movimiento de NPCs (Combat, Party, States) pasa por aquí. CERO delays, CERO yield return null, sistema profesional y robusto."*).

El problema: **no lo usa nadie**. Ninguna otra clase del proyecto lo menciona, y no está adjunto a ningún GameObject en ninguna escena ni prefab (confirmado por GUID). El movimiento real de los NPCs pasa hoy por `NavMeshAgentUtility` y la lógica propia de cada estado de la FSM (`IdleState`, `WanderState`, `FollowPlayerState`, etc.), tal como se documenta en el informe principal.

Todo indica que este archivo es un intento de centralizar el movimiento que se escribió pero nunca se terminó de adoptar — el proyecto siguió con el patrón descentralizado por estado. No es peligroso tal cual está (no se ejecuta), pero es ruido: cualquiera que lo encuentre puede asumir que "así es como se mueve un NPC" y perder tiempo, o peor, empezar a usarlo en paralelo al sistema real y crear justo el tipo de sistema-fantasma-duplicado del que hablas.

**Recomendación:** o se borra, o si la idea de centralizar sigue viva, se anota con un comentario claro tipo `// EXPERIMENTAL — no conectado, ver Behaviour NPC/States/ para el movimiento real` para que no se confunda con código en producción.

---

#### 3. Nodos del grafo narrativo marcados `[Obsolete]` — la higiene ya existe, un paso más la completa

`Assets/NarrativeGraph/Runtime/Graph/NodeTypes/`: `DeliverItemProximityNode`, `DeliverQuestCompleteNode`, `BranchBoolNode`, `ActivateGameObjectNode`, `UnlockTriggerNode`, `PlayTimelineNode`, `WaitBattleWinNode`, `OfferQuestNode`.

Esto es al revés de un problema: es la parte del proyecto donde la deprecación está **mejor hecha**. Cada uno tiene el atributo `[Obsolete("...")]` con una explicación de qué usar en su lugar, y `NarrativeGraphWindow` los filtra del menú "Añadir Nodo" para que nadie los arrastre por error a un grafo nuevo. `BranchBoolNode` incluso documenta en un comentario por qué está roto (*"no bifurca de verdad... confirmado sin uso en ningún grafo del proyecto (Agosto 2026)"*).

Lo comprobé contra los 7 assets reales del grafo (`MainNarrative.asset` + `MainNarrative_Cap1` a `Cap6`, que son los únicos grafos del proyecto): **ninguno de estos 8 tipos de nodo aparece en ningún grafo actual.** No son compatibilidad hacia atrás para datos que sigan vivos — son cadáveres ya completamente aislados.

**Recomendación:** dado que confirmadamente no hay ningún dato que dependa de ellos, se pueden borrar del todo (no solo marcar `[Obsolete]`) sin perder nada. Si prefieres quedarte con el margen de seguridad de "por si acaso", déjalos como están — el patrón actual ya es correcto y no genera ningún riesgo, solo ocupa espacio.

---

#### 4. Herramientas de editor de un solo uso — no son bugs, pero son candidatas a archivar

`Assets/Editor/MigrateNarrativeConfigToBehaviourManager.cs` (migra campos legacy de `NPCInteractiveNarrativeConfig` a `NPCBehaviourManagerV2` en todas las escenas/prefabs) y `Assets/Editor/ReserializeOldAssets.cs` (reserializa materiales viejos de un pack de la Asset Store para silenciar warnings de consola) son utilidades de migración de un solo uso, con su propio `[MenuItem]` en el menú "El Sendero". Si ya ejecutaste la migración de NPCs y no tienes más warnings de reserialización pendientes, ninguna de las dos hace falta ya.

No son peligrosas si se quedan (no se ejecutan solas), pero si algún día limpias el menú de Editor, son las primeras candidatas — junto con el resto de setup tools de un solo uso que aparecieron en el barrido (`NPCFacePartsSetup`, `NPCIdleVariationSetup`, `CrystalBallVisionSetup`, `ModularCharacterBaker`, `StartProductionBake`, `QuickDemoBake`, `SettingsMenuCreator`, `QuestMenuCreator`): todas son herramientas de construcción de contenido normales en un proyecto indie, no deuda técnica.

---

#### 5. Verificado como "no obsoleto" a pesar de las apariencias: `BootLoader`

`Assets/Scripts/Core/BootLoader.cs` — antes de escribir este addendum parecía un candidato obvio: un `MonoBehaviour` genérico de 30 líneas, con nombre parecido a `GameBootService` (el orquestador real y documentado en CLAUDE.md), sin ninguna otra clase del código que lo referencie, y que hace `SceneManager.LoadScene(sceneToLoad)` — carga **no aditiva** — algo que en un proyecto multi-escena como este suena a bandera roja inmediata.

Lo comprobé contra la escena real y **está vivo y en uso**: hay un GameObject `START_BootLoader` en `Assets/Scenes/Systems/Start.unity`, activo, con `sceneToLoad: MainMenu` y `delaySeconds: 0`. Es el mecanismo que lleva de la escena `Start` al `MainMenu` una vez arrancan los managers. No es peligroso: `GameBootService` tiene execution order -1000 y hace todo su trabajo de arranque en `Awake()`, que Unity garantiza que se ejecuta (para todos los objetos de la escena) antes que cualquier `Start()` — incluido el de `BootLoader` — así que no hay condición de carrera. Y como todos los managers persistentes están en `DontDestroyOnLoad`, la carga no-aditiva de `MainMenu` no se los lleva por delante.

**El único hallazgo real aquí es documental:** ni `CLAUDE.md` ni `TDD.md` mencionan `BootLoader` como parte del flujo de arranque — solo documentan `GameBootService`/`AutoBootstrapOnPlay`. Si algún día tocas el arranque, es fácil no saber que este componente existe y que es él quien dispara la transición a `MainMenu`. Vale la pena añadir una línea a CLAUDE.md §1.

---

#### 6. Los dos motores narrativos (Grafo vs. Interactive) — dato objetivo, no juicio

Esto ya lo documenta tu propio CLAUDE.md §7 como decisión de arquitectura aceptada ("un intento de unificarlos rompió el juego en Agosto 2026; no se intenta fusionar"), así que no lo reporto como problema. Solo te dejo el dato objetivo por si te sirve para decidir cuánto pesa mantenerlo: de los NPCs con `NPCBehaviourManagerV2` en prefabs/`_NPCs`, **13 siguen usando el executor Interactive** (`NPCInteractiveNarrativeExecutor`/`NPCInteractiveNarrativeConfig`) frente al total de NPCs con FSM. No es un sistema residual de dos o tres NPCs sueltos — sigue siendo una parte real y viva del contenido, así que la política de "congelado pero no migrado" del CLAUDE.md sigue siendo la decisión correcta: no es candidato a borrado, solo a no crecer más (que es exactamente lo que ya dice tu documentación).

---

#### Resumen accionable

| Acción | Archivos | Riesgo de borrar |
|---|---|---|
| Borrar ya | 8 archivos vacíos (§1) + carpeta `Initialization/` | Ninguno — verificado sin referencias |
| Borrar o dejar como está (ya bien marcado) | 8 nodos `[Obsolete]` del grafo (§3) | Ninguno — verificado sin uso en ningún grafo |
| Decidir: borrar o marcar claramente como experimental | `NPCMovementController.cs` (§2) | Ninguno — verificado sin uso |
| Archivar cuando confirmes que ya no hacen falta | `MigrateNarrativeConfigToBehaviourManager.cs`, `ReserializeOldAssets.cs` (§4) | Bajo — son ejecutables manuales, no se llaman desde código |
| Documentar en CLAUDE.md, no tocar el código | `BootLoader.cs` (§5) | N/A — está vivo y funcionando |
| No tocar — decisión ya correcta | Dualidad Grafo/Interactive (§6) | N/A |

Nada de lo encontrado aquí es urgente ni arriesgado — es limpieza de bajo riesgo. Lo más valioso es el `NPCMovementController`: si alguna vez alguien (tú u otra persona ayudando en el proyecto) lo encuentra y asume que ahí es donde hay que tocar el movimiento de NPCs, perderá tiempo con un sistema que no hace nada.

### 19.4 Auditoría de seguimiento — 12 de agosto de 2026

**Fecha:** 12 de agosto de 2026 · **Autor:** Claude (Cowork), a petición de Raúl ("hemos tenido muchos cambios") · **Ámbito:** los 22 commits desde `02a198f9f` (día siguiente a §19.1/19.2) hasta `HEAD` (`f7b86aa3c`) — integración completa del asset Quibli (post-procesado + shading + `QuibliMaterialAuditor`), sistema de glifos de mando (`InputGlyphs`), kit de HUD procedural, fixes de navegación NPC/diálogo, localización de hechizos/objetos, y un fix de NRE en extracción de strings. **Método:** cuatro revisiones paralelas — (1) reverificación línea a línea de los 19 hallazgos críticos/altos de §19.1 contra el código actual, (2) revisión de los ~48 archivos `.cs` propios tocados o creados en la ventana, (3) reverificación de los 8 puntos de entregabilidad de §19.2 más higiene de repo tras la importación masiva de Quibli, (4) seguimiento del catálogo de código muerto de §19.3 — todas hechas contra el repositorio real en la máquina de Raúl (no contra una copia), con `git show`/`grep -n` citando línea exacta. Se leyó además el informe automático `ReportesMateriales/auditoria_quibli_20260812_152402.csv`, generado hoy mismo por la herramienta propia del proyecto.

---

#### Veredicto general

Excelente noticia primero: **los 19 hallazgos críticos y altos de §19.1 (C1–C7, A1–A12) están corregidos**, cada uno con un comentario `// FIX Cx/Ax (auditoría 2026-08-07)` en el código — no es casualidad, es una resolución sistemática y trazable del catálogo completo. El identificador de build y la fecha de motor en la cabecera de `TDD.md`, los dos bloqueadores "vergüenza empresa grande" de §19.2, también están resueltos. La limpieza de código muerto de §19.3 se ejecutó y en un punto (`NPCMovementController.cs`) fue incluso más lejos de lo que la auditoría pedía.

Un commit del 11 de agosto borró, sin mencionarlo en el mensaje, el "kit de HUD unificado" que se había terminado dos commits antes — la revisión automática lo marcó como posible pérdida accidental, pero Raúl confirmó que fue una decisión consciente (el enfoque no acabó saliendo adelante). Queda documentado en §19.4.1 para que no se malinterprete en una lectura futura.

Por lo demás, el patrón se repite: la disciplina de código sigue siendo sólida (el nuevo sistema `InputGlyphs` es un ejemplo de manual del proyecto), y lo que falta sigue siendo lo mismo que en agosto — proceso (tests, CI, `.asmdef`) — más higiene de repositorio nueva, generada por la importación de ~152 MB del asset Quibli.

---

#### 19.4.1 Borrado del kit de HUD unificado — confirmado intencional, no accidental

`bd918d764` ("feat: kit de HUD unificado (Procedural UI Kit)") añadió 7 archivos completos y funcionales:

```
Assets/Scripts/UI/ProceduralUIKit.cs                (354 líneas)
Assets/Scripts/UI/ProceduralPanelSkin.cs             (65 líneas)
Assets/Scripts/UI/ProceduralSlotFrameSkin.cs         (44 líneas)
Assets/Scripts/UI/ProceduralAmbientSparkles.cs       (53 líneas)
Assets/Scripts/Editor/UI/CrearNuevoHUD.cs            (104 líneas)
Assets/Scripts/Editor/UI/CrearHUDDesdeCero.cs        (188 líneas)
Assets/Scripts/Editor/UI/AplicarLookQuibliAEscena.cs (67 líneas)
```

Dos commits después, `bfb27c983` ("feat: implement QuibliMaterialFixer tool for shader migration and texture recovery") borró los mismos 7 archivos completos, sin mencionarlo en el mensaje del commit. La revisión automática de esta auditoría lo marcó como posible pérdida accidental (el mensaje del commit no lo menciona y no hay commit de "revert del HUD" explícito) — **Raúl confirmó que fue intencional: el kit de HUD no acabó saliendo adelante como enfoque y se descartó a propósito.** Queda documentado aquí para que una lectura futura de este archivo (humana o de una IA) no vuelva a interpretarlo como una pérdida accidental ni intente "recuperarlo" sin preguntar antes.

Sigue siendo cierto que no se encontró ninguna referencia rota a esas clases en el resto del código (`git grep` no devuelve nada) — si algún prefab de escena antiguo tuviera un componente `ProceduralUIKit`/`ProceduralPanelSkin` asignado de cuando existió, esa referencia quedaría huérfana sin aviso en compilación, pero no bloquea nada y no requiere acción salvo que se note algo raro al abrir una escena/prefab de esa época.

---

#### 19.4.2 Seguimiento de §19.1 — los 19 críticos/altos, verificados hoy contra el código real

| # | Hallazgo | Estado |
|---|---|---|
| C1 | `DialogueManager.StartDialogue` reentrante | **Corregido** — `DialogueManager.cs:330-336` invoca el `_onEnd` previo si `IsOpen` antes de sobrescribir. |
| C2 | `PushMode` sin refcount | **Corregido** — `PlayerActionManager.cs:249-260`, refcount real, sin el `if (Top == mode) return;`. |
| C3 | Teleport a anchor inexistente sin emitir `OnTeleportEnded` | **Corregido** — `TeleportService.cs:137,155,168,266,288`, todos los paths de fallo emiten el evento. |
| C4 | Blackboards rancios en cada carga de escena | **Corregido** — `NarrativeGraphStarter.cs:152-157`, flag `_hasRestoredBlackboardsThisSession`. |
| C5 | `CinematicState.Cleanup()` no marca `IsCompleted` | **Corregido** — `CinematicState.cs:594`. |
| C6 | Hitstops solapados / timeScale sin árbitro | **Corregido** — `SimpleHitStopProvider.cs:28-36` + nuevo `TimeScaleArbiterService`. |
| C7 | Knockback aéreo interrumpido sin `OnDisable` | **Corregido** — `AerialKnockbackReceiver.cs:142-168`. |
| A1 | `ActiveCombatRegistry.Count` no limpia fake-null | **Corregido** — `ActiveCombatRegistry.cs:167-176`, `CleanupDestroyedNPCs()` antes de contar. |
| A2 | `BossArenaController` no reabre arena si el boss se destruye sin morir | **Corregido** — `BossArenaController.cs:594`, `ApplyBossClearedState(...)` en el path de emergencia. |
| A3 | `ObjectPool.Return()` hace push aunque detecte doble devolución | **Corregido** — `ObjectPool.cs:119-123`. |
| A4 | `GameBootService` ignora el retorno de `LoadProfile` | **Corregido** — `GameBootService.cs:285`. |
| A5 | Señales narrativas sticky consumidas por sistema equivocado | **Corregido** — `DefaultNarrativeSignals.cs:381-390`, nuevo `RequeueCustom(key)`. |
| A6 | Ramas fork sin `Exit()` | **Corregido** — `NarrativeRunner.cs`, nuevo `_activeBranchNodes`. |
| A7 | `Transition.cs` fuga de `sceneLoaded` | **Corregido** — `Transition.cs:106-109`, `OnDestroy()` desuscribe. |
| A8 | `DayNightCycle` oscurecimiento por lluvia compuesto exponencialmente | **Corregido** — `DayNightCycle.cs:389-390`. |
| A9 | `SimpleCinematicDirector` flag estático compartido entre instancias | **Corregido** — nuevo `s_activeInstance` como dueño real. |
| A10 | Muerte/revive del player sin limpiar pila de modos | **Corregido** — `PlayerHealthSystem.cs:404,455`, `ResetToDefault()`. |
| A11 | Bosses: higiene (reflection, animator.Play sin guard, `StopCoroutineSafe` roto) | **Corregido** en los tres (`GolemBossAI`, `ImpDemonAI`, `Spider1AI`). |
| A12 | Swap de personaje sin gating de estado | **Corregido** — `PartyControlManager.cs:135-142`, `IsSwapAllowedByCurrentMode()`. |

No queda ningún crítico ni alto abierto de este catálogo. Vale la pena tachar C1-C7/A1-A12 en §19.1 (dejarlos como referencia histórica, igual que se hizo con el catálogo de mayo en §13) para que una lectura futura no los dé por vigentes sin mirar.

---

#### 19.4.3 Código nuevo — hallazgos

Al margen del borrado intencional del kit de HUD (§19.4.1), el resto de código nuevo de la ventana está en buen estado. Dos hallazgos menores, ninguno urgente:

**MEDIO — VFX de spawn sin pool en archivo ya tocado.** `Assets/Scripts/Attacks/MagicProjectileSpawner.cs:287-291` y `:446-450` siguen haciendo `Instantiate(spell.spawnVFX, ...)` + `Destroy(fx, destroyTime)` para el flash de inicio de hechizo — viola la regla del proyecto (AGENTS.md §2: VFX de un solo uso siempre por `VfxPoolService`). Es deuda preexistente (no de esta ventana), pero el propio `MagicProjectil.cs` ya usa `VfxPoolService` correctamente para `impactVFX`/`despawnVFX`, y el archivo del spawner se tocó esta semana (FIX M8, `_chargingProjectiles`) — aprovechar para pasar también el `spawnVFX` al pool ya que se está ahí.

**BAJO — logging sin guardar por directiva de compilación, patrón reproducido, no nuevo.** `NPCQuestActionExecutor.cs:108` (línea añadida esta ventana) sigue el patrón ya existente en el resto del archivo: `debugMode` es un `[SerializeField] bool`, no una directiva `#if`, así que un `Debug.LogWarning` puede colarse en build si alguien activa el flag a mano en el inspector. No es una regresión nueva, pero tampoco corrige el patrón — mismo criterio que M13 en §19.1.

**Lo que está bien hecho (destacable):**

- `Assets/Scripts/Core/InputGlyphs/InputGlyphService.cs` — servicio nuevo completo, con `ResetStatics` correcto, sin `FindObjectOfType`/`GetComponent` sin cachear en el driver de `Update`, sin reflection, logs bien gateados. Ejemplo de manual de las reglas del proyecto.
- `InteractionDetector.cs` — de paso se corrigió el bug de obstrucción invertida de M7 (§19.1) y se le añadió throttle a 10 Hz + máscara cacheada en `Awake`.
- `PlayerParty.cs` y `FeedbackService.cs` — recibieron el patrón `ResetStatics` que les faltaba (cerraba parte de M9, §19.1).
- `TMP_FreezeFallbackAtlas.cs` (el fix de NRE) — el orden de comprobación (`is Object` antes de `IEnumerable`) es el correcto para el "fake null" de Unity, no un parche superficial.

---

#### 19.4.4 Entregabilidad — seguimiento de §19.2

| # | Punto | Estado el 8 de agosto | Estado el 12 de agosto (tarde) |
|---|---|---|---|
| 1 | `applicationIdentifier` del template | Bloqueante | **Resuelto** — `com.liyodev.elsenderodelasestrellas` en Android/Standalone, `projectName` correcto. |
| 2 | Cero tests automatizados | Sin cambios | **Sigue igual** — se decidió no abordarlo en esta pasada (ver nota abajo). |
| 3 | Sin CI | Sin cambios | **Sigue igual** — se decidió no abordarlo en esta pasada (ver nota abajo). |
| 4 | Cero `.asmdef` propios | Sin cambios | **Sigue igual, a propósito** — ver §19.4.7, no se tocó por riesgo de romper la compilación sin forma de verificarlo. |
| 5 | `m_LayerCollisionMatrix` sin personalizar | Sin cambios | **Resuelto parcialmente** — ver §19.4.7. Se desactivó la colisión de las 5 capas puramente visuales/UI; las capas de gameplay (Player, Enemy, Projectile, etc.) se dejaron tal cual por riesgo de romper mecánicas basadas en trigger. |
| 6 | `antiAliasing: 0` en ambas calidades | Sin cambios | **Resuelto en PC** — ver §19.4.7. MSAA 4x en calidad PC (`QualitySettings.asset` + `PC_RPAsset.asset`); Mobile se deja en 0 a propósito (gama baja). |
| 7 | `com.unity.ads`/`com.unity.analytics` sin uso | Sin cambios | **Resuelto** — ambos paquetes eliminados de `manifest.json` (ver §19.4.7). |
| 8 | Cabecera de `TDD.md` desactualizada ("Unity 2022.3+") | Pendiente | **Resuelto** — cabecera dice "Unity 6 (6000.5.4f1)". |

Del bloque original, quedan sin tocar deliberadamente: tests automatizados y CI (Raúl decidió dejarlos fuera de esta pasada) y `.asmdef` propios (Claude decidió no tocarlo por riesgo — ver §19.4.7). Los otros cinco puntos están resueltos, parcial o totalmente, a día 12 de agosto.

**Higiene de repositorio — hallazgos nuevos, causados por la importación de Quibli (2122 archivos, ~152 MB):**

- El `.git` del proyecto pesa ya **~2.05 GiB** (107.169 objetos en pack). Coherente con el volumen importado, pero vale la pena tenerlo en cuenta si algún día hay que clonar el repo en una máquina nueva o compartirlo.
- **65 objetos `tmp_obj_*` huérfanos en `.git/objects/` (~7.17 MiB)** — probablemente de una operación de Git interrumpida durante el commit masivo (falta de espacio, timeout). Antes de limpiar: `git fsck` para confirmar que no falta nada, luego `git gc --prune=now` si todo está sano.
- **Las texturas `.tif` que trae Quibli no estaban cubiertas por Git LFS — resuelto (ver §19.4.7).** Se añadió `*.tif`/`*.tiff filter=lfs diff=lfs merge=lfs -text` a `.gitattributes`. Los `.tif` ya commiteados (hasta 16.6 MB, ej. `Ellen_Body_Normal.tif`) siguen pesando en el historial existente salvo que se haga un `git lfs migrate` aparte — esto solo evita que el problema siga creciendo con archivos nuevos.
- **`Assets/Plugins/Quibli/Demos/` pesa 150 de los 152 MB del import total** — es contenido de demostración de terceros (personaje "Ellen", escenas "City"/"Nature"), no el shader/runtime que probablemente hace falta para el juego. Incluye una subcarpeta llamada literalmente `.../Ellen Textures/trash/` (~44 MB) que el propio autor del asset marcó como descarte. Candidato claro a borrar del repo si no se está usando como referencia activa.
- `git-lfs` no está instalado en la VM local usada para esta auditoría, así que no se pudo verificar `git lfs ls-files` directamente — vale la pena confirmar en las máquinas de desarrollo activas que los archivos LFS ya bien configurados se están clonando como binarios reales y no como punteros de texto rotos.

---

#### 19.4.5 Código muerto — seguimiento de §19.3

Todo lo que pedía acción se ejecutó en el commit `818a0ae7d` ("chore: limpieza de codigo muerto segun auditoria") u otros de la misma ventana:

- **Los 8 archivos `.cs` vacíos** (incluido el duplicado de `NPCInitializer.cs` en `Initialization/`) — **borrados**, confirmado uno a uno.
- **`NPCMovementController.cs`** (sistema fantasma de 449 líneas) — **borrado por completo**, más allá de lo que la auditoría pedía (solo lo señalaba como candidato). No se encontraron referencias rotas en el diff.
- **Los 8 nodos narrativos `[Obsolete]`** — siguen ahí sin cambios, tal como se esperaba (la auditoría no pedía borrarlos, solo los dejaba documentados como candidatos sin uso).
- **Las dos herramientas de migración de un solo uso** (`MigrateNarrativeConfigToBehaviourManager.cs`, `ReserializeOldAssets.cs`) — ya no existen.
- **`BootLoader.cs`** — documentado en `TDD.md` §1 y en el propio §19.3, como se recomendó.
- **`Assets/t2.txt` y `Assets/test_delete_me.txt`** — borrados en el mismo commit.
- El vaivén de Quibli (import → revert completo → reimport) no dejó restos huérfanos: `Assets/Plugins/Quibli/` está íntegro y no hay referencias colgantes en `ProjectSettings/*.asset`.

Nada pendiente de este catálogo salvo la decisión ya tomada (dejar los nodos `[Obsolete]` como están).

---

#### 19.4.6 Deuda de arte — informe de materiales Quibli (generado hoy por `QuibliMaterialAuditor`)

El proyecto tiene ahora su propia herramienta de editor (`Assets/Editor/QuibliMaterialAuditor.cs`) que barre todos los materiales y clasifica su estado de migración al shading Quibli. Su informe de hoy (`ReportesMateriales/auditoria_quibli_20260812_152402.csv`, 4.818 materiales) da una foto objetiva de cuánta migración de arte queda pendiente — dato nuevo que no existía en agosto:

| Categoría | Materiales |
|---|---|
| URP nativo, pendiente de migrar a Quibli | 3.847 |
| Ya migrados a Quibli (`Quibli/Stylized Lit`, `Quibli/Foliage`, `Quibli/Cloud2D`) | 215 |
| Shader Graphs/Toon (Ciro Continisio), a migrar | 100 |
| Built-in de Unity (Standard/Mobile/Legacy), a revisar si se migran | ~85 |
| Fuera de alcance (VFX/UI/Skybox) | ~250 |

Es decir, la migración de arte está en una fase muy temprana (~4-5% del total) en el momento de la auditoría original. No es un bug ni deuda técnica de código — es trabajo de arte pendiente, cuantificado por primera vez gracias a la herramienta que el propio proyecto se acaba de dar. Vale la pena tratar este CSV como una checklist viva: se puede volver a generar en cualquier momento para medir progreso.

**Actualización — 12 de agosto de 2026, última hora de la tarde: migración prácticamente terminada.** Raúl migró materiales durante varias horas (herramienta `QuibliMaterialFixer`/conversión directa, ver `ReportesMateriales/conversion_log_20260812_181144.csv`, 3.845 materiales procesados). El último CSV de auditoría generado por la herramienta (`auditoria_quibli_20260812_180747.csv`) quedó desactualizado porque se tomó *antes* de que terminara esa última tanda de conversión — así que en vez de fiarse de ese CSV, se verificó el estado real contra los `.mat` del disco (grep del GUID del shader `Universal Render Pipeline/Lit`, `933532a4fcc9baf4fa0491de14d08ed7`, y de `Quibli/StylizedLit`, `2a230514c860643f69b6a4d1871d3825`, directamente en los archivos):

- **327 materiales en `Quibli/StylizedLit`** (subía de 175 en el último CSV a medio migrar).
- **Solo 29 materiales siguen en `Universal Render Pipeline/Lit`** (bajó de 3.844 pendientes a 29 — 99.2% migrado). De esos 29:
  - **14 son del pack `Assets/Art/World/boss_of_war/materials/`** (anvil, aqueduct, aqueduct_2, aqueduct_3, big_stone, big_stone_2, cube_stone, forceps, gate, hummer, kruk, podium, wall, wall_stone). **Verificación exhaustiva completada (12 de agosto, noche):** se comprobó el GUID de cada uno de los 14 contra *todas* las escenas (`*.unity`) y *todos* los prefabs (`*.prefab`) del repositorio, no solo una muestra.
    - **13 sin ninguna referencia en ningún sitio** (`anvil`, `aqueduct`, `aqueduct_2`, `aqueduct_3`, `big_stone`, `big_stone_2`, `cube_stone`, `forceps`, `gate`, `hummer`, `kruk`, `podium`, `wall`) — contenido genuinamente sin usar hoy. **Decisión: se dejan tal cual, sin migrar.** No urge migrarlos porque nada los renderiza, y tampoco se borran todavía — un grep por GUID no cubre el 100% de las formas posibles de referenciar un asset (p. ej. una carga por ruta con `Resources.Load`), así que "sin uso confirmado" no equivale a "sin uso posible". Si se retoma el contenido de la arena del boss en el futuro, seguirán ahí.
    - **`wall_stone.mat` SÍ está en uso — y mucho:** 283 referencias en `Assets/Scenes/Worlds/MainWorld.unity`, como override de material sobre instancias repetidas de un prefab (piezas modulares de muro/piedra del terreno del mundo). Esto **corrige** la suposición inicial de la auditoría — no es contenido huérfano de una arena de boss sin usar, es geometría real y visible del mundo abierto. **Pendiente de migrar** — recomendado hacerlo con la herramienta propia de conversión (`QuibliMaterialFixer`) desde el Editor, no a mano: el salto de shader `URP/Lit` a `Quibli/StylizedLit` no es un simple cambio de GUID, las propiedades no mapean 1:1.
  - **Los otros 15 son contenido de ejemplo de plugins de terceros** — no del juego: 10 de `Invector-3rdPersonController_LITE/3D Models/Others/Materials/` (escalera, cubos, el logo de Invector — assets de demo del plugin), 2 de `Assets/Plugins/Quibli/Demos/Sample Scene with Quibli/` (las propias demos de Quibli, coherente con que esa carpeta no forma parte del contenido real del juego — ver §19.4.8), `Cloud.mat` de un pack "Low Poly Modular Terrain" bonus, y `Water_plane.mat` de los ejemplos del paquete "Height Fog". Ninguno de estos necesita migrarse — son ejemplos de asset packs, no arte del juego.

**Conclusión: la migración de arte a Quibli está prácticamente completa.** Queda un único material real por migrar (`wall_stone.mat`, ver arriba) — todo lo demás sin migrar es contenido confirmado sin uso o de terceros fuera de alcance. Recomendación: la próxima vez que abras el proyecto, vuelve a correr `QuibliMaterialAuditor` desde el editor para tener un CSV fresco que confirme este análisis con la herramienta oficial en vez de con `grep` externo.

---

#### 19.4.7 Acciones aplicadas — 12 de agosto de 2026 (misma tarde, a petición de Raúl)

Tras entregar el informe de arriba, Raúl pidió aplicar directamente la parte de bajo riesgo. Alcance acordado: cambios de código sin riesgo sí; tests/CI/limpieza de repo (borrar `Quibli/Demos`, `git gc`) se dejan para más adelante; los ajustes de Project Settings (Layer Collision Matrix, antialiasing, paquetes sin usar, `.asmdef`) los decide y aplica Claude por tener visibilidad del código. Esto es lo que se hizo, y por qué se paró donde se paró:

**Aplicado:**

- **`MagicProjectileSpawner.cs`** (líneas ~285-293 y ~444-454) — el `spawnVFX` de inicio de hechizo pasa por `VfxPoolService.Instance.Play(...)` en vez de `Instantiate`+`Destroy` directo, igual que ya hacían `impactVFX`/`despawnVFX` en `MagicProjectil.cs`. Cierra el hallazgo MEDIO de §19.4.3.
- **C1-C7/A1-A12 en §19.1** — cada cabecera lleva ahora "— CORREGIDO (ver §19.4.2)" para que una relectura futura no los dé por vigentes.
- **`.gitattributes`** — añadido `*.tif`/`*.tiff filter=lfs diff=lfs merge=lfs -text`. Los `.tif` ya commiteados (hasta 16.6 MB) siguen pesando en el historial existente — para eso hace falta un `git lfs migrate` aparte, que no se ha hecho.
- **`Packages/manifest.json`** — eliminadas las líneas `com.unity.ads` y `com.unity.analytics` (cero referencias en código, confirmado en §19.4.4/§19.2). No se tocó `com.unity.modules.unityanalytics`, que es un módulo distinto (built-in del motor, no el paquete de Analytics legacy).
- **Layer Collision Matrix** (`ProjectSettings/DynamicsManager.asset`) — **cambio parcial y deliberadamente conservador.** Se identificaron por código 5 capas puramente visuales/de UI que el propio proyecto ya trata como "ignorar" en la lógica de colisión (`EnemyProjectile.cs` las excluye explícitamente): `TransparentFX`, `InteractHint`, `PauseUI`, `UI_Portrait`, `Minimap`. Se desactivó su colisión física con absolutamente todo (incluidas entre ellas), lo que es seguro porque el código ya no-opea cualquier contacto con estas capas. **No se tocaron** las capas de gameplay real (`Default`, `Player`, `Enemy`, `Projectile`, `ProjectileEnemy`, `Interactable`, `Floor`, `Obstacle`, `Climb`, `Water`) porque varias de sus combinaciones dependen de `OnTrigger*` para mecánicas reales que sí usan la matriz de colisión — por ejemplo, `MagicProjectil.cs` detecta colisión con `ProjectileEnemy` a propósito (posible mecánica de clash/parry de proyectiles), así que la sugerencia genérica de §19.2 ("Projectile no debería colisionar con ProjectileEnemy") resultó ser **incorrecta** al verificarla contra el código real — desactivar ese par habría roto esa mecánica. Diseñar la matriz completa (y la idea de darle a los personajes su propia capa `Character`, separada de `Default`) es un trabajo que necesita playtesting en el Editor, no una pasada de texto a ciegas — se deja pendiente, ver "Sigue pendiente" abajo.
  - **Verificación pendiente por tu parte:** abre `Edit → Project Settings → Physics → Layer Collision Matrix` una vez en Unity y confirma que las 5 capas de arriba aparecen sin ninguna casilla marcada. El formato de este campo en el `.asset` es un blob binario poco documentado — hice el cambio con un script que decodifica/codifica el formato y lo verifiqué por consistencia (simetría de la matriz, que las capas de gameplay quedan exactamente igual que antes), pero no pude abrir Unity para confirmarlo visualmente.
- **Antialiasing** — activado **MSAA 4x en la calidad PC** (`ProjectSettings/QualitySettings.asset` → `antiAliasing: 4` en el tier `PC`, y `Assets/Settings/PC_RPAsset.asset` → `m_MSAA: 4`, que es el campo que URP realmente lee — `QualitySettings.antiAliasing` por sí solo no basta en URP, hay que tocar también el asset del render pipeline). **Mobile se dejó en 0/Disabled a propósito** — es una decisión de rendimiento en gama baja que no me correspondía tomar por ti; si quieres AA en Mobile, es un cambio de un campo en `Mobile_RPAsset.asset`.

**Sigue pendiente, a propósito:**

- **Tests automatizados y CI** — Raúl decidió dejarlos fuera de esta pasada (ver §19.4.8, se retomó después el mismo día).
- **Limpieza de repo** (`Assets/Plugins/Quibli/Demos/`, `git fsck` + `git gc --prune=now` para los `tmp_obj_*`) — Raúl decidió dejarla fuera de esta pasada; ver §19.4.8, se retomó después y apareció un hallazgo importante que cambia el plan.
- **`.asmdef` propios** — decisión de Claude, no de alcance: dividir ~450 scripts en ensamblados propios requiere añadir referencias explícitas a cada paquete que usa cada carpeta (Cinemachine, Timeline, Input System, AI Navigation, DOTween, Invector, TextMeshPro, Quibli...) y Unity solo confirma si la configuración es correcta al recompilar en el Editor — un error de referencia rompe la compilación del proyecto entero hasta que se abra Unity y se lea la Consola. Sin esa señal de vuelta no hay forma responsable de hacerlo a ciegas desde aquí; es el único punto de §19.2 que requiere trabajo directamente en el Editor, iterando contra los errores de compilación reales.
- **Layer Collision Matrix, parte completa** (capa `Character` propia para separar personajes de la geometría de `Default`) — igual que arriba, necesita playtesting en el Editor para no romper el raycast de obstrucción que hoy depende de que los personajes vivan en `Default` (ver AGENTS.md §2).

#### 19.4.8 Segunda pasada — tests, CI y limpieza de repo (12 de agosto, misma tarde)

Raúl pidió continuar hasta dejar el proyecto lo más completo posible. Esto es lo que se hizo, un hallazgo importante que cambió el plan sobre la marcha, y lo que quedó fuera y por qué.

**Hallazgo importante — `Assets/Plugins/Quibli/Demos/` NO es contenido muerto, tiene una dependencia real activa.** El plan original (§19.4.4) era borrar esta carpeta de 150 MB por ser contenido de muestra de terceros. Antes de borrar nada se verificó si algo la referenciaba de verdad (no solo por nombre, sino por GUID contra las escenas reales) — y sí: el `Volume` global de post-procesado de `Assets/Scenes/Worlds/MainWorld.unity` (`m_IsGlobal: 1`, el que añadió el commit `294507f73 "agregar Global Volume permanente de post-procesado en MainWorld"`) tiene su `sharedProfile` apuntando **directamente** a `Assets/Plugins/Quibli/Demos/Sample Scene with Quibli/Scene Settings/SampleSceneWithQuibli-MainCameraProfile.asset`. Es decir: el post-procesado real del juego en producción vive dentro de la carpeta de demos del plugin, no en un asset propio del proyecto. Borrar `Demos/` como estaba previsto habría roto el post-procesado de `MainWorld` en el momento en que alguien abriera esa escena.

Contexto: el commit `a0f8c1851` (posterior) sí creó una copia de los valores de ese perfil en `Assets/Scenes/Worlds/MainWorld/Volumen Profile.asset` — pero es una copia de valores en un asset *distinto*, no un redirigido del `Volume` de la escena. El `Volume` de `MainWorld.unity` nunca se actualizó para apuntar a esa copia; sigue leyendo el original dentro de `Demos/`. Se revisó también el propio perfil de `Demos/` (`SampleSceneWithQuibli-MainCameraProfile.asset`) y no referencia ninguna textura/LUT — solo parámetros numéricos de los overrides de post-proceso — así que la migración, cuando se haga, es sencilla: no arrastra dependencias adicionales.

**Decisión final de Raúl (12 de agosto, misma tarde): no se migra.** Se dejó preparada la migración (mover/copiar `SampleSceneWithQuibli-MainCameraProfile.asset` a una carpeta propia del proyecto — ya existe de hecho una copia de valores en `Assets/Scenes/Worlds/MainWorld/Volumen Profile.asset` — y repuntar el `sharedProfile` del `Volume` de `MainWorld.unity` a ese asset), pero Raúl decidió dejar `Quibli/Demos/` tal cual está, sin migrar ni borrar. Es una decisión razonable: no es un bug ni un riesgo funcional — el post-procesado funciona perfectamente donde está hoy, es puramente una cuestión de organización del proyecto (contenido de producción viviendo dentro de una carpeta de muestra de un plugin de terceros). Los 150 MB no son un coste relevante en un repo que ya usa Git LFS para el arte pesado. Lo único que importa de verdad es que quede documentado aquí, para que nadie (persona o IA) borre `Demos/` en el futuro asumiendo que es contenido de muestra sin uso real — sigue teniendo la dependencia activa descrita arriba.

**Sí se hizo, seguro e independiente de lo anterior:**

- **`git fsck --full --unreachable`** — limpio, sin objetos corruptos ni advertencias.
- **`git gc --prune=now`** — lanzado en segundo plano (el repo de ~2 GiB tarda más de lo que permite una sola llamada de shell); limpia los objetos `tmp_obj_*` huérfanos detectados en §19.4.4. **Confirmado terminado y limpio por Raúl (12 de agosto, noche):** `git count-objects -vH` final da `garbage: 0`, `1 pack` (antes 4), `size-pack: 2.03 GiB` (bajó de 2.06 GiB). Los `tmp_obj_*` seguían reapareciendo tras cada `gc` porque son objetos incompletos que git no borra por sí solo (por seguridad, al no ser objetos válidos) — hizo falta un borrado manual explícito de esos archivos concretos, ver §19.4.9.
- **Primer test automatizado del proyecto** — `Assets/Scripts/Editor/Tests/PlayerActionManagerTests.cs`, 5 tests de EditMode sobre el refcount de `PushMode`/`PopMode` de `PlayerActionManager`, incluyendo la reproducción exacta del escenario del bug C2 (dos sistemas empujando `Cinematic`, un solo Pop no debe devolver el control al jugador) y de A10 (`ResetToDefault` vacía la pila). Deliberadamente **sin `.asmdef` propio**: vive en una carpeta `Editor/` normal, así que compila como parte del ensamblado implícito `Assembly-CSharp-Editor` — se confirmó primero, leyendo los `.csproj` que Unity ya tiene generados en el repo, que ese ensamblado ya referencia `nunit.framework`/`UnityEditor.TestRunner`/`UnityEngine.TestRunner`, así que no hace falta crear ningún ensamblado nuevo ni arriesgar la compilación del proyecto. Se comprobó también que `PlayerActionManager.Awake()` no depende de ningún otro manager/singleton, así que es seguro instanciarlo aislado en un test. **Limitación real:** solo cubre lo que se puede probar sin entrar en Play — no hay tests de PlayMode todavía (necesitan su propio `.asmdef`, que si no compila bien bloquea la compilación de todo el proyecto hasta corregirlo en el Editor; se deja fuera por la misma razón que el resto de `.asmdef`).
  - **Verificado en el Test Runner de Unity (Raúl, misma tarde):** el Test Runner mostró primero "No tests to show" al ejecutar por primera vez (falso indicio de que hacía falta un `.asmdef` propio) — resultó ser solo un problema transitorio de indexado tras el primer import: tras recompilar, los 5 tests aparecieron correctamente agrupados bajo `Assembly-CSharp-Editor.dll → PlayerActionManagerTests`, confirmando que la carpeta `Editor/` sin `.asmdef` es suficiente y que no hacía falta crear ningún ensamblado nuevo.
  - **Bug real encontrado por el propio test — `PlayerLockService` invocando `DontDestroyOnLoad` en EditMode.** Al correr los tests, `PopMode_CuandoVacíaLaPila_VuelveADefault` falló con `System.InvalidOperationException` desde `PlayerLockService.Instance` (`PlayerLockService.cs:32`), invocado indirectamente vía `PlayerActionManager.ApplyTopMode() → UpdatePlayerLock()` (`PlayerActionManager.cs:470,643`). Causa: `DontDestroyOnLoad` solo es válido en Play mode, y el primer acceso de toda la sesión a `PlayerLockService.Instance` — que en producción siempre ocurre dentro de Play — pasó a ocurrir en Edit mode al ejecutarse desde un test. El singleton asigna `_instance` **antes** de llamar `DontDestroyOnLoad`, así que el objeto queda perfectamente utilizable pese a la excepción; solo revienta ese primerísimo acceso de la sesión, no accesos posteriores. **Fix aplicado solo en el test** (no se tocó `PlayerLockService.cs`, cuyo uso de `DontDestroyOnLoad` es correcto y necesario en el juego real): se añadió un `[OneTimeSetUp]` que "gasta" esa excepción esperada una sola vez, accediendo a `PlayerLockService.Instance` dentro de un `try/catch` antes de que corra ningún test. Con este fix los 5 tests pasan en verde, confirmado por Raúl en el Test Runner.
- **CI en GitHub Actions** — dos workflows (`unity-tests.yml` y `unity-request-activation-file.yml`) listos y entregados en el chat, pero **no pude escribirlos directamente en tu proyecto**: `.github/workflows/` está protegido contra escritura remota (por buenas razones — un cambio ahí puede ejecutar código con permisos del repo). Cópialos tú a `.github/workflows/` en tu carpeta del proyecto. El de tests correrá en cada push/PR a `main` y en la pestaña Actions bajo demanda, pero **no pasará en verde hasta que configures 3 secretos** (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) — el propio archivo trae los pasos exactos (ejecutar primero el workflow de activación, subir el `.alf` a license.unity3d.com, bajar el `.ulf` y pegarlo como secreto). Es un paso de 10-15 minutos que solo tú puedes hacer (necesita tu cuenta de Unity).

**Sigue pendiente, ahora sí por límite real de lo que se puede hacer sin el Editor abierto:**

- Colocar los dos workflows en `.github/workflows/` y configurar los 3 secretos de Unity.
- Tests de PlayMode y el resto de `.asmdef` — necesitan iteración en el Editor.

(La migración del `Volume Profile` fuera de `Demos/` ya NO está pendiente — Raúl decidió no hacerla, ver arriba.)

#### 19.4.9 Cierre de la CI y limpieza final (12 de agosto, noche)

**CI en GitHub Actions — en verde.** Los dos workflows se copiaron a `.github/workflows/` y se hizo push (commit `4353dc384`). Configurar la licencia resultó más enrevesado de lo previsto, así que queda documentado el camino real por si hay que repetirlo (por ejemplo, si la licencia caduca):

- El workflow `unity-request-activation-file.yml` (basado en `game-ci/unity-request-activation-file@v2`) **está descontinuado por el propio game-ci** — falla con "This action is no longer supported" al ejecutarlo. Se deja en el repo sin usar (documentado aquí el motivo); no hace falta borrarlo, simplemente no se ejecuta.
- El reemplazo oficial que recomienda game-ci es generar la licencia en la máquina local, no en CI. Pero la cuenta de Unity de Raúl usa el sistema de licencia "Named User" (activada desde 2021 vía Unity Hub), que no deja el `.ulf` clásico en `C:\ProgramData\Unity` como esperaba la documentación — un problema conocido y sin solución limpia única en la comunidad de Unity/game-ci ahora mismo (varios issues abiertos al respecto en los repos de game-ci).
- Se probó `unity-license-activate` (paquete npm de game-ci que automatiza la activación manual vía navegador) — **no funciona hoy**: la página de login de Unity cambió su HTML y la herramienta (sin mantenimiento activo) sigue buscando un formulario que ya no existe.
- **Camino que sí funcionó:** generar el `.alf` localmente con `Unity.exe -batchmode -createManualActivationFile`, subirlo a license.unity3d.com/manual, y ahí revelar a mano (vía DevTools del navegador, quitando un `style="display: none;"` de un `div.option-personal` que Unity oculta por CSS) la opción "Personal Edition" que la página ya no muestra por defecto. Se descargó el `.ulf` resultante y su contenido se guardó como el secreto `UNITY_LICENSE`, junto a `UNITY_EMAIL`/`UNITY_PASSWORD`.
- Con los 3 secretos configurados, el workflow **"Unity Tests" corrió y pasó en verde** (confirmado por Raúl, run #2 en Actions) — los 5 tests de `PlayerActionManagerTests` corriendo en modo batch en el runner de GitHub.

**Limpieza de archivos generados durante el proceso.** Los archivos `Unity_v6000.5.4f1.alf` y el `.ulf` descargado se generaron dentro de la carpeta raíz del proyecto (que es el propio repo) — se movieron a `_to_delete/` (ya en `.gitignore`) para que no se colaran en un commit por accidente, y se añadió `*.alf`/`*.ulf` al `.gitignore` como red de seguridad permanente para el futuro.

**Hallazgo de higiene de repo — no es nada nuevo que arreglar, pero queda anotado:** la carpeta `_to_delete/` ya tenía acumulados varios archivos de sesiones anteriores (restos de `.git/index.lock.*`, un `.zip` de importación de wardrobe, un par de `.cs` de herramientas de Quibli, carpetas de informes viejos). Es contenido inofensivo (por eso está en `.gitignore`, nunca llegó a versionarse), pero conviene que Raúl vacíe esa carpeta de vez en cuando desde su propio explorador de archivos — es la única vía, ya que las herramientas remotas usadas en estas sesiones no tienen permiso para borrar archivos directamente en su disco, solo moverlos.

**Nota aparte, sin relación con la CI:** el cambio de `Assets/Scenes/Worlds/MainWorld/Volumen Profile.asset` (el override de Tonemapping mencionado en el commit de la CI) lo está gestionando Raúl en otro hilo de trabajo — no se ha tocado ni revisado más en esta sesión.

#### Plan de acción priorizado (actualiza al de §19.2 §6 y §19.4.7)

1. ~~Abrir el proyecto en Unity una vez y comprobar: (a) el Layer Collision Matrix se ve como se describe en §19.4.7, (b) el proyecto compila y el nuevo test de `PlayerActionManagerTests` aparece y pasa en Window → General → Test Runner → EditMode, (c) el MSAA en PC se nota bien en cámara sin coste de rendimiento inaceptable.~~ **Hecho — confirmado por Raúl:** Layer Collision Matrix correcto visualmente, MSAA en PC confirmado visualmente, y los 5 tests de `PlayerActionManagerTests` pasan en verde en el Test Runner tras el fix de `[OneTimeSetUp]` de arriba.
2. ~~Migrar `SampleSceneWithQuibli-MainCameraProfile.asset` fuera de `Demos/`~~ — **Raúl decidió no hacerlo** (ver §19.4.8); `Quibli/Demos/` se queda como está, documentado para que no se borre por error en el futuro.
3. ~~Copiar `unity-tests.yml`/`unity-request-activation-file.yml` a `.github/workflows/` y configurar los secretos de Unity para que la CI pase en verde.~~ **Hecho — CI en verde** (ver §19.4.9). Pendiente solo un detalle menor: comitear el `.gitignore` actualizado (`*.alf`/`*.ulf`) — ver abajo.
4. ~~Confirmar que `git gc --prune=now` terminó~~ — **Hecho, repo limpio:** `garbage: 0`, 1 solo pack. Sigue pendiente comitear el cambio de `.gitignore` de §19.4.9.
5. Tests automatizados — seguir con los de PlayMode (invariantes narrativos de §10, reproducir C1) cuando haya sesión de Editor para iterar, junto con `.asmdef` propios (necesitan la misma iteración en el Editor).
6. ~~Migración de materiales a Quibli (§19.4.6) — decidir qué hacer con los 13 materiales de `boss_of_war` pendientes de confirmar uso.~~ **Resuelto (12 de agosto, noche):** verificación exhaustiva por GUID completada. 13 de los 14 confirmados sin uso, se quedan sin migrar. Solo **`wall_stone.mat` queda pendiente de migrar** — está en uso real (283 referencias en `MainWorld.unity`), hazlo con `QuibliMaterialFixer` desde el Editor cuando puedas.


---

## 20. Convenciones de documentación del proyecto

Desde el 12 de agosto de 2026, este proyecto mantiene su documentación en un único sitio para que no se disperse en archivos `.md` sueltos que nadie vuelve a mirar. La regla, a partir de ahora:

- **`TDD.md` (este documento) es la fuente de verdad única** para toda la documentación técnica: arquitectura, sistemas, reglas de código, bugs conocidos, troubleshooting, diseños en curso, checklists de proceso y auditorías.
- **Cualquier `.md` nuevo que documente algo de sustancia** (una auditoría, un análisis de diseño, una decisión de arquitectura, un checklist) **se añade como sección nueva de este documento**, no como archivo suelto en la raíz del proyecto. Al añadirlo: crear la sección al final (o dentro de la sección temática que corresponda si es una ampliación de algo ya documentado), actualizar el índice, y evitar repetir contenido que ya exista en otra sección — enlazar a ella en su lugar de copiarlo.
- **Excepciones — se quedan como archivos aparte, pero cortos y sin contenido duplicado:**
  - **`README.md`** — la portada del repositorio, lo primero que ve alguien en GitHub. Overview y enlaces, nunca el detalle técnico.
  - **`AGENTS.md` / `CLAUDE.md`** — herramientas de IA (Claude Code, Cursor, etc.) los leen **automáticamente** como contexto de proyecto en cada sesión; no son solo documentación para humanos y por eso no se retiran ni se fusionan aquí dentro. Se mantienen como resumen corto de las reglas no negociables con pointers a este documento para el detalle — nunca como copia completa de §10/§12. Si TDD.md cambia una regla no negociable o un invariante narrativo, hay que reflejar el resumen en estos dos archivos también (se mantienen sincronizados, no es "documentación muerta").
- Cualquier otro `.md` de sustancia que hoy exista o se cree en el futuro (una auditoría, un checklist, un análisis) va como sección de `TDD.md`, no como archivo nuevo en la raíz.

---

## 21. Diseño: Vestir MainWorld con el "look" de las demos de Quibli (árboles, hierba, rayos de sol, outline)

**Fecha:** 16 de agosto de 2026 · **Autor:** Claude (Cowork), a petición de Raúl ("por qué no tenemos lo de las demos de Quibli en MainWorld") · **Método:** inspección directa de `Assets/Plugins/Quibli/` y de este mismo documento (§16, §19.4) contra el proyecto real, no asumido.

### 21.1 Por qué MainWorld no tiene ya los árboles/hierba/rayos de sol de las demos

No es que "nadie los arrastrara" sin más — hay un motivo concreto documentado en §16: Quibli se **eliminó por completo** del proyecto el 11 de agosto (revert `81d65a9cc`, daño colateral de 30 materiales ajenos migrados sin querer a `Quibli/StylizedLit`) y volvió el 13 de agosto **solo por la vía del post-procesado y el cielo/nubes** (`Quibli/Cloud3D`, `Quibli/Skybox`, ver §16 "Estado de implementación"). El contenido de `Assets/Plugins/Quibli/Demos/Nature/` y `Demos/Plants/` (árboles/matorrales generados por el Foliage Generator, hierba del Grass Mesh Generator, los rayos de sol `LightBeam`) nunca formó parte de esa reincorporación — sigue existiendo en el proyecto (es contenido de la demo, ver §19.4.8, no se borra porque el post-proceso de `MainWorld` depende de otro asset de esa misma carpeta), pero nadie lo llevó todavía a una escena de mundo real.

En cambio, la migración de **materiales** del propio arte del juego al shading de Quibli (`Quibli/StylizedLit`) sí está prácticamente terminada desde el 12 de agosto por la tarde (§19.4.6/§19.4.7): 327+ materiales migrados, solo queda `wall_stone.mat`. Es decir: el "look" de sombreado plano/cel-shading de los modelos del juego ya está mayoritariamente puesto — lo que falta es (a) el vestido de entorno (árboles, hierba, rayos de sol) y (b) activar el outline, que es una propiedad más del mismo shader que ya usan la mayoría de los materiales, apagada por defecto.

### 21.2 Qué hay ya en el proyecto, listo para usar (verificado, no copiado)

- **Rayos de sol (los "rallos" de la captura):** shader `Quibli/Light Beam` (`Assets/Plugins/Quibli/Shaders/LightBeam.shader`), materiales de ejemplo `NatureScene_LightBeam 1.mat` / `2.mat` en `Assets/Plugins/Quibli/Demos/Nature/Materials/`. Es un material transparente aplicado sobre una malla simple (un quad estirado); el objeto real ya montado y con la luz orientada está dentro de `Assets/Plugins/Quibli/Demos/Nature/[Demo] Nature.unity` — la forma más fiable de traerlo es abrir esa escena, buscar "Beam" en la Hierarchy, copiar el GameObject (Ctrl+C) y pegarlo en `MainWorld.unity` (Ctrl+V), y luego reposicionar/rotar para alinearlo con el `DirectionalLight` de `MainWorld`.
- **Hierba:** `Assets/Plugins/Quibli/Prefabs/Grass Mesh Generator.prefab` (genera la malla de hierba proceduralmente) + materiales `NatureScene_Grass_Short/Long/Details/Separate_Sprouts.mat`, y dos prefabs ya montados con LOD listos para arrastrar tal cual: `Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Long.prefab` y `... Short.prefab`.
- **Árboles/matorrales pintados (billboard clouds):** `Assets/Plugins/Quibli/Prefabs/Foliage Generator.prefab` (herramienta genérica, no ligada a la demo) + presets en `Assets/Plugins/Quibli/Demos/Plants/Foliage Generator Presets/` (`ExamplePlant_1..15`, son parámetros de forma/partículas, **no** de color) + `Assets/Plugins/Quibli/Demos/[Common]/Foliage Generator Presets/`. El color de cada árbol/mata sale del material que se le asigna después de generar la malla (shader `Foliage.shadergraph`), no del preset.
- **Outline (bordes negros tipo las tazas):** ya integrado en el shader principal `Quibli/StylizedLit` (`Assets/Plugins/Quibli/Shaders/StylizedLit.shader`, líneas 35-40): `_OutlineEnabled` (toggle), `_OutlineColor`, `_OutlineWidth`, `_OutlineScale`, `_OutlineDepthOffset`, `_CameraDistanceImpact`. Como la mayoría de materiales del juego ya está en este shader (§19.4.6), activar el outline es un cambio de propiedad, no de shader — bajo riesgo, no repite la clase de problema de §16.

### 21.3 Qué se ha añadido en esta sesión

Todo lo nuevo vive fuera de `Assets/Plugins/Quibli/` (carpeta de terceros, no se toca) y no modifica ningún material ni escena existente:

- **`Assets/Editor/QuibliOutlineTools.cs`** — herramienta de menú `Tools > Quibli > Outline > Activar/Desactivar outline en selección` y `Buscar materiales Quibli sin outline (en selección)`. Solo actúa sobre los GameObjects seleccionados a mano (nunca la escena entera sola) y solo sobre materiales que **ya** exponen `_OutlineEnabled` (nunca cambia el shader de nada) — diseñada explícitamente para no repetir el incidente de §16.
- **`Assets/Art/World/Quibli Imports/Materials/SunRays.mat`** + **`Assets/Art/World/Quibli Imports/Prefabs/SunRays.prefab`** — copia portátil (fuera de `Demos/`) del material de rayos de sol sobre un quad ya escalado como haz de luz, para arrastrar directo a `MainWorld` sin depender de la carpeta de demos si se prefiere así.
- **`Assets/Art/World/Quibli Imports/Materials/Foliage_RojoOtonal_Arbol.mat`** — variante de color del material de árbol de la demo (`NatureScene_BillboardBush_01 6-Tree`, mismo shader `Foliage.shadergraph`, mismas texturas de forma/relleno) con el degradado de sombreado (`_ShadingGradientTexture`) recalculado a una paleta roja/vino → rosa (para la zona de árboles rojizos de la secuencia final que pidió Raúl).
- **`Assets/Art/World/Quibli Imports/Materials/Foliage_Rosal_Flor.mat`** — misma idea sobre el material de mata (`NatureScene_BillboardBush_01 1`), degradado recalculado a paleta magenta/rosa vivo → rosa pálido, pensado para rosales/matas bajas.
- Ambos degradados son editables a mano después: el shader tiene un `MaterialGradientDrawer` custom (`Assets/Plugins/Quibli/Scripts/Editor/CustomDrawersShaderEditor.cs`) que dibuja cualquier propiedad `*GradientTexture` como una barra de gradiente clicable en el Inspector — no hace falta tocar el asset para retocar el color, se arrastra ahí mismo.

### 21.4 Qué queda por hacer a mano en el Editor (no se puede hacer a ciegas por archivo)

La composición espacial (dónde va cada árbol, cuánta hierba, el ángulo exacto del rayo de sol contra el `DirectionalLight` de `MainWorld`) es una decisión visual que necesita el Editor abierto — no tiene sentido escribirla a ciegas en el `.unity` de 23 MB de `MainWorld` sin poder ver el resultado. Pasos recomendados, en orden:

1. Abrir `MainWorld.unity` y `Assets/Plugins/Quibli/Demos/Nature/[Demo] Nature.unity` a la vez (una en cada pestaña de Scene).
2. Arrastrar `Grass Mesh Generator.prefab` y/o los prefabs `Nature - Grass Patch Long/Short` a `MainWorld`, o pintar hierba en el Terrain con esos meshes/materiales como Detail.
3. Arrastrar `Foliage Generator.prefab`, elegir un preset de forma (o generar el propio) y asignarle `Foliage_RojoOtonal_Arbol.mat` / `Foliage_Rosal_Flor.mat` en la zona de la secuencia final, o cualquiera de los materiales originales de `NatureScene_BillboardBush_01` para el resto del mundo.
4. Copiar el/los GameObject de rayos de sol desde `[Demo] Nature.unity` a `MainWorld` (o arrastrar `SunRays.prefab`) y rotarlo para que coincida con la dirección del `DirectionalLight` de `MainWorld`; duplicar 2-3 veces con pequeñas rotaciones distintas alrededor de su eje si se quiere el efecto "volumétrico" de varios haces cruzados que se ve en la demo.
5. Para el outline: seleccionar el/los modelos ya migrados a `Quibli/StylizedLit` que se quieran con borde y usar `Tools > Quibli > Outline > Activar outline en selección`; probar primero con un personaje o prop suelto antes de aplicarlo en masa, porque un outline uniforme en todo el mundo puede recargar visualmente si no se dosifica.

### 21.5 Nota de cautela

No se ha ejecutado ninguna migración ni conversión masiva de materiales en esta sesión — esa parte ya está hecha (§19.4.6) y no había motivo para volver a tocarla. Si en el futuro se plantea activar outline en **todo** el mundo a la vez (no solo selección a mano), hacerlo por lotes pequeños y revisando en el Editor entre lote y lote — la lección de §16 es exactamente esa: los cambios masivos de shading sobre contenido ajeno o mixto son el tipo de cambio que ha costado un revert entero antes.

### 21.6 Ejecución en bloque (16 de agosto, misma sesión) — outline masivo + vestido de MainWorld

A petición explícita de Raúl ("prefiero que todo lo que puedas me lo hagas tú", con el proyecto ya guardado y subido a git como red de seguridad), se ejecutaron en bloque las dos partes que en 21.4 se habían dejado como manuales. Todo verificado contra el archivo real tras el cambio (recuento de documentos YAML, guids duplicados, cierre de listas) antes de darlo por bueno.

**Outline en bloque:** se localizaron por `guid` de shader (`2a230514c860643f69b6a4d1871d3825`, `Quibli/StylizedLit`) los materiales reales del juego (excluyendo `Assets/Plugins/Quibli/Demos/`, que es contenido de terceros) — **71 materiales**. En los 71 se activó `_OutlineEnabled` (0→1), se añadió la keyword `DR_OUTLINE_ON` a `m_ValidKeywords` (imprescindible: sin la keyword el toggle del float no alcanza al shader compilado y el outline no se ve — se confirmó comparándolo con `Material_Mug_1.mat`, que sí la lleva), y se fijó `_OutlineColor` a negro y `_OutlineWidth` a `2.5` **solo** donde el material seguía en su valor por defecto (blanco / 1) — si algún material ya tenía un outline personalizado no se tocó. No se forzó `_RimEnabled`/`_SpecularEnabled` en ningún material — decisión deliberada, ver razonamiento más abajo. Lista completa de los 71 archivos modificados en `/tmp/outline_report.txt` de esa sesión (no versionado; si hace falta la lista exacta, se puede regenerar con `grep -rl "guid: 2a230514c860643f69b6a4d1871d3825" Assets --include="*.mat"`).

**Vestido de MainWorld (árboles/hierba/rayos de sol):** en vez de copiar a ciegas desde la demo, se leyeron las posiciones reales de los árboles ya plantados en `MainWorld.unity` — el grupo `Trees` (fileID `884681002`) tiene **122 instancias** (mayormente `Tree02_d01`, prefab `00fdaf486ca9f5c488bf0f3eaf917ff2`, más una segunda especie con 9 instancias), repartidas en un área grande (X: -119 a 469, Z: -181 a 206), no en un bosque compacto. Con esas coordenadas:
- **Hierba:** un `PrefabInstance` de `Nature - Grass Patch Long/Short` (alternando al azar) junto a cada uno de los 122 árboles, con un jitter aleatorio de ±2.5 unidades en X/Z y rotación Y aleatoria para que no se vea en rejilla, agrupados bajo un nuevo GameObject raíz `Quibli - Hierba junto a arboles`.
- **Rayos de sol:** se agruparon los árboles por celdas de 25×25 unidades y se cogieron las 8 celdas con más densidad; en cada una se colocaron 2 instancias de `SunRays.prefab` (de 21.3) cruzadas ±15° entre sí, con la rotación base copiada del `Transform` local del `Directional light` de la escena (fileID `1948117833`) — aproximada, porque ese light está parentado bajo un rig de ciclo día/noche y su rotación mundial real varía con la hora del día; no se intentó resolver esa cadena de padres. Agrupados bajo `Quibli - Rayos de sol`. 16 instancias en total (8 celdas × 2).
- Ambos grupos nuevos son objetos raíz normales — se añadieron también a la lista `SceneRoots.m_Roots` del final del archivo de escena, que es donde Unity 6 registra explícitamente qué transforms son raíz (si se añaden objetos con `m_Father: {fileID: 0}` pero sin entrada en `SceneRoots`, no cuentan como raíz de la escena — se descubrió al inspeccionar el final real del archivo antes de tocarlo, no se sabía de antemano).

**Qué no se hizo y por qué:** no se forzó `_RimEnabled`/`_SpecularEnabled` en los 71 materiales aunque el material de referencia (`Material_Mug_1`) los lleva activados — cambiar la respuesta de luz de golpe en materiales tan distintos (cuerpo de araña, gemas de inventario, arquitectura del mundo) sin poder verlo en pantalla es un riesgo de calidad visual, no de rotura técnica; queda pendiente de decidir con Raúl mirándolo ya en el Editor. Tampoco se tocaron los ~19 árboles sueltos que existen fuera del grupo `Trees` (nombrados `Tree07_a01`/`Tree07_b01`, dispersos por otras zonas del archivo) — el vestido de esta pasada cubre el grupo grande y localizable, no cada árbol individual del mapa.

**Verificación aplicada antes de considerarlo terminado:** recuento de documentos YAML del `.unity` antes/después, comprobación de que no hay ningún `fileID` duplicado en todo el archivo, y confirmación de que los dos GameObjects nuevos aparecen con `grep` por nombre. No se ha podido abrir Unity para verlo renderizado — el primer chequeo real con el Editor lo tiene que hacer Raúl.

### 21.7 Corrección (misma sesión) — el barrido de outline se había saltado 103 materiales por un bug de `xargs`

Raúl detectó a ojo que el material del jugador (Will) no tenía el outline aplicado pese a lo descrito en 21.6. Causa: el primer barrido usaba `xargs grep -l ... < lista.txt`, y `xargs` corta la entrada por espacios en blanco — con carpetas del proyecto que llevan espacios en el nombre (`RPG Tiny Hero Duo`, `RPG Tiny Fantasy World 01 PBR`, `Imp Demon Cute Series`, etc.) eso rompe las rutas y `grep` no las encuentra, sin avisar de ningún error. Resultado: el primer barrido solo cubrió **71 de los 174** materiales reales en `Quibli/StylizedLit` que hay en el proyecto (excluyendo `Assets/Plugins/Quibli/Demos/`).

Repetido el barrido con `find -print0` / `xargs -0` (seguro con espacios), se identificaron y corrigieron los **103 materiales que faltaban** — incluidos los cuatro materiales reales de los héroes (`Assets/Art/Characters/RPG Tiny Hero Duo/Material/PBR_Default.mat`, `PBR_Liam.mat`, `Polyart_Default.mat`, `PBRMaskTint.mat`), enemigos (Imp Demon, Ghosts, Golem) y varios materiales de VFX/mundo. Verificado tras el fix: **174/174** materiales en `Quibli/StylizedLit` tienen ahora `_OutlineEnabled: 1` y la keyword `DR_OUTLINE_ON`, cero excepciones.

Lección para el futuro (humano o IA): en este proyecto, cualquier operación en bloque sobre rutas de archivo debe usar `find ... -print0 | xargs -0 ...` o iterar en Python con `os.walk`/`glob`, nunca `xargs` con entrada separada por saltos de línea — hay demasiadas carpetas de asset packs de terceros con espacios en el nombre para confiar en el comportamiento por defecto.

### 21.8 REVERTIDO (16 de agosto de 2026, noche) — outline descartado, causaba stutter grave en build

**Decisión de Raúl: el outline era una prueba visual, no se queda en el juego.** Tras jugar una build de la demo, reporte de "problemas gordos de rendimiento... va a trompicones". Diagnóstico con captura real del Profiler (Deep Profile, `profiler_data.json` de esa sesión):

- **Causa técnica encontrada:** el pase "Outline" de `Quibli/StylizedLit` (`Assets/Plugins/Quibli/Shaders/StylizedLit.shader`, líneas 218-300) es un **segundo pase de geometría completo** (`Name "Outline"`, `Cull Front`, `Tags {"LightMode"="SRPDefaultUnlit"}`, geometría con normales expandidas), no un efecto de post-proceso. Con los 174 materiales de `Quibli/StylizedLit` del juego real con `_OutlineEnabled: 1` desde §21.6/21.7, cada objeto se dibujaba **dos veces** — se confirmó en el Profiler: `SRPBRender.ApplyShader` (dentro de `DrawOpaqueObjects` → `RenderLoop.ScheduleDraw` → `RenderLoop.DrawSRPBatcher`) se llamaba **3.941 veces en un solo frame**, consumiendo 26,90 ms de self time (18,5% del frame) más 6,65 ms más en `SRPBatcher.Flush` — más de un tercio del frame entero solo en el pase de opacos de una cámara.
- **Por qué "desactivarlo" no bastaba por sí solo:** el mecanismo real que evita que ese segundo pase se envíe como draw call — `Material.SetShaderPassEnabled("SRPDefaultUnlit", ...)` — solo se llama dentro de `QuibliEditor.OnGUI` (`Assets/Plugins/Quibli/Scripts/Editor/QuibliEditor.cs:302`), código de Inspector que no corre en build ni en el Editor salvo que se abra el Inspector de ese material en concreto. Como los 174 materiales se tocaron editando el YAML directamente (§21.6/21.7, no vía la API de `Material`), ese pase nunca quedó realmente desactivado — el toggle visual y el coste de rendimiento son dos interruptores distintos en este shader, y solo uno de los dos se tocaba.
- **Arreglo aplicado:** `Assets/Editor/QuibliOutlineTools.cs` se corrigió para sincronizar los tres estados a la vez (float `_OutlineEnabled` + keyword `DR_OUTLINE_ON` + `SetShaderPassEnabled("SRPDefaultUnlit", ...)`) en vez de solo el float, y se añadió el comando `Tools > Quibli > Outline > Desactivar outline en TODO el proyecto (decisión final — no reactivar sin preguntar)`, que apaga los tres estados en cualquier material del proyecto que exponga `_OutlineEnabled`, sin importar el historial de cómo quedó cada uno (barrido de 71, corrección a 174, toggles sueltos por selección) — pensado para ejecutarse una sola vez y dejar el outline realmente a cero coste en todos los materiales.

**Para cualquier sesión futura (humana o IA): no reactivar el outline de Quibli sin que Raúl lo pida explícitamente.** Si en algún momento se reconsidera, hacerlo sabiendo que la técnica de geometría duplicada (inverted hull) no escala con cientos de objetos en pantalla — la alternativa correcta a esa escala es un outline por post-proceso (edge detection por normales/profundidad, un `Renderer Feature` de URP), que cuesta un pase full-screen fijo en vez de un draw call extra por objeto.

### 21.9 Actualización (16 de agosto de 2026) — hierba y rayos de sol de §21.6 retirados de MainWorld

Raúl ha eliminado de `MainWorld.unity` la hierba (`Quibli - Hierba junto a arboles`) y los rayos de sol (`Quibli - Rayos de sol`) que describía §21.6. Confirmado por archivo: ni los nombres de esos grupos ni el GUID de `SunRays.prefab` (`aca676ff59814c48979741958d2e63a7`) aparecen ya en la escena.

Contexto: se investigaron como posible causa de un problema de textura reportado en los árboles del grupo `Trees` (una instancia, en captura de pantalla, mostraba una textura que no encajaba con el resto de árboles). Se descartaron como causa — el material compartido de los árboles (`Tree.mat`) está limpio y correctamente asignado en los 51 prefabs `Tree01_*`–`Tree07_*` de `Fantasy_Kingdom_Pack/Perfabs/Vegetation` (verificado uno a uno), y ni la hierba ni los rayos de sol llegaron a estar presentes en la escena en el momento del reporte (búsqueda por nombre de grupo y por GUID de `SunRays.prefab`, cero resultados). El problema de textura del árbol sigue sin causa confirmada — pendiente de que Raúl identifique la instancia exacta en el Editor (nombre del GameObject, Inspector del Mesh Renderer, o Frame Debugger).

**Para cualquier sesión futura (humana o IA):** §21.6/21.7 describen un vestido de MainWorld (hierba + rayos de sol junto a los 122 árboles) que **ya no está aplicado en la escena** — no asumir que sigue ahí. Si se retoma, la investigación de posiciones/coordenadas de §21.6 sigue siendo válida como referencia, pero hay que volver a generarlo y colocarlo desde cero.
