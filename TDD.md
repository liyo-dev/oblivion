# TDD — El Sendero de las Estrellas

**Motor:** Unity 2022.3+  
**Pipeline:** URP  
**Input:** Unity Input System (nuevo) + Invector (movimiento base del jugador)  
**Última revisión:** Mayo 2026

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

---

## 1. Quick Start

### Iniciar el proyecto

1. Abre Unity 2022.3 o superior.
2. La escena de entrada es `Assets/Scenes/Systems/Start.unity`.
3. Todos los managers persistentes viven en Start.

### Probar una escena de mundo directamente

Puedes hacer Play desde cualquier escena (MainWorld, Woods, etc.) sin configuración manual:

- `Assets/Editor/AutoBootstrapOnPlay.cs` detecta automáticamente que no estás en Start y carga Start de forma aditiva antes de entrar en PlayMode.
- El sistema inicializa todos los managers y posiciona al jugador en el último anchor guardado (o el del preset de testing activo).

**Requisito:** Start.unity debe estar en Build Settings (posición 0).

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

### Instancias y pools

- Los proyectiles deberían usar un pool (`ObjectPool<T>`). Actualmente usan `Instantiate/Destroy`. El GolemBossAI tiene 9 TODOs al respecto.
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

### Críticos

| # | Sistema | Archivo | Descripción |
|---|---------|---------|-------------|
| C1 | Audio | `AudioService.cs` | `StopAllCoroutines()` al cambiar música destruye corrutinas del pool SFX. Los AudioSource nunca se devuelven al pool. Ver sección 6. |
| C2 | Save | `SaveSystem.cs` | Escritura no atómica: crash durante save corrompe el archivo. Ver sección 9. |
| C3 | Quests | `QuestManager.cs:921` | `FindObjectsByType` llamado en cada evento de inventario. O(n×m) por recogida de item. |
| C4 | Player | `MagicCaster.cs:53` | `new List<MagicSlot>(_slotCooldowns.Keys)` en `Update()` cada frame. |
| C5 | Player | `PlayerBattleModeController.cs:278` | 3× `GetComponentInChildren` en `Update` por cada collider detectado. |
| C6 | Player | `MagicProjectil.cs:229` | `Physics.OverlapSphereNonAlloc` en `Update` por cada proyectil en vuelo como fallback de colisión. |

### Importantes

| # | Sistema | Archivo | Descripción |
|---|---------|---------|-------------|
| I1 | Diálogo | `DialogueManager.cs` | Grace period (0.3s) reseteado en cada línea. Input parece no responder. |
| I2 | Diálogo | `DialogueManager.cs:906` | `GetComponent` múltiple por cada línea. Cachear en `StartDialogue`. |
| I3 | UI | `MagicSlotsUI.cs` | `StartCoroutine(FlashSlotReady)` lanzado en Update cada frame que la condición es verdadera. Acumula corrutinas. |
| I4 | UI | `PlayerHealthUI.cs` | `AddListener` sin `RemoveListener` previo en `OnEnable`. Listeners duplicados tras Enable/Disable. |
| I5 | UI | `CollectiblePopupQueue.cs` | Locks innecesarios en código single-thread. `Canvas.ForceUpdateCanvases()` bloquea todos los canvases. |
| I6 | UI | `MenuManager.cs:71` | `new HashSet<MenuKind>(allowed)` en `AnyOpenExcept()` llamado desde Update. |
| I7 | Shop | `ShopUI.cs:361` | Reflection para acceder a `currencyItem`. Exponer propiedad pública en ShopController. |
| I8 | Player | `PlayerActionManager.cs:66` | Reflection para leer `abilities.magic` (bool). Acceso directo. |
| I9 | Player | `PlayerLevitationController.cs:629` | Reflection + boxing en `Update` para leer propiedades estáticas. |
| I10 | Core | `GameBootService.cs` | Corrutina `while (QuestManager.Instance == null) yield return null` sin timeout. |
| I11 | Core | `SaveSystem.cs` | Escritura no atómica. Save puede corromperse. |
| I12 | Environment | `FogZone.cs` | Variables estáticas sin reset entre escenas. Referencia stale al objeto de escena anterior. |
| I13 | Cinematics | `AdditiveSceneCinematic.cs:596` | `PlayAndBlock()` puede bloquearse indefinidamente si `Play()` retorna early. |
| I14 | Cinematics | `SimpleCinematicDirector.cs` | `Time.timeScale` no se restaura si se para la cinemática con `StopCoroutine` sin destruir el objeto. |
| I15 | Player | `PlayerCarrySystem.cs:99` | `Invoke` no cancelado en `OnDisable`. NullReferenceException si el GO se destruye durante el delay. |
| I16 | Proyectiles | `MagicProjectil.cs:285` | Daño puede aplicarse dos veces por frame (OnTriggerEnter + CheckEnemyProximity en el mismo frame). |
| I17 | Inventory | `Inventory.cs:328` | `Resources.LoadAll<ItemData>("")` como fallback — carga todos los assets de Resources. Cachear resultado. |

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
