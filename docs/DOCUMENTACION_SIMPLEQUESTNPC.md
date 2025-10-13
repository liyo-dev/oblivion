# Documentación: SimpleQuestNPC - Sistema de Cadenas de Misiones

## Descripción General

`SimpleQuestNPC` es un componente que permite crear cadenas de misiones (quest chains) de N misiones anidadas. El sistema gestiona automáticamente la progresión de una misión a la siguiente, con diálogos específicos para cada estado y transiciones fluidas entre misiones.

**NUEVO**: Ahora incluye **modos de completado automático** para simplificar la gestión de misiones sin necesidad de código adicional.

## Características Principales

- ✅ **N misiones anidadas** en secuencia
- ✅ **Progresión automática o manual** entre misiones
- ✅ **Diálogos específicos** para cada estado de cada misión
- ✅ **3 modos de completado**: Manual, Auto al hablar, o Condicional
- ✅ **Métodos de consulta** para verificar estado y progreso
- ✅ **Métodos helper** para gestionar la cadena desde código
- ✅ **Herramientas de depuración** integradas

---

## 🆕 Modos de Completado de Misiones

Cada misión en la cadena puede configurarse con uno de estos **3 modos**:

### 1. **Manual** (Por defecto)
- Requiere completar todos los pasos manualmente
- El sistema original - máximo control
- **Uso**: Misiones complejas con múltiples pasos

### 2. **AutoCompleteOnTalk** ⭐ NUEVO
- **Se completa automáticamente al hablar con el NPC**
- Marca todos los pasos como completados automáticamente
- **Uso perfecto para**: "Ve a hablar con X", "Informa a Y"
- **Sin código necesario** - todo desde el Inspector

### 3. **CompleteOnTalkIfStepsReady** ⭐ NUEVO
- Solo se completa al hablar **si todos los pasos ya están listos**
- Si faltan pasos, muestra diálogo de progreso
- **Uso perfecto para**: "Tráeme la caja", "Consigue 5 objetos"
- Los pasos se completan por otros sistemas (SimpleQuestPickup, etc.)

---

## Configuración Rápida - Tu Caso de Uso

### Ejemplo: Carta → Hablar → Traer Caja

```
Misión 1: "Ve a hablar con el guardia"
- Completion Mode: AutoCompleteOnTalk
- Al hablar, se completa automáticamente ✓

Misión 2: "Tráele la caja al guardia"
- Completion Mode: CompleteOnTalkIfStepsReady
- La caja se recoge (otro sistema marca el paso)
- Al hablar con el guardia, si tiene la caja, se completa ✓
- Si no tiene la caja, le recuerda que la necesita
```

---

## Configuración Básica

### Requisitos

- **Componente Interactable** en el mismo GameObject
- **QuestManager** en la escena
- **DialogueManager** en la escena

### Setup Inicial

1. Agregar el componente `SimpleQuestNPC` a un GameObject con `Interactable`
2. Configurar la cadena de misiones en el Inspector
3. **Seleccionar el modo de completado** para cada misión
4. Asignar diálogos para cada estado de cada misión

---

## Estructura de la Cadena de Misiones

### QuestChainEntry

Cada entrada en la cadena contiene:

```csharp
[System.Serializable]
public class QuestChainEntry
{
    public QuestData questData;                // La misión en sí
    public QuestCompletionMode completionMode; // NUEVO: Modo de completado
    public int talkStepIndex;                  // Índice del paso "hablar con NPC" (default: 0)
    
    // Diálogos según estado
    public DialogueAsset dlgBefore;            // Antes de aceptar la quest
    public DialogueAsset dlgInProgress;        // Mientras está activa (y no completa)
    public DialogueAsset dlgTurnIn;            // Al entregar la quest (todos los pasos completos)
    public DialogueAsset dlgCompleted;         // Después de completarla
    
    // Transición
    public DialogueAsset dlgNextQuestOffer;    // Al ofrecer la siguiente quest
}
```

### Enum QuestCompletionMode

```csharp
public enum QuestCompletionMode
{
    Manual,                    // Requiere completar pasos manualmente
    AutoCompleteOnTalk,        // Se completa automáticamente al hablar
    CompleteOnTalkIfStepsReady // Solo se completa al hablar si pasos están listos
}
```

### Parámetros del Componente

- **questChain**: Lista de `QuestChainEntry` que define la cadena completa
- **autoStartNextQuest**: Si `true`, las misiones siguientes se inician automáticamente al completar la anterior

---

## Uso en el Inspector

### Configuración Paso a Paso

1. **Crear la cadena de quests:**
   - En el Inspector, expandir "Quest Chain"
   - Agregar elementos a la lista (tantos como misiones necesites)

2. **Configurar cada Quest Entry:**
   ```
   Quest Data: [Arrastrar QuestData asset]
   
   ⭐ Completion Mode: [Seleccionar modo]
      - Manual: Para control total
      - AutoCompleteOnTalk: Se completa al hablar
      - CompleteOnTalkIfStepsReady: Se completa si pasos listos
   
   Talk Step Index: 0 (o el índice del paso de hablar)
   
   Diálogos:
   - Dlg Before: Diálogo inicial (ofrecer quest)
   - Dlg In Progress: Recordatorio de objetivos
   - Dlg Turn In: Diálogo al completar
   - Dlg Completed: Diálogo después de completada
   - Dlg Next Quest Offer: Ofrecer siguiente misión
   ```

3. **Configurar auto-inicio:**
   - Marcar `autoStartNextQuest` si quieres progresión automática
   - Desmarcar si prefieres control manual

---

## 📋 Ejemplos de Configuración por Tipo

### Tipo 1: Misión "Ve a Hablar"
```
Quest: "Habla con el Guardia"
Completion Mode: AutoCompleteOnTalk ⭐
Talk Step Index: 0

Flujo:
1. Carta activa la quest
2. Jugador va al NPC
3. Al hablar → Se completa automáticamente
4. Siguiente quest se ofrece
```

### Tipo 2: Misión "Trae Objeto"
```
Quest: "Trae la Caja al Guardia"
Completion Mode: CompleteOnTalkIfStepsReady ⭐
Steps:
  [0] Hablar con guardia
  [1] Conseguir la caja

Flujo:
1. Quest activa
2. Jugador habla con guardia → Paso 0 completado
3. Jugador recoge caja (SimpleQuestPickup) → Paso 1 completado
4. Jugador vuelve al guardia → Quest se completa
```

### Tipo 3: Misión "Mata Enemigos"
```
Quest: "Derrota 5 Goblins"
Completion Mode: CompleteOnTalkIfStepsReady ⭐
Steps:
  [0] Hablar con guardia
  [1] Derrotar goblins (conditionId: "KILL_GOBLINS_5")

Flujo:
1. Quest activa
2. Jugador habla → Paso 0 completado
3. Si vuelve sin matar goblins → dlgInProgress
4. Mata 5 goblins → Paso 1 completado
5. Vuelve al guardia → Quest se completa
```

### Tipo 4: Misión Compleja (Control Manual)
```
Quest: "Misión de infiltración"
Completion Mode: Manual
Steps: Múltiples pasos complejos controlados por código

Flujo:
- Tu código controla cuándo se completan los pasos
- SimpleQuestNPC solo gestiona los diálogos
```

---

## Comparación de Modos

| Modo | Cuándo usar | Completa pasos | Al hablar |
|------|-------------|----------------|-----------|
| **Manual** | Control total por código | Por código | Verifica si todos completos |
| **AutoCompleteOnTalk** | "Ve a hablar con X" | Automáticamente | Completa inmediatamente |
| **CompleteOnTalkIfStepsReady** | "Tráeme X", "Mata Y" | Por otros sistemas | Completa si todos listos |

---

## API de Métodos Helper

### 1. Métodos para Agregar Misiones

#### Agregar una misión simple
```csharp
public void AddQuestToChain(QuestData quest)
```
**Uso:**
```csharp
npc.AddQuestToChain(misionData);
```

#### Agregar múltiples misiones
```csharp
public void AddQuestsToChain(params QuestData[] quests)
```
**Uso:**
```csharp
npc.AddQuestsToChain(quest1, quest2, quest3, quest4, quest5);
```

#### Agregar misión con todos los diálogos
```csharp
public void AddQuestToChainWithDialogues(
    QuestData quest, 
    DialogueAsset dlgBefore = null,
    DialogueAsset dlgInProgress = null, 
    DialogueAsset dlgTurnIn = null,
    DialogueAsset dlgCompleted = null, 
    DialogueAsset dlgNextQuestOffer = null, 
    int talkStepIndex = 0
)
```
**Uso:**
```csharp
npc.AddQuestToChainWithDialogues(
    misionData,
    dialogoInicio,
    dialogoProgreso,
    dialogoEntrega,
    dialogoCompletado,
    dialogoSiguiente,
    0
);
```

### 2. Métodos para Gestionar la Cadena

#### Insertar en posición específica
```csharp
public void InsertQuestAtIndex(int index, QuestData quest)
```
**Uso:**
```csharp
npc.InsertQuestAtIndex(2, nuevaMision); // Insertar en posición 2
```

#### Remover por índice
```csharp
public void RemoveQuestAtIndex(int index)
```
**Uso:**
```csharp
npc.RemoveQuestAtIndex(1); // Remover la segunda misión
```

#### Remover por referencia
```csharp
public bool RemoveQuest(QuestData quest)
```
**Uso:**
```csharp
if (npc.RemoveQuest(misionData))
{
    Debug.Log("Misión removida exitosamente");
}
```

#### Limpiar toda la cadena
```csharp
public void ClearQuestChain()
```
**Uso:**
```csharp
npc.ClearQuestChain();
```

### 3. Métodos de Consulta Básica

#### Obtener longitud de la cadena
```csharp
public int GetChainLength()
```
**Uso:**
```csharp
int totalQuests = npc.GetChainLength();
Debug.Log($"Total de misiones: {totalQuests}");
```

#### Verificar si contiene una misión
```csharp
public bool HasQuest(QuestData quest)
```
**Uso:**
```csharp
if (npc.HasQuest(misionData))
{
    Debug.Log("Esta misión está en la cadena");
}
```

#### Obtener índice de una misión
```csharp
public int GetQuestIndex(QuestData quest)
```
**Uso:**
```csharp
int indice = npc.GetQuestIndex(misionData);
Debug.Log($"La misión está en posición: {indice}");
```

#### Obtener entry por índice
```csharp
public QuestChainEntry GetQuestEntry(int index)
```
**Uso:**
```csharp
var entry = npc.GetQuestEntry(0);
if (entry != null)
{
    Debug.Log($"Primera misión: {entry.questData.questId}");
}
```

---

## API de Consulta de Estado (Runtime)

### 1. Verificar Misiones Completadas

#### Verificar una misión específica
```csharp
public bool IsQuestCompleted(QuestData quest)
```
**Uso:**
```csharp
if (npc.IsQuestCompleted(mision1))
{
    Debug.Log("¡Misión 1 completada!");
}
```

#### Verificar por índice
```csharp
public bool IsQuestCompletedAtIndex(int index)
```
**Uso:**
```csharp
if (npc.IsQuestCompletedAtIndex(0))
{
    Debug.Log("¡Primera misión completada!");
}
```

#### Verificar toda la cadena
```csharp
public bool IsChainCompleted()
```
**Uso:**
```csharp
if (npc.IsChainCompleted())
{
    Debug.Log("¡Todas las misiones completadas!");
    // Otorgar recompensa especial
}
```

### 2. Verificar Misiones Activas

```csharp
public bool IsQuestActive(QuestData quest)
```
**Uso:**
```csharp
if (npc.IsQuestActive(mision2))
{
    Debug.Log("Misión 2 está activa");
}
```

### 3. Obtener Estado de Misiones

#### Estado de una misión específica
```csharp
public QuestState GetQuestState(QuestData quest)
```
**Uso:**
```csharp
QuestState estado = npc.GetQuestState(mision3);
switch (estado)
{
    case QuestState.Inactive:
        Debug.Log("No iniciada");
        break;
    case QuestState.Active:
        Debug.Log("En progreso");
        break;
    case QuestState.Completed:
        Debug.Log("Completada");
        break;
}
```

#### Estado por índice
```csharp
public QuestState GetQuestStateAtIndex(int index)
```
**Uso:**
```csharp
QuestState estado = npc.GetQuestStateAtIndex(2);
```

### 4. Obtener Listas y Contadores

#### Lista de misiones completadas
```csharp
public List<QuestData> GetCompletedQuests()
```
**Uso:**
```csharp
var completadas = npc.GetCompletedQuests();
Debug.Log($"Misiones completadas: {completadas.Count}");
foreach (var quest in completadas)
{
    Debug.Log($"- {quest.displayName}");
}
```

#### Índices de misiones completadas
```csharp
public List<int> GetCompletedQuestIndices()
```
**Uso:**
```csharp
var indices = npc.GetCompletedQuestIndices();
Debug.Log($"Misiones completadas en posiciones: {string.Join(", ", indices)}");
// Ejemplo: "0, 1, 3" significa que completaste 1ª, 2ª y 4ª
```

#### Contador de completadas
```csharp
public int GetCompletedQuestCount()
```
**Uso:**
```csharp
int completadas = npc.GetCompletedQuestCount();
int total = npc.GetChainLength();
Debug.Log($"Progreso: {completadas}/{total}");
```

### 5. Información de Progreso

#### Índice de misión actual
```csharp
public int GetCurrentQuestIndex()
```
**Uso:**
```csharp
int actual = npc.GetCurrentQuestIndex();
if (actual >= 0)
{
    Debug.Log($"Misión activa: {actual + 1}");
}
else
{
    Debug.Log("Ninguna misión activa");
}
```

#### Porcentaje de progreso
```csharp
public float GetChainProgressPercentage()
```
**Uso:**
```csharp
float progreso = npc.GetChainProgressPercentage();
Debug.Log($"Progreso: {progreso:F1}%");

// Mostrar en UI
progressBar.fillAmount = progreso / 100f;
progressText.text = $"{progreso:F0}%";
```

### 6. Estado Detallado de la Cadena

```csharp
public List<QuestChainStatus> GetChainStatus()
```

**Estructura de QuestChainStatus:**
```csharp
public class QuestChainStatus
{
    public int index;              // Índice en la cadena
    public string questId;         // ID de la quest
    public string questName;       // Nombre para mostrar
    public QuestState state;       // Estado (Inactive/Active/Completed)
    public bool isCompleted;       // Atajos booleanos
    public bool isActive;          //
}
```

**Uso:**
```csharp
var status = npc.GetChainStatus();
foreach (var s in status)
{
    string icono = s.isCompleted ? "✓" : (s.isActive ? "→" : "○");
    Debug.Log($"{icono} [{s.index}] {s.questName} - {s.state}");
}

// Ejemplo de salida:
// ✓ [0] Misión Tutorial - Completed
// ✓ [1] Encuentra el libro - Completed
// → [2] Habla con el mago - Active
// ○ [3] Derrota al jefe - Inactive
// ○ [4] Recompensa final - Inactive
```

---

## Ejemplos de Uso Completos

### Ejemplo 1: Crear cadena de 5 misiones por código

```csharp
using UnityEngine;

public class QuestChainSetup : MonoBehaviour
{
    [SerializeField] private SimpleQuestNPC npc;
    
    [Header("Quest Data Assets")]
    [SerializeField] private QuestData quest1;
    [SerializeField] private QuestData quest2;
    [SerializeField] private QuestData quest3;
    [SerializeField] private QuestData quest4;
    [SerializeField] private QuestData quest5;
    
    void Start()
    {
        // Método 1: Agregar todas de una vez
        npc.AddQuestsToChain(quest1, quest2, quest3, quest4, quest5);
        
        Debug.Log($"Cadena creada con {npc.GetChainLength()} misiones");
    }
}
```

### Ejemplo 2: Sistema de UI de progreso

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestProgressUI : MonoBehaviour
{
    [SerializeField] private SimpleQuestNPC npc;
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI detailText;
    
    void Update()
    {
        UpdateProgressUI();
    }
    
    void UpdateProgressUI()
    {
        // Actualizar barra de progreso
        float progreso = npc.GetChainProgressPercentage();
        progressBar.fillAmount = progreso / 100f;
        
        // Texto de progreso
        int completadas = npc.GetCompletedQuestCount();
        int total = npc.GetChainLength();
        progressText.text = $"{completadas}/{total} ({progreso:F0}%)";
        
        // Detalle de misión actual
        int currentIndex = npc.GetCurrentQuestIndex();
        if (currentIndex >= 0)
        {
            var entry = npc.GetQuestEntry(currentIndex);
            if (entry != null)
            {
                detailText.text = $"Actual: {entry.questData.displayName}";
            }
        }
        else if (npc.IsChainCompleted())
        {
            detailText.text = "¡Todas las misiones completadas!";
        }
        else
        {
            detailText.text = "Habla con el NPC para comenzar";
        }
    }
}
```

### Ejemplo 3: Sistema de recompensas por progreso

```csharp
using UnityEngine;

public class QuestRewardSystem : MonoBehaviour
{
    [SerializeField] private SimpleQuestNPC npc;
    [SerializeField] private int goldPerQuest = 100;
    [SerializeField] private int bonusForChainComplete = 500;
    
    private int lastCompletedCount = 0;
    
    void Update()
    {
        CheckForNewCompletions();
    }
    
    void CheckForNewCompletions()
    {
        int currentCompleted = npc.GetCompletedQuestCount();
        
        // Nueva misión completada
        if (currentCompleted > lastCompletedCount)
        {
            int newCompletions = currentCompleted - lastCompletedCount;
            GiveReward(goldPerQuest * newCompletions);
            lastCompletedCount = currentCompleted;
            
            // Bonus si completó toda la cadena
            if (npc.IsChainCompleted())
            {
                GiveReward(bonusForChainComplete);
                Debug.Log("¡BONUS POR COMPLETAR TODA LA CADENA!");
            }
        }
    }
    
    void GiveReward(int gold)
    {
        // Tu sistema de inventario/oro
        Debug.Log($"¡Recompensa: {gold} oro!");
    }
}
```

### Ejemplo 4: Mostrar lista completa en UI

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class QuestListUI : MonoBehaviour
{
    [SerializeField] private SimpleQuestNPC npc;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject questItemPrefab;
    
    private List<GameObject> questItems = new List<GameObject>();
    
    public void ShowQuestList()
    {
        ClearList();
        
        var chainStatus = npc.GetChainStatus();
        foreach (var status in chainStatus)
        {
            GameObject item = Instantiate(questItemPrefab, listContainer);
            
            // Configurar el item
            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            string icon = status.isCompleted ? "✓" : (status.isActive ? "→" : "○");
            text.text = $"{icon} {status.questName}";
            
            // Colorear según estado
            var image = item.GetComponent<Image>();
            if (status.isCompleted)
                image.color = Color.green;
            else if (status.isActive)
                image.color = Color.yellow;
            else
                image.color = Color.gray;
            
            questItems.Add(item);
        }
    }
    
    void ClearList()
    {
        foreach (var item in questItems)
            Destroy(item);
        questItems.Clear();
    }
}
```

### Ejemplo 5: Desbloquear área al completar misión específica

```csharp
using UnityEngine;

public class AreaUnlocker : MonoBehaviour
{
    [SerializeField] private SimpleQuestNPC npc;
    [SerializeField] private int requiredQuestIndex = 2; // Tercera misión
    [SerializeField] private GameObject blockedDoor;
    
    void Update()
    {
        CheckUnlock();
    }
    
    void CheckUnlock()
    {
        if (npc.IsQuestCompletedAtIndex(requiredQuestIndex))
        {
            if (blockedDoor != null && blockedDoor.activeSelf)
            {
                blockedDoor.SetActive(false);
                Debug.Log("¡Área desbloqueada!");
            }
        }
    }
}
```

---

## Herramientas de Depuración

### Menú Contextual en el Editor

En el Editor de Unity, haz clic derecho en el componente `SimpleQuestNPC`:

#### 1. "Debug: Mostrar Info de Cadena"
Muestra la estructura de la cadena:
```
[SimpleQuestNPC] GuardiaNPC - Cadena de 5 quests:
  [0] QUEST_TUTORIAL (Talk Step: 0)
  [1] QUEST_FIND_BOOK (Talk Step: 1)
  [2] QUEST_TALK_WIZARD (Talk Step: 0)
  [3] QUEST_DEFEAT_BOSS (Talk Step: 2)
  [4] QUEST_FINAL_REWARD (Talk Step: 0)
  Auto-start siguiente: True
```

#### 2. "Debug: Mostrar Estado de Quests"
Muestra el estado actual en runtime:
```
[SimpleQuestNPC] GuardiaNPC - Estado de 5 quests:
  ✓ [0] QUEST_TUTORIAL - Estado: Completed
  ✓ [1] QUEST_FIND_BOOK - Estado: Completed
  → [2] QUEST_TALK_WIZARD - Estado: Active
  ○ [3] QUEST_DEFEAT_BOSS - Estado: Inactive
  ○ [4] QUEST_FINAL_REWARD - Estado: Inactive
  Completadas: 2/5 (40.0%)
```

### Script de Prueba

```csharp
using UnityEngine;

public class QuestDebugger : MonoBehaviour
{
    [SerializeField] private SimpleQuestNPC npc;
    
    [ContextMenu("Mostrar Estado Completo")]
    void ShowFullStatus()
    {
        Debug.Log("=== ESTADO DE LA CADENA DE QUESTS ===");
        Debug.Log($"Total: {npc.GetChainLength()} misiones");
        Debug.Log($"Completadas: {npc.GetCompletedQuestCount()}");
        Debug.Log($"Progreso: {npc.GetChainProgressPercentage():F1}%");
        Debug.Log($"Cadena completa: {npc.IsChainCompleted()}");
        
        int current = npc.GetCurrentQuestIndex();
        Debug.Log($"Misión actual: {(current >= 0 ? current.ToString() : "Ninguna")}");
        
        Debug.Log("\n=== DETALLE DE CADA MISIÓN ===");
        var status = npc.GetChainStatus();
        foreach (var s in status)
        {
            Debug.Log($"[{s.index}] {s.questName}");
            Debug.Log($"    Estado: {s.state}");
            Debug.Log($"    Completada: {s.isCompleted}");
            Debug.Log($"    Activa: {s.isActive}");
        }
    }
}
```

---

## Flujo de Estados de una Misión

```
1. INACTIVE (Inactiva)
   └─> Jugador interactúa con NPC
       └─> Muestra: dlgBefore
           └─> Jugador acepta (auto o manual)

2. ACTIVE (Activa - pasos incompletos)
   └─> Jugador interactúa con NPC
       └─> Marca talkStepIndex como completado
       └─> Muestra: dlgInProgress

3. ACTIVE (Activa - todos los pasos completos)
   └─> Jugador interactúa con NPC
       └─> Muestra: dlgTurnIn
           └─> Completa la quest
               └─> Si hay siguiente quest:
                   └─> Muestra: dlgNextQuestOffer
                       └─> Inicia siguiente quest
               └─> Si es la última:
                   └─> Muestra: dlgCompleted

4. COMPLETED (Completada)
   └─> Jugador interactúa con NPC
       └─> Muestra: dlgCompleted
```

---

## Mejores Prácticas

### 1. Organización de Assets
```
Assets/
├── Quests/
│   ├── Chapter1/
│   │   ├── Q_Tutorial.asset
│   │   ├── Q_FindBook.asset
│   │   └── Q_TalkWizard.asset
│   └── Chapter2/
│       ├── Q_DefeatBoss.asset
│       └── Q_FinalReward.asset
└── Dialogues/
    ├── Chapter1/
    │   ├── Tutorial/
    │   │   ├── DLG_Tutorial_Before.asset
    │   │   ├── DLG_Tutorial_InProgress.asset
    │   │   ├── DLG_Tutorial_TurnIn.asset
    │   │   ├── DLG_Tutorial_Completed.asset
    │   │   └── DLG_Tutorial_NextOffer.asset
    │   └── ...
    └── ...
```

### 2. Convenciones de Nombres
- **Quests**: `Q_NombreDescriptivo`
- **Diálogos**: `DLG_QuestName_Estado`
- **NPCs**: `NPC_Nombre` o `NPC_Rol`

### 3. Testing
Usa los métodos de depuración en modo Play:
1. Hacer clic derecho en el componente
2. Ejecutar "Debug: Mostrar Estado de Quests"
3. Verificar el progreso en la consola

### 4. Performance
- Los métodos de consulta son ligeros, puedes llamarlos cada frame si es necesario
- Usa eventos (`QuestManager.OnQuestsChanged`) para actualizar UI solo cuando cambia el estado

---

## Solución de Problemas

### "El NPC no responde"
- ✅ Verificar que tenga componente `Interactable`
- ✅ Verificar que `QuestManager.Instance` no sea null
- ✅ Revisar que la cadena tenga al menos una quest

### "La quest no avanza"
- ✅ Verificar que `dlgTurnIn` esté asignado
- ✅ Comprobar que todos los pasos estén completados con `AreAllStepsCompleted()`
- ✅ Revisar que `talkStepIndex` sea correcto

### "No se inicia la siguiente quest"
- ✅ Verificar `autoStartNextQuest` esté marcado
- ✅ Asegurarse de que `dlgNextQuestOffer` esté asignado
- ✅ Comprobar que la quest anterior esté realmente completada

### "GetChainStatus() devuelve lista vacía"
- ✅ Verificar que `QuestManager.Instance` exista
- ✅ Comprobar que las quests tengan `questData` asignado

---

## Compatibilidad

- Unity 2020.3 o superior
- Requiere sistema de Quest existente (QuestManager, QuestData)
- Requiere sistema de Diálogos (DialogueManager, DialogueAsset)
- Compatible con sistema de Interacción (Interactable)

---

## Notas Adicionales

### Estados de Quest (QuestState)
```csharp
public enum QuestState
{
    Inactive,   // No iniciada
    Active,     // En progreso
    Completed   // Finalizada
}
```

### Orden de Ejecución
1. `Interact()` - Detecta interacción del jugador
2. `FindActiveQuestIndex()` - Busca quest activa más avanzada
3. `HandleQuestState()` - Gestiona el estado actual
4. `OfferNextQuest()` - Ofrece la siguiente si aplica

---

## Changelog

### Versión Actual
- ✅ Soporte para N misiones anidadas
- ✅ Métodos helper completos para gestión
- ✅ API completa de consulta de estado
- ✅ Herramientas de depuración integradas
- ✅ Clase `QuestChainStatus` para información detallada
- ✅ Progresión automática o manual configurable
- ⭐ NUEVO: Modos de completado automático para misiones

---

## Créditos y Soporte

Sistema desarrollado para el proyecto Unity "Alex".

Para reportar bugs o solicitar features, revisar el código fuente en:
`Assets/Scripts/Quests/SimpleQuestNPC.cs`
