# Sistema de Juego - Alex Adventure
## Documentación Técnica Completa

### 📋 Índice
- [Arquitectura General](#arquitectura-general)
- [Sistema de Localización](#sistema-de-localización)
- [GameBootService y GameBootProfile](#gamebootservice-y-gamebootprofile)
- [Sistema de Salud del Jugador](#sistema-de-salud-del-jugador)
- [Sistema de Maná](#sistema-de-maná)
- [Sistema de Spawn y Anchors](#sistema-de-spawn-y-anchors)
- [Sistema de Interacciones](#sistema-de-interacciones)
- [Sistema de UI](#sistema-de-ui)
- [Sistema de Feedback](#sistema-de-feedback)
- [Sistema de Save/Load](#sistema-de-saveload)
- [Guías de Uso](#guías-de-uso)

---

## 🏗️ Arquitectura General

El juego utiliza una arquitectura centralizada basada en **GameBootService** que gestiona el **GameBootProfile** desde la escena start. Se eliminó el PlayerState para simplificar la gestión de datos y se reemplazó el sistema singleton automático por un servicio explícito.

### Componentes Principales:
- **GameBootService** - Servicio en escena start que gestiona el GameBootProfile
- **GameBootProfile** - ScriptableObject con configuración y estado del juego (sin singleton automático)
- **PlayerPresetSO** - Datos del jugador (vida, maná, habilidades, etc.)
- **PlayerHealthSystem** - Gestión específica de vida del jugador
- **ManaPool** - Gestión de maná
- **SpawnManager** - Control de posición y anchors
- **LocalizationManager** - Sistema multiidioma

### Flujo de Inicialización:
1. **Escena Start** → GameBootService carga GameBootProfile SO
2. Prepara el runtimePreset (ver Save/Load)
3. Notifica `OnProfileReady` a los sistemas dependientes
4. **Acceso** → `GameBootService.Profile.GetActivePresetResolved()` en lugar de singletons ocultos

---

## 🌍 Sistema de Localización

### LocalizationManager.cs
Sistema completo de localización con soporte para múltiples idiomas.

**Características:**
- Carga automática desde archivos JSON en `Resources/Localization/`
- Sistema de fallback al idioma por defecto
- Soporte para textos UI y subtítulos
- Cambio dinámico de idioma

**Archivos JSON:**
- `ui_es.json` - Textos de interfaz en español
- `ui_en.json` - Textos de interfaz en inglés  
- `prologue_es.json` - Diálogos del prólogo en español

### Enumerados de Localización
```csharp
public enum UITextId 
{
    MainMenu_NewGame,
    MainMenu_Continue,
    Settings_Language,
    UI_Health,
    UI_Mana,
    Interact_Press,
    // ... más IDs
}

public enum DialogueId
{
    NPC_Villager_01,
    Object_Sign_01,
    Tutorial_Movement,
    // ... más IDs
}
```

### LocalizedUI.cs
Componente para textos UI que se actualizan automáticamente:
```csharp
[SerializeField] private UITextId textId;
[SerializeField] private string fallbackText = "";
```

### LocalizedMessage.cs
Para mostrar mensajes simples usando DialogueManager:
```csharp
[SerializeField] private DialogueId messageId = DialogueId.Object_Sign_01;
public void ShowMessage() // Muestra el mensaje localizado
```

### LocalizationTester.cs
Para testing de idiomas usando nuevo Input System:
- **Tecla Q** - Español
- **Tecla W** - Inglés  
- **Tecla E** - Alternar entre ambos
- **Tecla U** - Ayuda
- Métodos públicos para UI: `ChangeToSpanish()`, `ChangeToEnglish()`

### SubtitleController.cs
Sistema de subtítulos integrado con localización:
```csharp
// Métodos principales
public void ShowLineById(string id)           // Usa LocalizationManager
public void ShowLineByIdTimed(string id, float time)
public void ShowSubtitleEvent(string id)     // Para Animation Events
```

**Características:**
- Actualización automática al cambiar idioma
- Espera a LocalizationManager antes de suscribirse
- Compatible con Timeline y Animation Events

---

## 🎮 GameBootService y GameBootProfile

### GameBootService.cs
**Servicio principal** que gestiona el GameBootProfile desde la escena start.

```csharp
public class GameBootService : MonoBehaviour
{
    [SerializeField] private GameBootProfile bootProfile;
    
    // API estática
    public static GameBootService Instance { get; }
    public static GameBootProfile Profile => Instance?.bootProfile;
    
    // Métodos principales
    public static GameBootProfile GetProfile()
    public static bool IsReady()
    public static PlayerPresetSO GetActivePreset()
}
```

**Configuración:**
1. Crear GameObject en escena start
2. Añadir componente GameBootService
3. Asignar GameBootProfile SO en inspector
4. Se hace DontDestroyOnLoad automáticamente

### GameBootProfile.cs (SO sin Singleton)
```csharp
[CreateAssetMenu(fileName = "GameBootProfile", menuName = "Game/Boot Profile")]
public class GameBootProfile : ScriptableObject
{
    [Header("Arranque")]
    public string sceneToLoad = "MainWorld";
    public string defaultAnchorId = "Bedroom";
    public PlayerPresetSO defaultPlayerPreset;

    [Header("Boot Settings")]
    public bool usePresetInsteadOfSave = false;
    public PlayerPresetSO bootPreset;
    public string startAnchorId = "Bedroom";

    [Header("Runtime Fallback")]
    public PlayerPresetSO runtimePreset;
}
```

### Métodos Principales
```csharp
// Obtener preset activo
PlayerPresetSO GetActivePresetResolved()

// Gestión de save/load
bool SaveCurrentGameState(SaveSystem saveSystem)
bool LoadProfile(SaveSystem saveSystem)

// Aplicar datos de save al preset en runtime
void SetRuntimePresetFromSave(PlayerSaveData data)
```

Notas de comportamiento:
- `SetRuntimePresetFromSave` aplica level, HP/MP, abilities, spells, flags y anchor del save al `runtimePreset`.
- Slots al cargar desde save: si hay spells desbloqueados, se asigna el primero al slot izquierdo; los slots derecho y especial quedan vacíos (None).
- `GetActivePresetResolved` siempre devuelve el `runtimePreset` (asegurándolo si hace falta), clonando de `bootPreset` o `defaultPlayerPreset` como fallback.

### Flujo de Inicialización
1. **Modo Preset** - Si `usePresetInsteadOfSave = true`, usa `bootPreset`
2. **Modo Normal** - Carga save y crea `runtimePreset`
3. **Fallback** - Usa `defaultPlayerPreset` si no hay otros

### Patrón de Uso en Scripts
```csharp
private IEnumerator DelayedInitialization()
{
    // Esperar hasta que GameBootService esté disponible
    while (!GameBootService.IsReady())
    {
        yield return new WaitForSeconds(0.1f);
    }
    
    // Usar el servicio
    var bootProfile = GameBootService.GetProfile();
    var preset = bootProfile?.GetActivePresetResolved();
    // ... lógica del script
}
```

---

## 💚 Sistema de Salud del Jugador

### PlayerHealthSystem.cs
Sistema independiente que se sincroniza con GameBootService.

**Configuración:**
```csharp
[Header("Configuración de Daño")]
[SerializeField] private float invulnerabilityDuration = 1f;
[SerializeField] private bool godMode = false;

[Header("Efectos Visuales")]
[SerializeField] private float damageFlashDuration = 0.2f;
[SerializeField] private Color damageFlashColor = Color.red;
[SerializeField] private GameObject damageVFX;
```

**API Principal:**
```csharp
// Propiedades
bool IsAlive { get; }
float CurrentHealth { get; }
float MaxHealth { get; }
float HealthPercentage { get; }

// Métodos
bool TakeDamage(float amount)
bool Heal(float amount)
void Kill()
void Revive(float healthPercentage = 1f)
void SetMaxHealth(float newMaxHp)
void SetCurrentHealth(float newCurrentHp)
```

**Eventos:**
```csharp
public UnityEvent<float> OnHealthChanged;
public UnityEvent<float, float> OnDamageTaken;
public UnityEvent OnPlayerDeath;
public UnityEvent OnPlayerRevived;
```

**Sincronización Automática:**
- Lee valores iniciales del GameBootService al arrancar
- Actualiza el GameBootProfile cuando cambia la vida
- Efectos visuales, sonoros y animaciones integradas

---

## 🔵 Sistema de Maná

### ManaPool.cs
```csharp
public float Max { get; }
public float Current { get; }

public void Init(float maxMP, float currentMP)
public bool TrySpend(float amount)
public void Refill(float amount)
```

Se sincroniza automáticamente con GameBootService para persistencia.

---

## 📍 Sistema de Spawn y Anchors

### PlayerPresetSO - Campo Anchor
```csharp
[Header("Spawn")]
[Tooltip("ID del anchor donde debe aparecer el jugador con este preset")]
public string spawnAnchorId = "Bedroom";
```

### SpawnManager.cs
```csharp
public static string CurrentAnchorId { get; }
public static event Action<string> OnAnchorChanged;

public static void SetCurrentAnchor(string id)
public static void PlaceAtAnchor(GameObject player, string anchorId, bool immediate = true)
```

**Inicialización:**
- Espera a GameBootService antes de inicializar
- Lee anchor por defecto del GameBootProfile

### WorldBootstrap.cs
Inicialización del mundo:
1. Espera a `GameBootService.IsReady()`
2. Aplica preset o carga save
3. Establece anchor y coloca jugador

---

## 🎯 Sistema de Interacciones

### Interactable.cs
```csharp
public enum Mode { OpenDialogue, HandOffToTarget }

[Header("Modo")]
[SerializeField] private Mode mode = Mode.OpenDialogue;

[Header("Abrir diálogo")]
[SerializeField] private DialogueAsset dialogue;

[Header("Ceder control")]
[SerializeField] private MonoBehaviour sessionTarget; // IInteractionSession
```

**Controles:**
- **Botón A del mando** - Interacción principal
- **Input System** - Compatible con nuevo sistema de Unity

### InteractionDetector.cs
Detecta objetos interactuables cerca del jugador.

### Scripts de Mundo

#### SavePoint.cs
```csharp
[Header("Config")]
public string anchorIdToSet;
public bool healOnSave = true;
public KeyCode interactKey = KeyCode.E;
```
- Cura al jugador via PlayerHealthSystem + ManaPool
- Guarda usando `GameBootService.GetProfile().SaveCurrentGameState()`

#### PortalTrigger.cs
```csharp
public string targetAnchorId;
public string requiredFlag;      // Flag requerida para usar
public string setFlagOnEnter;    // Flag a establecer al entrar
```
- Verifica flags en `GameBootService.GetProfile().preset.flags`
- Teletransporta usando `TeleportService`

#### AnchorSetter.cs
```csharp
public string anchorId;
```
- Establece anchor actual via `SpawnManager.SetCurrentAnchor()`
- Usa GameBootService para verificaciones

---

## 🖼️ Sistema de UI

### PlayerAbilitiesUI.cs
Muestra habilidades y hechizos desbloqueados:
```csharp
[Header("Referencias UI - Habilidades")]
[SerializeField] private Transform abilitiesContainer;
[SerializeField] private GameObject abilityUIPrefab;

[Header("Referencias UI - Información")]
[SerializeField] private TextMeshProUGUI levelText;
[SerializeField] private TextMeshProUGUI manaText;

[Header("Configuración")]
[SerializeField] private bool autoRefresh = true;
[SerializeField] private float refreshInterval = 1f;
```

**Características:**
- Espera a GameBootService antes de inicializar
- Se actualiza automáticamente desde GameBootService
- Refresh por intervalo configurable
- Muestra nivel, vida, maná, habilidades y hechizos
- Usa `DestroyImmediate()` para limpiar UI

**Patrón de inicialización:**
```csharp
private IEnumerator DelayedInitialization()
{
    while (!GameBootService.IsReady())
        yield return new WaitForSeconds(0.1f);
    
    FindPlayerComponents();
    RefreshAll();
    
    if (autoRefresh)
        InvokeRepeating(nameof(RefreshAll), refreshInterval, refreshInterval);
}
```

### PlayerHealthUI.cs
UI específica para barra de vida:
- Se conecta automáticamente con PlayerHealthSystem
- Actualización en tiempo real via eventos

---

## 🌊 Sistema de Flotación

### PlayerWaterFloat.cs
Sistema para flotar en agua:
```csharp
[Header("Configuración de Flotación")]
[SerializeField] private float buoyancyForce = 15f;
[SerializeField] private float waterDrag = 2f;
[SerializeField] private LayerMask waterLayerMask = -1;
```

**Funcionamiento:**
- Detecta entrada/salida del agua via Triggers
- Aplica fuerza de flotación basada en profundidad
- Cambia propiedades físicas (drag, angular drag)
- Sistema de debug con Gizmos

---

## 🌟 Sistema de Feedback

Sistema centralizado de retroalimentación visual/sonora. Punto único de entrada: `FeedbackService` (singleton auto-instalable, DontDestroyOnLoad) bajo el namespace `Oblivion.Core.Feedback`.

- Orquestador: `FeedbackService`
- Proveedores (Strategy) por defecto:
  - `ICameraShakeProvider` → `TransformPivotCameraShakeProvider` (sacude solo la Main Camera base mediante un pivot; no mueve al Player; compatible con URP camera stacking).
  - `IScreenFlashProvider` → `UiOverlayScreenFlashProvider` (Canvas Overlay con Image fullscreen y fade de alpha).
  - `IHitStopProvider` → `SimpleHitStopProvider` (ajusta temporalmente `Time.timeScale` y usa `Time.unscaledDeltaTime`).
  - `IVfxProvider` → `SimpleVfxProvider` (instancia prefab VFX con vida limitada y limpieza automática).
  - `ISfxProvider` → `SimpleSfxProvider` (AudioSource 3D temporal en posición, limpieza al terminar).

API de uso (desde cualquier script)
- Camera Shake: `FeedbackService.CameraShake(0.6f, 0.25f)`
- Screen Flash: `FeedbackService.ScreenFlash(new Color(1,0,0,0.35f), 0.25f)`
- Hit Stop: `FeedbackService.HitStop(0f, 0.06f)`
- VFX: `FeedbackService.PlayVFX(prefab, position, rotation, 2f)`
- SFX: `FeedbackService.PlaySfx(clip, position, 0.9f)`

Sustituir proveedores (por ejemplo, Cinemachine):
```csharp
using Oblivion.Core.Feedback;

void Awake()
{
    FeedbackService.SetCameraShakeProvider(new CinemachineCameraShakeProvider());
    // FeedbackService.SetScreenFlashProvider(...);
    // FeedbackService.SetHitStopProvider(...);
    // FeedbackService.SetVfxProvider(...);
    // FeedbackService.SetSfxProvider(...);
}
```

Integraciones clave
- `PlayerHealthSystem` llama a `FeedbackService.CameraShake` al recibir daño.
- En URP camera stacking, sólo la Main Camera con tag `MainCamera` tiembla; la overlay de UI/interactuables queda estable.

---

## 💾 Sistema de Save/Load

Flujo de arranque (preparación del `runtimePreset`):
1. Si `usePresetInsteadOfSave = true` y hay `bootPreset` → se copia al `runtimePreset` (modo test/desarrollo).
2. Si existe archivo de guardado (con `SaveSystem`) → se carga `PlayerSaveData` y se vuelca al `runtimePreset`.
3. En otro caso → se copia `defaultPlayerPreset` al `runtimePreset` (fallback); si falta, se crea vacío.

Reglas/convenciones:
- Todos los sistemas leen/escriben sobre el `runtimePreset` vía `GameBootService.Profile.GetActivePresetResolved()`.
- El punto de spawn se guarda en `PlayerPresetSO.spawnAnchorId` (no en GameBootProfile). `SpawnManager.SetCurrentAnchor(id)` sincroniza `runtimePreset.spawnAnchorId` y `SpawnManager.CurrentAnchorId`.
- Salud/Maná:
  - `PlayerHealthSystem` sincroniza `currentHP/maxHP` con el `runtimePreset` al cambiar.
  - `ManaPool` hace lo mismo con `currentMP/maxMP` (si está presente).

Guardar partida:
- `GameBootProfile.SaveCurrentGameState(saveSystem)`:
  1) Actualiza el `runtimePreset` con el estado actual (anchor, HP/MP, etc.).
  2) Construye `PlayerSaveData` desde el `runtimePreset` y lo guarda en JSON.

Cargar partida:
- `GameBootProfile.LoadProfile(saveSystem)`:
  1) Lee `PlayerSaveData` del JSON.
  2) Aplica los datos al `runtimePreset` (nivel, HP/MP, habilidades, hechizos, flags, anchor...).

Notas prácticas:
- Para testear, usa `usePresetInsteadOfSave = true` y asigna `bootPreset` con la configuración deseada.
- En escenas nuevas, coloca `SpawnAnchor` con el `anchorId` que usarás (p. ej. "Bedroom").
- Asegúrate de que sólo la cámara base del stack tenga el tag `MainCamera`.

---

## 🧭 Guías de Uso

- Acceso a datos del jugador:
```csharp
var profile = GameBootService.Profile;
var preset  = profile.GetActivePresetResolved();
// leer/escribir sobre preset: vida, maná, hechizos, flags, anchor...
```

- Teletransporte:
```csharp
SpawnManager.SetCurrentAnchor("Bedroom"); // sincroniza runtimePreset.spawnAnchorId
SpawnManager.TeleportToCurrent(true);       // opcional: con transición si está disponible
```

- Guardar/Continuar:
```csharp
var saveSystem = FindObjectOfType<SaveSystem>(true);
GameBootService.Profile.SaveCurrentGameState(saveSystem); // guardar

// Al iniciar:
// GameBootService prepara automáticamente runtimePreset desde test/save/default
```
