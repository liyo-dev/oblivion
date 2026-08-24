# AGENTS.md — El Sendero de las Estrellas

RPG de acción/aventura en Unity 6 (6000.5.4f1) + URP 17.5. Proyecto indie en solitario (Raúl Báez).

**Toda la documentación técnica detallada vive en `TDD.md` — fuente de verdad única.** Este archivo es un resumen corto de lo no negociable para quien (humano o IA) toque código; no sustituye a TDD.md, y si algo de aquí y de TDD.md alguna vez difieren, manda TDD.md. Detalle completo de cada punto de abajo, con ejemplos y contexto: ver la sección de TDD.md indicada entre paréntesis.

**Toda la documentación de diseño y narrativa (historia, guión, fichas de personaje, balance de hechizos, estado de diálogos) vive en `GDD.md` — fuente de verdad única, desde el 24 ago 2026.** Sustituye al antiguo Google Doc "GDD" v1.0, que se quedó desactualizado (ejemplo: contradecía una corrección de lore de Liam ya confirmada). Antes de escribir o corregir diálogos, hechizos nuevos, o cualquier ficha de personaje, consultar `GDD.md` primero.

**Todas las incidencias (bugs, mejoras, funciones) viven en `TRACKER.md` — fuente de verdad única, desde el 24 ago 2026.** Sustituye al Tracker en Google Sheets (no se podía escribir en él directamente desde una sesión de IA, solo leer) y fusiona la numeración `INC-XXX` que hasta hoy llevaba también, por separado, el catálogo de bugs de `TDD.md` § 13. Al encontrar o arreglar una incidencia: añadir/actualizar su fila en `TRACKER.md` directamente (mismo commit que el fix), nunca dejarla pendiente de pegar a mano.

---

## 1. Entrada al proyecto (detalle: TDD.md § 1)

- **Escena de entrada:** `Assets/Scenes/Systems/Start.unity` — contiene todos los managers persistentes (`DontDestroyOnLoad`). Siempre debe estar cargada.
- Para testear cualquier escena directamente: abrir y dar Play. `AutoBootstrapOnPlay.cs` carga `Start` aditivamente de forma automática.
- **Script Execution Order crítico:** `GameBootService` -1000 → `PlayerService` -900 → `ServiceLocator` -800 → `WorldBootstrap` +200.

## 2. Reglas de código — no negociables (detalle: TDD.md § 12)

**Nunca en `Update` / `LateUpdate` / `FixedUpdate`:**
`FindObjectOfType`/`FindObjectsByType` (usar registros) · `GetComponent` sin cachear (cachear en `Awake`) · `Camera.main` sin cachear · `new List<T>()`/`.ToList()`/`.Where(...).ToList()` (iterar directo) · `LayerMask.GetMask(...)` sin cachear · `animator.parameters` (getter, cachear hashes) · `StartCoroutine(...)` sin comprobar si ya hay una corriendo · `SetActive(...)` sin guard de estado previo.

**Physics:** siempre `OverlapSphereNonAlloc` con buffer pre-alocado, nunca `Physics.OverlapSphere(...)`.

**VFX de un solo uso** (impacto, explosión, despawn): siempre `VfxPoolService.Instance.Play(prefab, pos, rot, lifetime)`, nunca `Instantiate(...); Destroy(fx, t)` directo.

**Personajes vs. geometría en raycasts:** los personajes no tienen capa propia (viven en `Default`, igual que la geometría). Para distinguirlos en un raycast de obstrucción, comprobar `root.GetComponent<NPCSimpleAnimator>() != null` (lo tienen el player y todos los NPCs, ningún prop de escenario).

**Reflection:** no usar `System.Reflection` en código de runtime frecuente (lento y frágil en IL2CPP).

**Logging:** todo `Debug.Log` de diagnóstico bajo `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

**Idioma:** comentarios, documentación y mensajes de commit **en español**.

## 3. Arquitectura clave (detalle: TDD.md § 2)

Multi-escena aditiva (`Start` persiste siempre) · `ServiceLocator` para singletons globales · ScriptableObjects como datos, nunca lógica · eventos C# (`Action<T>`) para comunicación desacoplada · `DontDestroyOnLoad` solo en managers de `Start`.

**Patrón obligatorio en singletons con estado estático** (evita contaminación entre sesiones de PlayMode en el editor):
```csharp
#if UNITY_EDITOR
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() { _instance = null; OnMyStaticEvent = null; }
#endif
```

**FSM de NPCs:** `NPCBehaviourManagerV2 → NPCBrain → NPCStateContext → INPCState` (`Assets/Scripts/Behaviour NPC/`).

**Stack de modos del jugador** (`PlayerActionManager`): `PushMode(ActionMode.X)` / `PopMode(ActionMode.X)` — siempre en pareja.

## 4. Invariantes del grafo narrativo — NO violar (detalle y las 5 reglas completas: TDD.md § 10)

El grafo narrativo (`MainNarrative.asset`) y sus runners son `DontDestroyOnLoad`. Antes de tocar `GameBootService.PrepareActivePreset()`/`ReloadTestPreset()`, `NarrativeRunner.StartFromStartNode`/`StartFromNode`, `WaitQuestCompleteNode`, la fork detection, o `DefaultNarrativeSignals._raised` — **leer TDD.md § 10 primero**. Son las reglas más fáciles de romper sin darse cuenta y las más caras de depurar después.

## 5. Convivencia Interactive ↔ Grafo narrativo (política completa: TDD.md § 10)

Dos motores narrativos en paralelo a propósito (`NarrativeGraph` activo, `NPCInteractiveNarrativeExecutor` **congelado**, no se fusionan). Todo NPC/quest nueva se construye en `NarrativeGraph`. Antes de dar por buena una entrega, correr `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)`.

## 6. Bugs conocidos y estado del proyecto

Seguimiento (ID, estado, prioridad) en `TRACKER.md`. Detalle técnico largo (causa raíz, código) de los bugs encontrados en sesiones de IA en TDD.md § 13 (bugs pendientes) y § 19 (auditorías), enlazado desde su fila en `TRACKER.md`. No asumir que un bug de un catálogo viejo sigue vivo — verificar el archivo actual primero.

## 7. Dónde encontrar información

| Qué | Dónde |
|-----|-------|
| Arquitectura completa, API de sistemas, bugs, diseños, auditorías | `TDD.md` |
| Historia, guión técnico, fichas de personaje, balance de hechizos, estado de diálogos | `GDD.md` |
| Incidencias (bugs/mejoras/funciones) y su estado | `TRACKER.md` |
| Portada del repo | `README.md` |
| Configuración de NPCs, quests, hechizos | Assets SO en `_NPCs/`, `_QUEST/`, `_SPELLS/` |
| FSM de NPCs | `Assets/Scripts/Behaviour NPC/` |
| Grafo narrativo (runtime) | `Assets/NarrativeGraph/Runtime/` |
| Debug visual en runtime | F3 (NPCs), F4 (panel general) |

---

**Nota de mantenimiento:** este archivo (y su gemelo `CLAUDE.md`) es un resumen deliberadamente corto para que herramientas de IA lo carguen como contexto sin gastar espacio de más. Si cambia una regla no negociable o un invariante narrativo en `TDD.md`, algo estructural en `GDD.md` (personajes, sistemas de balance), o el criterio de seguimiento de incidencias en `TRACKER.md`, actualizar el resumen aquí también — no dejar que se desincronicen.
