# 🌟 El Sendero de las Estrellas

RPG de acción/aventura en 3D, desarrollado en solitario por [Raúl Báez](https://github.com/liyo-dev).

**Motor:** Unity 6 (6000.5.x) · **Render pipeline:** URP 17.5 · **Estado:** en desarrollo activo

<!--
TODO: capturas de pantalla / GIF de gameplay aquí.
Sugerencias: una del mundo abierto, una de combate, una de diálogo con NPC.
![Gameplay](docs/screenshots/gameplay.png)
-->

---

## 📖 Sobre el proyecto

Un RPG de acción/aventura construido sobre una arquitectura multi-escena aditiva: una escena `Start` con los managers persistentes (quests, diálogos, guardado, audio, input) y escenas de mundo que se cargan y descargan dinámicamente sobre ella. Los NPCs usan una FSM modular configurable desde ScriptableObjects, y la narrativa se dirige mediante un grafo de nodos visual (`NarrativeGraph`) más un sistema legacy de diálogo condicional para NPCs ya cerrados.

## 📘 Documentación

| Documento | Contenido |
|---|---|
| **[CLAUDE.md](CLAUDE.md)** | Guía de arquitectura, reglas de código no negociables y convenciones del proyecto |
| **[TDD.md](TDD.md)** | Documento técnico de diseño completo: todos los sistemas, API interna, troubleshooting |
| **[STEAM_DEMO_CHECKLIST.md](STEAM_DEMO_CHECKLIST.md)** | Checklist para publicar la demo en Steam |

`TDD.md` es la fuente de verdad técnica del proyecto — ante cualquier duda de arquitectura, se consulta ahí primero.

## 🚀 Quick start

1. Abre el proyecto con **Unity 6 (6000.5.x)** o superior.
2. La escena de entrada es `Assets/Scenes/Systems/Start.unity` — contiene todos los managers persistentes (`DontDestroyOnLoad`) y siempre debe estar cargada.
3. Para testear cualquier otra escena (mundo, cinemática, etc.), ábrela directamente y dale a Play: `AutoBootstrapOnPlay.cs` detecta que no es `Start` y la carga aditivamente antes de entrar en PlayMode. No hace falta configuración manual.

**Requisito:** `Start.unity` debe estar en la posición 0 de Build Settings.

### Script Execution Order crítico

```
GameBootService   -1000   (primero)
PlayerService      -900
ServiceLocator     -800
WorldBootstrap      +200
```

## 🏗️ Arquitectura clave

- **Multi-escena aditiva** — `Start` persiste siempre; el resto de escenas se cargan/descargan sobre ella.
- **ServiceLocator** (`Core/ServiceLocator.cs`) — punto de acceso a singletons globales, cachea tras la primera búsqueda.
- **ScriptableObjects como datos** — configuración de NPCs, quests, hechizos y presets. Nunca lógica.
- **Eventos C# (`Action<T>`)** — comunicación desacoplada entre sistemas, sin referencias directas entre managers.
- **FSM de NPCs:** `NPCBehaviourManagerV2 → NPCBrain → NPCStateContext → INPCState` (`Assets/Scripts/Behaviour NPC/`).
- **Narrativa:** `NarrativeGraph` (grafo de nodos, sistema activo para todo NPC/quest nueva) conviviendo con el executor legacy `NPCInteractiveNarrativeExecutor` (congelado, sin nuevos NPCs). Detalle completo en `CLAUDE.md` § 7.

Detalle completo de cada sistema, reglas de rendimiento y bugs conocidos: ver `TDD.md` y `CLAUDE.md`.

## 📂 Estructura del proyecto

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

## 🎮 Stack técnico

- Unity 6 · URP 17.5
- Unity Input System 1.19 (input centralizado por eventos)
- Cinemachine 3.1.7 · Timeline 1.8.12 · AI Navigation 2.0.13
- Invector 3rd Person Controller (base de movimiento del jugador) · DOTween

## 📝 Convenciones de código (resumen)

Reglas completas y no negociables en [CLAUDE.md](CLAUDE.md#2-reglas-de-código--no-negociables). Las más importantes:

```csharp
// ❌ Nunca en Update/LateUpdate/FixedUpdate
FindObjectOfType<T>();                 // usar registros (ActiveCombatRegistry, PlayerParty...)
GetComponent<T>();                     // cachear en Awake
Physics.OverlapSphere(...);            // usar OverlapSphereNonAlloc con buffer

// ✅ Patrón preferido
var dm = DialogueManager.Instance;
QuestManager.OnQuestCompleted += HandleQuestComplete;
```

- VFX de un solo uso → `VfxPoolService.Instance.Play(...)`, nunca `Instantiate` + `Destroy` directo.
- Comentarios, documentación y mensajes de commit **en español**.
- `Debug.Log` de diagnóstico siempre bajo `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

## 🔧 Herramientas de desarrollo

- **F3** — debug visual de NPCs · **F4** — panel de debug general
- `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)` — valida que quests/eventos no estén referenciados a la vez por el grafo y por el sistema legacy (ver `CLAUDE.md` § 7)

---

Consulta [TDD.md](TDD.md) para la documentación técnica completa y [CLAUDE.md](CLAUDE.md) para las reglas de arquitectura del proyecto.
