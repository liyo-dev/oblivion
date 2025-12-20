# 📘 El Sendero de las Estrellas - Documentación Técnica

**Proyecto:** El Sendero de las Estrellas  
**Motor:** Unity 2020.3+  
**Fecha:** Diciembre 2025  
**Versión del Documento:** 1.0

---

## 📑 Índice

1. [Filosofía del Proyecto](#1-filosofía-del-proyecto)
2. [Arquitectura de Escenas](#2-arquitectura-de-escenas)
   - 2.1 [Escena START - Núcleo Arquitectónico](#21-escena-start---núcleo-arquitectónico)
   - 2.2 [Escenas del Mundo](#22-escenas-del-mundo)
   - 2.3 [Sistema de Carga](#23-sistema-de-carga)
3. [Sistema de NPCs (NPCBehaviourManagerV2)](#3-sistema-de-npcs-npcbehaviourmanagerv2)
   - 3.1 [Arquitectura FSM](#31-arquitectura-fsm)
   - 3.2 [Configuración de Módulos](#32-configuración-de-módulos)
   - 3.3 [Sistema de Combate](#33-sistema-de-combate)
   - 3.4 [Sistema de Misiones](#34-sistema-de-misiones)
4. [Sistemas Core](#4-sistemas-core)
   - 4.1 [ServiceLocator](#41-servicelocator)
   - 4.2 [PlayerService](#42-playerservice)
   - 4.3 [QuestManager](#43-questmanager)
   - 4.4 [DialogueManager](#44-dialoguemanager)
5. [Sistema de Input](#5-sistema-de-input)
6. [Sistema de UI](#6-sistema-de-ui)
7. [Sistema de Localización](#7-sistema-de-localización)
8. [Sistema de Guardado](#8-sistema-de-guardado)
9. [Sistema de Cinemáticas](#9-sistema-de-cinematicas)
10. [Solución de Problemas Comunes](#10-solución-de-problemas-comunes)
11. [Mejores Prácticas](#11-mejores-prácticas)

---

## 1. Filosofía del Proyecto

> **"RPG clásico simple: desde el Inspector digo 'narrativa completa misión → activa otra → gana batalla → NPC se mueve'. Sin código denso ni complejidad innecesaria."**

### Principios de Diseño

1. **Configuración desde Inspector:** Todo lo configurable debe estar accesible sin escribir código
2. **Eventos C# sobre UnityEvents:** Sistema de eventos centralizado y tipado
3. **ServiceLocator:** Referencias globales sin `FindObjectOfType`
4. **Localización First:** Todo texto visible usa IDs de localización
5. **Modular y Extensible:** Sistemas independientes que se comunican mediante eventos

---

## 2. Arquitectura de Escenas

### 2.1 Escena START - Núcleo Arquitectónico

**Ubicación:** `Assets/Scenes/Systems/Start.unity`

#### Propósito Crítico

La escena **Start** es el **corazón arquitectónico** del proyecto y la **primera escena** que se carga siempre. Contiene todos los managers persistentes que deben existir durante toda la sesión de juego.

**Desde Start se carga:**
- MainMenu.unity → Para flujos de usuario (Nueva Partida, Continuar, Opciones)
- Desde el menú → Diferentes escenas según la opción elegida

#### Componentes Esenciales

```
📦 Start Scene
 ├── 🎮 GameManager (DontDestroyOnLoad)
 ├── 🎯 QuestManager (DontDestroyOnLoad)
 ├── 💬 DialogueManager (DontDestroyOnLoad)
 ├── 🎨 UIManager (DontDestroyOnLoad)
 ├── 🎮 PlayerInputManager (DontDestroyOnLoad)
 ├── 🔧 ServiceLocator (DontDestroyOnLoad)
 ├── 🌐 LocalizationManager (DontDestroyOnLoad)
 ├── 💾 SaveSystem (DontDestroyOnLoad)
 └── 🔊 AudioManager (DontDestroyOnLoad)
```

#### Flujo de Inicialización

```
1. Start.unity carga (escena inicial del proyecto)
2. Start inicializa todos los managers (DontDestroyOnLoad)
3. Start carga MainMenu.unity (additive)
4. Desde el menú hay múltiples flujos:
   - Nueva Partida → MainWorld
   - Continuar → Última escena guardada
   - Opciones → Configuración
5. Start permanece cargada durante toda la sesión
```

#### Sistema de Testing Rápido

**Problema:** Para testear áreas específicas es tedioso iniciar siempre desde Start → Menú → Continuar → Navegar a la zona.

**Solución:** `EnsureStartSceneLoaded.cs`

Si inicias el juego desde **cualquier escena** (ej: MainWorld, Town, Cave), el sistema:
1. Detecta que Start no está cargada
2. Carga Start.unity aditivamente **antes** que nada
3. Espera a que Start inicialice los managers
4. Continúa la ejecución de la escena de testing

**Resultado:** Puedes dar Play desde cualquier escena y todo funciona correctamente.

```csharp
[DefaultExecutionOrder(-1000)] // Se ejecuta ANTES que todo
public class EnsureStartSceneLoaded : MonoBehaviour
{
    void Awake()
    {
        // Si Start no está cargada, cargarla aditivamente
        if (!IsSceneLoaded("Start"))
        {
            SceneManager.LoadSceneAsync("Start", LoadSceneMode.Additive);
        }
    }
}
```

**Colocación:** Añadir `EnsureStartSceneLoaded` a todos los GameObjects raíz de escenas principales (MainWorld, Town, Woods, etc.)

#### Importancia

- ⚠️ **Sin Start, nada funciona:** NPCs, Quests, UI, Input, etc.
- ✅ **Persistencia:** Los managers sobreviven cambios de escena
- ✅ **Testing Rápido:** Da Play desde cualquier escena y funciona
- ✅ **Escena Inicial:** Start es la primera en Build Settings, carga el menú automáticamente

---

### 2.2 Escenas del Mundo

#### Escenas Principales

```
📁 Assets/Scenes/Main World/
├── 🌍 MainWorld.unity       - Mundo principal exterior
├── 🏘️ Town.unity            - Pueblo
├── 🌲 Woods.unity           - Bosque
├── 🏔️ Cave.unity            - Cueva
├── 🍬 CandyLand.unity       - Área especial
└── 🏡 Pensilvania.unity     - Casa de Pennsylvania
```

#### Características de Cada Escena

| Escena | Propósito | NPCs | Quests | Combate |
|--------|-----------|------|--------|---------|
| **MainWorld** | Hub principal, conexión entre áreas | Múltiples | Sí | Sí |
| **Town** | Pueblo con comercios y misiones | Alta densidad | Múltiples | No |
| **Woods** | Área de exploración y combate | Pocos | Sí | Sí |
| **Cave** | Dungeon, desafío | Enemigos | Boss | Sí |
| **CandyLand** | Área temática especial | Únicos | Secundarias | Opcional |
| **Pensilvania** | Interior, diálogos | 1-2 NPCs | Narrativa | No |

#### Carga de Escenas

Las escenas de juego (MainWorld, Cave, CandyLand, etc.) se cargan de forma **normal**, reemplazando la escena anterior.

**Excepciones que usan carga aditiva:**
1. **Start:** Cuando se hace testing desde otra escena, `EnsureStartSceneLoaded` carga Start aditivamente
2. **Cinemáticas:** `AdditiveSceneCinematic` carga las escenas de cinemáticas aditivamente sin descargar la escena principal

---

### 2.3 Sistema de Carga

#### LoadingScreen.unity

**Ubicación:** `Assets/Scenes/Systems/LoadingScreen.unity`

**Función:** Pantalla de transición entre escenas con:
- Barra de progreso
- Tips aleatorios localizados
- Animación de fondo
- Ocultación de streaming de assets

#### Flujo de Transición

```
Escena Actual → LoadingScreen (additive) → Descarga Anterior → Carga Nueva → Cierra Loading
```

---

## 3. Sistema de NPCs (NPCBehaviourManagerV2)

### 3.1 Arquitectura FSM

**Namespace:** `Game.NPC`

El sistema de NPCs fue completamente **refactorizado** a una arquitectura FSM (Finite State Machine) modular y profesional.

#### Estados Disponibles

```csharp
📦 Game.NPC.States
 ├── IdleState           - NPC en reposo
 ├── WanderState         - Vagabundeo aleatorio
 ├── FollowPathState     - Seguir waypoints
 ├── InteractionState    - En conversación
 ├── CombatState         - Combate con jugador
 ├── CinematicState      - Secuencia cinemática
 └── DeathState          - NPC derrotado (futuro)
```

#### NPCStateContext

Contenedor de datos compartido entre estados:

```csharp
public class NPCStateContext
{
    public Transform Transform { get; }
    public NavMeshAgent Agent { get; }
    public NPCSimpleAnimator Animator { get; }
    public Animator UnityAnimator { get; }
    public Rigidbody Rigidbody { get; }
    public Transform Player { get; set; }
    public NPCBrain Brain { get; set; }
    public NPCConfiguration Config { get; set; }
    public bool IsInCombat { get; set; }
    public bool IsInCinematic { get; set; }
}
```

#### NPCBrain

Controlador central del FSM que gestiona transiciones:

```csharp
public class NPCBrain
{
    public INPCState CurrentState { get; private set; }
    
    public void ChangeState(INPCState newState) { }
    public void ForceState(INPCState newState) { }
    public void Update() { }
}
```

---

### 3.2 Configuración de Módulos

#### NPCConfiguration (ScriptableObject)

Configuración modular del NPC con comportamientos activables:

```csharp
[CreateAssetMenu(fileName = "NPC_Config", menuName = "NPC/Configuration")]
public class NPCConfiguration : ScriptableObject
{
    [Header("Módulos")]
    public NPCBehaviourType behaviours; // Flags enum
    
    // Configuraciones por módulo
    public NPCWanderConfig wanderConfig;
    public NPCCombatConfig combatConfig;
    public NPCNarrativeConfig narrativeConfig;
    public NPCPatrolConfig patrolConfig;
}
```

#### Módulos Disponibles

```csharp
[Flags]
public enum NPCBehaviourType
{
    None = 0,
    Wander = 1 << 0,      // Vagabundeo aleatorio
    Combat = 1 << 1,      // Sistema de combate
    Narrative = 1 << 2,   // Diálogos y misiones
    Patrol = 1 << 3,      // Patrulla por waypoints
    Guard = 1 << 4        // Guardia estático
}
```

#### Ejemplo de Configuración

```
NPC_Eldran_Config:
  behaviours: Narrative | Wander
  wanderConfig:
    - radius: 10
    - minIdleTime: 2
    - maxIdleTime: 5
  narrativeConfig:
    - narrativeID: "ELDRAN"
    - narrativeTag: "QUEST_GIVER"
```

---

### 3.3 Sistema de Combate

#### Integración FSM + CombatBrain

**Antes de la refactorización:** Dos sistemas separados (`NPCCombatBrain` + `SimpleNPCCombat`) sin comunicación.

**Después:** Sistema unificado donde `CombatState` controla `NPCCombatBrain`.

#### CombatState

```csharp
public class CombatState : NPCStateBase
{
    private NPCCombatBrain _combatBrain;
    
    public override void OnEnter(NPCStateContext context)
    {
        _combatBrain = context.Transform.GetComponent<NPCCombatBrain>();
        _combatBrain.Initialize(manager);
        _combatBrain.BeginCombat(settings);
    }
    
    public override void OnUpdate(NPCStateContext context)
    {
        // Combat brain maneja táctica automáticamente
        // Solo verificamos condiciones de salida
    }
}
```

#### Mejoras de Movimiento

**Rotación Suavizada:**
```csharp
// Antes: Slerp brusco
transform.rotation = Quaternion.Slerp(...);

// Ahora: SmoothDampAngle natural
float angle = Mathf.SmoothDampAngle(currentAngle, targetAngle, 
                                    ref _currentTurnVelocity, 0.15f);
transform.rotation = Quaternion.Euler(0f, angle, 0f);
```

**NavMeshAgent Configurado:**
```csharp
_agent.acceleration = 8f;      // Aceleración gradual
_agent.angularSpeed = 180f;    // Rotación moderada
_agent.autoBraking = true;     // Frenado suave
```

**Resultado:** Movimiento fluido y natural en lugar de "ortopédico"

#### NPCCombatConfig

```csharp
[CreateAssetMenu]
public class NPCCombatConfig : NPCModuleConfigBase
{
    public float health = 100f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    public float detectionRange = 10f;
    public float combatRange = 8f;
    public float meleeRange = 2f;
    public bool isAggressive = true;
}
```

---

### 3.4 Sistema de Misiones

#### SimpleQuestNPC (Legacy - Migrado a NPCConfiguration)

Sistema de cadenas de misiones configurable desde Inspector:

```csharp
public class SimpleQuestNPC : MonoBehaviour
{
    [Serializable]
    public class QuestChainEntry
    {
        public QuestData quest;
        public QuestCompletionMode completionMode;
        public DialogueAsset dlgBefore;
        public DialogueAsset dlgInProgress;
        public DialogueAsset dlgTurnIn;
        public DialogueAsset dlgCompleted;
    }
    
    public List<QuestChainEntry> questChain;
}
```

#### Modos de Completado

```csharp
public enum QuestCompletionMode
{
    Manual,                      // Requiere QuestManager.CompleteQuest() externo
    AutoCompleteOnTalk,          // Se completa al hablar (ej. "Habla con Eldran")
    CompleteOnTalkIfStepsReady   // Se completa al hablar SI todos los pasos están OK
}
```

#### Ejemplo: Misión de Eldran

```
Misión 1: "Habla con Eldran"
  - Mode: AutoCompleteOnTalk
  - Al interactuar → Completa automáticamente → Ofrece Misión 2

Misión 2: "Trae la caja de frutas"
  - Mode: CompleteOnTalkIfStepsReady
  - Paso 0: Hablar con Eldran ✓
  - Paso 1: Recoger caja en bosque
  - Si vuelves sin la caja → dlgInProgress
  - Si vuelves con la caja → Completa automáticamente
```

---

## 4. Sistemas Core

### 4.1 ServiceLocator

**Propósito:** Acceso global a servicios sin `FindObjectOfType`.

```csharp
public class ServiceLocator : MonoBehaviour
{
    private static Dictionary<Type, object> _services = new();
    
    public static void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }
    
    public static bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = obj as T;
            return service != null;
        }
        service = null;
        return false;
    }
}
```

**Uso:**
```csharp
// Registro (en Awake del servicio)
ServiceLocator.Register(this);

// Consulta
if (ServiceLocator.TryGet<QuestManager>(out var qm))
{
    qm.StartQuest("ELDRAN_MISSION1");
}
```

---

### 4.2 PlayerService

**Propósito:** Referencia global al jugador sin búsquedas repetidas.

```csharp
public sealed class PlayerService : MonoBehaviour
{
    public static GameObject Player { get; }
    public static Transform PlayerTransform { get; }
    
    public static bool TryGetComponent<T>(out T component) where T : Component;
    public static void RegisterPlayer(GameObject player);
}
```

**Eventos:**
```csharp
public static event Action<GameObject> OnPlayerRegistered;
public static event Action OnPlayerUnregistered;
```

**Uso en NPCs:**
```csharp
if (PlayerService.TryGetComponent<Transform>(out var playerTransform))
{
    float distance = Vector3.Distance(transform.position, playerTransform.position);
}
```

---

### 4.3 QuestManager

**Propósito:** Cerebro central del sistema de misiones.

```csharp
public class QuestManager : MonoBehaviour
{
    // Eventos globales
    public static event Action<string> OnQuestStarted;
    public static event Action<string> OnQuestCompleted;
    public static event Action<string, int> OnQuestStepCompleted;
    
    // API pública
    public void StartQuest(string questId);
    public void CompleteQuest(string questId);
    public void CompleteQuestStep(string questId, int stepIndex);
    public bool IsQuestActive(string questId);
    public bool IsQuestCompleted(string questId);
}
```

**Suscripción desde NPCs:**
```csharp
void OnEnable()
{
    QuestManager.OnQuestCompleted += OnQuestCompleted;
}

void OnQuestCompleted(string questId)
{
    if (questId == "ELDRAN_MISSION1")
    {
        // Mover NPC, cambiar diálogo, etc.
    }
}
```

---

### 4.4 DialogueManager

**Propósito:** Gestión de conversaciones con NPCs.

```csharp
public class DialogueManager : MonoBehaviour
{
    public void StartDialogue(DialogueAsset dialogue);
    public void AdvanceDialogue();
    public void SelectOption(int index);
    public void EndDialogue();
    
    public static event Action OnDialogueStarted;
    public static event Action OnDialogueEnded;
}
```

**DialogueAsset (ScriptableObject):**
```csharp
[CreateAssetMenu(fileName = "DLG_", menuName = "Dialogue/Asset")]
public class DialogueAsset : ScriptableObject
{
    [Serializable]
    public class DialogueLine
    {
        public string speakerNameId;  // ID de localización
        public string textId;          // ID de localización
        public AudioClip voiceClip;
    }
    
    public List<DialogueLine> lines;
}
```

---

## 5. Sistema de Input

### GamepadInputReader

**Propósito:** Centralizar input de gamepad/teclado con eventos C#.

```csharp
public static class GamepadInputReader
{
    // Eventos globales
    public static event Action OnSubmit;
    public static event Action OnCancel;
    public static event Action OnMenuOpen;
    
    // Propiedades estáticas
    public static bool SubmitPressed { get; }
    public static bool CancelPressed { get; }
    public static bool LeftShoulderPressed { get; }
    public static bool RightShoulderPressed { get; }
    public static Vector2 Navigation { get; }
    public static Vector2 Movement { get; }
}
```

**Uso en UI:**
```csharp
void Update()
{
    if (GamepadInputReader.SubmitPressed)
    {
        OnConfirm();
    }
    
    if (GamepadInputReader.LeftShoulderPressed)
    {
        PreviousTab();
    }
}
```

**Mejoras Recientes:**
- ✅ Añadido `LeftShoulderPressed` / `RightShoulderPressed`
- ✅ Compatible con múltiples controles (Xbox, PS4, Switch Pro, genéricos)
- ✅ Fallback a teclado automático

---

## 6. Sistema de UI

### MenuManager

**Propósito:** Gestión de menús con stack automático.

```csharp
public enum MenuKind
{
    Equipment,
    Settings,
    Map,
    Quests,
    Dialogue
}

public class MenuManager : MonoBehaviour
{
    public void OpenMenu(MenuKind kind);
    public void CloseMenu(MenuKind kind);
    public void CloseTopMenu();
    public MenuKind GetCurrentMenu();
}
```

**Stack de Menús:**
```
Equipment abierto → Abres Settings
Stack: [Equipment, Settings] ← Settings activo
Cierras Settings
Stack: [Equipment] ← Equipment vuelve a estar activo
```

### PlayerEquipmentMenu

**Problema resuelto:** Menu no se abría al iniciar desde MainWorld sin pasar por Start.

**Solución:** `EnsureStartSceneLoaded.cs` garantiza que Start esté siempre cargada.

**Logs de Debug:**
```csharp
[PlayerEquipmentMenu] EnsureViews() OK - abriendo menú
[PlayerEquipmentMenu] Menú cerrado por jugador
```

---

## 7. Sistema de Localización

### LocalizationManager

**Propósito:** Soporte multiidioma (ES/EN).

**Archivos JSON:**
```
StreamingAssets/Localization/
├── dialogues_es.json
├── dialogues_en.json
├── quests_es.json
├── quests_en.json
├── ui_es.json
└── ui_en.json
```

**Estructura JSON:**
```json
{
  "DLG_ELDRAN_MISSION1_01": "Ya estás aquí.",
  "DLG_ELDRAN_MISSION1_02": "Te hice venir porque ayer escuché algo en el bosque.",
  "QUEST_ELDRAN_MISSION1_NAME": "Habla con Eldran",
  "QUEST_ELDRAN_MISSION1_DESC": "Eldran te ha llamado. Ve a hablar con él.",
  "UI_CONTINUE": "Continuar"
}
```

**API:**
```csharp
public class LocalizationManager : MonoBehaviour
{
    public string GetLocalizedString(string key);
    public void SetLanguage(string languageCode); // "es", "en"
    public string CurrentLanguage { get; }
}
```

**Uso:**
```csharp
string questName = LocalizationManager.Instance.GetLocalizedString("QUEST_ELDRAN_MISSION1_NAME");
```

---

## 7.1 Sistema de Quests

### QuestManager

**Propósito:** Sistema central de gestión de misiones con progreso por pasos.

**Ubicación:** `Assets/Scripts/Quests/QuestManager.cs`

#### Componentes Principales

**QuestData (ScriptableObject):**
```csharp
[CreateAssetMenu(fileName = "Quest_", menuName = "Quests/Quest Data")]
public class QuestData : ScriptableObject
{
    public string questId;           // ID único
    public string titleKey;          // Localización
    public string descriptionKey;    // Localización
    public QuestStep[] steps;        // Pasos de la quest
}
```

**API:**
```csharp
public class QuestManager : MonoBehaviour
{
    public void StartQuest(string questId);
    public void MarkStepDone(string questId, int stepIndex);
    public QuestState GetState(string questId);
    public bool IsStepCompleted(string questId, int stepIndex);
}
```

#### Configuración de Entrega de Ítems

**Método 1: Entrega Automática (Recomendado)**

1. **Crear Quest (ScriptableObject):**
```
Click derecho → Create → Quests → Quest Data
Quest ID: "entregar_caja_01"
Steps:
  [0] "Encontrar la caja misteriosa"
  [1] "Llevar la caja al mercader"
```

2. **Configurar el Ítem:**
```csharp
// En el GameObject de la caja
Componentes:
  - Interactable
  - SimpleQuestPickup:
      Quest ID: "entregar_caja_01"
      Step Index: 0
      Auto Complete Step: ✓
  - Tag: "QuestItem"
```

3. **Configurar NPC Receptor:**
```csharp
// En el GameObject del NPC
Componentes:
  - SimpleQuestNPC:
      Quest Chain [0]:
        - Quest Data: Quest_EntregarCaja
        - Completion Mode: CompleteOnTalkIfStepsReady
        - Talk Step Index: 1
        - Dialogues:
          * Before: "¿Tienes un paquete para mí?"
          * InProgress: "Todavía no tienes la caja..."
          * TurnIn: "¡Gracias por traer la caja!"
          * Completed: "El paquete llegó a salvo"
```

**Flujo Automático:**
- ✓ Step 0 se marca al recoger la caja
- ✓ Quest se completa al hablar con NPC (si todos los steps están listos)

**Método 2: Con Verificación de Inventario**

Añadir `QuestItemDelivery` al NPC para mayor control:

```csharp
// Componente QuestItemDelivery en NPC
Quest ID: "entregar_caja_01"
Delivery Step Index: 1
Required Item Tag: "QuestItem"
Item Display Name: "la caja"

// Mensajes opcionales
Has Item Message: "Tienes la caja para entregar"
Needs Item Message: "Necesitas encontrar la caja"
Delivered Message: "¡Caja entregada!"
```

#### Scripts Relacionados

- `SimpleQuestNPC.cs` - NPC que da/recibe quests
- `SimpleQuestPickup.cs` - Ítems que completan steps
- `QuestItemDelivery.cs` - Entrega con verificación de inventario
- `QuestProgressOnEvent.cs` - Progreso por eventos custom
- `QuestProgressOnTrigger.cs` - Progreso por zonas de trigger

#### Tips

💡 **Múltiples Items:** Crea un step por cada item
💡 **Chain Quests:** Añade varias quests en SimpleQuestNPC
💡 **Debugging:** Activa "Show Debug Info" en QuestManager


---

## 8. Sistema de Guardado

### SaveSystem

**Propósito:** Persistencia de progreso (quests, inventario, posiciones).

```csharp
public class SaveSystem : MonoBehaviour
{
    public void SaveGame(string slotName);
    public void LoadGame(string slotName);
    public bool HasSaveData(string slotName);
}
```

**Datos Guardados:**
```csharp
[Serializable]
public class GameSaveData
{
    public PlayerData player;
    public List<QuestSaveData> quests;
    public List<NPCPositionData> npcPositions;
    public InventoryData inventory;
    public string currentScene;
    public float playTime;
}
```

**Persistencia de NPCs:**
```csharp
// En NPCBehaviourManagerV2
[SerializeField] public bool persistLastPosition = false;
[NonSerialized] public Vector3 lastPosition;

public void ApplyLastPositionIfNeeded()
{
    if (persistLastPosition && _agent.isOnNavMesh)
    {
        _agent.Warp(lastPosition);
    }
}
```

**Uso desde WorldBootstrap:**
```csharp
void RestoreNPCPositions()
{
    foreach (var entry in presetData.npcPositions)
    {
        var npc = FindNPC(entry.npcId);
        if (npc.persistLastPosition)
        {
            npc.lastPosition = entry.position;
            npc.ApplyLastPositionIfNeeded();
        }
    }
}
```

---

### 8.1 GameBootProfileDebugger

**Propósito:** Herramienta de debugging visual para GameBootProfile que muestra el estado del sistema de guardado y sincronización en tiempo real.

**Ubicación:** `Assets/Scripts/Core/GameBootProfileDebugger.cs`

#### Instalación

1. Añadir el componente al GameObject que tiene `GameBootService`
2. Configurar en Inspector:
   - `Show Debug Panel`: Mostrar panel en pantalla
   - `Toggle Key`: F4 (tecla para mostrar/ocultar)
   - `Track History`: Registrar historial de operaciones
   - `Max History Entries`: 20

El debugger auto-detecta `GameBootProfile` y `SaveSystem`.

#### Panel Visual (F4)

Presiona F4 para ver:

1. **Estado General:**
   - Profile activo
   - Estado de `usePresetInsteadOfSave`
   - Estado de `allowAutoSaves`
   - Existencia de save guardado

2. **Presets Configurados:**
   - Default Preset (template base)
   - Boot Preset (testing)
   - Runtime Preset (activo en runtime)

3. **Estado Runtime Actual:**
   - Spawn Anchor, Level, HP, MP
   - Abilities y Spells
   - Permisos de acciones
   - Flags, Inventory, Bosses

4. **Estado de Sistemas Vivos:**
   - Comparación entre runtimePreset y sistemas activos
   - Detecta desincronización entre preset y juego

5. **Historial de Operaciones:**
   - Registro cronológico de Save/Load
   - Advertencias y errores

6. **Acciones de Debug:**
   - 🔄 Update Runtime from State
   - 💾 Force Save
   - 📂 Load Save
   - 🗑️ Clear History

#### Flujos Típicos de Debug

**Testing de Save/Load:**
```
1. Presiona F4
2. Verifica Estado Runtime Actual
3. Haz cambios en el juego
4. Presiona "🔄 Update Runtime from State"
5. Presiona "💾 Force Save"
6. Reinicia y carga
7. Compara Estado Runtime con Sistemas Vivos
```

**Debugging de Desincronización:**
```
Síntoma: HP se guarda pero al cargar está diferente
1. F4 → Estado Runtime: HP 50
2. Estado de Sistemas Vivos: PlayerHealth 100
3. ❌ Desincronización detectada
4. Causa: Sistema se reseteó después de aplicar preset
```

#### Logs Automáticos

```csharp
// SaveProfile()
✅ Guardado exitoso (context: Manual)
❌ Error al guardar

// LoadProfile()
✅ Cargado exitoso - Anchor: Bedroom, HP: 45.0
❌ Sin save disponible

// UpdateRuntimePreset()
✅ Sincronizados: Health(45/100), Mana(30/50), Inventory(3)

// NewGameReset()
🆕 Nueva partida desde defaultPlayerPreset: DefaultPlayer
```

#### Beneficios

- ✅ Visibilidad completa del flujo save/load
- ✅ Detección inmediata de desincronización
- ✅ Historial de operaciones para debugging
- ✅ Testing manual sin reiniciar
- ✅ Comparación visual preset vs sistemas vivos

**Nota:** Desactivar en builds finales por impacto de rendimiento (OnGUI).

---

## 9. Sistema de Cinemáticas

### 9.1 AdditiveSceneCinematic

**Propósito:** Reproducir cinemáticas en escenas aditivas sin romper el flujo de juego.

**Ubicación:** `Assets/Scripts/Cinematics/AdditiveSceneCinematic.cs`

#### Características

- **Carga aditiva:** Carga la escena de cinemática sin descargar la escena principal
- **Integración con Timeline:** Usa PlayableDirector para secuencias complejas
- **Fade to black:** Sistema de transición suave para ocultar el mundo principal
- **Restauración de estado:** Guarda y restaura posición del player
- **Play-once system:** Evita repetir cinemáticas ya vistas

#### Uso en Narrative Graph

**PlayCinematicNode:**
```csharp
// El nodo busca automáticamente el AdditiveSceneCinematic en escena
public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
{
    var cinematic = ServiceLocator.GetAll<AdditiveSceneCinematic>()
        .FirstOrDefault(c => c.CinematicSceneName == cinematicSceneName);
    
    if (cinematic != null)
    {
        // PlayAndBlock espera hasta que termine
        ctx.Runner.StartCoroutine(PlayAndAdvance(cinematic, onReadyToAdvance));
    }
}
```

#### Propiedades Clave

```csharp
public class AdditiveSceneCinematic : MonoBehaviour
{
    // Configuración
    [SerializeField] private string cinematicSceneName;
    [SerializeField] private bool playOnlyOnce = true;
    [SerializeField] private string singlePlayId = "";
    
    // Estado
    public static bool IsAnyAdditiveCinematicPlaying { get; }
    
    // Eventos
    public event Action OnCinematicFinished;
    
    // Métodos públicos
    public IEnumerator PlayAndBlock(); // Reproduce y bloquea hasta terminar
    public string CinematicSceneName { get; set; }
}
```

#### Flujo Completo

```
1. Trigger (NarrativeGraph, colisión, etc.)
   ↓
2. Fade to black + Alejar cámara
   ↓
3. Cargar escena cinemática (aditiva)
   ↓
4. Reproducir Timeline
   ↓
5. Descargar escena cinemática
   ↓
6. Restaurar cámara + Fade in
```

### 9.2 Debugging de Cinemáticas

#### Logs Útiles

```csharp
[AdditiveSceneCinematic] Desactivando gameplay y cargando escena: Cinematic_VillainPlans
[AdditiveSceneCinematic] Cámara alejada 100m para ocultar mundo principal
[AdditiveSceneCinematic] Director detenido
[AdditiveSceneCinematic] Descargando escena: Cinematic_VillainPlans
[AdditiveSceneCinematic] Cámara restaurada a posición original
```

#### Problemas Comunes

| Problema | Causa | Solución |
|----------|-------|----------|
| Mundo visible durante cinemática | FeedbackService no funciona | Verificar namespace `using Sendero.Core.Feedback;` |
| Cinemática se repite | playOnlyOnce = false | Activar playOnlyOnce o verificar singlePlayId |
| Director no se encuentra | directorInAdditive = true pero no hay director | Añadir PlayableDirector en escena cinemática |

---

## 10. Solución de Problemas Comunes

### Problema: "PlayerEquipmentMenu no se abre"

**Causa:** Iniciaste desde MainWorld sin cargar Start primero.

**Solución:**
1. Añade `EnsureStartSceneLoaded` component a GameObject raíz de MainWorld
2. O siempre inicia desde MainMenu → Start → MainWorld

**Verificación:**
```csharp
// En consola debe aparecer:
[EnsureStartSceneLoaded] Start scene loaded additively for testing
```

---

### Problema: "NPC no se mueve suavemente en combate"

**Causa:** NavMeshAgent con valores por defecto demasiado altos.

**Solución:**
```csharp
// En NPCCombatBrain.BeginCombat():
_agent.acceleration = 8f;      // Era 100
_agent.angularSpeed = 180f;    // Era 360
_agent.autoBraking = true;
```

**Valores recomendados:**
- `acceleration`: 6-12 (gradual)
- `angularSpeed`: 120-240 (moderado)
- `stoppingDistance`: 0.1-0.5 (preciso)

---

### Problema: "Errores de compilación con NPCBehaviourManager"

**Causa:** Scripts legacy aún referenciando la versión antigua.

**Solución:**
```csharp
// Buscar y reemplazar en todo el proyecto:
NPCBehaviourManager → NPCBehaviourManagerV2
```

**Scripts afectados:**
- `NpcAutoMoveNode.cs` ✅
- `Interactable.cs` ✅
- `NPCCombatBrain.cs` ✅
- `WorldBootstrap.cs` ✅
- `GameBootProfile.cs` ✅

---

### Problema: "Quest no se completa al hablar con NPC"

**Diagnóstico:**
1. ¿El NPC tiene `SimpleQuestNPC` component?
2. ¿La quest está en `questChain`?
3. ¿El `completionMode` es correcto?
4. ¿El diálogo `dlgTurnIn` está asignado?

**Debug:**
```csharp
// Activar logs en SimpleQuestNPC
[SerializeField] private bool debugMode = true;

// En consola verás:
[SimpleQuestNPC:Eldran] Quest ELDRAN_MISSION1 auto-completed on talk
```

---

## 10. Mejores Prácticas

### Configuración de NPCs

**✅ HACER:**
```csharp
// Usar NPCConfiguration ScriptableObject
[SerializeField] private NPCConfiguration configuration;

// Comportamientos como flags
configuration.behaviours = NPCBehaviourType.Narrative | NPCBehaviourType.Wander;

// Configurar módulos específicos
configuration.wanderConfig.radius = 10f;
configuration.narrativeConfig.narrativeID = "ELDRAN";
```

**❌ EVITAR:**
```csharp
// Hardcodear valores en código
public float wanderRadius = 10f;
public bool canCombat = true;
public string npcName = "Eldran"; // Usar localización!
```

---

### Sistema de Eventos

**✅ HACER:**
```csharp
// Eventos C# tipados
public static event Action<string> OnQuestStarted;

// Suscripción limpia
void OnEnable()
{
    QuestManager.OnQuestStarted += HandleQuestStarted;
}

void OnDisable()
{
    QuestManager.OnQuestStarted -= HandleQuestStarted;
}
```

**❌ EVITAR:**
```csharp
// UnityEvents en Inspector (solo para casos simples)
public UnityEvent onQuestStarted; // Pierde tipado, difícil de rastrear
```

---

### Localización

**✅ HACER:**
```csharp
// Siempre usar IDs de localización
public string dialogueLineId = "DLG_ELDRAN_MISSION1_01";
string text = LocalizationManager.Instance.GetLocalizedString(dialogueLineId);
```

**❌ EVITAR:**
```csharp
// Texto hardcodeado
public string dialogueLine = "Ya estás aquí."; // NO!
```

---

### Estructura de Assets

**✅ ORGANIZACIÓN RECOMENDADA:**
```
Assets/
├── Data/
│   ├── NPCs/
│   │   ├── Configs/
│   │   │   ├── NPC_Eldran_Config.asset
│   │   │   └── NPC_Guard_Config.asset
│   │   └── Dialogues/
│   │       ├── DLG_ELDRAN_MISSION1.asset
│   │       └── DLG_ELDRAN_MISSION2.asset
│   └── Quests/
│       ├── Q_ELDRAN_MISSION1.asset
│       └── Q_ELDRAN_MISSION2.asset
├── Scenes/
│   ├── Systems/ (Start, MainMenu, Loading)
│   └── Main World/ (MainWorld, Town, Woods, etc.)
└── Scripts/
    ├── Core/ (Managers, Services)
    ├── Behaviour NPC/ (FSM, States)
    └── UI/
```

---

### Testing desde Editor

**Para testear escenas individuales:**

1. Añade `EnsureStartSceneLoaded` a GameObject raíz
2. Marca `debugMode = true` en NPCBehaviourManagerV2
3. Verifica logs en consola

**Shortcuts útiles:**
- `F5` - Play
- `F8` - Pause
- `Shift+F5` - Stop

**Gizmos en Scene:**
- NPCs muestran su estado actual
- Destinos de movimiento en amarillo
- Rango de detección en verde

---

## 📚 Referencias Rápidas

### Jerarquía de Sistemas (Orden de Ejecución)

```
-1000: EnsureStartSceneLoaded
 -600: PlayerService
 -500: ServiceLocator
 -400: LocalizationManager
 -300: QuestManager
 -200: DialogueManager
 -100: MenuManager
    0: Gameplay scripts (NPCBehaviourManagerV2, etc.)
```

### Namespaces del Proyecto

```csharp
// Core
using Core;
using Core.Services;

// NPCs
using Game.NPC;
using Game.NPC.States;
using Game.NPC.Common;
using Game.NPC.Modules;

// UI
using UI;
using UI.Menus;

// Quests
using Quests;

// Dialogue
using Dialogue;
```

### Eventos Globales Disponibles

```csharp
// Player
PlayerService.OnPlayerRegistered
PlayerService.OnPlayerUnregistered

// Quests
QuestManager.OnQuestStarted
QuestManager.OnQuestCompleted
QuestManager.OnQuestStepCompleted

// Dialogue
DialogueManager.OnDialogueStarted
DialogueManager.OnDialogueEnded

// Input
GamepadInputReader.OnSubmit
GamepadInputReader.OnCancel
GamepadInputReader.OnMenuOpen
```

---

## 🎯 Checklist de Setup para Nuevo NPC

- [ ] Crear NPCConfiguration ScriptableObject
- [ ] Configurar módulos necesarios (Wander, Combat, Narrative)
- [ ] Crear DialogueAssets con IDs de localización
- [ ] Si tiene quests: Crear QuestData assets
- [ ] Añadir componentes al GameObject:
  - [ ] Animator
  - [ ] NavMeshAgent
  - [ ] NPCSimpleAnimator
  - [ ] Interactable
  - [ ] NPCBehaviourManagerV2
- [ ] Asignar NPCConfiguration en NPCBehaviourManagerV2
- [ ] Testear desde Inspector con debugMode = true

---

## 📊 Métricas del Proyecto

**Estado actual (Diciembre 2025):**

- ✅ Sistema de NPCs: Refactorizado a FSM
- ✅ Sistema de Combate: Mejorado y fluido
- ✅ Sistema de Quests: Funcional con postActions
- ✅ Sistema de Localización: ES/EN completo
- ✅ Arquitectura de Escenas: START como núcleo
- ✅ Cinemáticas: Sin desactivar GameObjects
- ✅ Errores de compilación: 0
- ✅ Race conditions: Resueltas

**Sistemas Nuevos (Dic 2025):**
- ✨ Sistema de fade para cinemáticas
- ✨ NPCQuestActionExecutor con postActions

**Scripts legacy eliminados:**
- ❌ `SimpleNPCWander` (migrado a WanderState)
- ❌ `SimpleNPCCombat` (migrado a CombatState)
- ❌ `NPCAmbientBrain` (migrado a NPCConfiguration)
- ❌ `NPCBehaviourManager` (v1 → NPCBehaviourManagerV2)

---

## 📝 Historial de Cambios Mayores

### Diciembre 2025 - Gran Refactorización y Mejoras

**NPCs:**
- Migración completa a FSM (Finite State Machine)
- Nuevo NPCBehaviourManagerV2 con configuración modular
- Sistema de combate integrado con movimiento fluido
- Rotación y movimiento suavizado (SmoothDampAngle)
- NPCQuestActionExecutor con postActions configurables

**Sistemas Core:**
- ✨ **NUEVO: Sistema de Cinemáticas mejorado** - Fade + alejar cámara sin desactivar objetos
- Escena START como núcleo persistente
- EnsureStartSceneLoaded para testing desde cualquier escena
- ServiceLocator para referencias globales
- PlayerService con caché de componentes
- PlayerEquipmentMenuController con bootstrap mejorado

**Cinemáticas:**
- AdditiveSceneCinematic refactorizado para NO desactivar GameObjects
- Sistema de fade to black usando FeedbackService.ScreenFlash
- Cámara del player se aleja 100m en lugar de desactivar objetos

**Correcciones:**
- ✅ Eliminados errores "Coroutine couldn't be started because game object is inactive"
- ✅ PostActions de quests ahora se ejecutan correctamente después de cinemáticas
- ✅ Popups de habilidades esperan a que termine la cinemática
- ✅ BossArenaController restaura objetos ANTES de invocar eventos
- ✅ 14 errores de compilación resueltos
- ✅ Sistema de guardado de posiciones de NPCs
- ✅ PlayerEquipmentMenu fix para testing

**Mejoras de Arquitectura:**
- Eliminación de código duplicado para gestión de diferimientos
- Sistema de timing robusto con contador de contextos bloqueantes
- Logs detallados para debugging de flujo de acciones
- Código más mantenible y escalable

---

## 🔮 Roadmap Futuro

### Alta Prioridad
- [ ] Sistema de inventario expandido
- [ ] Sistema de crafting básico
- [ ] Más estados FSM (FleeState, DeathState, VictoryState)
- [ ] Combat phases por salud del NPC

### Media Prioridad
- [ ] Editor tools para crear NPCs rápidamente
- [ ] Sistema de diálogos con branching
- [ ] Cinemáticas con Timeline
- [ ] Quest tracker UI mejorado

### Baja Prioridad (Polish)
- [ ] VFX para combate (partículas, trails)
- [ ] Audio dinámico en NPCs
- [ ] Animaciones faciales
- [ ] Weather system

---

## 📞 Soporte y Contacto

**Documentación actualizada:** Diciembre 2025  
**Motor:** Unity 2020.3+  
**Lenguaje:** C# 8.0+

Para reportar bugs o sugerir mejoras, consulta los archivos `.md` en:
- `docs/` - Documentación específica
- Raíz del proyecto - Resúmenes y fixes

---

**FIN DEL DOCUMENTO TÉCNICO**

*Mantén este documento actualizado conforme el proyecto evoluciona.*

