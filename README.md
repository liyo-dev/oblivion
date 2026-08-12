# 🌟 El Sendero de las Estrellas

RPG de acción/aventura en 3D, desarrollado en solitario por [Raúl Báez](https://github.com/liyo-dev).

**Motor:** Unity 6 (6000.5.4f1) · **Render pipeline:** URP 17.5 · **Estado:** en desarrollo activo

<!--
TODO: capturas de pantalla / GIF de gameplay aquí.
Sugerencias: una del mundo abierto, una de combate, una de diálogo con NPC.
![Gameplay](docs/screenshots/gameplay.png)
-->

---

## 📖 Sobre el proyecto

Un RPG de acción/aventura construido sobre una arquitectura multi-escena aditiva: una escena `Start` con los managers persistentes (quests, diálogos, guardado, audio, input) y escenas de mundo que se cargan y descargan dinámicamente sobre ella. Los NPCs usan una FSM modular configurable desde ScriptableObjects, y la narrativa se dirige mediante un grafo de nodos visual (`NarrativeGraph`) más un sistema legacy de diálogo condicional para NPCs ya cerrados.

## 📘 Documentación

Toda la documentación técnica del proyecto vive en un único documento — sin archivos `.md` sueltos por la raíz que se queden desactualizados:

| Documento | Contenido |
|---|---|
| **[TDD.md](TDD.md)** | Documento técnico de diseño completo y **fuente de verdad única**: arquitectura, API interna de cada sistema, reglas de código no negociables, invariantes del grafo narrativo, bugs conocidos, troubleshooting, diseños en curso, checklist de publicación en Steam y auditorías del proyecto. |
| **README.md** (este archivo) | Portada del repositorio — overview rápido para quien llega nuevo. |

Ante cualquier duda de arquitectura, se consulta `TDD.md` primero. Si vas a añadir documentación nueva de sustancia (una auditoría, un diseño, un checklist), añádela como sección de `TDD.md` en vez de crear otro `.md` suelto — la convención está descrita en `TDD.md` § 20.

## ✅ Estado del proyecto

La auditoría más reciente (`TDD.md` § 19) confirma que el código está en muy buen estado para un proyecto indie en solitario: disciplina de rendimiento por frame por encima de la media (sin `OverlapSphere` sin `NonAlloc`, buffers cacheados, hashes de animator cacheados), guardado con escritura atómica, y una FSM de NPCs sólida. Lo que falta para nivel "estudio" no son bugs de código sino ausencias de proceso — tests automatizados, identidad de build configurada, CI — y ya está priorizado en `TDD.md` § 19.2 y § 19.3.

## 🚀 Quick start

1. Abre el proyecto con **Unity 6 (6000.5.4f1)** o superior.
2. La escena de entrada es `Assets/Scenes/Systems/Start.unity` — contiene todos los managers persistentes (`DontDestroyOnLoad`) y siempre debe estar cargada.
3. Para testear cualquier otra escena (mundo, cinemática, etc.), ábrela directamente y dale a Play: `AutoBootstrapOnPlay.cs` detecta que no es `Start` y la carga aditivamente antes de entrar en PlayMode. No hace falta configuración manual.

**Requisito:** `Start.unity` debe estar en la posición 0 de Build Settings.

Detalle completo del arranque (incluido el paso `Start → MainMenu` vía `BootLoader`), Script Execution Order y presets de testing: `TDD.md` § 1.

## 🏗️ Arquitectura clave

- **Multi-escena aditiva** — `Start` persiste siempre; el resto de escenas se cargan/descargan sobre ella.
- **ServiceLocator** (`Core/ServiceLocator.cs`) — punto de acceso a singletons globales, cachea tras la primera búsqueda.
- **ScriptableObjects como datos** — configuración de NPCs, quests, hechizos y presets. Nunca lógica.
- **Eventos C# (`Action<T>`)** — comunicación desacoplada entre sistemas, sin referencias directas entre managers.
- **FSM de NPCs:** `NPCBehaviourManagerV2 → NPCBrain → NPCStateContext → INPCState` (`Assets/Scripts/Behaviour NPC/`).
- **Narrativa:** `NarrativeGraph` (grafo de nodos, sistema activo para todo NPC/quest nueva) conviviendo con el executor legacy `NPCInteractiveNarrativeExecutor` (congelado, sin nuevos NPCs) — política formal completa en `TDD.md` § 10.

Detalle completo de cada sistema, reglas de rendimiento y bugs conocidos: ver `TDD.md`.

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

Árbol completo y detalle de cada carpeta: `TDD.md` § 2.

## 🎮 Stack técnico

- Unity 6 · URP 17.5
- Unity Input System 1.19 (input centralizado por eventos)
- Cinemachine 3.1.7 · Timeline 1.8.12 · AI Navigation 2.0.13
- Invector 3rd Person Controller (base de movimiento del jugador) · DOTween

## 📝 Convenciones de código (resumen)

Reglas completas y no negociables en [`TDD.md` § 12](TDD.md#12-reglas-de-rendimiento). Las más importantes:

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
- `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)` — valida que quests/eventos no estén referenciados a la vez por el grafo y por el sistema legacy (política completa en `TDD.md` § 10)

## 🚢 Publicación

Checklist paso a paso para subir la demo a Steam (cuenta de Steamworks, store page, build técnico con SteamPipe): `TDD.md` § 18.

---

Consulta [TDD.md](TDD.md) para toda la documentación técnica del proyecto: arquitectura, sistemas, reglas de código, bugs conocidos, diseños en curso, checklist de Steam y auditorías.
