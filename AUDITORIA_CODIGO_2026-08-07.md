# Auditoría de código — El Sendero de las Estrellas
**Fecha:** 7 de agosto de 2026 · **Ámbito:** 530 archivos C# (Assets/Scripts, Assets/NarrativeGraph, Assets/Editor) · **Método:** 5 revisiones paralelas por subsistema + barrido automático de patrones + verificación manual de los hallazgos críticos (todos los citados como críticos han sido comprobados línea a línea sobre el código actual).

---

## Veredicto general

El proyecto está en buena forma. La disciplina micro es notable y muy por encima de lo habitual en un proyecto indie: cero `OverlapSphere` sin NonAlloc en todo el código, buffers cacheados, hashes de animator, `sqrMagnitude`, ResetStatics presente en la gran mayoría de singletons, y las correcciones C1–C6 y la pasada "Fase 2" están confirmadas en el código real. Los invariantes del grafo narrativo (CLAUDE.md §4) **se cumplen hoy**: test mode no mezcla JSON, `ReloadTestPreset` sigue la secuencia correcta, `StartFromNode/StartFromStartNode` llaman a `StopExecution()` primero, y `_raised` se preserva bien.

El punto débil no está en el rendimiento por frame (que está sano) sino en **el ciclo de vida de las interrupciones**: qué pasa cuando una corrutina que dejó estado global a medias (input bloqueado, timeScale alterado, renderers apagados, puertas cerradas) muere porque algo la interrumpe — muerte, cambio de escena, cinemática, desactivación del GameObject. Ese patrón se repite en al menos 8 sistemas distintos y es el origen de casi todos los críticos de abajo.

Hay 4 temas transversales que, arreglados una vez, eliminan familias enteras de bugs:

1. **`PushMode` sin refcount** — dos sistemas que empujan el mismo `ActionMode` se roban el Pop entre sí (detalle en C2). Un solo fix arregla conflictos diálogo↔cinemática↔victoria↔stun.
2. **`Time.timeScale` sin árbitro** — lo tocan al menos 4 actores sin coordinarse (hitstop, muerte, cinemáticas, OnDestroy de NPCs). Un servicio central con contador/baseline elimina el "slow-mo permanente" y el "pausa rota".
3. **Corrutinas que restauran estado al final sin `OnDisable` de seguridad** — knockback aéreo, secuencia de victoria, cast con carga, parpadeo de invulnerabilidad, fades. El patrón correcto ya existe en el propio proyecto (`PlayerFlyingController.OnDisable`, `CinematicSequencerBase.Co_SequenceGuarded`): replicarlo.
4. **Logging sin guarda `#if UNITY_EDITOR || DEVELOPMENT_BUILD`** en rutas calientes de combate — es la mayor penalización de rendimiento evitable en builds (cientos de allocs de string por segundo con varios NPCs peleando). Viola la propia regla §2 del proyecto.

---

## CRÍTICOS — pueden colgar una partida en flujos normales de juego

### C1. Reentrada en `DialogueManager.StartDialogue` → grafo narrativo colgado para siempre
`Assets/Scripts/Dialogue/DialogueManager.cs:319` *(verificado)*

`StartDialogue` no comprueba `IsOpen`: sobrescribe `_current` y `_onEnd` **sin invocar el callback del diálogo anterior**. `PlayDialogueNode` (`NarrativeGraph/Runtime/Graph/NodeTypes/PlayDialogueNode.cs:81-89`) espera `while (!completed)` sobre ese callback. Escenario real: al completarse una quest reaccionan a la vez el grafo (siguiente `PlayDialogueNode`) y la post-action de `NPCQuestActionExecutor` (que también abre diálogo, con ventanas de 0.5 s en su chequeo de `IsOpen`). El que llega segundo pisa al primero → la rama del grafo queda bloqueada eternamente. Es el punto único de fallo donde convergen grafo, Interactive y post-actions.

**Fix:** si `IsOpen`, encolar o rechazar; y si se decide pisar, invocar el `_onEnd` anterior antes de sustituirlo.

### C2. `PushMode` dedupe sin refcount roba el Pop entre sistemas
`Assets/Scripts/Player/PlayerActionManager.cs:249-274` *(verificado; detectado independientemente por dos revisores)*

`if (Top == mode) return;` ignora el segundo Push del mismo modo, pero el segundo sistema hará su Pop igualmente y eliminará la entrada del primero. `Cinematic` lo usan DialogueManager, SleepTrigger, CinematicSequencerBase y PlayVictorySequence; `Stunned` lo usan AerialKnockback y PlayerCarrySystem. Escenarios reales: victoria de combate con diálogo abierto → input desbloqueado en mitad del diálogo; diálogo abierto durante cinemática → jugador controlable en mitad de la cinemática.

**Fix:** refcount por modo, o permitir entradas repetidas en la pila (quitar el early-return; el Pop ya elimina solo una instancia).

### C3. Teleport a anchor inexistente → jugador sin input permanentemente
`Assets/Scripts/Teleport/TeleportSystem.cs:211` + `Assets/Scripts/World/TeleportService.cs:99-116` *(verificado)*

`TeleportSequence` empuja `Cutscene`, deshabilita el input y espera `WaitUntil(() => transitionEnded)`, que depende de `OnTeleportEnded`. Pero `TeleportService.TeleportToAnchor` retorna temprano **sin emitir ningún evento** si `Inst` es null o el anchor no se encuentra (solo un LogWarning). Resultado: input muerto, fase Cutscene y `IsTeleporting=true` para siempre (bloquea además todos los teleports futuros).

**Fix:** emitir siempre `OnTeleportEnded` en los paths de fallo, más un timeout de seguridad en el `WaitUntil`.

### C4. `NarrativeGraphStarter` restaura blackboards rancios en cada carga de escena → ítems duplicados (patrón INC-020)
`Assets/NarrativeGraph/Runtime/Integration/NarrativeGraphStarter.cs:98,159`

Restaura `preset.narrativeBlackboards` cada vez que una escena de gameplay se activa, pero ese snapshot solo se refresca al guardar en un SavePoint (`GameBootProfile.cs:715` ← `SavePoint.cs:168`). Secuencia normal: guardas → avanzas el grafo (recibes ítem vía `GiveInventoryItemNode`) → cambias de escena sin guardar → el blackboard retrocede al save: el flag `INV_GIVEN` desaparece pero el inventario no se revierte → **el nodo vuelve a entregar el ítem**. Diálogos sin `oneShotFlag` se repiten y el grafo se desincroniza del QuestManager.

**Fix:** capturar blackboards al preset en cada transición de escena, o restaurar solo una vez por sesión (tras load real), no en cada `Start()`.

### C5. Interrumpir la cinemática de un NPC → corrutina zombie y secuenciadores colgados
`Assets/Scripts/Behaviour NPC/States/CinematicState.cs:562` + `NPCBehaviourManagerV2.cs:655-659` *(verificado)*

`Cleanup()` (salida forzada del estado) detiene la corrutina y restaura avoidance, pero **no marca `IsCompleted = true`** (solo `CleanupAndComplete` lo hace). `WaitForSequence` hace `while (!seq.IsCompleted) yield return null;` → si el NPC sale de `CinematicState` a mitad de secuencia, esa espera gira para siempre y el `onComplete` no dispara. Y hay una vía fácil de provocarlo: `NPCCombatLifecycleHandler.OnDamaged` llama `ForceEnterCombat` **sin comprobar `IsInCinematic`** — golpear a un NPC durante una cinemática cuelga los secuenciadores que encadenan pasos vía `onComplete` (MountainSequencer, ReinoExitBanterSequencer). Relacionado: `CheckTransitions` de CinematicState tampoco mira `WasDefeatedInCombat` → NPC que muere en cinemática queda atrapado en el estado.

**Fix:** `IsCompleted = true` en `Cleanup()`, gate de `IsInCinematic` en `OnDamaged`/`ForceEnterCombat`, y prioridad `WasDefeatedInCombat → DeadState` en `CheckTransitions`.

### C6. Hitstops solapados dejan el juego en cámara lenta permanente
`Assets/Scripts/Core/Feedback/SimpleHitStopProvider.cs:18-29`

Cada `Co_HitStop` captura `original = Time.timeScale` al empezar y lo restaura al acabar, sin cancelar el anterior. Dos golpes en <0.2 s (trivial en combate): A captura 1.0, B captura el 0.1 que puso A → A restaura 1.0, B restaura 0.1 → **slow-mo permanente**. Además pelea con el menú de pausa y con `DeathCameraEffect` (que fuerza `timeScale = 1` incondicional al final, rompiendo una pausa activa). Misma familia: `NPCCombatLifecycleHandler.OnDestroy` fuerza `timeScale = 1` si no es 1 — descargar una escena con NPCs estando en pausa revierte la pausa.

**Fix:** árbitro central de timeScale (contador de efectos + baseline gestionado). Un solo servicio resuelve los 4 actores.

### C7. Knockback aéreo interrumpido → input bloqueado para siempre
`Assets/Scripts/Attacks/AerialKnockbackReceiver.cs:147-289`

`LaunchRoutine` empuja `Stunned`, deshabilita el controller y pone el Rigidbody kinemático; la restauración está al final de la corrutina y **no hay `OnDisable`**. Si el componente se desactiva a mitad del arco (~0.6 s) — cinemática con `ModeRule.disableComponents`, muerte, cambio de escena — quedan: `Stunned` pushed para siempre, controller deshabilitado, RB sin gravedad y `_isLaunching=true` (bloquea futuros knockbacks). El propio proyecto tiene el patrón correcto en `PlayerFlyingController.OnDisable` y `PlayerSwimmingController.OnDisable`.

**Fix:** `OnDisable` que restaure RB/controller/rootMotion y haga `PopMode(Stunned)`.

---

## ALTOS — rompen sistemas concretos o corrompen estado en escenarios alcanzables

### A1. `ActiveCombatRegistry` retiene enemigos destruidos → player atrapado en modo combate
`Assets/Scripts/Attacks/ActiveCombatRegistry.cs:164` + `Player/PlayerBattleModeController.cs:311`

`Count` no limpia referencias fake-null. Un enemigo destruido sin `UnregisterNPC` (Destroy directo, descarga de escena aditiva — `ClearAll` solo se llama en GameOver) deja `Count>0` para siempre → Battle Mode + `ActionMode.Combat` permanentes (que además bloquea `Interact`). `InteractionDetector` ya se defiende con `CleanupDestroyedNPCs()`; los otros dos consumidores no. **Fix:** limpieza dentro de `Count` o auto-unregister en `OnDestroy` del NPC.

### A2. `BossArenaController`: arena cerrada sin salida si el boss se destruye sin morir
`Assets/Scripts/Rooms/BossArenaController.cs:585-591`

Si el boss desaparece sin pasar por `Damageable.OnDied` (killzone, despawn, limpieza externa), el path de emergencia solo hace `started=false`: no reabre puertas, no llama `UnlockArea()` ni `RestoreBattleDisables()`, ni cierra la música de batalla → jugador encerrado con música infinita y sin posibilidad de re-disparar el trigger. **Fix:** en ese path, restaurar puertas/área/disables y `AudioService.EndBattleById`.

### A3. Pooling: devolución doble corrompe el pool y los parents destruidos lo agotan
`Assets/Scripts/Core/Pooling/ObjectPool.cs:114-121` *(verificado)* + `VfxPoolService.cs:74-119`

`Return()` detecta la devolución doble pero **aun así hace push** → la misma instancia dos veces en la pila → dos `Get()` devuelven el mismo Transform. Y `VfxPoolService.Play` con `parent` externo: si el parent se destruye, el VFX muere con él pero `_inUse` del ObjectPool lo cuenta para siempre → tras `MaxPoolSizePerPrefab` (64) instancias muertas, ese VFX **deja de verse el resto de la sesión**. **Fix:** `if (!_inUse.Remove(obj)) return;` y, en la rama `instance == null` del Update del servicio, purgar también `_instancePool`/`_inUse`.

### A4. Save corrupto arranca el juego en estado indefinido
`Assets/Scripts/Core/GameBootService.cs:280` *(verificado)*

En el arranque normal, `_profile.LoadProfile(_saveSystem)` **ignora el valor de retorno**. Si el JSON está corrupto (cierre forzado a mitad de escritura), `LoadProfile` devuelve false y no hay fallback al `defaultPlayerPreset`: el juego arranca con el runtimePreset residual, sin HP/inventario/flags coherentes. **Fix (2 líneas):** `if (!_profile.LoadProfile(_saveSystem))` → rama del preset por defecto.

Relacionado (MEDIO): `SaveSystem.Save` hace `File.Delete` + `File.Move` *(verificado)* — hay una ventana sin ningún save en disco; usar `File.Replace`, o leer `save.json.tmp` como fallback en `Load()`. Y `PlayerSaveData` no tiene campo de versión de esquema: cualquier renombrado de campo hará que saves antiguos carguen en silencio con defaults. Añadir `saveVersion` antes de la demo de Steam.

### A5. Señales narrativas sticky consumidas por el sistema equivocado
`Assets/NarrativeGraph/Runtime/Integration/DefaultNarrativeSignals.cs:350-361` + `NPCInteractiveNarrativeExecutor.cs:342-349`

`OnCustom` consume `_pending`/`_raised` en el momento de suscribirse, y el executor Interactive se re-suscribe durante la carga **antes** de que los runners restauren blackboards y suscriban sus `WaitCustomEventNode`. Una señal pendiente puede ser consumida por el executor (que luego la ignora por `singleUse`/preset) → el `WaitCustomEventNode` del grafo nunca la ve → grafo bloqueado. Es la versión runtime del conflicto que el `CrossSystemNarrativeValidator` solo detecta en editor. **Fix:** consumo por-suscriptor, o que el executor re-emita la señal cuando decide ignorarla.

### A6. Ramas fork del grafo: `Exit()` nunca se llama y el estado de suscripción vive en el asset compartido
`Assets/NarrativeGraph/Runtime/Graph/NarrativeRunner.cs:327-457`

Las ramas fork hacen `Enter` de cada nodo pero jamás `Exit`; `StopExecution()` solo hace `Exit` del nodo del camino principal. Nodos en espera dentro de ramas (`WaitQuestCompleteNode._cb`, `StartBattleNode._onBattleWonCb`) quedan suscritos tras `StopAllRunners`/recarga — y esos campos viven en el `NarrativeNode` serializado del asset compartido, así que una re-entrada pisa `_cb` y el `Exit` posterior ya no puede desuscribir el callback viejo → **callbacks fantasma de sesiones muertas ejecutando side effects reales** (completar quests al ganar una batalla de la sesión nueva). **Fix:** rastrear los nodos activos por rama y hacer su `Exit` en `StopExecution`; mover el estado de suscripción a un diccionario por runner.

Relacionados en el mismo archivo: el resume de forks re-ejecuta el `Enter` del nodo fork en cada carga (si es `RaiseCustomEventNode`, re-emite la señal en cada load); y `RequireInventoryItemNode.HandleMissing` usa `ForceJumpToOutput` (mecanismo del camino principal) desde ramas → rama nunca marcada `__DONE__` y `__currentNodeGuid` corrupto; además con `consumeOnSuccess` + `completeQuestInstead` el ítem puede consumirse dos veces (el guard `_itemsConsumedForQuest` no cubre el consumo hecho por el nodo).

### A7. `Transition.cs`: fuga de `sceneLoaded` + disparo prematuro con cargas aditivas
`Assets/Scripts/Core/EasyTransitions/Scripts/Transition.cs:99`

Suscribe `SceneManager.sceneLoaded` y no existe `OnDestroy` que desuscriba (el objeto muere con `Destroy(gameObject, destroyTime)`). En un proyecto multi-escena **aditiva**, además, cualquier carga aditiva durante la espera dispara `OnSceneLoad` prematuramente (no filtra por `LoadSceneMode`). **Fix:** `OnDestroy` desuscribiendo + ignorar `mode == Additive`. En la misma familia: `TeleportService.cs:226` y `CinematicSequencerBase.cs:266-279` dejan handlers de `onTransitionCutPointReached` suscritos al TransitionManager persistente si la transición se interrumpe → la siguiente transición de cualquier sistema puede teleportar al jugador al destino antiguo o ejecutar `BeginCinematic()` de un sequencer destruido. Desuscribir en `OnDestroy`/finally.

### A8. `DayNightCycle`: oscurecimiento por lluvia compuesto exponencialmente y luz clavada tras la lluvia
`Assets/Scripts/World/DayNightCycle.cs:379-386` *(verificado)*

`LateUpdate` lee `directionalLight.intensity` (ya oscurecida el frame anterior) y la vuelve a multiplicar cada frame — exactamente el bug que ya se corrigió para la niebla con `_baseFogDensity` (el comentario de las líneas 248-253 lo documenta), pero sin aplicar a la luz. En ~4 frames la luz cae al suelo (0.28) y al terminar la lluvia **se queda ahí** hasta la siguiente transición de periodo. **Fix:** cachear `_baseLightIntensity` igual que la niebla.

### A9. `SimpleCinematicDirector`: estado global compartido entre instancias
`Assets/Scripts/Cinematics/SimpleCinematicDirector.cs:214-240`

`OnDisable`/`OnDestroy` deciden con el flag **estático** `IsAnyCinematicPlaying`: si el director A reproduce y un director B (que nunca reprodujo) se desactiva por descarga de escena, B resetea el flag global, fuerza `timeScale=1` y cierra el override de A. La limpieza de interrupción además no restaura HUD/minimapa ni prioridad de cámara. Y `PlayRoutine` no está blindada con try/finally (a diferencia de `CinematicSequencerBase.Co_SequenceGuarded`, que sí lo está): una NRE deja flag global, HUD y cámara en estado de cinemática. El campo `lockPlayer` no se usa en ninguna parte. **Fix:** flag de instancia, rutina de restauración completa, y el patrón guarded de la clase base.

### A10. Muerte y revive del player sin limpiar contexto
`Assets/Scripts/Player/PlayerHealthSystem.cs:182-225, 363-408, 501-513`

`TakeDamage`/`Die` no comprueban Cinematic (un AoE residual puede matar al player en mitad de una cinemática y disparar el GameOver dentro de ella); `Die()`/`ReviveInternal` no tocan la pila de modos (morir con `Flying`/`Carrying` pushed los deja vivos de cara al respawn) ni conceden invulnerabilidad temporal al revivir. Y `InvulnerabilityFlashCoroutine` apaga renderers: si el GO se desactiva en el medio ciclo apagado, **el player queda invisible permanente** (nadie llama a `ResetDamageVisuals` al reactivar). **Fix:** god-frame en Cinematic, reset de pila en muerte/revive, `OnDisable → ResetDamageVisuals()`.

### A11. Bosses: pasada de higiene propia
- `GolemBossAI.cs` — muerte a mitad de salto/embestida deja cadáver flotando (agente desactivado que `StopAgent` no puede parar) y `animator.speed` en 1.8; onda expansiva con `OverlapSphereNonAlloc` **sin layermask** y buffer de 32 en un mundo donde todo vive en `Default` → en zonas densas el player puede quedar fuera del buffer y no recibir daño; reflection en runtime (`GetMethod("Shake")`) cuando el propio archivo ya usa `FeedbackService.CameraShake`; `SetDestination` por frame en embestida.
- `ImpDemonAI.cs` — `PlayAnimation` hace `animator.Play(hash, layer, 0f)` **cada frame** sin guard → reinicia la animación en el frame 0 continuamente (animación congelada + coste). `Spider1AI.cs:387` tiene el guard correcto: portarlo. VFX de casteo/lluvia instanciados sin `Destroy` programado ni pool.
- `Spider1AI.cs` — `StopCoroutineSafe(AttackPlayer())` crea un enumerator nuevo y el helper está vacío: la "cancelación" no cancela nada; el daño se aplica aunque la araña esté en stun. `SetDestination` cada frame en persecución (y las arañas atacan en grupo).

### A12. Swap de personaje sin gating por estado
`Assets/Scripts/Player/PartyControlManager.cs:102-119`

`HandleInput` solo comprueba `IsInUIMode`: se puede hacer swap en pleno vuelo/nado/carry/knockback, aplicando el controller a un personaje que quizá no tiene esa habilidad, con los modos aún en la pila. **Fix:** rechazar swap salvo `Top == Default || Combat`.

---

## MEDIOS — deuda que conviene saldar, sin urgencia de hotfix

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

## BAJOS — apuntados para cuando toque pasar por ahí

`GamepadInputReader`: `InputSystem.onAfterUpdate += PollHardwareFallback` nunca se desuscribe (se acumula un registro por sesión de PlayMode) y `_controls` cacheado podría apuntar a un asset dispuesto si PlayerInputManager se recrea · `PlayerHealthSystem.cs:172`: `new Material(renderer.material)` duplica la instancia y ninguna se destruye (fuga por respawn) · `TransitionManager`: si un suscriptor del cut point lanza excepción, `runningTransition` queda en true para siempre (todas las transiciones futuras ignoradas) y su `Start()` es un poll infinito inútil cada 1 s · `PersistOnLoad`: singleton por **clase** — el segundo GameObject distinto con el componente se autodestruye en silencio · `ProjectilePoolManager.cs`: archivo vacío (0 bytes) — eliminar · `PlayerService` declara `[DefaultExecutionOrder(-600)]` pero CLAUDE.md documenta -900 — alinear · `ServiceLocator.TryGet` de un servicio ausente hace `FindAnyObjectByType` en cada llamada sin caché negativa — peligroso si alguien lo sondea por frame · `PlayerSettings.SaveToDisk` escribe a disco síncronamente en cada notch del slider de volumen · Executor Interactive: dos `ConditionalNarrative` del mismo NPC con la misma `customEventKey` → solo la primera recibe el evento (trampa de datos que el validador no cubre; sistema congelado, solo documentarlo) · `NarrativeGraphHub.RestoreBlackboards` no limpia runners sin snapshot (un grafo no empezado en el save conserva el progreso de la sesión anterior en memoria) y `RelaunchForkBranches` con GUID desaparecido (grafo editado tras el save) mata la rama en silencio en vez de relanzarla desde `branchStartGuid` · `Assets/t2.txt` y `Assets/test_delete_me.txt` — basura de pruebas en el raíz de Assets.

---

## Lo que está bien (y merece decirse)

La gestión de corrutinas de música de `AudioService` (referencias explícitas, INC-056 documentado en código) está muy cuidada. `VfxPoolService` con un único Update centralizado es el patrón correcto. `PlayerInputManager` resuelve con elegancia un problema real del Input System (cambios de mapa diferidos a `onAfterUpdate`). `CinematicSequencerBase.Co_SequenceGuarded`, `TagMinigameController`, `Inventory/Shop` y `CloudCoverSpawner` están bien blindados. La FSM de NPCs (Brain/Context/States) es sólida, con throttling y NonAlloc bien aplicados. Los registros de NPCs (registro/desregistro en escenas aditivas) están correctos. Y los invariantes narrativos de §4 se cumplen íntegramente.

El patrón general es claro: la infraestructura reciente es de buena calidad; los sistemas más antiguos (TeleportService/System, BossArenaController, SimpleCinematicDirector, DayNightCycle, los tres bosses) arrastran los mismos problemas que el proyecto ya identificó y corrigió en otros sitios. No hace falta rediseñar nada: hace falta llevar los patrones buenos que ya existen a los archivos que se quedaron atrás.

## Orden de ataque sugerido

1. **C2 (PushMode refcount)** — el fix más rentable: una tarde, elimina una familia entera de conflictos entre diálogo, cinemáticas, victoria y stun.
2. **C1 (reentrada DialogueManager)** — el soft-lock narrativo más probable en juego normal.
3. **C4 (blackboards rancios)** — duplicación de ítems con la secuencia guardar→avanzar→cambiar de escena; directo contra la demo.
4. **C6 + M4-parcial (árbitro de timeScale)** — un servicio pequeño, cierra 4 bugs.
5. **C3, C5, C7, A1, A2** — los "jugador/NPC bloqueado"; todos son fixes locales de pocas líneas.
6. **A4 (save corrupto, 2 líneas) + versionado del save** — antes de que haya saves de jugadores reales.
7. La pasada de logging (M13) y los bosses (A11) cuando toque optimizar la build.
