# 📘 El Sendero de las Estrellas - Documentación Técnica Completa

**Proyecto:** El Sendero de las Estrellas  
**Motor:** Unity 2020.3+  
**Última Actualización:** 11 Febrero 2026  
**Versión del Documento:** 2.1 (Consolidada + Troubleshooting Avanzado)

---

## 📑 Índice

### 1. [Introducción y Quick Start](#1-introducción-y-quick-start)
### 2. [Arquitectura del Proyecto](#2-arquitectura-del-proyecto)
### 3. [Sistema de NPCs](#3-sistema-de-npcs)
### 4. [Sistema de Combate](#4-sistema-de-combate)
### 5. [Sistemas Core](#5-sistemas-core)
### 6. [Sistema de Quests](#6-sistema-de-quests)
### 7. [Sistema de Guardado](#7-sistema-de-guardado)
### 8. [Sistema de Puzzles](#8-sistema-de-puzzles)
### 9. [Optimizaciones de Rendimiento](#9-optimizaciones-de-rendimiento)
### 10. [Fixes Importantes Aplicados](#10-fixes-importantes-aplicados)
### 11. [Debugging y Troubleshooting](#11-debugging-y-troubleshooting)
### 12. [Mejores Prácticas](#12-mejores-prácticas)
### 13. [Troubleshooting Avanzado - Problemas Resueltos (Febrero 2026)](#13-troubleshooting-avanzado---problemas-resueltos-febrero-2026) ⭐ NUEVO

---

## 1. Introducción y Quick Start

### 🚀 Para Iniciar el Proyecto

1. **Abre Unity 2020.3 o superior**
2. **Inicia desde:** `Assets/Scenes/Systems/Start.unity`
3. El sistema carga MainMenu automáticamente (managers persistentes)

### Testing Rápido

Para testear una escena específica (MainWorld, Woods, etc.):
1. **Simplemente abre la escena y presiona Play** ✅
2. El sistema `AutoBootstrapOnPlay` (en `Assets/Editor/`) detecta que no es Start
3. Carga Start aditivamente **automáticamente** antes de entrar en PlayMode
4. Todos los sistemas se inicializan correctamente
5. El anchor y demás datos se cargan del save/preset correctamente

**Nota:** No necesitas añadir ningún componente manual, el sistema es completamente automático.

---

## 2. Arquitectura del Proyecto

### 2.1 Filosofía Arquitectónica

**El Sendero de las Estrellas** usa una arquitectura **multi-escena aditiva** con un núcleo persistente.

#### Principios Clave:
- ✅ **START es el núcleo**: Escena persistente con todos los managers
- ✅ **Escenas aditivas**: Mundo se carga/descarga dinámicamente
- ✅ **ServiceLocator**: Singleton global para servicios
- ✅ **ScriptableObjects**: Configuración modular y reutilizable
- ✅ **FSM para NPCs**: Máquina de estados finita para comportamiento complejo

### 2.2 Escena START - Núcleo Arquitectónico

**Ubicación:** `Assets/Scenes/Systems/Start.unity`

#### Managers Persistentes (DontDestroyOnLoad):

| Manager | Responsabilidad | Instancia |
|---------|----------------|-----------|
| **GameManager** | Ciclo de vida del juego, control de escenas | Singleton |
| **PlayerService** | Referencia global al jugador | Singleton |
| **QuestManager** | Sistema de misiones | Singleton |
| **DialogueManager** | Sistema de diálogos | Singleton |
| **AudioService** | Gestión de música y SFX | Singleton |
| **SaveSystem** | Guardado/carga de partidas | Singleton |
| **GamepadInputReader** | Input centralizado | Singleton |
| **LocalizationManager** | Textos multiidioma | Singleton |

**¿Por qué START?**
- Los managers se cargan UNA VEZ al inicio
- Permanecen activos durante todo el juego
- Evita duplicación de managers

### 2.3 Sistema de Carga de Escenas

#### Escenas del Mundo

```
Scenes/
├── Systems/
│   └── Start.unity          ← Núcleo (cargar PRIMERO)
├── World/
│   ├── MainMenu.unity       ← Menú principal
│   ├── Village.unity        ← Pueblo
│   ├── Forest.unity         ← Bosque
│   └── Cave.unity           ← Cueva
└── UI/
    └── HUD.unity            ← UI del juego
```

#### Flujo de Carga

```
1. Start.unity (carga managers)
   ↓
2. MainMenu.unity (aditiva)
   ↓
3. Usuario inicia partida
   ↓
4. Village.unity (aditiva, MainMenu descargada)
   ↓
5. Player cambia zona → Forest.unity (aditiva, Village descargada)
```

#### Código de Carga (GameManager)

```csharp
public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Additive)
{
    if (!IsSceneLoaded(sceneName))
    {
        SceneManager.LoadSceneAsync(sceneName, mode);
    }
}
```

---

## 3. Sistema de NPCs

### 3.1 NPCBehaviourManagerV2 - FSM Modular

**Archivo:** `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs`

#### Arquitectura FSM

```
NPCBehaviourManagerV2
├── IdleState (patrulla, espera)
├── FollowPlayerState (seguir jugador en party)
├── DialogueState (conversación bloqueante)
├── CombatState (enemigo)
├── AllyCombatState (aliado en combate)
└── InteractionState (narrativa interactiva)
```

#### Módulos del Sistema

| Módulo | Responsabilidad | ScriptableObject |
|--------|----------------|------------------|
| **NPCCombatHandler** | Ataque, defensa, vida | `NPCCombatConfig` |
| **NPCAnimator** | Animaciones (locomotion, combate, muerte) | N/A |
| **NPCCombatLifecycleHandler** | Muerte, mareo, post-derrota | `NPCCombatConfig` |
| **NPCInteractiveNarrativeExecutor** | Diálogos con condiciones | `InteractiveNarrativeConfig` |
| **NPCCombatTeam** | Coordinación de equipos enemigos | N/A |
| **NPCPartyMember** | Seguimiento del jugador | N/A |

### 3.2 Estados Detallados

#### IdleState

```csharp
// NPCs estáticos o que patrullan
public class IdleState : INPCState
{
    public void Enter()
    {
        // Animar idle/patrulla
    }
    
    public void Update()
    {
        // Detectar jugador (si es enemigo)
        // Escuchar interacciones (si es NPC)
    }
}
```

**Transiciones:**
- → CombatState (si enemigo ve jugador)
- → DialogueState (si jugador interactúa)
- → FollowPlayerState (si se une al party)

#### FollowPlayerState

```csharp
// Aliados que siguen al jugador
public class FollowPlayerState : INPCState
{
    private Transform _player;
    private float _followDistance = 3f;
    
    public void Update()
    {
        float distance = Vector3.Distance(transform.position, _player.position);
        
        if (distance > _followDistance)
        {
            // Navegar hacia el jugador
            _agent.SetDestination(_player.position);
        }
        else
        {
            _agent.isStopped = true;
        }
    }
}
```

**Transiciones:**
- → AllyCombatState (si enemigos cerca)
- → IdleState (si abandona party)

#### AllyCombatState

```csharp
// Aliados que ayudan en combate
public class AllyCombatState : INPCState
{
    public void Update()
    {
        // Detectar enemigos
        var enemies = Physics.OverlapSphereNonAlloc(...);
        
        // Atacar al más cercano
        if (enemies.Count > 0)
        {
            _combatHandler.Attack(nearestEnemy);
        }
    }
}
```

**Optimización Crítica:**
- ✅ Usa `Physics.OverlapSphereNonAlloc` (sin allocations)
- ✅ DETECTION_RANGE reducido a 30m (era 100m)

### 3.3 Sistema de Combate de NPCs

#### NPCCombatHandler

**Responsabilidades:**
- Gestión de vida (HP)
- Animaciones de ataque
- Detección de colisión con armas
- Aplicación de daño

**Ejemplo:**

```csharp
public void TakeDamage(float damage, Transform attacker)
{
    if (_isDead) return;
    
    _currentHP -= damage;
    
    if (_currentHP <= 0)
    {
        Die();
    }
    else
    {
        PlayHitReaction();
    }
}
```

#### NPCCombatLifecycleHandler

**Responsabilidades:**
- Secuencia de muerte (animación + slow motion)
- Estado "dizzy" (mareado) después de morir
- Post-defeat actions (huir, quedarse, seguir)

**Post-Defeat Actions:**

| Acción | Descripción | Config |
|--------|-------------|--------|
| **FleeAndDisappear** | NPC huye y desaparece | `PostDefeatAction.FleeAndDisappear` |
| **StayAndRespawn** | NPC se queda en el suelo | `PostDefeatAction.StayAndRespawn` |
| **FollowPlayer** | NPC se une al party | `PostDefeatAction.FollowPlayer` |
| **None** | Solo animación dizzy | `PostDefeatAction.None` |

#### Sistema de Equipos (NPCCombatTeam)

**Funcionalidad:**
- Coordinar varios NPCs enemigos
- Un líder gestiona el diálogo post-derrota
- Miembros esperan a que el líder termine

```csharp
public class NPCCombatTeam : MonoBehaviour
{
    public string TeamId;
    public List<NPCBehaviourManagerV2> Members;
    
    public NPCBehaviourManagerV2 Leader => Members.FirstOrDefault();
    
    public void NotifyPostDefeatDialogueFinished()
    {
        // Notificar a todos los miembros
        foreach (var member in Members)
        {
            member.OnTeamLeaderFinishedDialogue();
        }
    }
}
```

### 3.4 Sistema de Animación de NPCs (NPCSimpleAnimator)

**Archivo:** `Assets/Scripts/Behaviour NPC/NPCSimpleAnimator.cs`

#### Estados Soportados

| Estado | Animación | Transición |
|--------|-----------|------------|
| **Idle** | Stand_NoWeapon | Automático desde cualquier estado |
| **Walk** | Walk_NoWeapon | Basado en velocity del NavMeshAgent |
| **Run** | Run_NoWeapon | Velocity > 3.5 |
| **Attack** | Attack01-08_NoWeapon | Manual con ComboSystem |
| **Hit** | GetHit_NoWeapon | Al recibir daño |
| **Die** | Die01-04_NoWeapon | Desde Hit o directamente |
| **Dizzy** | GetUpFront/Back_NoWeapon | Después de Die |
| **Victory** | Victory_NoWeapon | Jugador después de combate |

#### Métodos Clave

```csharp
// Transición automática a locomotion
public void TransitionToLocomotion()
{
    _isInCombat = false;
    _animator.SetTrigger("Idle");
}

// Reproducir ataque
public void PlayAttack(int attackIndex)
{
    string attackState = GetAttackStateName(attackIndex);
    _animator.Play(attackState, 0, 0f);
}

// Reproducir muerte
public void PlayDeath()
{
    string dieState = GetRandomDieState();
    _animator.Play(dieState, 0, 0f);
    
    // Detener NavMeshAgent
    if (_agent != null)
    {
        _agent.isStopped = true;
        _agent.updatePosition = false; // ⚠️ Requiere restauración
    }
}
```

---

## 4. Sistema de Combate

### 4.1 Combate del Jugador

#### PlayerBattleModeController

**Archivo:** `Assets/Scripts/Player/PlayerBattleModeController.cs`

**Responsabilidades:**
- Detectar enemigos cercanos → Entrar en Battle Mode
- Activar UpperBody layer para combate
- Secuencia de victoria
- Restaurar música después de combate

**Battle Mode:**

```csharp
private void Update()
{
    if (IsInBattleMode)
    {
        // Detectar enemigos
        int enemyCount = Physics.OverlapSphereNonAlloc(
            transform.position, 
            enemyDetectionRadius, 
            _hitColliders, 
            enemyLayer
        );
        
        if (enemyCount == 0)
        {
            ExitBattleMode();
        }
    }
}
```

**Secuencia de Victoria:**

```csharp
public void PlayVictory(string battleId)
{
    if (_isPlayingVictory) return;
    
    StartCoroutine(PlayVictorySequence(battleId));
}

private IEnumerator PlayVictorySequence(string battleId)
{
    _isPlayingVictory = true;
    
    // 1. Deshabilitar control del jugador
    _controller.enabled = false;
    
    // 2. Reproducir animación de victoria
    _animator.Play("Victory_NoWeapon", 0, 0f);
    
    // 3. Reproducir música de victoria
    AudioService.Instance.PlayVictoryForBattle(battleId, victoryMusicId, 0f);
    
    // 4. Esperar duración de animación
    yield return new WaitForSeconds(victoryAnimationDuration);
    
    // 5. Restaurar control
    _controller.enabled = true;
    _isPlayingVictory = false;
}
```

### 4.2 Detección de Targets

#### PlayerTargeting

**Archivo:** `Assets/Scripts/Player/PlayerTargeting.cs`

**Funcionalidad:**
- Escanear enemigos en radio configurable
- Seleccionar target más cercano
- UI de indicador de target

```csharp
private void ScanForTargets()
{
    int hitCount = Physics.OverlapSphereNonAlloc(
        origin, 
        scanRadius, 
        _overlapBuffer, 
        enemyMask
    );
    
    Damageable nearestTarget = null;
    float nearestDistance = float.MaxValue;
    
    for (int i = 0; i < hitCount; i++)
    {
        var damageable = _overlapBuffer[i].GetComponentInParent<Damageable>();
        
        if (damageable != null && damageable.IsAlive)
        {
            float distance = Vector3.Distance(origin, damageable.transform.position);
            
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = damageable;
            }
        }
    }
    
    CurrentTarget = nearestTarget;
}
```

---

## 5. Sistemas Core

### 5.1 ServiceLocator

**Archivo:** `Assets/Scripts/Core/ServiceLocator.cs`

**Patrón Singleton Global:**

```csharp
public class ServiceLocator : MonoBehaviour
{
    private static ServiceLocator _instance;
    
    public static ServiceLocator Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ServiceLocator>();
            }
            return _instance;
        }
    }
    
    // Cacheo de servicios globales
    private static PlayerService _playerService;
    public static PlayerService PlayerService
    {
        get
        {
            if (_playerService == null)
            {
                _playerService = FindObjectOfType<PlayerService>();
            }
            return _playerService;
        }
    }
}
```

**Servicios Disponibles:**
- `ServiceLocator.PlayerService`
- `ServiceLocator.QuestManager`
- `ServiceLocator.DialogueManager`
- `ServiceLocator.AudioService`
- `ServiceLocator.SaveSystem`

### 5.2 QuestManager

**Archivo:** `Assets/Scripts/Quests/QuestManager.cs`

#### Sistema de Quests

**Quest Structure:**

```csharp
[CreateAssetMenu]
public class QuestData : ScriptableObject
{
    public string QuestId;
    public LocalizedString QuestName;
    public LocalizedString QuestDescription;
    public List<QuestObjective> Objectives;
    public QuestReward Reward;
}

public class QuestObjective
{
    public string ObjectiveId;
    public QuestObjectiveType Type; // Collect, Talk, Defeat, etc.
    public string TargetId;
    public int RequiredAmount;
}
```

**Quest Tracking:**

```csharp
public void StartQuest(string questId)
{
    var quest = GetQuestData(questId);
    
    if (quest == null) return;
    
    var instance = new QuestInstance(quest);
    _activeQuests.Add(instance);
    
    OnQuestStarted?.Invoke(questId);
}

public void CompleteObjective(string questId, string objectiveId)
{
    var quest = _activeQuests.Find(q => q.QuestId == questId);
    
    if (quest == null) return;
    
    quest.CompleteObjective(objectiveId);
    
    if (quest.IsCompleted)
    {
        CompleteQuest(questId);
    }
}
```

### 5.3 DialogueManager

**Archivo:** `Assets/Scripts/Dialogue/DialogueManager.cs`

#### Sistema de Diálogos

**Dialogue Data:**

```csharp
[CreateAssetMenu]
public class DialogueData : ScriptableObject
{
    public string DialogueId;
    public List<DialogueLine> Lines;
}

public class DialogueLine
{
    public string SpeakerName;
    public LocalizedString Text;
    public Sprite SpeakerPortrait;
    public AudioClip VoiceClip;
}
```

**Playback:**

```csharp
public void StartDialogue(DialogueData dialogue)
{
    _currentDialogue = dialogue;
    _currentLineIndex = 0;
    
    // Bloquear jugador
    PlayerActionManager.Instance.PushMode(ActionMode.Cinematic);
    
    // Mostrar UI
    _dialogueUI.SetActive(true);
    
    // Mostrar primera línea
    ShowLine(_currentLineIndex);
}

public void Advance()
{
    if (_isTyping)
    {
        // Skip typing
        CompleteTyping();
    }
    else
    {
        // Next line
        _currentLineIndex++;
        
        if (_currentLineIndex < _currentDialogue.Lines.Count)
        {
            ShowLine(_currentLineIndex);
        }
        else
        {
            Close();
        }
    }
}
```

**Integración con NPCs:**

```csharp
// En NPCInteractiveNarrativeExecutor
public void ExecuteDialogue(string dialogueId)
{
    var dialogue = Resources.Load<DialogueData>($"Dialogues/{dialogueId}");
    
    if (dialogue != null)
    {
        DialogueManager.Instance.StartDialogue(dialogue);
        
        // Esperar a que termine
        DialogueManager.Instance.OnDialogueClosed += HandleDialogueFinished;
    }
}
```

---

## 6. Sistema de Quests

### 6.1 Quest Objectives

#### Tipos de Objetivos

| Tipo | Descripción | Ejemplo |
|------|-------------|---------|
| **Collect** | Recolectar items | "Recoge 5 hierbas" |
| **Talk** | Hablar con NPC | "Habla con el Anciano" |
| **Defeat** | Derrotar enemigos | "Derrota 3 goblins" |
| **Explore** | Llegar a ubicación | "Encuentra la cueva" |
| **Deliver** | Entregar item | "Entrega la carta" |

#### Detección de Items del Wardrobe

**Funcionalidad:** Los quests pueden detectar cuando el jugador obtiene items del sistema de Wardrobe.

```csharp
// En QuestManager
private void OnWardrobeChanged(WardrobeItem item)
{
    foreach (var quest in _activeQuests)
    {
        foreach (var objective in quest.Objectives)
        {
            if (objective.Type == QuestObjectiveType.ObtainWardrobeItem)
            {
                if (objective.TargetId == item.ItemId)
                {
                    objective.CurrentAmount++;
                    
                    if (objective.IsCompleted)
                    {
                        CompleteObjective(quest.QuestId, objective.ObjectiveId);
                    }
                }
            }
        }
    }
}
```

### 6.2 Quest UI

**Elementos:**
- Quest log (lista de quests activos)
- Objetivo tracker (HUD con objetivo actual)
- Quest complete popup

```csharp
// Ejemplo de UI de quest tracker
public class QuestTrackerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _objectiveText;
    
    private void OnEnable()
    {
        QuestManager.Instance.OnObjectiveUpdated += UpdateDisplay;
    }
    
    private void UpdateDisplay(string questId, string objectiveId)
    {
        var quest = QuestManager.Instance.GetActiveQuest(questId);
        var objective = quest.GetObjective(objectiveId);
        
        _objectiveText.text = $"{objective.Description} ({objective.CurrentAmount}/{objective.RequiredAmount})";
    }
}
```

---

## 7. Sistema de Guardado

### 7.1 SaveSystem

**Archivo:** `Assets/Scripts/Save/SaveSystem.cs`

#### Arquitectura de Guardado

**Save Data Structure:**

```csharp
[Serializable]
public class SaveData
{
    // Player data
    public Vector3 PlayerPosition;
    public int PlayerLevel;
    public float PlayerHP;
    
    // Quests
    public List<string> ActiveQuestIds;
    public List<string> CompletedQuestIds;
    
    // Inventory
    public List<InventoryItemData> InventoryItems;
    
    // Party members
    public List<string> PartyMemberIds;
    
    // Scene state
    public string CurrentSceneName;
    public List<NPCStateData> NPCStates;
}
```

#### Save/Load

```csharp
public class SaveSystem : MonoBehaviour
{
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");
    
    public void Save()
    {
        var saveData = new SaveData();
        
        // Recopilar datos
        saveData.PlayerPosition = PlayerService.Instance.transform.position;
        saveData.ActiveQuestIds = QuestManager.Instance.GetActiveQuestIds();
        // ...
        
        // Serializar
        string json = JsonUtility.ToJson(saveData, true);
        
        // Guardar
        File.WriteAllText(SaveFilePath, json);
        
        Debug.Log("Partida guardada");
    }
    
    public void Load()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.LogWarning("No hay partida guardada");
            return;
        }
        
        // Leer
        string json = File.ReadAllText(SaveFilePath);
        
        // Deserializar
        var saveData = JsonUtility.FromJson<SaveData>(json);
        
        // Restaurar datos
        PlayerService.Instance.transform.position = saveData.PlayerPosition;
        QuestManager.Instance.RestoreQuests(saveData.ActiveQuestIds);
        // ...
        
        Debug.Log("Partida cargada");
    }
}
```

### 7.2 Sistema de Testing con Presets

**Archivo:** `Assets/Scripts/Save/PlayerPreset.cs`

**Funcionalidad:** Configurar el estado inicial del party para testing.

```csharp
[CreateAssetMenu(fileName = "PlayerPreset", menuName = "Testing/Player Preset")]
public class PlayerPreset : ScriptableObject
{
    public List<string> PartyMemberIds;
    
    public void Apply()
    {
        PlayerParty.Instance.Clear();
        
        foreach (var memberId in PartyMemberIds)
        {
            var npc = NPCRegistry.Instance.GetNPC(memberId);
            
            if (npc != null)
            {
                PlayerParty.Instance.AddMember(npc);
            }
        }
    }
}
```

---

## 8. Sistema de Puzzles

### 8.1 Burnable - Objetos Quemables

**Archivo:** `Assets/Scripts/Puzzles/Burnable.cs`

**Funcionalidad:** Objetos que el jugador puede quemar con fuego mágico.

```csharp
public class Burnable : MonoBehaviour
{
    [SerializeField] private GameObject _burntVersion;
    [SerializeField] private ParticleSystem _fireEffect;
    
    public void Burn()
    {
        // Activar efecto de fuego
        _fireEffect.Play();
        
        // Esperar 2 segundos
        StartCoroutine(BurnSequence());
    }
    
    private IEnumerator BurnSequence()
    {
        yield return new WaitForSeconds(2f);
        
        // Reemplazar con versión quemada
        if (_burntVersion != null)
        {
            _burntVersion.SetActive(true);
        }
        
        gameObject.SetActive(false);
    }
}
```

### 8.2 PressurePlate - Interruptor de Presión

**Archivo:** `Assets/Scripts/Puzzles/PressurePlate.cs`

**Funcionalidad:** Placa que se activa cuando un objeto tiene peso encima.

```csharp
public class PressurePlate : MonoBehaviour
{
    [SerializeField] private UnityEvent OnActivated;
    [SerializeField] private UnityEvent OnDeactivated;
    
    private bool _isActivated = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Pushable"))
        {
            if (!_isActivated)
            {
                _isActivated = true;
                OnActivated?.Invoke();
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Pushable"))
        {
            if (_isActivated)
            {
                _isActivated = false;
                OnDeactivated?.Invoke();
            }
        }
    }
}
```

### 8.3 PlatformElevator

**Archivo:** `Assets/Scripts/Puzzles/PlatformElevator.cs`

**Funcionalidad:** Plataforma que se mueve entre dos puntos.

```csharp
public class PlatformElevator : MonoBehaviour
{
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;
    [SerializeField] private float _speed = 2f;
    
    private bool _isMoving = false;
    private bool _isAtEnd = false;
    
    public void Activate()
    {
        if (!_isMoving)
        {
            StartCoroutine(MovePlatform());
        }
    }
    
    private IEnumerator MovePlatform()
    {
        _isMoving = true;
        
        Vector3 target = _isAtEnd ? _startPoint.position : _endPoint.position;
        
        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, _speed * Time.deltaTime);
            yield return null;
        }
        
        _isAtEnd = !_isAtEnd;
        _isMoving = false;
    }
}
```

---

## 9. Optimizaciones de Rendimiento

### 9.1 Reporte Final de Optimizaciones

**Fecha:** 21 Enero 2026  
**Estado:** ✅ COMPLETADO AL 100%

#### Ganancia Total

```
FASE 1:           175-910ms/seg
FASE 2:             9-38ms/seg
OPTIMIZACIÓN FINAL: 4-20ms/seg
──────────────────────────────────
TOTAL:           188-968ms/seg ✨
```

#### FPS Proyectado

```
ANTES:  40-50 FPS (combate)
AHORA:  60-75 FPS (combate)
MEJORA: +50-90% 🚀
```

### 9.2 Cambios Aplicados - FASE 1 (Crítico)

#### 1. AllyCombatState.cs ⭐⭐⭐

**Problemas Eliminados:**
- ❌ `FindObjectOfType<GolemBossAI>()` - 50-200ms/frame
- ❌ `FindObjectsOfType<Damageable>()` - 30-150ms/frame
- ❌ `FindGameObjectsWithTag("Enemy")` - 20-100ms/frame (2 ubicaciones)

**Soluciones:**
```csharp
// ANTES (LENTO)
var golem = FindObjectOfType<GolemBossAI>();
var enemies = FindGameObjectsWithTag("Enemy");

// DESPUÉS (RÁPIDO)
private Collider[] _detectionBuffer = new Collider[32];

void DetectEnemies()
{
    int count = Physics.OverlapSphereNonAlloc(
        transform.position,
        DETECTION_RANGE, // 30m (era 100m)
        _detectionBuffer,
        enemyLayer
    );
    
    for (int i = 0; i < count; i++)
    {
        var enemy = _detectionBuffer[i].GetComponent<Damageable>();
        // Procesar enemy
    }
}
```

**Ganancia: 150-800ms/seg**

#### 2. GolemBossAI.cs

**Optimizaciones:**
```csharp
// NonAlloc para Physics queries
private Collider[] _punchBuffer = new Collider[16];
private Collider[] _shockwaveBuffer = new Collider[32];

void ApplyPunchDamage()
{
    int count = Physics.OverlapSphereNonAlloc(
        punchPoint.position,
        punchRadius,
        _punchBuffer,
        targetLayer
    );
    
    for (int i = 0; i < count; i++)
    {
        // Aplicar daño
    }
}
```

**Ganancia: 10-45ms/seg**

#### 3. PlayerParty.cs

**Optimizaciones:**
```csharp
// Throttling de verificación de distancias
private float _lastDistanceCheck = 0f;
private const float DISTANCE_CHECK_INTERVAL = 0.5f;

void Update()
{
    if (Time.time - _lastDistanceCheck > DISTANCE_CHECK_INTERVAL)
    {
        CheckMemberDistances();
        _lastDistanceCheck = Time.time;
    }
}
```

**Ganancia: 15-65ms/seg**

### 9.3 Cambios Aplicados - FASE 2 (Alta Prioridad)

#### 4. NPCCombatBrain.cs

**Optimizaciones:**
```csharp
// NonAlloc en múltiples métodos
private Collider[] _coverBuffer = new Collider[16];

bool TryGetCoverPosition(out Vector3 coverPos)
{
    int count = Physics.OverlapSphereNonAlloc(
        transform.position,
        coverSearchRadius,
        _coverBuffer,
        coverLayer
    );
    
    // Buscar mejor cover
    // ...
}
```

**Ganancia: 5-20ms/seg**

#### 5. MagicProjectile.cs

**Optimizaciones:**
```csharp
// NonAlloc para AOE damage
private Collider[] _aoeBuffer = new Collider[32];

void ExplodeAOE()
{
    int count = Physics.OverlapSphereNonAlloc(
        transform.position,
        explosionRadius,
        _aoeBuffer,
        enemyLayer
    );
    
    for (int i = 0; i < count; i++)
    {
        // Aplicar daño
    }
}
```

**Ganancia: 2-10ms/seg**

### 9.4 Mejores Prácticas de Optimización

#### ✅ Usar Physics.OverlapSphereNonAlloc

```csharp
// ❌ MAL - Allocations cada frame
Collider[] hits = Physics.OverlapSphere(pos, radius);

// ✅ BIEN - Sin allocations
private Collider[] _buffer = new Collider[32];
int count = Physics.OverlapSphereNonAlloc(pos, radius, _buffer);
```

#### ✅ Cachear Referencias

```csharp
// ❌ MAL - GetComponent cada vez
void Update()
{
    var animator = GetComponent<Animator>();
    animator.SetFloat("Speed", speed);
}

// ✅ BIEN - Cachear en Awake
private Animator _animator;

void Awake()
{
    _animator = GetComponent<Animator>();
}

void Update()
{
    _animator.SetFloat("Speed", speed);
}
```

#### ✅ Throttling de Operaciones Costosas

```csharp
// ❌ MAL - Cada frame
void Update()
{
    CheckForEnemies(); // Costoso
}

// ✅ BIEN - Cada X segundos
private float _lastCheck = 0f;

void Update()
{
    if (Time.time - _lastCheck > 0.5f)
    {
        CheckForEnemies();
        _lastCheck = Time.time;
    }
}
```

#### ✅ Evitar FindObjectOfType

```csharp
// ❌ MAL - Muy lento
void Update()
{
    var player = FindObjectOfType<PlayerService>();
}

// ✅ BIEN - Singleton cacheado
private PlayerService _player;

void Awake()
{
    _player = ServiceLocator.PlayerService;
}
```

---

## 10. Fixes Importantes Aplicados

### 10.1 FIX: NPCs Caminan en Sitio (v4)

**Fecha:** 23 Enero 2026  
**Estado:** ✅ RESUELTO

#### Problema

Los NPCs reproducían la animación de caminar después del combate pero no se desplazaban físicamente (caminaban en el sitio).

#### Causa Raíz

1. `NavMeshAgent.updatePosition = false` en `PlayDeath()`
2. NavMeshAgent no sincronizado después de reactivarse

**Síntomas:**
- `remainingDistance = Infinito`
- `velocity = 0.0`
- Animación de caminar pero sin movimiento

#### Solución Aplicada

**Cambio 1: Restaurar `updatePosition = true`**

```csharp
// En HandlePostDefeatAction() - línea ~673
_agent.isStopped = false;
_agent.updatePosition = true;  // ✅ CRÍTICO
_agent.updateRotation = false;
```

**Cambio 2: Warp del NavMeshAgent**

```csharp
// En HandlePostDefeatAction() - línea ~678-686
// ✅ CRÍTICO v4: Warp para re-sincronizar con NavMesh
if (!_agent.Warp(transform.position))
{
    Debug.LogError($"[Lifecycle] ❌ {name} no pudo hacer Warp");
    yield break;
}
Debug.Log($"[Lifecycle] ✅ {name} warped correctamente");

// Ahora sí establecer destino
_agent.SetDestination(fleePos);
```

**Cambio 3: Transición explícita a Locomotion**

```csharp
// En HandlePostDefeatAction() - línea ~704-715
_animator.TransitionToLocomotion();
Debug.Log($"[Lifecycle] 🎬 {name} transicionado a locomotion");

_animator.SetMovementSpeed(1f, 0.1f);
```

#### Verificación

**Logs esperados:**
```log
[Lifecycle] ✅ Lety warped correctamente a (1.17, 2.02, -17.33)
[Lifecycle] 🎬 Lety transicionado a locomotion (v3 fix aplicado)
[Lifecycle] 🏃 Lety huyendo hacia (16.54, 0.02, 11.57)
  DesiredVelocity: (1.23, 0.00, 0.45)  ← DEBE SER > 0
  remainingDistance: 10.94  ← DEBE SER VÁLIDO
```

**NO debe aparecer:**
```log
⚠️ velocity = 0 durante huida
remainingDistance: Infinito  ← YA NO DEBE APARECER
```

### 10.2 FIX: Combate en Equipo (NPCCombatTeam)

**Problema:** NPCs de equipo no coordinaban diálogos post-derrota.

**Solución:**
- Líder gestiona el diálogo
- Miembros esperan a que líder termine
- `NotifyPostDefeatDialogueFinished()` sincroniza el equipo

### 10.3 FIX: Música de Victoria

**Problema:** Música de batalla no se restauraba después del combate.

**Solución:**
```csharp
// En PlayVictorySequence()
AudioService.Instance.PlayVictoryForBattle(battleId, victoryMusicId, 0f);

// En HandleGetUpDizzy() - después del diálogo
if (isLastTeamMember && isLeader)
{
    AudioService.Instance.RestoreBattleMusic(battleMusicId);
}
```

---

## 11. Debugging y Troubleshooting

### 11.1 Teclas de Debug

| Tecla | Funcionalidad | Archivo |
|-------|--------------|---------|
| **F3** | Toggle debug de NPCs (Gizmos, estados) | `NPCBehaviourManagerV2.cs` |
| **F4** | Toggle debug de pathfinding | `NPCBehaviourManagerV2.cs` |
| **F5** | Reload scene | `GameManager.cs` |

### 11.2 Logs de Debug

#### NPCCombatLifecycleHandler

```log
[Lifecycle] 💀 Animación de muerte iniciada
[Lifecycle] 🎉 Llamando a PlayVictory() del player
[Lifecycle] ⏱️ Slow motion terminado
[Lifecycle] 😵 Esperando transición a animación dizzy
[Lifecycle] 👥 Vicky es parte de un equipo - IsLeader: False
[Lifecycle] 🎬 Ejecutando acción post-derrota: FleeAndDisappear
[Lifecycle] 🏃 Lety huyendo hacia (16.54, 0.02, 11.57)
[Lifecycle] ✅ Lety completó huida
```

#### NPCSimpleAnimator

```log
[NPCAnimator:Vicky] 💀 PlayDeath() llamado - dieState: 'Die02_NoWeapon'
[NPCAnimator:Vicky] 🎬 Reproduciendo animación de muerte
[NPCAnimator:Vicky] ✅ animator.Play('Die02_NoWeapon', 0) ejecutado
[NPCAnimator:Vicky] NavMeshAgent detenido
[NPCAnimator:Vicky] ✅ Animación de muerte iniciada
```

#### PlayerBattleModeController

```log
[PlayerBattleMode] 🎯 PlayVictory() LLAMADO
[PlayerBattleMode] 🎉 ✅ INICIANDO ANIMACIÓN DE VICTORIA
[PlayerBattleMode] 🎮 Controlador del jugador deshabilitado
[PlayerBattleMode] 🎬 ✅ Reproduciendo animación de victoria
[PlayerBattleMode] 🎵 ✅ Reproduciendo música de victoria
[PlayerBattleMode] ⏱️ Esperando 3s
[PlayerBattleMode] 🔄 Terminando animación de victoria
[PlayerBattleMode] ✅ Secuencia de victoria COMPLETADA
```

### 11.3 Problemas Comunes

#### Problema: NPC no se mueve

**Síntomas:**
- NPC reproduce animación pero no se desplaza
- `velocity = 0` en logs

**Verificar:**
1. ¿`NavMeshAgent.updatePosition = true`?
2. ¿NavMesh cubre el área?
3. ¿`isStopped = false`?
4. ¿Se llamó a `SetDestination()`?

**Solución:**
```csharp
_agent.isStopped = false;
_agent.updatePosition = true;
_agent.Warp(transform.position); // Re-sincronizar
_agent.SetDestination(target);
```

#### Problema: Animación atascada

**Síntomas:**
- NPC no transiciona a idle/locomotion
- Se queda en estado de combate

**Verificar:**
1. ¿Se llamó a `TransitionToLocomotion()`?
2. ¿`_isInCombat = false` en animator?

**Solución:**
```csharp
_animator.TransitionToLocomotion();
_animator.SetMovementSpeed(1f, 0.1f);
```

#### Problema: Música no se restaura

**Síntomas:**
- Música de victoria sigue sonando
- No vuelve la música de ambiente

**Verificar:**
1. ¿Se llamó a `RestoreBattleMusic()`?
2. ¿`isLastTeamMember` es correcto?

**Solución:**
```csharp
if (isLastTeamMember && AudioService.Instance != null)
{
    AudioService.Instance.RestoreBattleMusic(battleMusicId);
}
```

---

## 12. Mejores Prácticas

### 12.1 NPCs

✅ **DO:**
- Usar ScriptableObjects para configuración
- Cachear referencias en `Awake()`
- Usar FSM para comportamiento complejo
- Logs descriptivos con emojis
- Throttling para operaciones costosas

❌ **DON'T:**
- `FindObjectOfType` en `Update()`
- Physics queries sin NonAlloc
- Múltiples `GetComponent` sin cache
- Lógica de IA sin throttling
- Detection range > 30m sin necesidad

### 12.2 Combate

✅ **DO:**
- Usar layers para filtrar targets
- NavMeshAgent para movimiento
- Animaciones con eventos para timing
- Slow motion para impacto visual
- Secuencia de victoria después de combate

❌ **DON'T:**
- Detección por tag sin layer mask
- Movimiento manual sin NavMesh
- Daño instantáneo sin feedback visual
- Música de batalla sin restauración

### 12.3 Performance

✅ **DO:**
- `Physics.OverlapSphereNonAlloc`
- Buffer reutilizable (`_buffer = new Collider[32]`)
- Throttling (`Time.time - lastCheck > interval`)
- Object pooling para projectiles
- Cachear ComponentsInParent

❌ **DON'T:**
- `FindObjectsOfType` en runtime
- Allocations en loops
- Physics queries cada frame sin throttling
- Instantiate/Destroy frecuente

### 12.4 Arquitectura

✅ **DO:**
- ServiceLocator para singletons
- ScriptableObjects para configuración
- Events para desacoplamiento
- Módulos reutilizables
- Documentación inline

❌ **DON'T:**
- Referencias hardcoded
- Acoplamiento fuerte entre sistemas
- Código monolítico
- Magic numbers sin constants

---

## 13. Troubleshooting Avanzado - Problemas Resueltos (Febrero 2026)

### 13.1 Problema: Spawn Inconsistente Según Escena Inicial

#### Síntoma
- Iniciar desde Start: Player spawnea correctamente ✅
- Iniciar desde MainWorld: Player spawnea en anchor incorrecto ❌

#### Causa Raíz
`WorldBootstrap.cs` tenía un **timeout de solo 10 frames**:
```csharp
// ANTES (INCORRECTO):
int maxWaitFrames = 10;
while (!GameBootService.IsAvailable && framesWaited < maxWaitFrames)
{
    yield return null;
    framesWaited++;
}

if (!GameBootService.IsAvailable)
{
    // ❌ Fallback peligroso con anchor hardcodeado
    StartCoroutine(InitializeWorldWithoutBootService());
}
```

**Timeline del problema:**
```
Frame 0:  MainWorld cargada
Frame 1:  WorldBootstrap.Awake() - inicia espera
Frame 3:  AutoBootstrapOnPlay detecta que no es Start
Frame 4:  Carga Start.unity aditivamente (asíncrono)
Frame 5-10: WorldBootstrap esperando...
Frame 11: ❌ TIMEOUT → Usa "SpawnPoint" hardcodeado
Frame 15: GameBootService finalmente disponible (demasiado tarde)
```

#### Solución Aplicada

**Eliminado timeout y fallback:**
```csharp
// AHORA (CORRECTO):
while (!GameBootService.IsAvailable)
{
    yield return null;
    framesWaited++;
    
    // Timeout de seguridad solo para detectar errores FATALES
    if (framesWaited > 1800) // 30 segundos a 60fps
    {
        Debug.LogError("[WorldBootstrap] ❌ FATAL: GameBootService no disponible después de 30 segundos");
        yield break;
    }
}

Debug.Log($"[WorldBootstrap] ✅ GameBootService disponible después de {framesWaited} frame(s)");
HandleProfileReady();
```

**Archivo modificado:**
- `Assets/Scripts/World/WorldBootstrap.cs`

#### Verificación
```
Logs esperados desde MainWorld:
[WorldBootstrap] ✅ GameBootService disponible después de 3-15 frame(s)
[SpawnManager] ✅ Anchor establecido desde profile: '[ANCHOR_CORRECTO]'
```

---

### 13.2 Problema: Boss Trigger Funciona con JSON pero NO con Preset

#### Síntoma
```
CON JSON:    [Signals] Custom: EXIT_FROM_WOODS_ESTELA ✅
CON PRESET:  [Signals] Custom: EXIT_FROM_WOODS_ESTELA (sin oyentes → pendiente) ❌
```

#### Causa Raíz (Actualizada - Febrero 2026)

El problema era que `ResetForLoadedProfile()` llamaba a `ResetState()` que **limpiaba los eventos pendientes** (`_pending.Clear()`).

**Secuencia del problema:**
1. Player se posiciona en `Woods_Entrance_SavePoint` (área del trigger)
2. Trigger se activa y emite `EXIT_FROM_WOODS_ESTELA` → Se guarda en `_pending`
3. `NarrativeAutoSetup.ResetForLoadedProfile()` se llama → `ResetState()` **LIMPIA `_pending`** ❌
4. Grafo narrativo se inicia y llega al `WaitCustomEventNode`
5. `OnCustom()` busca en `_pending` pero ya está vacío → Se queda esperando permanentemente

**Por qué funcionaba con JSON:**
- El JSON guardaba el `__currentNodeGuid` apuntando al `WaitCustomEventNode`
- El grafo continuaba desde ese nodo, suscribiéndose al evento ANTES de que el trigger se activara
- Por timing, el listener existía cuando el trigger se disparaba

#### Solución Aplicada (Febrero 2026)

**Modificación en `DefaultNarrativeSignals.cs`:**
```csharp
// Nuevo método que permite preservar eventos pendientes
public void ResetState(bool preservePending)
{
    _custom.Clear();
    if (!preservePending)
    {
        _pending.Clear();
        _battlePending.Clear();
    }
    _battleSubscribers.Clear();
    _qs = null;
}
```

**Modificación en `NarrativeAutoSetup.cs`:**
```csharp
public static void ResetForLoadedProfile()
{
    // IMPORTANTE: Preservamos eventos pendientes porque el trigger puede activarse antes que el grafo
    _instance.HandleReset("ResetForLoadedProfile", clearBlackboard: false, restartGraph: false, preservePending: true);
}
```

**Archivos modificados:**
- `Assets/NarrativeGraph/Runtime/Integration/DefaultNarrativeSignals.cs`
- `Assets/NarrativeGraph/Runtime/Integration/NarrativeAutoSetup.cs`

#### Verificación
```
Al entrar al trigger con preset:
[Signals] Custom: EXIT_FROM_WOODS_ESTELA (sin oyentes → pendiente)  ← Evento guardado
[Signals] ResetState preservando 1 eventos pendientes              ← NO se limpia
[Signals] Custom: EXIT_FROM_WOODS_ESTELA (consumido desde pendientes) ← ✅ Grafo lo consume
[StartBattleNode] Suscrito a OnBattleWon                           ← ✅ BATALLA INICIA
```

#### Causa Anterior (Ya corregida)

Anteriormente también había un problema si el preset tenía el flag `EXIT_FROM_WOODS_ESTELA_received` marcado:

```yaml
# INCORRECTO - Si el preset tiene esto, el nodo avanza sin esperar
- key: __event_1db8d4ab-1b78-48f8-961e-9e0668aacf6e_EXIT_FROM_WOODS_ESTELA_received
  type: 
  value: 1
```

**Solución:** El preset NO debe tener el flag `EXIT_FROM_WOODS_ESTELA_received` si queremos que el trigger funcione.

#### Causa Adicional: Tipos Vacíos en Blackboard (Febrero 2026)

**Problema descubierto:** El sistema `SimpleBlackboard.ImportFromSerializable()` ignoraba entradas cuyo campo `type` estaba vacío:

```csharp
// Código original problemático:
if (e == null || string.IsNullOrEmpty(e.key) || string.IsNullOrEmpty(e.type)) continue; // ← SALTA si type vacío
```

**En el preset YAML:**
```yaml
- key: __currentNodeGuid
  type:          # ← VACÍO - la entrada se ignoraba!
  value: 1db8d4ab-1b78-48f8-961e-9e0668aacf6e
```

**Resultado:** El `__currentNodeGuid` nunca se restauraba aunque estuviera en el preset.

**Solución aplicada en `SimpleBlackboard.cs`:**
- Ahora infiere el tipo automáticamente basándose en la clave y el valor
- Claves con `_received` o `_started` → bool
- Valores con formato GUID → string
- Valores numéricos → int/float
- Por defecto → string

**Archivos modificados:**
- `Assets/NarrativeGraph/Runtime/Graph/SimpleBlackboard.cs`

---

### 13.3 Problema: Variables Estáticas Contaminadas Entre Sesiones de PlayMode

#### Síntoma
- Al hacer Play/Stop/Play múltiples veces en el Editor:
  - Managers duplicados
  - Eventos con múltiples suscriptores
  - Valores de variables incorrectos
  - Comportamiento impredecible

#### Causa
Unity **NO resetea variables estáticas** automáticamente entre sesiones de PlayMode.

**Ejemplo del problema:**
```csharp
public class SpawnManager : MonoBehaviour
{
    private static string CurrentAnchorId; // ← Mantiene valor entre sesiones
    
    void Awake()
    {
        if (string.IsNullOrEmpty(CurrentAnchorId))
            CurrentAnchorId = "DefaultSpawn";
        
        // Primera sesión: CurrentAnchorId = "Woods_SavePoint"
        // Segunda sesión: CurrentAnchorId = "Woods_SavePoint" (del anterior)
        // ❌ Puede causar spawn en posición incorrecta
    }
}
```

#### Solución Aplicada

**Patrón de reset automático:**
```csharp
public class SpawnManager : MonoBehaviour
{
    private static string CurrentAnchorId;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        CurrentAnchorId = null;
        OnAnchorChanged = null; // También eventos estáticos
    }
    #endif
    
    // ...resto del código
}
```

**`RuntimeInitializeLoadType.SubsystemRegistration`:**
- Se ejecuta **ANTES de cualquier Awake()**
- Solo en el Editor (builds no lo necesitan)
- Automático, no requiere llamadas manuales

#### Archivos Modificados (18 total)

**Críticos (afectaban gameplay):**
1. ⭐ `SpawnManager.cs` - Anchor contaminado
2. ⭐ `PlayerParty.cs` - Party members persistían
3. ⭐ `GamepadInputReader.cs` - Input contaminado
4. ⭐ `BossProgressTracker.cs` - Bosses derrotados incorrectos

**Importantes (managers):**
5. `PlayerInputManager.cs`
6. `AudioService.cs`
7. `MenuManager.cs`
8. `TeleportSystem.cs`
9. `TeleportRegistry.cs`
10. `TeleportUI.cs`

**Secundarios (evitan leaks):**
11. `EnvironmentController.cs`
12. `SimpleCinematicManager.cs`
13. `ProjectileCollisionService.cs`
14. `NPCInitializer.cs`
15. `GameState.cs`
16. `ActiveCombatRegistry.cs`
17. `PlayerEquipmentMenuController.cs`
18. `ProfileReadySubscriptionAnalyzer.cs`

#### Verificación

**Script de validación:**
```powershell
.\validate_static_resets.ps1
```

**Logs esperados:**
```
Sistemas con reset: 18/18 ✅
No se encontraron problemas
```

---

### 13.4 Sistema AutoBootstrapOnPlay

#### Propósito
Permite **iniciar PlayMode desde cualquier escena** sin necesidad de cargar Start manualmente.

#### Ubicación
```
Assets/Editor/AutoBootstrapOnPlay.cs
```

#### Funcionamiento
```csharp
[InitializeOnEnterPlayMode]
static void OnEnterPlayModeInEditor(EnterPlayModeOptions options)
{
    if (!EditorSceneManager.playModeStartScene)
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        
        // Si NO es Start, cargar Start primero
        if (activeScene.name != "Start")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Systems/Start.unity", 
                                         OpenSceneMode.Additive);
        }
    }
}
```

**Timeline:**
```
Usuario presiona Play en MainWorld
  ↓
AutoBootstrapOnPlay detecta que NO es Start
  ↓
Carga Start.unity aditivamente (2-5 frames)
  ↓
GameBootService.Awake() se ejecuta
  ↓
WorldBootstrap espera a GameBootService
  ↓
GameBootService dispara OnProfileReady
  ↓
SpawnManager recibe anchor correcto
  ↓
Player spawnea en posición correcta ✅
```

#### Configuración Requerida

**Build Settings:**
```
Start.unity debe estar en la lista de escenas
Index: 0 (primera escena)
```

**Script Execution Order:**
```
GameBootService: -1000 (se ejecuta primero)
```

---

### 13.5 Diferencias JSON vs Preset - Aclaración Definitiva

#### Arquitectura del Sistema

**NO hay código específico para JSON o Preset:**
```csharp
// GameBootService.cs
private void PrepareActivePreset()
{
    if (ShouldBootFromPreset())
    {
        // Cargar preset de testeo
        activePreset = testingPreset;
    }
    else
    {
        // Cargar último save (JSON)
        activePreset = GameBootProfile.LoadProfile(saveSystem);
    }
    
    // ✅ Mismo código para ambos casos:
    ApplyPreset(activePreset);
}
```

**La única diferencia es la fuente de datos:**
- **Modo Normal:** `activePreset` viene del JSON
- **Modo Testeo:** `activePreset` viene del SO

**Después de cargar, TODO es igual:**
- Mismo flujo de inicialización
- Mismas suscripciones
- Mismo spawn
- Mismas validaciones

#### Problema Común: Presets con Datos Incorrectos

**Presets creados DESPUÉS de eventos:**
```yaml
# ❌ Preset creado después del trigger del Golem:
- key: __event_EXIT_FROM_WOODS_ESTELA_received
  value: 1  ← El grafo piensa que ya pasó

# ✅ Preset creado ANTES del trigger:
# (No tiene ese flag, el grafo espera el evento)
```

**Solución:**
1. Crear presets en momentos específicos del juego
2. Documentar qué incluye cada preset
3. Verificar blackboards con `analyze_test_presets_clean.ps1`

---

### 13.6 Checklist de Resolución de Problemas

#### Problema: Player NO Spawnea Correctamente

**Verificar:**
```
□ GameBootService está en Script Execution Order (-1000)
□ Start.unity está en Build Settings (index 0)
□ AutoBootstrapOnPlay está en Assets/Editor/
□ Logs muestran: [WorldBootstrap] ✅ GameBootService disponible
□ Logs muestran: [SpawnManager] ✅ Anchor establecido desde profile
```

**Si falla desde MainWorld pero funciona desde Start:**
```
→ WorldBootstrap tiene timeout corto
→ Verificar que esperamos indefinidamente (ver sección 13.1)
```

---

#### Problema: Triggers Narrativos NO Funcionan con Preset

**Verificar:**
```
□ El preset NO tiene flags de eventos con "_received"
□ Ejecutar: .\analyze_test_presets_clean.ps1
□ Verificar blackboard en Inspector del preset
□ Logs muestran: (sin oyentes → pendiente) ← Si aparece, preset tiene el flag
```

**Si funciona con JSON pero NO con preset:**
```
→ El preset fue creado DESPUÉS del trigger
→ Limpiar blackboard del preset (ver sección 13.2)
```

---

#### Problema: Variables Estáticas Contaminadas

**Síntomas:**
```
□ Managers duplicados al hacer Play/Stop/Play
□ Eventos se disparan múltiples veces
□ Valores incorrectos en singletons
□ Comportamiento diferente en primera vs segunda sesión
```

**Verificar:**
```
□ Los singletons tienen ResetStatics()
□ El reset está marcado con #if UNITY_EDITOR
□ Usar: .\validate_static_resets.ps1
```

**Patrón correcto:**
```csharp
public class MiManager : MonoBehaviour
{
    private static MiManager _instance;
    public static MiManager Instance => _instance;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        // Resetear TODAS las variables estáticas
    }
    #endif
}
```

---

### 13.7 Scripts de Diagnóstico

#### Validar Resets de Estáticas
```powershell
.\validate_static_resets.ps1
```

**Output esperado:**
```
✅ SpawnManager tiene ResetStatics
✅ PlayerParty tiene ResetStatics
✅ GamepadInputReader tiene ResetStatics
...
Total: 18/18 sistemas con reset
```

#### Analizar Presets de Testeo
```powershell
.\analyze_test_presets_clean.ps1
```

**Output esperado:**
```
OK: PlayerPreset_Test.asset
OK: PlayerPreset_Test_Golem_noderrotado.asset
...
Total presets: 6
Con problemas: 0
Limpios: 6
```

---

### 13.8 Matriz de Compatibilidad (Actualizada Febrero 2026)

| Escenario | Spawn | Boss Trigger | Party | Quests | Estado |
|-----------|-------|--------------|-------|--------|--------|
| Start + JSON | ✅ | ✅ | ✅ | ✅ | Siempre funcionó |
| MainWorld + JSON | ✅ | ✅ | ✅ | ✅ | Arreglado Feb 2026 |
| Start + Preset | ✅ | ✅ | ✅ | ✅ | Arreglado Feb 2026 |
| MainWorld + Preset | ✅ | ✅ | ✅ | ✅ | Arreglado Feb 2026 |

**Conclusión:** Ahora es **consistente** iniciar desde cualquier escena con cualquier método de carga.

---

### 13.9 Logs de Referencia - Comportamiento Correcto

#### Inicialización Correcta (Cualquier Escena)
```
[AutoBootstrapOnPlay] Cargando Start.unity aditivamente
[GameBootService] Awake - Inicializando servicios core
[WorldBootstrap] ✅ GameBootService disponible después de 3 frame(s)
[SpawnManager] ✅ Anchor establecido desde profile: 'Woods_Entrance_SavePoint'
[PlayerParty] 🔄 Restaurando 1 miembros del party
[ProfileReadyDiagnostics] 📊 Suscripciones: 13/13 presentes esperados
```

#### Boss Trigger Correcto (Con Preset Limpio)
```
[BossArenaController] 🚪 OnTriggerEnter - Tag: Player
[Signals] Custom: EXIT_FROM_WOODS_ESTELA
[StartBattleNode] Suscrito a OnBattleWon con context='Golem_1'
[BossArenaController] 🔍 Tracker encontrado - IsDefeated=False
[BossArenaController] Área del boss bloqueada
[Signals] Custom: BATTLE_START:Golem_1
[BossHealthBar] UI creada para 'Boss Golem'
```

---

## 📝 Notas Finales

Este documento consolida TODA la documentación técnica del proyecto "El Sendero de las Estrellas". 

**Última actualización:** 11 Febrero 2026

**Cambios recientes:**
- ✅ Fix de spawn inconsistente (WorldBootstrap timeout)
- ✅ Fix de boss triggers con presets (blackboard limpiado)
- ✅ Reset de variables estáticas (18 archivos)
- ✅ Sistema AutoBootstrapOnPlay documentado
- ✅ Troubleshooting avanzado agregado

**Todos los archivos MD individuales han sido consolidados aquí.**

Para reportar problemas o sugerir mejoras, contactar al equipo de desarrollo.

---

**🌟 El Sendero de las Estrellas - Documentación Técnica Completa v2.1 🌟**

