# Addendum — Código y sistemas obsoletos
**Fecha:** 7 de agosto de 2026 · Complementa a `AUDITORIA_CODIGO_2026-08-07.md`. **Método:** cada hallazgo de este documento está verificado contra el proyecto real (no solo contra el código): para todo lo que se marca "sin uso" comprobé el GUID del script en `Assets/Scenes`, `Assets/Prefabs` y las carpetas de datos (`_NPCs`, `_QUEST`, `_DIALOGUES`, `_BootProfile`, `NarrativeGraph`, `Resources`) para confirmar que ningún GameObject ni asset lo referencia. Nada de esta lista es "probablemente muerto": o está confirmado muerto, o está confirmado vivo y se dice explícitamente.

---

## 1. Archivos completamente vacíos — cero uso confirmado, borrado seguro

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

## 2. Sistema completo escrito y nunca conectado: `NPCMovementController`

`Assets/Scripts/Behaviour NPC/Movement/NPCMovementController.cs`

Este archivo no está vacío — es una clase completa y cuidada (eventos `OnDestinationReached`/`OnMovementBlocked`/`OnMovementStarted`, `[RequireComponent(typeof(NavMeshAgent))]`, comentario de cabecera: *"Sistema centralizado de movimiento para NPCs. TODO el movimiento de NPCs (Combat, Party, States) pasa por aquí. CERO delays, CERO yield return null, sistema profesional y robusto."*).

El problema: **no lo usa nadie**. Ninguna otra clase del proyecto lo menciona, y no está adjunto a ningún GameObject en ninguna escena ni prefab (confirmado por GUID). El movimiento real de los NPCs pasa hoy por `NavMeshAgentUtility` y la lógica propia de cada estado de la FSM (`IdleState`, `WanderState`, `FollowPlayerState`, etc.), tal como se documenta en el informe principal.

Todo indica que este archivo es un intento de centralizar el movimiento que se escribió pero nunca se terminó de adoptar — el proyecto siguió con el patrón descentralizado por estado. No es peligroso tal cual está (no se ejecuta), pero es ruido: cualquiera que lo encuentre puede asumir que "así es como se mueve un NPC" y perder tiempo, o peor, empezar a usarlo en paralelo al sistema real y crear justo el tipo de sistema-fantasma-duplicado del que hablas.

**Recomendación:** o se borra, o si la idea de centralizar sigue viva, se anota con un comentario claro tipo `// EXPERIMENTAL — no conectado, ver Behaviour NPC/States/ para el movimiento real` para que no se confunda con código en producción.

---

## 3. Nodos del grafo narrativo marcados `[Obsolete]` — la higiene ya existe, un paso más la completa

`Assets/NarrativeGraph/Runtime/Graph/NodeTypes/`: `DeliverItemProximityNode`, `DeliverQuestCompleteNode`, `BranchBoolNode`, `ActivateGameObjectNode`, `UnlockTriggerNode`, `PlayTimelineNode`, `WaitBattleWinNode`, `OfferQuestNode`.

Esto es al revés de un problema: es la parte del proyecto donde la deprecación está **mejor hecha**. Cada uno tiene el atributo `[Obsolete("...")]` con una explicación de qué usar en su lugar, y `NarrativeGraphWindow` los filtra del menú "Añadir Nodo" para que nadie los arrastre por error a un grafo nuevo. `BranchBoolNode` incluso documenta en un comentario por qué está roto (*"no bifurca de verdad... confirmado sin uso en ningún grafo del proyecto (Agosto 2026)"*).

Lo comprobé contra los 7 assets reales del grafo (`MainNarrative.asset` + `MainNarrative_Cap1` a `Cap6`, que son los únicos grafos del proyecto): **ninguno de estos 8 tipos de nodo aparece en ningún grafo actual.** No son compatibilidad hacia atrás para datos que sigan vivos — son cadáveres ya completamente aislados.

**Recomendación:** dado que confirmadamente no hay ningún dato que dependa de ellos, se pueden borrar del todo (no solo marcar `[Obsolete]`) sin perder nada. Si prefieres quedarte con el margen de seguridad de "por si acaso", déjalos como están — el patrón actual ya es correcto y no genera ningún riesgo, solo ocupa espacio.

---

## 4. Herramientas de editor de un solo uso — no son bugs, pero son candidatas a archivar

`Assets/Editor/MigrateNarrativeConfigToBehaviourManager.cs` (migra campos legacy de `NPCInteractiveNarrativeConfig` a `NPCBehaviourManagerV2` en todas las escenas/prefabs) y `Assets/Editor/ReserializeOldAssets.cs` (reserializa materiales viejos de un pack de la Asset Store para silenciar warnings de consola) son utilidades de migración de un solo uso, con su propio `[MenuItem]` en el menú "El Sendero". Si ya ejecutaste la migración de NPCs y no tienes más warnings de reserialización pendientes, ninguna de las dos hace falta ya.

No son peligrosas si se quedan (no se ejecutan solas), pero si algún día limpias el menú de Editor, son las primeras candidatas — junto con el resto de setup tools de un solo uso que aparecieron en el barrido (`NPCFacePartsSetup`, `NPCIdleVariationSetup`, `CrystalBallVisionSetup`, `ModularCharacterBaker`, `StartProductionBake`, `QuickDemoBake`, `SettingsMenuCreator`, `QuestMenuCreator`): todas son herramientas de construcción de contenido normales en un proyecto indie, no deuda técnica.

---

## 5. Verificado como "no obsoleto" a pesar de las apariencias: `BootLoader`

`Assets/Scripts/Core/BootLoader.cs` — antes de escribir este addendum parecía un candidato obvio: un `MonoBehaviour` genérico de 30 líneas, con nombre parecido a `GameBootService` (el orquestador real y documentado en CLAUDE.md), sin ninguna otra clase del código que lo referencie, y que hace `SceneManager.LoadScene(sceneToLoad)` — carga **no aditiva** — algo que en un proyecto multi-escena como este suena a bandera roja inmediata.

Lo comprobé contra la escena real y **está vivo y en uso**: hay un GameObject `START_BootLoader` en `Assets/Scenes/Systems/Start.unity`, activo, con `sceneToLoad: MainMenu` y `delaySeconds: 0`. Es el mecanismo que lleva de la escena `Start` al `MainMenu` una vez arrancan los managers. No es peligroso: `GameBootService` tiene execution order -1000 y hace todo su trabajo de arranque en `Awake()`, que Unity garantiza que se ejecuta (para todos los objetos de la escena) antes que cualquier `Start()` — incluido el de `BootLoader` — así que no hay condición de carrera. Y como todos los managers persistentes están en `DontDestroyOnLoad`, la carga no-aditiva de `MainMenu` no se los lleva por delante.

**El único hallazgo real aquí es documental:** ni `CLAUDE.md` ni `TDD.md` mencionan `BootLoader` como parte del flujo de arranque — solo documentan `GameBootService`/`AutoBootstrapOnPlay`. Si algún día tocas el arranque, es fácil no saber que este componente existe y que es él quien dispara la transición a `MainMenu`. Vale la pena añadir una línea a CLAUDE.md §1.

---

## 6. Los dos motores narrativos (Grafo vs. Interactive) — dato objetivo, no juicio

Esto ya lo documenta tu propio CLAUDE.md §7 como decisión de arquitectura aceptada ("un intento de unificarlos rompió el juego en Agosto 2026; no se intenta fusionar"), así que no lo reporto como problema. Solo te dejo el dato objetivo por si te sirve para decidir cuánto pesa mantenerlo: de los NPCs con `NPCBehaviourManagerV2` en prefabs/`_NPCs`, **13 siguen usando el executor Interactive** (`NPCInteractiveNarrativeExecutor`/`NPCInteractiveNarrativeConfig`) frente al total de NPCs con FSM. No es un sistema residual de dos o tres NPCs sueltos — sigue siendo una parte real y viva del contenido, así que la política de "congelado pero no migrado" del CLAUDE.md sigue siendo la decisión correcta: no es candidato a borrado, solo a no crecer más (que es exactamente lo que ya dice tu documentación).

---

## Resumen accionable

| Acción | Archivos | Riesgo de borrar |
|---|---|---|
| Borrar ya | 8 archivos vacíos (§1) + carpeta `Initialization/` | Ninguno — verificado sin referencias |
| Borrar o dejar como está (ya bien marcado) | 8 nodos `[Obsolete]` del grafo (§3) | Ninguno — verificado sin uso en ningún grafo |
| Decidir: borrar o marcar claramente como experimental | `NPCMovementController.cs` (§2) | Ninguno — verificado sin uso |
| Archivar cuando confirmes que ya no hacen falta | `MigrateNarrativeConfigToBehaviourManager.cs`, `ReserializeOldAssets.cs` (§4) | Bajo — son ejecutables manuales, no se llaman desde código |
| Documentar en CLAUDE.md, no tocar el código | `BootLoader.cs` (§5) | N/A — está vivo y funcionando |
| No tocar — decisión ya correcta | Dualidad Grafo/Interactive (§6) | N/A |

Nada de lo encontrado aquí es urgente ni arriesgado — es limpieza de bajo riesgo. Lo más valioso es el `NPCMovementController`: si alguna vez alguien (tú u otra persona ayudando en el proyecto) lo encuentra y asume que ahí es donde hay que tocar el movimiento de NPCs, perderá tiempo con un sistema que no hace nada.
