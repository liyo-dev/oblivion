# TDD — El Sendero de las Estrellas

**Motor:** Unity 6 (6000.5.4f1)  
**Pipeline:** URP  
**Input:** Unity Input System (nuevo) + Invector (movimiento base del jugador)  
**Última revisión:** 12 de agosto de 2026 (unificación de toda la documentación del proyecto en este único archivo — ver § 20)

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
- Un miembro es el líder (el primero en la lista).
- Solo el líder gestiona el diálogo post-derrota.
- El líder llama a `NotifyPostDefeatDialogueFinished()` cuando su diálogo termina, desbloqueando a los demás.

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

**Bug histórico (invisibilidad tras separar el equipo, resuelto Agosto 2026):** a diferencia de Liam/Estela (NPCs que ya llevan frames renderizándose desde la carga de la escena), el NPC de Will se `Instantiate()` de cero en `SpawnWillNpc()`. El AABB de culling de cada `SkinnedMeshRenderer` se calcula a partir de la pose del rootBone; si ese primer cálculo ocurre antes de que el Animator corra un frame (o antes de que `ModularAutoBuilder` active las partes correctas) y cae fuera del frustum de la cámara real —lo normal, porque la cámara ya está mirando al personaje recién activado, no a Will—, Unity nunca lo considera "visible" una primera vez. Sin ese primer "visible", nada dispara un recálculo del AABB, así que nunca vuelve a considerarse visible: punto muerto. Resultado: Will invisible pero presente (colisiones/IA/Interactable intactos), indefinidamente, muy notorio al dejarlo quieto anclado en un puzle. Diagnóstico confirmado en el editor: seleccionar el NPC en la Hierarchy (que fuerza a Unity a leer `Renderer.bounds` para dibujar el gizmo de selección) lo hacía "reaparecer" al instante en el Game View — la firma exacta de un AABB de culling nunca refrescado. Las reafirmaciones de `renderer.enabled` que ya existían (`ReassertWillVisibilityNextFrames`, `EnsureWillNpcVisible`) no lo arreglaban porque el problema no era `enabled=false`, era el AABB de culling atascado. Fix en `SpawnWillNpc()`: `SkinnedMeshRenderer.updateWhenOffscreen = true` en todos los renderers (para que se sigan actualizando aunque se consideren no-visibles) + `Animator.Update(0f)` inmediato tras instanciar/aplicar apariencia (para que las bones ya estén en su pose real) + lectura forzada de `.bounds` por cada renderer (para forzar el recálculo del AABB con esa pose ya correcta, antes de que la cámara real haga su primer test de culling). Mismo trío repetido en `EnsureWillNpcVisible()` (red de seguridad cada 0.5s) por si algún recálculo se pierde entre el spawn y ese tick.

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

---

## 16. Diseño: Cielo unificado, clima dinámico y cielo nocturno temático (nubes, estrellas, arcoíris)

**Proyecto:** El Sendero de las Estrellas
**Fecha:** 8 agosto 2026
**Estado:** Propuesta de diseño — pendiente de aprobación antes de implementar

> **NOTA (11 ago 2026):** Esta sección analizaba una implementación basada en el asset Quibli (shaders Quibli/Cloud3D, Quibli/Cloud2D, Quibli/Skybox). Quibli se ha eliminado por completo del proyecto y CloudCoverSpawner.cs / DayNightCycle.cs se han revertido a su versión previa (nubes Low Poly Modular Terrain Pack, skyboxes por franja horaria). Todo lo que sigue sobre shaders/mallas Quibli es historico y no aplica al codigo actual.


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

## 17. Diseño: Refugio de NPCs bajo la lluvia + Relaciones sociales dinámicas

**Proyecto:** El Sendero de las Estrellas
**Fecha:** 4 agosto 2026
**Estado:** Propuesta de diseño — pendiente de aprobación antes de implementar

Decisiones ya tomadas contigo:
- Refugio en casas = el NPC desaparece al llegar a la puerta (no hay interior real que visitar).
- Relaciones dinámicas = persistentes desde la v1 (se guardan en el save).

Todo lo citado abajo (rutas, clases, métodos, líneas) está verificado leyendo el código real del proyecto, no asumido.

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
2. **Parte A después** (refugio de lluvia) — más trabajo de nivel (colocar puntos manualmente) y más superficie de casos límite (NPCs narrativos, agentes de NavMesh). Construir `NPCShelterPoint` + `SeekShelterState` con solo `TreeCanopy` primero, verificar que el ciclo completo (lluvia → refugio → vuelta) funciona bien, y añadir `HouseDoor` (con el `SetActive`) después.
3. **B.6** (radar de amistad / idle social) al final, como pulido, una vez lo esencial de ambas partes esté verificado en el juego real.

### Preguntas abiertas para validar antes de programar

- Umbrales de `bondScore` (10/30/60) y velocidad de acumulación: valores de partida, se ajustan jugando.
- ¿Quién llama a `NPCWeatherAwareness.Resubscribe()` tras cada carga aditiva de escena? Propuesto `WorldBootstrap`, a confirmar mirando su `Start()` real.
- Lista de NPCs que deben quedar excluidos del refugio de lluvia (guardias apostados, vendedores con puesto fijo, cualquiera con `narrativeID` activo) — se puede generar automáticamente o requerir marcado manual en el inspector; recomendado automático con override manual.


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

##### C1. Reentrada en `DialogueManager.StartDialogue` → grafo narrativo colgado para siempre
`Assets/Scripts/Dialogue/DialogueManager.cs:319` *(verificado)*

`StartDialogue` no comprueba `IsOpen`: sobrescribe `_current` y `_onEnd` **sin invocar el callback del diálogo anterior**. `PlayDialogueNode` (`NarrativeGraph/Runtime/Graph/NodeTypes/PlayDialogueNode.cs:81-89`) espera `while (!completed)` sobre ese callback. Escenario real: al completarse una quest reaccionan a la vez el grafo (siguiente `PlayDialogueNode`) y la post-action de `NPCQuestActionExecutor` (que también abre diálogo, con ventanas de 0.5 s en su chequeo de `IsOpen`). El que llega segundo pisa al primero → la rama del grafo queda bloqueada eternamente. Es el punto único de fallo donde convergen grafo, Interactive y post-actions.

**Fix:** si `IsOpen`, encolar o rechazar; y si se decide pisar, invocar el `_onEnd` anterior antes de sustituirlo.

##### C2. `PushMode` dedupe sin refcount roba el Pop entre sistemas
`Assets/Scripts/Player/PlayerActionManager.cs:249-274` *(verificado; detectado independientemente por dos revisores)*

`if (Top == mode) return;` ignora el segundo Push del mismo modo, pero el segundo sistema hará su Pop igualmente y eliminará la entrada del primero. `Cinematic` lo usan DialogueManager, SleepTrigger, CinematicSequencerBase y PlayVictorySequence; `Stunned` lo usan AerialKnockback y PlayerCarrySystem. Escenarios reales: victoria de combate con diálogo abierto → input desbloqueado en mitad del diálogo; diálogo abierto durante cinemática → jugador controlable en mitad de la cinemática.

**Fix:** refcount por modo, o permitir entradas repetidas en la pila (quitar el early-return; el Pop ya elimina solo una instancia).

##### C3. Teleport a anchor inexistente → jugador sin input permanentemente
`Assets/Scripts/Teleport/TeleportSystem.cs:211` + `Assets/Scripts/World/TeleportService.cs:99-116` *(verificado)*

`TeleportSequence` empuja `Cutscene`, deshabilita el input y espera `WaitUntil(() => transitionEnded)`, que depende de `OnTeleportEnded`. Pero `TeleportService.TeleportToAnchor` retorna temprano **sin emitir ningún evento** si `Inst` es null o el anchor no se encuentra (solo un LogWarning). Resultado: input muerto, fase Cutscene y `IsTeleporting=true` para siempre (bloquea además todos los teleports futuros).

**Fix:** emitir siempre `OnTeleportEnded` en los paths de fallo, más un timeout de seguridad en el `WaitUntil`.

##### C4. `NarrativeGraphStarter` restaura blackboards rancios en cada carga de escena → ítems duplicados (patrón INC-020)
`Assets/NarrativeGraph/Runtime/Integration/NarrativeGraphStarter.cs:98,159`

Restaura `preset.narrativeBlackboards` cada vez que una escena de gameplay se activa, pero ese snapshot solo se refresca al guardar en un SavePoint (`GameBootProfile.cs:715` ← `SavePoint.cs:168`). Secuencia normal: guardas → avanzas el grafo (recibes ítem vía `GiveInventoryItemNode`) → cambias de escena sin guardar → el blackboard retrocede al save: el flag `INV_GIVEN` desaparece pero el inventario no se revierte → **el nodo vuelve a entregar el ítem**. Diálogos sin `oneShotFlag` se repiten y el grafo se desincroniza del QuestManager.

**Fix:** capturar blackboards al preset en cada transición de escena, o restaurar solo una vez por sesión (tras load real), no en cada `Start()`.

##### C5. Interrumpir la cinemática de un NPC → corrutina zombie y secuenciadores colgados
`Assets/Scripts/Behaviour NPC/States/CinematicState.cs:562` + `NPCBehaviourManagerV2.cs:655-659` *(verificado)*

`Cleanup()` (salida forzada del estado) detiene la corrutina y restaura avoidance, pero **no marca `IsCompleted = true`** (solo `CleanupAndComplete` lo hace). `WaitForSequence` hace `while (!seq.IsCompleted) yield return null;` → si el NPC sale de `CinematicState` a mitad de secuencia, esa espera gira para siempre y el `onComplete` no dispara. Y hay una vía fácil de provocarlo: `NPCCombatLifecycleHandler.OnDamaged` llama `ForceEnterCombat` **sin comprobar `IsInCinematic`** — golpear a un NPC durante una cinemática cuelga los secuenciadores que encadenan pasos vía `onComplete` (MountainSequencer, ReinoExitBanterSequencer). Relacionado: `CheckTransitions` de CinematicState tampoco mira `WasDefeatedInCombat` → NPC que muere en cinemática queda atrapado en el estado.

**Fix:** `IsCompleted = true` en `Cleanup()`, gate de `IsInCinematic` en `OnDamaged`/`ForceEnterCombat`, y prioridad `WasDefeatedInCombat → DeadState` en `CheckTransitions`.

##### C6. Hitstops solapados dejan el juego en cámara lenta permanente
`Assets/Scripts/Core/Feedback/SimpleHitStopProvider.cs:18-29`

Cada `Co_HitStop` captura `original = Time.timeScale` al empezar y lo restaura al acabar, sin cancelar el anterior. Dos golpes en <0.2 s (trivial en combate): A captura 1.0, B captura el 0.1 que puso A → A restaura 1.0, B restaura 0.1 → **slow-mo permanente**. Además pelea con el menú de pausa y con `DeathCameraEffect` (que fuerza `timeScale = 1` incondicional al final, rompiendo una pausa activa). Misma familia: `NPCCombatLifecycleHandler.OnDestroy` fuerza `timeScale = 1` si no es 1 — descargar una escena con NPCs estando en pausa revierte la pausa.

**Fix:** árbitro central de timeScale (contador de efectos + baseline gestionado). Un solo servicio resuelve los 4 actores.

##### C7. Knockback aéreo interrumpido → input bloqueado para siempre
`Assets/Scripts/Attacks/AerialKnockbackReceiver.cs:147-289`

`LaunchRoutine` empuja `Stunned`, deshabilita el controller y pone el Rigidbody kinemático; la restauración está al final de la corrutina y **no hay `OnDisable`**. Si el componente se desactiva a mitad del arco (~0.6 s) — cinemática con `ModeRule.disableComponents`, muerte, cambio de escena — quedan: `Stunned` pushed para siempre, controller deshabilitado, RB sin gravedad y `_isLaunching=true` (bloquea futuros knockbacks). El propio proyecto tiene el patrón correcto en `PlayerFlyingController.OnDisable` y `PlayerSwimmingController.OnDisable`.

**Fix:** `OnDisable` que restaure RB/controller/rootMotion y haga `PopMode(Stunned)`.

---

#### ALTOS — rompen sistemas concretos o corrompen estado en escenarios alcanzables

##### A1. `ActiveCombatRegistry` retiene enemigos destruidos → player atrapado en modo combate
`Assets/Scripts/Attacks/ActiveCombatRegistry.cs:164` + `Player/PlayerBattleModeController.cs:311`

`Count` no limpia referencias fake-null. Un enemigo destruido sin `UnregisterNPC` (Destroy directo, descarga de escena aditiva — `ClearAll` solo se llama en GameOver) deja `Count>0` para siempre → Battle Mode + `ActionMode.Combat` permanentes (que además bloquea `Interact`). `InteractionDetector` ya se defiende con `CleanupDestroyedNPCs()`; los otros dos consumidores no. **Fix:** limpieza dentro de `Count` o auto-unregister en `OnDestroy` del NPC.

##### A2. `BossArenaController`: arena cerrada sin salida si el boss se destruye sin morir
`Assets/Scripts/Rooms/BossArenaController.cs:585-591`

Si el boss desaparece sin pasar por `Damageable.OnDied` (killzone, despawn, limpieza externa), el path de emergencia solo hace `started=false`: no reabre puertas, no llama `UnlockArea()` ni `RestoreBattleDisables()`, ni cierra la música de batalla → jugador encerrado con música infinita y sin posibilidad de re-disparar el trigger. **Fix:** en ese path, restaurar puertas/área/disables y `AudioService.EndBattleById`.

##### A3. Pooling: devolución doble corrompe el pool y los parents destruidos lo agotan
`Assets/Scripts/Core/Pooling/ObjectPool.cs:114-121` *(verificado)* + `VfxPoolService.cs:74-119`

`Return()` detecta la devolución doble pero **aun así hace push** → la misma instancia dos veces en la pila → dos `Get()` devuelven el mismo Transform. Y `VfxPoolService.Play` con `parent` externo: si el parent se destruye, el VFX muere con él pero `_inUse` del ObjectPool lo cuenta para siempre → tras `MaxPoolSizePerPrefab` (64) instancias muertas, ese VFX **deja de verse el resto de la sesión**. **Fix:** `if (!_inUse.Remove(obj)) return;` y, en la rama `instance == null` del Update del servicio, purgar también `_instancePool`/`_inUse`.

##### A4. Save corrupto arranca el juego en estado indefinido
`Assets/Scripts/Core/GameBootService.cs:280` *(verificado)*

En el arranque normal, `_profile.LoadProfile(_saveSystem)` **ignora el valor de retorno**. Si el JSON está corrupto (cierre forzado a mitad de escritura), `LoadProfile` devuelve false y no hay fallback al `defaultPlayerPreset`: el juego arranca con el runtimePreset residual, sin HP/inventario/flags coherentes. **Fix (2 líneas):** `if (!_profile.LoadProfile(_saveSystem))` → rama del preset por defecto.

Relacionado (MEDIO): `SaveSystem.Save` hace `File.Delete` + `File.Move` *(verificado)* — hay una ventana sin ningún save en disco; usar `File.Replace`, o leer `save.json.tmp` como fallback en `Load()`. Y `PlayerSaveData` no tiene campo de versión de esquema: cualquier renombrado de campo hará que saves antiguos carguen en silencio con defaults. Añadir `saveVersion` antes de la demo de Steam.

##### A5. Señales narrativas sticky consumidas por el sistema equivocado
`Assets/NarrativeGraph/Runtime/Integration/DefaultNarrativeSignals.cs:350-361` + `NPCInteractiveNarrativeExecutor.cs:342-349`

`OnCustom` consume `_pending`/`_raised` en el momento de suscribirse, y el executor Interactive se re-suscribe durante la carga **antes** de que los runners restauren blackboards y suscriban sus `WaitCustomEventNode`. Una señal pendiente puede ser consumida por el executor (que luego la ignora por `singleUse`/preset) → el `WaitCustomEventNode` del grafo nunca la ve → grafo bloqueado. Es la versión runtime del conflicto que el `CrossSystemNarrativeValidator` solo detecta en editor. **Fix:** consumo por-suscriptor, o que el executor re-emita la señal cuando decide ignorarla.

##### A6. Ramas fork del grafo: `Exit()` nunca se llama y el estado de suscripción vive en el asset compartido
`Assets/NarrativeGraph/Runtime/Graph/NarrativeRunner.cs:327-457`

Las ramas fork hacen `Enter` de cada nodo pero jamás `Exit`; `StopExecution()` solo hace `Exit` del nodo del camino principal. Nodos en espera dentro de ramas (`WaitQuestCompleteNode._cb`, `StartBattleNode._onBattleWonCb`) quedan suscritos tras `StopAllRunners`/recarga — y esos campos viven en el `NarrativeNode` serializado del asset compartido, así que una re-entrada pisa `_cb` y el `Exit` posterior ya no puede desuscribir el callback viejo → **callbacks fantasma de sesiones muertas ejecutando side effects reales** (completar quests al ganar una batalla de la sesión nueva). **Fix:** rastrear los nodos activos por rama y hacer su `Exit` en `StopExecution`; mover el estado de suscripción a un diccionario por runner.

Relacionados en el mismo archivo: el resume de forks re-ejecuta el `Enter` del nodo fork en cada carga (si es `RaiseCustomEventNode`, re-emite la señal en cada load); y `RequireInventoryItemNode.HandleMissing` usa `ForceJumpToOutput` (mecanismo del camino principal) desde ramas → rama nunca marcada `__DONE__` y `__currentNodeGuid` corrupto; además con `consumeOnSuccess` + `completeQuestInstead` el ítem puede consumirse dos veces (el guard `_itemsConsumedForQuest` no cubre el consumo hecho por el nodo).

##### A7. `Transition.cs`: fuga de `sceneLoaded` + disparo prematuro con cargas aditivas
`Assets/Scripts/Core/EasyTransitions/Scripts/Transition.cs:99`

Suscribe `SceneManager.sceneLoaded` y no existe `OnDestroy` que desuscriba (el objeto muere con `Destroy(gameObject, destroyTime)`). En un proyecto multi-escena **aditiva**, además, cualquier carga aditiva durante la espera dispara `OnSceneLoad` prematuramente (no filtra por `LoadSceneMode`). **Fix:** `OnDestroy` desuscribiendo + ignorar `mode == Additive`. En la misma familia: `TeleportService.cs:226` y `CinematicSequencerBase.cs:266-279` dejan handlers de `onTransitionCutPointReached` suscritos al TransitionManager persistente si la transición se interrumpe → la siguiente transición de cualquier sistema puede teleportar al jugador al destino antiguo o ejecutar `BeginCinematic()` de un sequencer destruido. Desuscribir en `OnDestroy`/finally.

##### A8. `DayNightCycle`: oscurecimiento por lluvia compuesto exponencialmente y luz clavada tras la lluvia
`Assets/Scripts/World/DayNightCycle.cs:379-386` *(verificado)*

`LateUpdate` lee `directionalLight.intensity` (ya oscurecida el frame anterior) y la vuelve a multiplicar cada frame — exactamente el bug que ya se corrigió para la niebla con `_baseFogDensity` (el comentario de las líneas 248-253 lo documenta), pero sin aplicar a la luz. En ~4 frames la luz cae al suelo (0.28) y al terminar la lluvia **se queda ahí** hasta la siguiente transición de periodo. **Fix:** cachear `_baseLightIntensity` igual que la niebla.

##### A9. `SimpleCinematicDirector`: estado global compartido entre instancias
`Assets/Scripts/Cinematics/SimpleCinematicDirector.cs:214-240`

`OnDisable`/`OnDestroy` deciden con el flag **estático** `IsAnyCinematicPlaying`: si el director A reproduce y un director B (que nunca reprodujo) se desactiva por descarga de escena, B resetea el flag global, fuerza `timeScale=1` y cierra el override de A. La limpieza de interrupción además no restaura HUD/minimapa ni prioridad de cámara. Y `PlayRoutine` no está blindada con try/finally (a diferencia de `CinematicSequencerBase.Co_SequenceGuarded`, que sí lo está): una NRE deja flag global, HUD y cámara en estado de cinemática. El campo `lockPlayer` no se usa en ninguna parte. **Fix:** flag de instancia, rutina de restauración completa, y el patrón guarded de la clase base.

##### A10. Muerte y revive del player sin limpiar contexto
`Assets/Scripts/Player/PlayerHealthSystem.cs:182-225, 363-408, 501-513`

`TakeDamage`/`Die` no comprueban Cinematic (un AoE residual puede matar al player en mitad de una cinemática y disparar el GameOver dentro de ella); `Die()`/`ReviveInternal` no tocan la pila de modos (morir con `Flying`/`Carrying` pushed los deja vivos de cara al respawn) ni conceden invulnerabilidad temporal al revivir. Y `InvulnerabilityFlashCoroutine` apaga renderers: si el GO se desactiva en el medio ciclo apagado, **el player queda invisible permanente** (nadie llama a `ResetDamageVisuals` al reactivar). **Fix:** god-frame en Cinematic, reset de pila en muerte/revive, `OnDisable → ResetDamageVisuals()`.

##### A11. Bosses: pasada de higiene propia
- `GolemBossAI.cs` — muerte a mitad de salto/embestida deja cadáver flotando (agente desactivado que `StopAgent` no puede parar) y `animator.speed` en 1.8; onda expansiva con `OverlapSphereNonAlloc` **sin layermask** y buffer de 32 en un mundo donde todo vive en `Default` → en zonas densas el player puede quedar fuera del buffer y no recibir daño; reflection en runtime (`GetMethod("Shake")`) cuando el propio archivo ya usa `FeedbackService.CameraShake`; `SetDestination` por frame en embestida.
- `ImpDemonAI.cs` — `PlayAnimation` hace `animator.Play(hash, layer, 0f)` **cada frame** sin guard → reinicia la animación en el frame 0 continuamente (animación congelada + coste). `Spider1AI.cs:387` tiene el guard correcto: portarlo. VFX de casteo/lluvia instanciados sin `Destroy` programado ni pool.
- `Spider1AI.cs` — `StopCoroutineSafe(AttackPlayer())` crea un enumerator nuevo y el helper está vacío: la "cancelación" no cancela nada; el daño se aplica aunque la araña esté en stun. `SetDestination` cada frame en persecución (y las arañas atacan en grupo).

##### A12. Swap de personaje sin gating por estado
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


---

## 20. Convenciones de documentación del proyecto

Desde el 12 de agosto de 2026, este proyecto mantiene su documentación en un único sitio para que no se disperse en archivos `.md` sueltos que nadie vuelve a mirar. La regla, a partir de ahora:

- **`TDD.md` (este documento) es la fuente de verdad única** para toda la documentación técnica: arquitectura, sistemas, reglas de código, bugs conocidos, troubleshooting, diseños en curso, checklists de proceso y auditorías.
- **Cualquier `.md` nuevo que documente algo de sustancia** (una auditoría, un análisis de diseño, una decisión de arquitectura, un checklist) **se añade como sección nueva de este documento**, no como archivo suelto en la raíz del proyecto. Al añadirlo: crear la sección al final (o dentro de la sección temática que corresponda si es una ampliación de algo ya documentado), actualizar el índice, y evitar repetir contenido que ya exista en otra sección — enlazar a ella en su lugar de copiarlo.
- **Excepciones — se quedan como archivos aparte, pero cortos y sin contenido duplicado:**
  - **`README.md`** — la portada del repositorio, lo primero que ve alguien en GitHub. Overview y enlaces, nunca el detalle técnico.
  - **`AGENTS.md` / `CLAUDE.md`** — herramientas de IA (Claude Code, Cursor, etc.) los leen **automáticamente** como contexto de proyecto en cada sesión; no son solo documentación para humanos y por eso no se retiran ni se fusionan aquí dentro. Se mantienen como resumen corto de las reglas no negociables con pointers a este documento para el detalle — nunca como copia completa de §10/§12. Si TDD.md cambia una regla no negociable o un invariante narrativo, hay que reflejar el resumen en estos dos archivos también (se mantienen sincronizados, no es "documentación muerta").
- Cualquier otro `.md` de sustancia que hoy exista o se cree en el futuro (una auditoría, un checklist, un análisis) va como sección de `TDD.md`, no como archivo nuevo en la raíz.
