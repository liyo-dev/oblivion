﻿﻿﻿﻿# 📘 El Sendero de las Estrellas - Documentación Técnica

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
   - 3.2 [Configuración Modular con ScriptableObjects](#32-configuración-modular-con-scriptableobjects)
   - 3.3 [Estados Detallados](#33-estados-detallados)
   - 3.4 [Sistema de Combate Completo](#34-sistema-de-combate-completo)
   - 3.5 [Sistema de Quests](#35-sistema-de-quests)
   - 3.6 [Sistema de Narrativa Interactiva](#36-sistema-de-narrativa-interactiva)
   - 3.7 [Sistema de Alerta Visual](#37-sistema-de-alerta-visual-compartido)
   - 3.8 [Debugging y Visualización](#38-debugging-y-visualización)
   - 3.9 [Tabla Resumen de Módulos](#39-tabla-resumen-de-módulos)
   - 3.10 [Prioridades de Interacción](#310-prioridades-de-interacción)
   - 3.11 [Mejores Prácticas para NPCs](#311-mejores-prácticas-para-npcs)
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

> **Sistema FSM Modular:** Arquitectura de estados finitos completamente configurable desde Inspector con ScriptableObjects reutilizables.

### 3.1 Arquitectura FSM

**Namespace:** `Game.NPC`  
**Componente Principal:** `NPCBehaviourManagerV2`

El sistema de NPCs está basado en una **Finite State Machine (FSM)** profesional donde cada NPC puede transitar entre diferentes estados según su configuración y contexto.

#### 🎯 Filosofía de Diseño

```
UN SCRIPT + CONFIGURACIÓN MODULAR = NPC COMPLETO

GameObject NPC
├─ NPCBehaviourManagerV2 (script único)
├─ NavMeshAgent (requerido)
├─ NPCSimpleAnimator (requerido)
├─ Animator (requerido)
└─ Interactable (opcional)

Configuración:
└─ NPCConfiguration (inline en Inspector)
    ├─ behaviourType: [Flags: Ambient, Combat, Quest, etc.]
    ├─ Configuración Base (común a todos)
    └─ Módulos (ScriptableObjects opcionales)
        ├─ ambientConfig (SO reutilizable)
        ├─ combatConfig (SO reutilizable)
        ├─ questConfig (SO reutilizable)
        └─ interactiveNarrativeConfig (SO reutilizable)
```

#### Estados de la FSM

```
┌─────────────────┐
│ CinematicState  │ ← Prioridad MÁXIMA
└─────────────────┘

Estados Normales:
┌─────────────┐
│ IdleState   │ ◄──────────┐
└──────┬──────┘             │
       │                     │
       ├─ enableWander       │
       ↓                     │
┌─────────────┐             │
│ WanderState │ ────────────┤
└──────┬──────┘             │
       │                     │
       ├─ Detecta jugador    │
       │  (isAggressive)     │
       ↓                     │
┌─────────────┐             │
│ AlertState  │ ⭐ NUEVO    │
│ ❗→ 📢 → 🏃               │
└──────┬──────┘             │
       │                     │
       ├─ Alerta completa    │
       ↓                     │
┌─────────────┐             │
│ CombatState │             │
│ ⚔️ → 💀 → 📢             │
└──────┬──────┘             │
       │                     │
       └─────────────────────┘
```

#### NPCStateContext

Contexto compartido entre todos los estados:

```csharp
public class NPCStateContext
{
    // Componentes
    public Transform Transform { get; }
    public NavMeshAgent Agent { get; }
    public NPCSimpleAnimator Animator { get; }
    public Animator UnityAnimator { get; }
    public Rigidbody Rigidbody { get; }
    
    // Referencias dinámicas
    public Transform Player { get; set; }
    public NPCBrain Brain { get; set; }
    public NPCConfiguration Config { get; set; }
    
    // Estado
    public bool IsInCombat { get; set; }
    public bool IsInCinematic { get; set; }
    public bool IsInteracting { get; set; }
    
    // Helpers
    public void Log(string message);
    public void LogWarning(string message);
    public void LogError(string message);
}
```

#### NPCBrain

Controlador central del FSM:

```csharp
public class NPCBrain
{
    public INPCState CurrentState { get; private set; }
    
    public void ChangeState(INPCState newState);
    public void Update();
    public bool HandleInteraction(GameObject interactor);
}
```

### 3.2 Configuración Modular con ScriptableObjects

#### NPCConfiguration (Inline en Inspector)

**NO es un ScriptableObject**, es una clase `[Serializable]` que se configura directamente en el componente:

```csharp
[Serializable]
public class NPCConfiguration
{
    [Header("Tipo de Comportamiento")]
    public NPCBehaviourType behaviourType; // Flags
    
    [Header("Módulos (ScriptableObjects)")]
    public NPCAmbientConfig ambientConfig;
    public NPCCombatConfig combatConfig;
    public NPCQuestConfig questConfig;
    public NPCNarrativeConfig narrativeConfig;
    public NPCInteractiveNarrativeConfig interactiveNarrativeConfig;
    
    [Header("Configuración Base")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4f;
    public float rotationSpeed = 180f;
    // ...más propiedades comunes
}
```

#### Flags de Comportamiento

```csharp
[Flags]
public enum NPCBehaviourType
{
    None = 0,
    Ambient = 1 << 0,              // Idle/Wander básico
    Combat = 1 << 1,               // Sistema de combate
    Quest = 1 << 2,                // Sistema de quests
    Narrative = 1 << 3,            // Narrativa con grafo
    InteractiveNarrative = 1 << 4  // Cadenas narrativas interactivas
}
```

#### Workflow de Creación de NPCs

**Paso 1: Crear Templates Reutilizables (una vez)**

```
Assets/ScriptableObjects/NPC Configs/
├─ Ambient/
│   ├─ NPC_Ambient_Wander_Small.asset (radio 5)
│   ├─ NPC_Ambient_Wander_Medium.asset (radio 10)
│   └─ NPC_Ambient_Wander_Large.asset (radio 20)
├─ Combat/
│   ├─ NPC_Combat_Weak.asset (HP 30, DMG 5)
│   ├─ NPC_Combat_Normal.asset (HP 50, DMG 10)
│   └─ NPC_Combat_Strong.asset (HP 100, DMG 20)
└─ Quest/
    └─ NPC_Quest_Simple.asset
```

**Paso 2: Crear NPC (30 segundos por NPC)**

```
1. GameObject con NPCBehaviourManagerV2
2. Añadir modelo 3D/sprite
3. Configurar en Inspector:
   ├─ behaviourType: ☑ Combat ☑ Ambient
   ├─ ambientConfig: → NPC_Ambient_Wander_Small
   └─ combatConfig: → NPC_Combat_Normal
4. ¡Listo! NPC funcional
```

**Resultado:** Puedes crear 100 NPCs en ~50 minutos reutilizando templates.

---

### 3.3 Estados Detallados

#### IdleState

**Función:** NPC en reposo, esperando.

**Comportamiento:**
- Permanece quieto durante `idleTime` (aleatorio entre min/max)
- **Detección de jugador** (cada 0.3s):
  - Si `combatConfig.isAggressive = true`
  - Y jugador está en rango de detección
  - Y en campo de visión (180°)
  - → Transita a **AlertState**

**Transiciones:**
```
IdleState →
  ├─ CinematicState (prioridad máxima)
  ├─ AlertState (si detecta jugador agresivo)
  ├─ CombatState (si IsInCombat = true)
  └─ WanderState (después de idleTime si enableWander)
```

#### WanderState

**Función:** Vagabundeo aleatorio dentro de un radio.

**Comportamiento:**
- Busca punto aleatorio dentro de `wanderRadius`
- Camina hacia el punto
- Al llegar → vuelve a IdleState
- **También detecta jugador** mientras camina (cada 0.3s)

**Configuración (NPCAmbientConfig SO):**
```csharp
[CreateAssetMenu(menuName = "NPC/Módulos/Ambient Config")]
public class NPCAmbientConfig : NPCModuleConfigBase
{
    public bool enableWander = true;
    public float wanderRadius = 10f;
    public float minIdleTime = 2f;
    public float maxIdleTime = 5f;
}
```

**Transiciones:**
```
WanderState →
  ├─ CinematicState (prioridad máxima)
  ├─ AlertState (si detecta jugador)
  ├─ IdleState (llegó a destino / atascado / camino bloqueado)
  └─ CombatState (si IsInCombat = true)
```

#### AlertState ⭐ NUEVO

**Función:** El NPC detectó al jugador y se prepara para el combate.

**Secuencia de Alerta:**
```
1. Mostrar icono ❗ sobre la cabeza
2. Reproducir dialogueOnAlert (ej: "¡Alto ahí!")
3. Mirar al jugador
4. Caminar hacia el jugador (opcional)
5. Esperar a que termine el diálogo (opcional)
6. Transitar a CombatState
```

**Características:**
- ✅ Icono de alerta visual (`NPCAlertIconController`)
  - Soporta GameObject prefab O Sprite 2D
  - Billboard automático (siempre mira a la cámara)
  - Animación de bounce
- ✅ Diálogo antes del combate
- ✅ Opción `waitForAlertDialogue` para esperar a que el jugador lea

**Configuración (en NPCCombatConfig):**
```csharp
[Header("Alert Visual")]
public GameObject alertIconPrefab;     // Prefab 3D (ej: partículas)
public Sprite alertIconSprite;         // O sprite 2D simple
public float alertIconDuration = 2f;
public Vector3 alertIconOffset = new Vector3(0, 2.5f, 0);

[Header("Diálogos")]
public DialogueAsset dialogueOnAlert;  // "¡Grrr!" o diálogo largo
public bool waitForAlertDialogue = true; // Esperar a que termine
```

**Transiciones:**
```
AlertState →
  ├─ CinematicState (prioridad máxima)
  ├─ IdleState (jugador perdido)
  └─ CombatState (alerta completa + diálogo terminado)
```

#### CombatState

**Función:** Combate activo con el jugador.

**Inicialización Automática:**
```csharp
OnEnter:
1. Crear/obtener componente Damageable
2. Configurar vida del NPC (combatConfig.health)
3. Crear/obtener NPCCombatLifecycleHandler
4. Inicializar NPCCombatBrain (IA táctica)
5. Comenzar combate
```

**Sistema de Diálogos:**
- **Durante alerta**: `dialogueOnAlert` (ya reproducido en AlertState)
- **Al morir**: `dialogueOnDefeat` (una vez)
- **Post-derrota**: `dialogueAfterDefeat` (repetible en interacciones)

**NPCCombatLifecycleHandler:**
```csharp
// Componente que se añade automáticamente
public class NPCCombatLifecycleHandler : MonoBehaviour
{
    // Se suscribe a Damageable.OnDied
    // Reproduce dialogueOnDefeat
    // Cambia a IdleState después de la derrota
    // Maneja interacciones post-derrota
    
    public bool HasBeenDefeated { get; }
    public bool HandlePostDefeatInteraction(GameObject interactor);
}
```

**NPCCombatBrain:**
- IA táctica avanzada (movimiento, ataques, esquivas)
- Configurado automáticamente desde `combatConfig`
- Maneja 3 slots de ataque (Left, Right, Special)
- Comportamiento adaptativo (agresivo/defensivo/neutral)

**Transiciones:**
```
CombatState →
  ├─ CinematicState (prioridad máxima)
  ├─ IdleState (NPC derrotado O jugador demasiado lejos)
  └─ Continúa en combate
```

#### CinematicState

**Función:** Secuencias cinemáticas controladas externamente.

**Prioridad:** Absoluta - se activa desde cualquier estado.

**Uso:**
```csharp
// Iniciar secuencia cinemática
npcManager.StartCinematicSequence(new MoveToPositionSequence(...));

// El NPC entra automáticamente en CinematicState
// Ejecuta la secuencia
// Vuelve al estado anterior al terminar
```

**Transiciones:**
```
CinematicState →
  └─ Estado Anterior (cuando la secuencia termina)
```

---

### 3.4 Sistema de Combate Completo

#### Componentes del Sistema

```
Sistema de Combate:
├─ NPCCombatConfig (SO) - Configuración
├─ CombatState - Estado FSM
├─ NPCCombatBrain - IA táctica
├─ NPCCombatLifecycleHandler - Ciclo de vida
├─ Damageable - Vida del NPC
└─ NPCAlertIconController - Iconos visuales
```

#### NPCCombatConfig (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "NPC/Módulos/Combat Config")]
public class NPCCombatConfig : NPCModuleConfigBase
{
    [Header("Combat Stats")]
    public float health = 100f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    
    [Header("Ranges")]
    public float detectionRange = 10f;  // Detectar jugador
    public float combatRange = 8f;      // Rango de ataque
    public float meleeRange = 2f;       // Cuerpo a cuerpo
    
    [Header("Behavior")]
    public bool isAggressive = true;    // Detecta automáticamente
    public bool canChaseOutOfBounds = false;
    public float maxChaseDistance = 20f;
    
    [Header("Alert Visual")]
    public GameObject alertIconPrefab;
    public Sprite alertIconSprite;
    public float alertIconDuration = 2f;
    public Vector3 alertIconOffset = new Vector3(0, 2.5f, 0);
    
    [Header("Diálogos")]
    public DialogueAsset dialogueOnAlert;      // Antes del combate
    public DialogueAsset dialogueOnDefeat;     // Al morir (una vez)
    public DialogueAsset dialogueAfterDefeat;  // Post-derrota (repetible)
    public bool waitForAlertDialogue = true;
    
    [Header("Projectiles")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 15f;
}
```

#### Flujo Completo de Combate

```
1. IdleState/WanderState
   └─ Detecta jugador (cada 0.3s)
      └─ distancia <= detectionRange
         └─ isAggressive = true
            ↓
2. AlertState
   ├─ Icono ❗ aparece
   ├─ 📢 Reproduce dialogueOnAlert
   ├─ NPC mira al jugador
   ├─ NPC camina hacia el jugador
   └─ Espera a que termine el diálogo (si waitForAlertDialogue)
      ↓
3. CombatState
   ├─ Añade Damageable (vida = combatConfig.health)
   ├─ Añade NPCCombatLifecycleHandler
   ├─ Inicia NPCCombatBrain (IA táctica)
   └─ Combate activo...
      ↓
4. NPC Derrotado (vida = 0)
   ├─ Damageable.OnDied → NPCCombatLifecycleHandler
   ├─ 📢 Reproduce dialogueOnDefeat
   ├─ IsInCombat = false
   └─ Transita a IdleState
      ↓
5. Interacción Post-Derrota
   ├─ Jugador interactúa con NPC derrotado
   └─ 📢 Reproduce dialogueAfterDefeat (repetible)
```

#### NPCCombatBrain (IA Táctica)

**Comportamientos:**
- Mantiene distancia óptima (minDistance - maxDistance)
- Ataques con cooldowns independientes por slot
- Movimiento circular alrededor del jugador
- Esquivas y retrocesos
- Estados tácticos (Agresivo/Neutral/Defensivo)
- Micro-pausas para ritmo humano
- Burst attacks seguidos de reposicionamiento

**Configuración Automática:**
El `CombatState` configura el brain automáticamente desde `combatConfig`:

```csharp
var settings = new NPCCombatBrain.Settings
{
    sightRadius = combatConfig.detectionRange,
    minDistance = combatConfig.meleeRange,
    maxDistance = combatConfig.combatRange,
    leftAttack = new AttackSlot 
    { 
        animationState = "Attack_Left",
        cooldown = combatConfig.attackCooldown,
        slotIndex = 0
    },
    // ...más configuración
};
_combatBrain.BeginCombat(settings);
```

#### Ejemplo de Configuración Completa

**NPC Enemigo Goblin:**

```
GameObject: "Enemy_Goblin"
└─ NPCBehaviourManagerV2
    └─ Configuration
        ├─ behaviourType: [Combat + Ambient]
        ├─ ambientConfig: → NPC_Ambient_Wander_Small.asset
        │   ├─ enableWander: true
        │   ├─ wanderRadius: 10
        │   ├─ minIdleTime: 2
        │   └─ maxIdleTime: 5
        │
        └─ combatConfig: → NPC_Combat_Goblin.asset
            ├─ [Combat Stats]
            │   ├─ health: 50
            │   ├─ attackDamage: 10
            │   └─ attackCooldown: 1.5
            │
            ├─ [Ranges]
            │   ├─ detectionRange: 12
            │   ├─ combatRange: 8
            │   └─ meleeRange: 2
            │
            ├─ [Behavior]
            │   └─ isAggressive: ☑
            │
            ├─ [Alert Visual]
            │   ├─ alertIconPrefab: Exclamation_Prefab
            │   └─ alertIconDuration: 2
            │
            └─ [Diálogos]
                ├─ dialogueOnAlert: "Dialogue_GoblinChallenge"
                │   └─ "¡Grrr! ¡Intruso!"
                ├─ dialogueOnDefeat: "Dialogue_GoblinDefeat"
                │   └─ "Ugh..."
                └─ waitForAlertDialogue: ☑
```

**Resultado:**
1. Goblin vaga aleatoriamente (WanderState)
2. Ve al jugador → Icono ❗ + "¡Grrr!" (AlertState)
3. Espera a que jugador lea → Inicia combate (CombatState)
4. Al ser derrotado → "Ugh..." (dialogueOnDefeat)
5. Jugador puede interactuar después (dialogueAfterDefeat opcional)

---

#### Sistema de Barras de Vida NPC

**Arquitectura:**

El sistema de barras de vida sigue el mismo patrón que el `alertIconPrefab`. El prefab de la barra se configura en `NPCCombatConfig` y se instancia automáticamente al entrar en combate.

```
Sistema de Barras de Vida:
├─ NPCCombatConfig (SO) - Contiene healthBarPrefab
├─ CombatState - Instancia la barra al entrar en combate
├─ NPCHealthBarSpawner - Gestiona la instancia del prefab
└─ NPCHealthBarUI - Sincroniza con Damageable
```

**Configuración en NPCCombatConfig:**

```csharp
[Header("Health Bar UI")]
[Tooltip("Prefab de la barra de vida del NPC (Canvas con NPCHealthBarUI)")]
public GameObject healthBarPrefab;
```

**Prefab de Barra de Vida:**

```
Canvas HealthBar.prefab
├─ Canvas (renderMode: World Space)
├─ NPCHealthBarUI (Script) ⭐
│  ├─ fillImage: [Referencia a la imagen de relleno]
│  └─ healthText: [Opcional - TextMeshPro]
├─ Background (Image)
└─ Fill (Image) ← Este es el fillImage que se actualiza
```

**NPCHealthBarUI:**

Este componente se auto-configura automáticamente:
1. Busca el `NPCBehaviourManagerV2` en el padre
2. Obtiene el `Damageable` desde el BehaviourManager
3. Se suscribe a `OnDamaged` y `OnDied`
4. Actualiza `fillImage.fillAmount` basado en `Damageable.Current / Max`

```csharp
// Sincronización automática
fillImage.fillAmount = _npcDamageable.Current / _npcDamageable.Max;

// Cambio de color según vida
if (healthPercent <= 0.25f) fillImage.color = criticalColor;  // Rojo
else if (healthPercent <= 0.5f) fillImage.color = warningColor; // Amarillo
else fillImage.color = healthyColor; // Verde
```

**Flujo de Instanciación:**

```
1. NPC entra en CombatState
   ↓
2. CombatState añade Damageable al BehaviourManager
   ↓
3. CombatState añade NPCHealthBarSpawner al NPC
   ↓
4. NPCHealthBarSpawner recibe healthBarPrefab desde combatConfig
   ↓
5. Se instancia Canvas HealthBar como hijo del NPC
   ↓
6. NPCHealthBarUI (en el Canvas) busca BehaviourManager
   ↓
7. NPCHealthBarUI obtiene Damageable del BehaviourManager
   ↓
8. ✅ La barra se actualiza automáticamente con cada daño
```

**Setup en Inspector:**

```
NPCCombatConfig
├─ [Combat Stats]
│   └─ health: 100
│
├─ [Alert Visual]
│   ├─ alertIconPrefab: Exclamation_Prefab
│   └─ alertIconDuration: 2
│
├─ [Health Bar UI] ⭐
│   └─ healthBarPrefab: Canvas HealthBar.prefab
│
└─ [Diálogos]
    └─ ...
```

**Componentes Añadidos Automáticamente en Combate:**

```
NPC (durante combate)
├─ NPCBehaviourManagerV2
│  └─ Damageable ⭐ (añadido por CombatState)
├─ NPCHealthBarSpawner ⭐ (añadido por CombatState)
├─ NPCCombatBrain
└─ Canvas HealthBar (Clone) ⭐ (instanciado)
   └─ NPCHealthBarUI
```

**Características:**
- ✅ Configuración centralizada en NPCCombatConfig
- ✅ Instanciación automática al entrar en combate
- ✅ Sincronización automática con Damageable
- ✅ Cambio de color según porcentaje de vida
- ✅ Se oculta automáticamente cuando el NPC muere
- ✅ Diferentes NPCs pueden tener diferentes barras

---

### 3.5 Sistema de Quests

#### NPCQuestConfig (ScriptableObject)

Para NPCs que dan misiones (quest givers).

```csharp
[CreateAssetMenu(menuName = "NPC/Módulos/Quest Config")]
public class NPCQuestConfig : NPCModuleConfigBase
{
    [Serializable]
    public class QuestChainEntry
    {
        public QuestData questData;
        public QuestCompletionMode completionMode;
        
        // Diálogos
        public DialogueAsset dlgBefore;      // Oferta
        public DialogueAsset dlgInProgress;  // En progreso
        public DialogueAsset dlgTurnIn;      // Entrega
        public DialogueAsset dlgCompleted;   // Completada
        
        // Detección de ítems
        public bool autoDetectItemDelivery = false;
        public string itemTag = "QuestItem";
        public int itemDeliveryStepIndex = 0;
        
        // Verificación de inventario
        public bool requireItemInInventory = false;
        public ItemData requiredItem;
        public int requiredAmount = 1;
        public bool consumeItemOnComplete = true;
        
        // Eventos
        public UnityEvent onQuestCompleted;
    }
    
    public QuestChainEntry[] chain;
    public bool enableItemDetection = true;
    public float detectionRadius = 3f;
}
```

**Uso:**
```
NPC Quest Giver:
└─ behaviourType: [Quest + Ambient]
    ├─ ambientConfig: NPC_Ambient_StandStill
    └─ questConfig: NPC_Quest_MainStory
        └─ chain:
            ├─ [0] Quest_FindCat
            │   ├─ dlgBefore: "Mi gato se perdió..."
            │   ├─ dlgInProgress: "¿Lo encontraste?"
            │   └─ dlgTurnIn: "¡Gracias!"
            └─ [1] Quest_DefeatBoss
```

---

### 3.6 Sistema de Narrativa Interactiva

#### NPCInteractiveNarrativeConfig (ScriptableObject)

**Sistema de cadenas narrativas:** Encadena acciones secuenciales (diálogos, movimientos, animaciones, etc.)

```csharp
[CreateAssetMenu(menuName = "NPC/Módulos/Interactive Narrative Config")]
public class NPCInteractiveNarrativeConfig : NPCModuleConfigBase
{
    [Header("Narrative Chain")]
    public NarrativeChainEntry[] narrativeChain;
    
    [Header("Configuración")]
    public bool singleUse = true;           // Solo se ejecuta una vez
    public bool persistState = true;        // Guardar estado
    public string persistenceId;            // ID único (auto-generado)
    
    [Header("Behavior")]
    public bool rotateToPlayerOnInteract = true;
    public float rotationDuration = 0.3f;
    
    [Header("Auto-Inicio (Alerta)")]
    public bool autoStartOnPlayerDetection = false;  // ⭐ NUEVO
    public float detectionRange = 10f;
    public Sprite alertIcon;                         // Icono de alerta
    public float alertIconDuration = 1f;
    public bool walkTowardsPlayerOnAlert = true;
    public float stopDistanceFromPlayer = 2f;
    
    [Header("Estado Post-Narrativa")]
    public PostNarrativeState postNarrativeState;
    public NPCAmbientConfig postNarrativeAmbientConfig;
}
```

#### Tipos de Acciones Narrativas

```csharp
public enum NarrativeActionType
{
    Dialogue,       // Mostrar diálogo
    Move,          // Mover a punto (anchor o transform)
    PlayAnimation, // Reproducir animación
    StartQuest,    // Iniciar quest
    StartCombat,   // Iniciar combate
    Wait,          // Esperar X segundos
    Custom         // UnityEvent personalizado
}
```

#### NarrativeChainEntry

```csharp
[Serializable]
public class NarrativeChainEntry
{
    public NarrativeActionType actionType;
    
    // Dialogue
    public DialogueAsset dialogue;
    
    // Movement
    public string targetAnchorName;           // SpawnAnchor por ID
    public Transform targetTransform;         // O transform directo
    public float maxMovementDuration = 15f;
    public float walkDisplayDuration = 999f;
    public bool turnAroundOnArrival = false;
    public bool waitForPlayer = false;        // Espera si jugador se aleja
    public float maxPlayerDistance = 10f;
    public float resumePlayerDistance = 5f;
    
    // Animation
    public string animationTrigger;
    public AnimationClip animationClip;
    public float animationDuration = 0f;
    
    // Quest
    public QuestData questToStart;
    
    // Combat
    public Transform combatTarget;
    
    // Wait
    public float waitDuration = 1f;
    
    // Custom
    public UnityEvent customAction;
    
    // Eventos
    public UnityEvent onActionStarted;
    public UnityEvent onActionCompleted;
}
```

#### Ejemplo Completo: Tutorial Guide

```
NPC Tutorial Guide:
└─ behaviourType: [InteractiveNarrative]
    └─ interactiveNarrativeConfig: NPC_Narrative_Tutorial
        ├─ narrativeChain:
        │   ├─ [0] Dialogue
        │   │   └─ dialogue: "¡Bienvenido! Sígueme."
        │   ├─ [1] Move
        │   │   ├─ targetAnchorName: "Tutorial_Point_1"
        │   │   ├─ waitForPlayer: true
        │   │   └─ maxPlayerDistance: 15
        │   ├─ [2] Dialogue
        │   │   └─ dialogue: "Este es el cofre del tesoro."
        │   ├─ [3] PlayAnimation
        │   │   └─ animationTrigger: "Point"
        │   └─ [4] StartQuest
        │       └─ questToStart: Quest_Tutorial_FindKey
        │
        ├─ singleUse: true
        ├─ persistState: true
        │
        ├─ [Auto-Inicio]
        │   ├─ autoStartOnPlayerDetection: true
        │   ├─ detectionRange: 8
        │   ├─ alertIcon: Sprite_QuestionMark
        │   ├─ alertIconDuration: 1
        │   └─ walkTowardsPlayerOnAlert: true
        │
        └─ postNarrativeState: SwitchToAmbient
            └─ postNarrativeAmbientConfig: NPC_Ambient_Idle
```

**Resultado:**
1. Jugador se acerca → Icono ❓ aparece
2. NPC camina hacia el jugador
3. Auto-inicia la cadena narrativa:
   - Diálogo de bienvenida
   - Camina al punto 1 (esperando al jugador)
   - Diálogo en el punto
   - Animación de señalar
   - Inicia quest
4. Después → cambia a estado Ambient (Idle)

#### Auto-Inicio vs Interacción Manual

**Interacción Manual (por defecto):**
```
autoStartOnPlayerDetection = false
→ Jugador debe interactuar con el NPC (tecla E)
→ Se inicia la cadena narrativa
```

**Auto-Inicio Automático:**
```
autoStartOnPlayerDetection = true
→ Jugador entra en detectionRange
→ Icono de alerta aparece
→ NPC camina hacia el jugador (opcional)
→ Cadena narrativa se inicia automáticamente
```

#### NPCInteractiveNarrativeExecutor

**Componente que ejecuta las cadenas:**
- Se añade automáticamente si hay `interactiveNarrativeConfig`
- Maneja el flujo secuencial de acciones
- Espera a que cada acción termine antes de continuar
- Sistema robusto con logs detallados

**Flujo de Ejecución:**
```
1. TryExecuteNarrative()
2. Para cada acción en narrativeChain:
   ├─ onActionStarted.Invoke()
   ├─ ExecuteAction(entry)
   │   ├─ Dialogue: Espera a DialogueManager.IsOpen = false
   │   ├─ Move: Espera a que llegue al destino
   │   ├─ Animation: Espera duración de la animación
   │   └─ etc...
   ├─ onActionCompleted.Invoke()
   └─ Continúa con siguiente acción
3. HandlePostNarrativeState()
```

---

### 3.7 Sistema de Alerta Visual (Compartido)

#### NPCAlertIconController

**Componente reutilizable** para mostrar iconos sobre la cabeza de los NPCs.

**Uso:**
- AlertState (combate) → Icono ❗
- Narrativa Interactiva (auto-inicio) → Icono ❓ o cualquier otro

**Características:**
```csharp
public class NPCAlertIconController : MonoBehaviour
{
    // Configuración
    public Vector3 iconOffset = new Vector3(0, 2.5f, 0);
    public float iconDuration = 2f;
    public bool animateBounce = true;
    public float bounceAmplitude = 0.2f;
    public float bounceSpeed = 3f;
    
    // Canvas en Escena (Opcional) ⭐ NUEVO
    public GameObject sceneIconCanvas;
    
    // API
    public void ShowAlertIcon(GameObject iconPrefab, float duration);
    public void ShowAlertIcon(Sprite iconSprite, float duration);
    public void ShowAlertIcon(float duration);  // ⭐ Usa canvas de escena
    public void HideAlertIcon();
}
```

**Soporta:**
- ✅ GameObject prefabs (partículas, modelos 3D, etc.)
- ✅ Sprites 2D (simple y rápido)
- ✅ **Canvas de escena** (hijo del NPC) ⭐ NUEVO
- ✅ Billboard automático (siempre mira a la cámara)
- ✅ Animación de bounce
- ✅ Limpieza automática

#### Tres Formas de Usar Iconos de Alerta

##### Opción 1: Prefab Reutilizable (Recomendado para muchos NPCs)

**Ventajas:**
- ✅ Reutilizable en múltiples NPCs
- ✅ Se configura desde el ScriptableObject
- ✅ Fácil de actualizar (cambias el prefab, afecta a todos)

**Configuración:**
```
1. Crear prefab del icono:
   Assets/Prefabs/UI/Icon_Alert_Exclamation.prefab
   └─ Canvas
       └─ Image (sprite de exclamación)

2. En NPCCombatConfig (SO):
   alertIconPrefab = Icon_Alert_Exclamation

3. ¡Listo! Todos los NPCs que usen ese config mostrarán el icono
```

##### Opción 2: Sprite Simple (Más rápido)

**Ventajas:**
- ✅ Súper simple
- ✅ Solo necesitas un sprite
- ✅ No necesitas crear prefab

**Configuración:**
```
1. En NPCCombatConfig (SO):
   alertIconSprite = Sprite_Exclamation

2. ¡Listo!
```

##### Opción 3: Canvas en Escena ⭐ NUEVO (Para NPCs únicos)

**Ventajas:**
- ✅ Perfecto para NPCs únicos con UI especial
- ✅ Puedes tener animaciones complejas en el Canvas
- ✅ No necesitas configurar nada en el SO

**Desventajas:**
- ⚠️ No reutilizable (está en la escena, no en prefab)
- ⚠️ Solo funciona para ese NPC específico

**Configuración:**
```
1. En la jerarquía del NPC:
   Erika
   ├─ Model
   ├─ Canvas Fight (hijo del NPC)
   │   └─ Image (tu icono de lucha)
   └─ NPCBehaviourManagerV2

2. Añadir NPCAlertIconController al NPC manualmente:
   Component > Add > NPCAlertIconController
   └─ Scene Icon Canvas: Arrastra "Canvas Fight" aquí

3. Dejar el SO sin iconos:
   NPCCombatConfig:
   ├─ alertIconPrefab: (vacío)
   └─ alertIconSprite: (vacío)

4. ¡Listo! Automáticamente usará el canvas de escena
```

#### Sistema de Prioridades

Cuando se llama `ShowAlertIcon()`:

```
Prioridad 1: sceneIconCanvas (si está asignado en el componente)
   └─ Usa el canvas hijo de la escena

Prioridad 2: alertIconPrefab (del SO)
   └─ Instancia el prefab

Prioridad 3: alertIconSprite (del SO)
   └─ Crea un GameObject con SpriteRenderer

Fallback: Log de advertencia
```

**Ejemplos de Uso:**

**Caso de Uso 1: 100 Goblins (Prefab)**
```
Todos los goblins comparten:
└─ NPCCombatConfig_Goblin
    └─ alertIconPrefab: Icon_Alert_Exclamation
```

**Caso de Uso 2: Boss Único (Canvas en Escena)**
```
Boss Erika (única):
└─ NPCAlertIconController
    └─ sceneIconCanvas: Canvas Fight (su UI especial)
```

**Caso de Uso 3: NPCs Simples (Sprite)**
```
NPC aldeano:
└─ NPCCombatConfig_Villager
    └─ alertIconSprite: Sprite_Question
```

#### Configuración del Canvas de Escena

Si usas un Canvas hijo en la escena, sigue esta estructura:

```
NPC GameObject (ej: Erika)
├─ Model (mesh/sprite del NPC)
├─ Canvas Fight (World Space Canvas)
│   ├─ Render Mode: World Space
│   ├─ Position: (0, 2.5, 0) encima del NPC
│   ├─ Scale: (0.01, 0.01, 0.01) para que sea pequeño
│   └─ Image
│       └─ Sprite: Tu icono de exclamación
├─ NPCBehaviourManagerV2
└─ NPCAlertIconController ⭐ Añadido manualmente
    └─ Scene Icon Canvas: Canvas Fight
```

**IMPORTANTE:**
- El canvas debe estar **inactivo por defecto** (disabled)
- El `NPCAlertIconController` lo activará automáticamente cuando detecte al jugador
- Se desactivará automáticamente después de la duración configurada

---

### 3.8 Debugging y Visualización

#### Debug Mode

Activar logs detallados:

```
NPCBehaviourManagerV2
└─ Debug Mode: ☑
```

**Logs que verás:**
```
[IdleState] Jugador detectado a 8.5m, activando alerta
[AlertState] Iniciando diálogo de alerta
[AlertState] Esperando a que termine el diálogo
[AlertState] Alerta completada, iniciando combate
[CombatState] Damageable añadido al NPC
[CombatState] Combat brain initialized successfully
[NPCCombatLifecycleHandler] NPC derrotado
[NPCInteractiveNarrativeExecutor] ▶️ INICIO Acción 0: Dialogue
[NPCInteractiveNarrativeExecutor] ✅ COMPLETADA Acción 0
```

#### Gizmos Visuales (OnDrawGizmosSelected)

**Ambient/Wander:**
- 🟡 Esfera amarilla: Wander radius

**Combat:**
- 🟡 Esfera amarilla: Detection range
- 🔴 Esfera roja: Combat range
- 🔵 Esfera azul: Melee range

**Narrativa Interactiva:**
- 🟡 Esfera amarilla: Detection range (auto-inicio)
- 🟢 Esfera verde: Stop distance from player

**Estado Actual:**
- 📝 Label sobre el NPC: Nombre del estado FSM actual
- 🟡 Línea: Destino actual del NavMeshAgent

---

### 3.9 Tabla Resumen de Módulos

| Módulo | Flag | SO Config | Componentes Auto | Uso Principal |
|--------|------|-----------|------------------|---------------|
| **Ambient** | `Ambient` | NPCAmbientConfig | - | Idle/Wander básico |
| **Combat** | `Combat` | NPCCombatConfig | Damageable, NPCCombatLifecycleHandler, NPCAlertIconController | Enemigos agresivos |
| **Quest** | `Quest` | NPCQuestConfig | - | Quest givers |
| **Narrative** | `Narrative` | NPCNarrativeConfig | - | NPCs con grafo narrativo |
| **Interactive Narrative** | `InteractiveNarrative` | NPCInteractiveNarrativeConfig | NPCInteractiveNarrativeExecutor, NPCAlertIconController | Secuencias guiadas |

#### Combinaciones Comunes

| Tipo de NPC | Flags | Comportamiento |
|-------------|-------|----------------|
| **Villager** | `Ambient` | Vaga por el pueblo |
| **Enemy** | `Ambient + Combat` | Patrulla y ataca |
| **Quest Giver** | `Quest + Ambient` | Da misiones, puede moverse |
| **Boss** | `Combat` | Solo combate, estático |
| **Tutorial Guide** | `InteractiveNarrative` | Secuencia guiada |
| **Complex NPC** | `Quest + Combat + InteractiveNarrative` | Todo combinado |

---

### 3.10 Prioridades de Interacción

Cuando el jugador interactúa con un NPC (`NPCBrain.HandleInteraction`):

```
Prioridad 0: NPC Derrotado
  └─ Si HasBeenDefeated = true
     └─ Reproduce dialogueAfterDefeat (repetible)

Prioridad 1: Interactive Narrative
  └─ Si interactiveNarrativeConfig != null
     └─ Ejecuta cadena narrativa

Prioridad 2: Quest
  └─ Si questConfig != null
     └─ Procesa quest actual

Prioridad 3: Fallback
  └─ Interactable.OnInteract (diálogo genérico)
```

---

### 3.11 Mejores Prácticas para NPCs

#### ✅ Creación Rápida de NPCs

**1. Crea Templates Reutilizables (una vez):**
```
Assets/ScriptableObjects/NPC Configs/
├─ _Templates/
│   ├─ Ambient/
│   │   ├─ NPC_Ambient_Wander_Small.asset
│   │   ├─ NPC_Ambient_Wander_Medium.asset
│   │   └─ NPC_Ambient_StandStill.asset
│   ├─ Combat/
│   │   ├─ NPC_Combat_Weak.asset
│   │   ├─ NPC_Combat_Normal.asset
│   │   └─ NPC_Combat_Strong.asset
│   └─ Quest/
│       └─ NPC_Quest_Simple.asset
```

**2. Workflow para cada NPC (30 segundos):**
1. Drag & drop prefab base "NPC_Base"
2. Renombrar: "Enemy_Goblin_01"
3. Cambiar modelo 3D/sprite
4. En Inspector:
   - behaviourType: ☑ Combat ☑ Ambient
   - ambientConfig: → NPC_Ambient_Wander_Small
   - combatConfig: → NPC_Combat_Normal
5. ¡Listo!

**Resultado:** 100 NPCs en ~50 minutos.

#### ✅ Organización de Assets

```
Assets/
├─ Prefabs/
│   └─ NPCs/
│       ├─ _Base/
│       │   └─ NPC_Base.prefab
│       ├─ Enemies/
│       │   ├─ Enemy_Goblin.prefab
│       │   └─ Enemy_Wolf.prefab
│       └─ Town/
│           ├─ NPC_Merchant.prefab
│           └─ NPC_Guard.prefab
│
├─ ScriptableObjects/
│   └─ NPC Configs/
│       ├─ _Templates/ (reutilizables)
│       └─ Unique/ (NPCs específicos)
│
└─ Art/
    └─ UI/
        └─ NPC Icons/
            ├─ Icon_Alert_Exclamation.png
            ├─ Icon_Alert_Question.png
            └─ Icon_Alert_Anger.png
```

#### ✅ Naming Conventions

**GameObjects:**
```
NPC_<Nombre>_<Número>
Enemy_<Tipo>_<Número>

Ejemplos:
- NPC_Eldran_01
- Enemy_Goblin_01
- NPC_QuestGiver_Mayor
```

**ScriptableObjects:**
```
NPC_<Módulo>_<Descripción>

Ejemplos:
- NPC_Ambient_Wander_Small
- NPC_Combat_Goblin
- NPC_Quest_MainStory
- NPC_Narrative_Tutorial
```

#### ✅ Performance

**Detección de Jugador:**
- ✅ Se hace cada 0.3s (no cada frame)
- ✅ Solo en IdleState y WanderState
- ✅ Solo si `isAggressive = true`

**NavMeshAgent:**
- ✅ Se desactiva en CinematicState
- ✅ Se resetea correctamente en transiciones
- ✅ Verificación de "stuck" automática

**Componentes Automáticos:**
- ✅ Se añaden solo cuando se necesitan
- ✅ Se limpian automáticamente

#### ⚠️ Errores Comunes

**1. NPC no se mueve:**
```
Verificar:
- ☑ NavMeshAgent está presente
- ☑ NavMesh bakeado en la escena
- ☑ Agent.enabled = true
- ☑ behaviourType tiene Ambient activado
- ☑ ambientConfig.enableWander = true
```

**2. NPC no ataca:**
```
Verificar:
- ☑ behaviourType tiene Combat activado
- ☑ combatConfig.isAggressive = true
- ☑ detectionRange es suficiente
- ☑ Jugador está en rango y campo de visión
```

**3. Diálogo no aparece:**
```
Verificar:
- ☑ DialogueManager existe en la escena Start
- ☑ dialogueOnAlert está asignado en combatConfig
- ☑ El DialogueAsset es válido
```

**4. Icono de alerta no aparece:**
```
Verificar:
- ☑ alertIconPrefab O alertIconSprite está asignado
- ☑ El sprite/prefab es válido
- ☑ alertIconDuration > 0
```

**5. Cadena narrativa no continúa:**
```
Verificar logs:
- ¿El diálogo se cerró correctamente?
- ¿La acción anterior completó?
- ¿Hay errores en la consola?

Debug Mode: ☑ para ver flujo completo
```

#### ✅ Testing

**Testing Rápido desde Escena:**
1. Asegúrate de que `EnsureStartSceneLoaded` está en la escena
2. Da Play desde cualquier escena
3. Start se carga automáticamente
4. Todo funciona

**Testing de Combate:**
```
1. Crear NPC de prueba:
   - behaviourType: Combat + Ambient
   - combatConfig: Template básico
   - isAggressive: true
   - detectionRange: 15 (amplio para testing)

2. Debug Mode: ☑

3. Acercarse al NPC

4. Verificar logs:
   - "Jugador detectado"
   - "AlertState"
   - "Diálogo iniciado"
   - "CombatState"
```

**Testing de Narrativa:**
```
1. Crear cadena simple:
   [0] Dialogue: "Hola"
   [1] Move: anchor cercano
   [2] Dialogue: "Adiós"

2. autoStartOnPlayerDetection: false (manual)

3. Interactuar y verificar que ejecuta todas las acciones

4. Revisar logs detallados
```

---

## 4. Sistemas Core

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

#### NPCCombatConfig (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "NPC/Módulos/Combat Config")]
public class NPCCombatConfig : NPCModuleConfigBase
{
    [Header("Combat Stats")]
    public float health = 100f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    
    [Header("Ranges")]
    public float detectionRange = 10f;
    public float combatRange = 8f;
    public float meleeRange = 2f;
    
    [Header("Behavior")]
    public bool isAggressive = true;
    public bool canChaseOutOfBounds = false;
    public float maxChaseDistance = 20f;
    
    [Header("Alert Visual")]
    public GameObject alertIconPrefab;
    public float alertIconDuration = 2f;
    
    [Header("Diálogos")]
    public DialogueAsset dialogueOnAlert;
    public DialogueAsset dialogueOnDefeat;
    public DialogueAsset dialogueAfterDefeat;
    public bool waitForAlertDialogue = true;
    
    [Header("Spells / Attacks (3 Slots) ⭐ NUEVO")]
    [Tooltip("Hechizo/ataque básico (slot 1)")]
    public GameObject spell1Prefab;
    [Tooltip("Hechizo/ataque intermedio (slot 2)")]
    public GameObject spell2Prefab;
    [Tooltip("Hechizo/ataque especial (slot 3)")]
    public GameObject spell3Prefab;
    
    [Header("Spell Cooldowns")]
    public float spell1Cooldown = 1.5f;
    public float spell2Cooldown = 2.5f;
    public float spell3Cooldown = 5f;
    
    [Header("Spell Usage Probability")]
    [Range(0f, 1f)] public float spell1Chance = 0.5f;
    [Range(0f, 1f)] public float spell2Chance = 0.3f;
    [Range(0f, 1f)] public float spell3Chance = 0.2f;
    
    // API Helper ⭐ NUEVO
    public GameObject GetSpellPrefab(int spellIndex);
    public float GetSpellCooldown(int spellIndex);
    public float GetSpellChance(int spellIndex);
    public int SelectRandomSpell();  // Selección ponderada por probabilidades
    public bool HasSpell(int spellIndex);
    public int GetSpellCount();
}
```

#### Sistema de 3 Hechizos (Estilo Pokemon) ⭐ NUEVO

Similar a Pokemon donde cada NPC tiene hasta 4 ataques, en este juego **cada NPC tiene 3 hechizos/ataques**.

**Filosofía:**
- ✅ **Configuración en Prefabs**: Cada prefab de hechizo tiene sus propias stats (velocidad, daño, etc.)
- ✅ **SO solo guarda referencias**: El ScriptableObject solo almacena referencias a los prefabs
- ✅ **Cooldowns individuales**: Cada hechizo tiene su propio cooldown
- ✅ **Probabilidades configurables**: Control de frecuencia de uso

**Ejemplo de Configuración:**
```
NPCCombatConfig: "NPC_Combat_FireMage"

Spells (3 Slots):
├─ Spell 1: Fireball_Small (básico)
│   ├─ Cooldown: 1.5s
│   └─ Chance: 50%
├─ Spell 2: Fireball_Medium (intermedio)
│   ├─ Cooldown: 2.5s
│   └─ Chance: 30%
└─ Spell 3: Meteor_Special (poderoso)
    ├─ Cooldown: 5s
    └─ Chance: 20%

Resultado: Ataca frecuentemente con Fireball básico, ocasionalmente medio, raramente Meteor
```

**Configuración del Prefab de Hechizo:**
```
Fireball_Small.prefab
├─ Mesh/Sprite (visual)
├─ Rigidbody (movimiento)
├─ Collider (detección)
└─ SpellProjectile (Script con stats)
    ├─ damage: 10
    ├─ speed: 15
    ├─ lifetime: 5s
    ├─ explosionRadius: 1
    └─ element: Fire
```

**Uso en Código (NPCCombatBrain):**
```csharp
// Seleccionar hechizo aleatorio basado en probabilidades
int spellIndex = combatConfig.SelectRandomSpell();

// Obtener prefab
GameObject spellPrefab = combatConfig.GetSpellPrefab(spellIndex);

// Lanzar hechizo
GameObject spell = Instantiate(spellPrefab, spawnPoint.position, spawnPoint.rotation);
```

---

### 3.6 Sistema de Narrativa Interactiva ⭐ ACTUALIZADO

#### Modos de Configuración

El sistema ahora tiene **2 modos**:
1. **Modo Simple**: Una sola cadena narrativa (como antes)
2. **Modo Condicional**: Múltiples narrativas con condiciones basadas en quests ⭐ NUEVO

#### Modo Condicional ⭐ NUEVO

**Problema resuelto:** NPCs que entregan ítems importantes antes de que se inicie la quest.

**Solución:** Diferentes narrativas según el estado de las quests.

```csharp
[CreateAssetMenu(menuName = "NPC/Módulos/Interactive Narrative Config")]
public class NPCInteractiveNarrativeConfig : NPCModuleConfigBase
{
    [Header("Modo de Configuración ⭐ NUEVO")]
    public NarrativeModeType narrativeMode; // Simple o Conditional
    
    [Header("Narrative Chain (Simple Mode)")]
    public NarrativeChainEntry[] narrativeChain;
    
    [Header("Conditional Narratives (Conditional Mode) ⭐ NUEVO")]
    public ConditionalNarrative[] conditionalNarratives;
    
    // ...resto de configuración
}
```

#### ConditionalNarrative

```csharp
[Serializable]
public class ConditionalNarrative
{
    [Header("Condición")]
    public NarrativeCondition condition;
    
    [Header("Narrativa")]
    public NarrativeChainEntry[] narrativeChain;
    
    [Header("Icono Persistente ⭐ NUEVO")]
    public bool showPersistentIcon;
    public GameObject persistentIconPrefab;
    
    [Header("Evento al Grafo Narrativo ⭐ NUEVO")]
    public bool sendNarrativeEvent;
    public string narrativeEventKey;
    
    [Header("Configuración")]
    public bool singleUse;
    public int priority;
    public string description;  // Descripción para el Inspector
}
```

#### NarrativeCondition

```csharp
public enum NarrativeConditionType
{
    None,              // Siempre se ejecuta
    QuestNotStarted,   // La quest NO ha sido iniciada
    QuestStarted,      // La quest ha sido iniciada
    QuestCompleted,    // La quest ha sido completada
    QuestActive,       // La quest está activa (iniciada pero no completada)
    Custom             // Condición custom
}

[Serializable]
public class NarrativeCondition
{
    public NarrativeConditionType conditionType;
    public QuestData targetQuest;
    
    public bool Evaluate();  // Evalúa si la condición se cumple
    public string GetDescription();  // Descripción legible
}
```

#### Ejemplo Completo: NPC que Entrega Ítem Importante

**Problema:** El jugador podía hablar con el NPC antes de aceptar la quest y obtener el ítem.

**Solución con Modo Condicional:**

```
NPCInteractiveNarrativeConfig: "NPC_ItemImportante"

Mode: Conditional

Conditional Narratives [2]:
├─ [0] ANTES DE LA QUEST
│   ├─ Description: "Saludo antes de quest"
│   ├─ [Condición]
│   │   ├─ Condition Type: Quest Not Started
│   │   └─ Target Quest: Quest_MainStory
│   ├─ [Narrativa]
│   │   └─ [0] Dialogue: "Hola, que tengas un buen día"
│   ├─ Show Persistent Icon: NO
│   ├─ Send Narrative Event: NO
│   ├─ Single Use: NO  (repetible)
│   └─ Priority: 0
│
└─ [1] DESPUÉS DE INICIAR QUEST
    ├─ Description: "Entregar item cuando quest activa"
    ├─ [Condición]
    │   ├─ Condition Type: Quest Active
    │   └─ Target Quest: Quest_MainStory
    ├─ [Narrativa]
    │   └─ [0] Dialogue: "¡Ah! Necesitas esto para tu misión"
    │   └─ [1] Custom Event: EntregarItem()
    ├─ [Icono Persistente ⭐]
    │   ├─ Show Persistent Icon: SÍ
    │   └─ Persistent Icon Prefab: Icon_Quest (❗)
    ├─ [Evento al Grafo ⭐]
    │   ├─ Send Narrative Event: SÍ
    │   └─ Narrative Event Key: "NPC_ItemEntregado"
    ├─ Single Use: SÍ
    └─ Priority: 10
```

**Flujo en Juego:**

1. **Antes de quest:**
   ```
   Jugador → Habla con NPC
   Sistema → Quest_MainStory.state = Inactive
   Sistema → ✅ Condición "Quest Not Started" se cumple
   NPC → "Hola, que tengas un buen día"
   ```

2. **Después de aceptar quest:**
   ```
   Jugador → Acepta Quest_MainStory
   Sistema → Quest_MainStory.state = Active
   
   Jugador → Vuelve al NPC
   Sistema → ✅ Condición "Quest Active" se cumple
   NPC → ❗ Icono aparece sobre su cabeza
   NPC → "¡Ah! Necesitas esto para tu misión"
   Sistema → Ejecuta EntregarItem()
   Sistema → Envía evento "NPC_ItemEntregado" al grafo narrativo
   Sistema → Oculta icono ❗
   Sistema → Marca narrativa como ejecutada (Single Use)
   ```

3. **Después de entregar:**
   ```
   Jugador → Vuelve al NPC de nuevo
   Sistema → Narrativa [1] ya ejecutada (Single Use)
   Sistema → ❌ No hay narrativas disponibles
   NPC → (No hace nada o usa otra narrativa configurada)
   ```

#### Iconos Persistentes Automáticos ⭐ NUEVO

Los iconos persistentes aparecen **automáticamente** cuando una narrativa condicional está disponible:

```csharp
// NPCInteractiveNarrativeExecutor (Update loop)
void Update()
{
    if (narrativeMode == Conditional)
    {
        var activeNarrative = config.GetActiveNarrative();
        
        if (activeNarrative != null && activeNarrative.showPersistentIcon)
        {
            npcManager.ShowPersistentIcon();  // ❗ Aparece
        }
        else
        {
            npcManager.HidePersistentIcon();  // Se oculta
        }
    }
}
```

#### Envío de Eventos al Grafo Narrativo ⭐ NUEVO

Las narrativas condicionales pueden enviar eventos al grafo narrativo global:

```csharp
// Al completar la narrativa
if (conditionalNarrative.sendNarrativeEvent)
{
    DefaultNarrativeSignals.Instance.RaiseCustom(
        conditionalNarrative.narrativeEventKey
    );
}
```

**Uso en Grafo Narrativo:**
```
Grafo puede escuchar "NPC_ItemEntregado" y:
- Avanzar a otro nodo
- Activar/desactivar otros NPCs
- Cambiar música/ambiente
- Desbloquear áreas
- Etc.
```

#### Sistema de Prioridades

Si múltiples narrativas cumplen sus condiciones, se ejecuta la de **mayor prioridad**:

```
Narrativas:
├─ [0] Priority: 0  - "Saludo genérico" (Quest Not Started)
├─ [1] Priority: 10 - "Entregar ítem" (Quest Active)
└─ [2] Priority: 5  - "Felicitaciones" (Quest Completed)

Si la quest está activa:
  ├─ [0] ✅ Condición cumplida (Priority 0)
  └─ [1] ✅ Condición cumplida (Priority 10) ← Se ejecuta esta
```

---

### 3.7 Sistema de Animaciones (NPCSimpleAnimator) ⭐ COMPLETAMENTE REDISEÑADO

#### Problemas Resueltos

**Antes:**
- ❌ Movimiento ortopédico
- ❌ Rotación brusca
- ❌ Foot sliding (pies patinando)
- ❌ NPCs se chocan con paredes
- ❌ "Pie avanza 500 metros" en batalla
- ❌ Solo 2-3 animaciones básicas

**Ahora:**
- ✅ Movimiento fluido y natural
- ✅ Rotación suave (360°/s configurable)
- ✅ Sin foot sliding (velocidad ajustada dinámicamente)
- ✅ Sincronización perfecta con NavMeshAgent
- ✅ 50+ animaciones integradas
- ✅ Sistema profesional de estados

#### NPCSimpleAnimator

**El ÚNICO cerebro de animaciones del NPC.** Todos los demás sistemas (FSM, Combat, Quest, etc.) piden animaciones aquí.

```csharp
public class NPCSimpleAnimator : MonoBehaviour
{
    [Header("Animation States")]
    public string locomotionState = "Free Locomotion";
    public string idleNormalState = "Idle_Normal_NoWeapon";
    public string idleBattleState = "Idle_Battle_NoWeapon";
    
    [Header("Locomotion Settings")]
    [Range(0.01f, 0.5f)] public float movementThreshold = 0.1f;
    [Range(0.5f, 2f)] public float locomotionSpeedMultiplier = 1.0f;
    
    [Header("Rotation Settings")]
    [Range(90f, 720f)] public float rotationSpeed = 360f;
    [Range(0f, 1f)] public float rotationSmoothness = 0.15f;
    
    // Estados
    public enum AnimationState
    {
        Idle, Walking, Running, Battle, Interacting, OneShot, Dead
    }
}
```

#### API Completa

**Movement & Speed:**
```csharp
// Establecer velocidad (0-1)
animator.SetMovementSpeed(0.75f);

// Resetear
animator.ResetMovement();
```

**Battle Mode:**
```csharp
// Activar modo batalla
animator.SetBattleMode(true);

// Idle de batalla
animator.PlayBattleIdle();
```

**One-Shot Animations:**
```csharp
// Reproducir animación con callback
animator.PlayOneShot("Attack01_NoWeapon", 0, () => {
    Debug.Log("Ataque completado");
});
```

**Combat Animations:**
```csharp
animator.PlayChallenging();
animator.PlaySenseSomething();
animator.PlayDefend();
animator.PlayGetHit();
animator.PlayDeath();
animator.PlayVictory();
```

**Interaction:**
```csharp
animator.BeginInteraction();
animator.PlayGreeting();
animator.SetTalking(bool isTalking);
animator.EndInteraction();
```

**Rotation:**
```csharp
// Mirar hacia un objetivo (suave)
animator.FaceTarget(player.position);

// Rotar durante un tiempo
animator.RotateTowardsTarget(target, 0.3f);
```

#### Sincronización Automática con NavMeshAgent

```csharp
private void SyncWithNavMeshAgent()
{
    // Velocidad normalizada del agente
    float normalizedSpeed = navAgent.velocity.magnitude / navAgent.speed;
    
    // Aplicar a animación
    SetMovementSpeed(normalizedSpeed);
    
    // Rotar según dirección de movimiento
    if (navAgent.velocity.sqrMagnitude > 0.01f)
    {
        FaceDirection(navAgent.velocity.normalized);
    }
}
```

**Resultado:** El NPC siempre mira donde se mueve, sin giros bruscos.

#### Sistema Anti-Foot Sliding

```csharp
public void SetMovementSpeed(float normalizedSpeed)
{
    // Ajustar velocidad de reproducción de la animación
    if (normalizedSpeed > movementThreshold)
    {
        animator.speed = Mathf.Lerp(1f, locomotionSpeedMultiplier, normalizedSpeed);
    }
    
    // Actualizar parámetro InputMagnitude
    animator.SetFloat("InputMagnitude", normalizedSpeed);
}
```

**Resultado:** La velocidad de la animación se ajusta a la velocidad real del movimiento, eliminando el patinaje de pies.

#### Rotación Suave

```csharp
private void ApplySmoothRotation()
{
    float angle = Quaternion.Angle(transform.rotation, _targetRotation);
    
    if (angle < minRotationAngle)
        return;
    
    // Rotación suave con límite de grados por frame
    float maxDegreesDelta = rotationSpeed * Time.deltaTime;
    transform.rotation = Quaternion.RotateTowards(
        transform.rotation,
        _targetRotation,
        maxDegreesDelta
    );
}
```

**Resultado:** Rotación fluida de 360°/s, no más saltos instantáneos.

#### Animaciones Disponibles (NoWeapon)

**Locomotion:**
- `Free Locomotion` (Blend Tree)
- `Idle_Normal_NoWeapon`
- `Idle_Battle_NoWeapon`
- `WalkFWD_NoWeapon`
- `MoveFWD_Battle_RM_NoWeapon`

**Combat:**
- `Attack01_NoWeapon` → `Attack05_NoWeapon` (5 ataques)
- `Challenging_NoWeapon`
- `SenseSomethingStart_NoWeapon`
- `Defend_NoWeapon`
- `GetHit01_NoWeapon`, `GetHit02_NoWeapon`
- `Die01_NoWeapon`, `Die02_NoWeapon`
- `Victory_NoWeapon`

**Interaction:**
- `Greeting01_NoWeapon`, `Greeting02_NoWeapon`
- `InteractWithPeople_NoWeapon`
- `InteractWithGateObject_NoWeapon`

**States:**
- `Sleeping_NoWeapon`
- `Dizzy_NoWeapon`
- `Dance_NoWeapon`
- `DrinkPotion_NoWeapon`
- `FoundSomething_NoWeapon`
- `LevelUp_NoWeapon`

**Root Motion (Combos y Dashes):**
- `Combo01_RM` → `Combo05_RM`
- `DashFWD/BWD/LFT/RGT_Battle_RM`
- `RollFWD/BWD/LFT/RGT_Battle_RM`
- `JumpFull_RM`, `JumpFullSpin_RM`

#### Integración con FSM

Los estados de la FSM ahora **solo piden animaciones**, el animator hace todo el trabajo:

```csharp
// CombatState
public override void OnEnter(NPCStateContext context)
{
    context.Animator.SetBattleMode(true);
}

public void OnAttack()
{
    context.Animator.PlayOneShot("Attack01_NoWeapon");
}

// WanderState
public override void OnUpdate(NPCStateContext context)
{
    // ¡No necesitas hacer nada!
    // El animator sincroniza automáticamente con NavMeshAgent
}
```

#### Parámetros del Animator

**Solo usa 1 parámetro obligatorio:**
- ✅ `InputMagnitude` (float) - Control de velocidad de locomoción

**Opcionales:**
- ⚠️ `IsTalking` (bool) - Si lo añades, se usa para animaciones de diálogo
- ⚠️ Otros parámetros no necesarios, todo se controla por código con `CrossFade()`

---

### 3.8 Sistema de Alerta Visual (Compartido)

**Ubicación:** `Assets/Scripts/Behaviour NPC/Modules/NPCQuestConfig.cs`

Módulo que maneja la lógica completa del sistema de quests para un NPC:

```csharp
[CreateAssetMenu(fileName = "NPC_Quest_Config", menuName = "NPC/Módulos/Quest Config")]
public class NPCQuestConfig : NPCModuleConfigBase
{
    [Header("Quest Chain")]
    public QuestChainEntry[] questChain;
    
    [Header("Item Detection")]
    public bool enableItemDetection = true;
    public float detectionRadius = 3f;
    
    [Header("Behavior")]
    public bool rotateToPlayerOnInteract = true;
    public float rotationDuration = 0.3f;
    
    // MÉTODOS DE LÓGICA (no en NPCBrain)
    public bool ProcessInteraction(GameObject interactor, NPCStateContext context)
    {
        // Busca quest activa en la cadena
        // Procesa completion modes
        // Reproduce diálogos
        // Invoca eventos
    }
}
```

#### QuestChainEntry

Cada entrada en la cadena de misiones del NPC:

```csharp
[Serializable]
public class QuestChainEntry
{
    public QuestData questData;
    public QuestCompletionMode completionMode;
    
    // Diálogos
    public DialogueAsset dlgBefore;        // Antes de aceptar
    public DialogueAsset dlgInProgress;    // Quest activa, pasos incompletos
    public DialogueAsset dlgTurnIn;        // Al completar
    public DialogueAsset dlgCompleted;     // Ya completada
    
    // Eventos
    public UnityEvent onQuestCompleted;
    public UnityEvent onOfferDialogueStarted;
    public UnityEvent onOfferDialogueFinished;
    public UnityEvent onPostActionCompleted;
    
    // Post-Action (movimiento, teleport, fade, etc.)
    public QuestPostAction postAction;
    
    // Detección de ítems
    public bool autoDetectItemDelivery;
    public string itemTag;
    public bool requireItemInInventory;
    public ItemData requiredItem;
}
```

#### Modos de Completado

```csharp
public enum QuestCompletionMode
{
    Manual,                      // Requiere QuestManager.CompleteQuest() externo
    AutoCompleteOnTalk,          // Autocompleta todos los pasos al hablar
    CompleteOnTalkIfStepsReady   // Completa solo si todos los pasos están listos
}
```

#### Flujo de Interacción

```
1. Player interactúa → Interactable.OnInteract()
2. Interactable (HandOffToTarget mode) → NPCBehaviourManagerV2.HandleInteraction()
3. NPCBehaviourManagerV2 → NPCBrain.HandleInteraction()
4. NPCBrain marca context.IsInteracting = true
5. NPCBrain delega → questConfig.ProcessInteraction(interactor, context)
6. NPCQuestConfig:
   - Busca quest activa en questChain (de atrás hacia adelante)
   - Si encuentra quest activa → HandleQuestState()
   - Procesa según QuestCompletionMode
   - Reproduce diálogo correspondiente
   - Invoca eventos (onQuestCompleted, etc.)
   - Si no hay quest activa → Verifica primera quest
```

#### ⚠️ Sistema de Quest Chain: Timing Crítico

**IMPORTANTE:** La quest solo aparece en UI **DESPUÉS** de que el NPC termine el diálogo de oferta.

**Flujo correcto de completar y encadenar:**

```
1. Quest activa se completa:
   ├─ qm.CompleteQuest() → marca como completada
   ├─ PlayDialogue(dlgTurnIn) → NPC dice "Gracias, bien hecho"
   └─ TryStartNextQuestInChain() → busca siguiente quest

2. Si hay siguiente quest en la cadena:
   ├─ SI tiene dlgBefore:
   │  ├─ Reproduce dlgBefore → "Ahora necesito que me traigas..."
   │  ├─ Se suscribe a DialogueManager.OnDialogueEnded
   │  └─ Cuando diálogo TERMINA → qm.StartQuest() → ✅ APARECE EN UI
   │
   └─ SI NO tiene dlgBefore:
      └─ NO hace nada → ⚠️ Quest se iniciará desde otro lugar (grafo narrativo)

3. Primera quest de la cadena:
   └─ Mismo comportamiento: solo inicia automáticamente si tiene dlgBefore
```

**Ejemplo del flujo:**

```
[Jugador lee carta] → "Ve a hablar con Eldran"
[Jugador habla con Eldran]
  → NPC dice: "Gracias por venir" (dlgTurnIn de quest anterior)
  → ⏸️ UI aún NO muestra nueva quest
  → NPC dice: "Necesito que me traigas una caja" (dlgBefore de siguiente quest)
  → ✅ AHORA aparece "Trae la caja" en UI (diálogo terminó)
```

**Por qué es importante:**

- ❌ **MAL:** Quest aparece en UI antes de que NPC la ofrezca → jugador confundido
- ✅ **BIEN:** Quest aparece en UI solo después del diálogo completo → experiencia coherente

**Quests sin dlgBefore:**

Si una quest en la cadena NO tiene `dlgBefore`, significa que se iniciará desde otro lugar:
- Grafo narrativo (NarrativeGraph con nodo StartQuest)
- Evento del mundo (trigger, cinemática)
- Script externo

En este caso, NPCQuestConfig NO inicia la quest automáticamente al completar la anterior.

#### Ejemplo: Configuración de Eldran

```yaml
NPC_Eldran_Config (NPCConfiguration):
  behaviours: Quest | Wander
  questConfig: NPC_Eldran_QuestConfig
  
NPC_Eldran_QuestConfig (NPCQuestConfig):
  questChain[0]:
    questData: ELDRAN_MISSION1
    completionMode: AutoCompleteOnTalk
    dlgBefore: DLG_ELDRAN_INTRO
    dlgTurnIn: DLG_ELDRAN_MISSION1_COMPLETE
    
  questChain[1]:
    questData: ELDRAN_MISSION2
    completionMode: CompleteOnTalkIfStepsReady
    dlgBefore: DLG_ELDRAN_MISSION2_OFFER
    dlgInProgress: DLG_ELDRAN_WAITING
    dlgTurnIn: DLG_ELDRAN_MISSION2_COMPLETE
    requireItemInInventory: true
    requiredItem: FruitBox
```

#### Ejemplo en Gameplay

```
Misión 1: "Habla con Eldran"
  - Mode: AutoCompleteOnTalk
  - dlgBefore: "Hola, necesito tu ayuda"
  - dlgTurnIn: "Gracias por venir"
  
  Flujo:
  1. Player habla con Eldran
  2. Reproduce dlgBefore → "Hola, necesito tu ayuda"
  3. Diálogo termina → Quest aparece en UI
  4. Quest se autocompleta (AutoCompleteOnTalk)
  5. Reproduce dlgTurnIn → "Gracias por venir"
  6. Busca siguiente quest en cadena...

Misión 2: "Trae la caja de frutas"
  - Mode: CompleteOnTalkIfStepsReady
  - dlgBefore: "Ahora necesito que me traigas una caja del bosque"
  - dlgInProgress: "¿Ya encontraste la caja?"
  - dlgTurnIn: "¡Perfecto, muchas gracias!"
  
  Flujo:
  1. Después de completar Misión 1
  2. Reproduce dlgBefore → "Ahora necesito que me traigas..."
  3. Diálogo termina → Quest aparece en UI ("Trae la caja")
  4. Player busca caja en bosque
  5. Player vuelve sin caja → dlgInProgress ("¿Ya la encontraste?")
  6. Player vuelve con caja → Completa automáticamente → dlgTurnIn

Misión 3: "Derrota al enemigo" (sin dlgBefore)
  - Mode: Manual
  - dlgBefore: null ← NO tiene diálogo de oferta
  - dlgTurnIn: "¡Has derrotado al enemigo!"
  
  Flujo:
  1. Después de completar Misión 2
  2. NPCQuestConfig NO inicia esta quest (no tiene dlgBefore)
  3. Quest se inicia desde NarrativeGraph (nodo StartQuest)
  4. Player completa quest
  5. Vuelve al NPC → dlgTurnIn
```

#### Integración con QuestManager

NPCQuestConfig usa QuestManager.Instance directamente:

```csharp
// En NPCQuestConfig.ProcessInteraction()
var qm = QuestManager.Instance;
var state = qm.GetState(questId);  // GetState(), NO GetQuestState()

if (state == QuestState.Active)
{
    bool allDone = qm.AreAllStepsCompleted(questId);
    if (entry.completionMode == QuestCompletionMode.AutoCompleteOnTalk)
    {
        CompleteAllSteps(qm, entry, questId, context);
    }
}

// RuntimeQuest tiene propiedades: Id, State, Steps[]
var quest = qm.GetAll().FirstOrDefault(q => q.Id == questId);
if (quest?.Steps != null)
{
    for (int i = 0; i < quest.Steps.Length; i++)
    {
        if (!quest.Steps[i].completed)  // .completed, NO .isCompleted
        {
            qm.MarkStepDone(questId, i);
        }
    }
}
```

#### ⚠️ Sistema Legacy (NO USAR)

`SimpleQuestNPC` es obsoleto. Fue migrado a NPCQuestConfig. **No uses SimpleQuestNPC en nuevos NPCs.**

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

### 8.1 Filosofía del Sistema

> **"El jugador avanza, el runtimePreset se actualiza dinámicamente. SOLO cuando el jugador guarda manualmente en un punto de guardado, se vuelca el runtimePreset al JSON."**

#### Principios Fundamentales

1. **📝 Runtime Preset como "RAM":** Todo progreso se escribe primero en `runtimePreset`
2. **💾 JSON como "Disco Duro":** El save JSON solo se escribe en puntos de guardado manuales
3. **🔒 Sin Auto-Save por defecto:** `allowAutoSaves = false` garantiza control total del jugador
4. **🔄 Sincronización Explícita:** Los sistemas escriben en `runtimePreset`, NO directamente en JSON

---

### 8.2 Los Tres Presets

```
┌─────────────────────────────────────────────────────────┐
│ GameBootProfile (ScriptableObject)                     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1️⃣ defaultPlayerPreset (Template)                      │
│     └─ Config de Nueva Partida (nivel 1, 100 HP, etc) │
│     └─ NO se modifica nunca en runtime                 │
│                                                         │
│  2️⃣ bootPreset (Testing - Opcional)                     │
│     └─ Preset personalizado para testing              │
│     └─ Permite probar con facilidad (nivel 10, etc)   │
│                                                         │
│  3️⃣ runtimePreset (Activo - CRÍTICO)                    │
│     └─ El preset que se escribe durante la partida    │
│     └─ Se actualiza dinámicamente al jugar            │
│     └─ Se vuelca a JSON solo en guardado manual       │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

### 8.3 Flujo Completo del Sistema

#### 🆕 **Nueva Partida**

```
1. Jugador → "Nueva Partida"
2. GameBootProfile.NewGameReset()
   └─ Copia defaultPlayerPreset → runtimePreset
   └─ Limpia save JSON (si existe)
   └─ Resetea QuestManager, BossTracker, etc.
3. Player entra al juego con config inicial
```

#### 🎮 **Durante el Juego**

```
Jugador avanza (recoge item, completa quest, sube nivel)
   ↓
Sistema específico actualiza su estado
   ↓
runtimePreset se mantiene EN MEMORIA
   ↓
NO se guarda en JSON automáticamente (allowAutoSaves = false)
```

**Ejemplo:**
```csharp
// ❌ INCORRECTO: Guardar directamente en JSON
saveSystem.Save(data);

// ✅ CORRECTO: Escribir en runtimePreset
var preset = gameBootProfile.GetActivePresetResolved();
preset.currentHP = newHP;
preset.flags.Add("QUEST_COMPLETED_01");
```

#### 💾 **Guardado Manual**

```
Jugador → Punto de Guardado Manual
   ↓
SaveCurrentGameState(SaveSystem, SaveRequestContext.Manual)
   ↓
1. UpdateRuntimePresetFromCurrentState()
   └─ PlayerHealthSystem → runtimePreset.currentHP
   └─ ManaPool → runtimePreset.currentMP
   └─ QuestManager → runtimePreset.flags
   └─ Inventory → runtimePreset.inventoryItems
   └─ NPCs → runtimePreset.npcPositions
   └─ Etc.
   ↓
2. BuildSaveDataFromProfile()
   └─ runtimePreset → PlayerSaveData
   ↓
3. saveSystem.Save(data)
   └─ PlayerSaveData → JSON (disco)
```

#### 📂 **Carga de Partida**

```
Jugador → "Continuar"
   ↓
LoadProfile(SaveSystem)
   ↓
1. saveSystem.Load(out data)
   └─ JSON → PlayerSaveData
   ↓
2. SetRuntimePresetFromSave(data)
   └─ PlayerSaveData → runtimePreset
   ↓
3. ApplySaveDataToProfile(data)
   └─ PlayerHealthSystem ← runtimePreset.currentHP
   └─ ManaPool ← runtimePreset.currentMP
   └─ QuestManager ← runtimePreset.flags
   └─ Inventory ← runtimePreset.inventoryItems
   └─ NPCs ← runtimePreset.npcPositions
```

---

### 8.4 API del GameBootProfile

#### Métodos Principales

```csharp
// 🆕 Nueva Partida (limpia TODO y resetea a default)
GameBootProfile.NewGameReset(SaveSystem saveSystem = null);

// 🔄 Actualizar runtimePreset desde sistemas vivos
GameBootProfile.UpdateRuntimePresetFromCurrentState();

// 💾 Guardar estado actual (Manual o Auto)
GameBootProfile.SaveCurrentGameState(SaveSystem, SaveRequestContext);

// 📂 Cargar desde JSON y aplicar a sistemas
GameBootProfile.LoadProfile(SaveSystem);

// 📋 Obtener preset activo (siempre devuelve runtimePreset)
PlayerPresetSO preset = GameBootProfile.GetActivePresetResolved();
```

#### SaveRequestContext

```csharp
public enum SaveRequestContext
{
    Manual,  // Guardado explícito del jugador (punto de guardado)
    Auto     // Auto-guardado (solo si allowAutoSaves = true)
}
```

---

### 8.5 Configuración en Inspector

```
GameBootProfile (ScriptableObject)
├─ [Arranque]
│  ├─ Scene To Load: "MainWorld"
│  └─ Default Player Preset: DefaultPlayerPreset (SO)
│
├─ [Boot Settings]
│  ├─ Use Preset Instead Of Save: ☐ (testing)
│  └─ Boot Preset: TestPreset (SO) (opcional)
│
├─ [Runtime Fallback]
│  └─ Runtime Preset: (auto-generado en runtime)
│
└─ [Save Options]
   └─ Allow Auto Saves: ☐ FALSE por defecto
```

**⚠️ IMPORTANTE:**
- `allowAutoSaves` debe estar **FALSE** en producción
- Solo activar para testing de auto-guardado

---

### 8.6 Datos Persistidos

#### PlayerPresetSO (runtimePreset)

```csharp
public class PlayerPresetSO : ScriptableObject
{
    // Spawn
    public string spawnAnchorId;
    
    // Stats
    public int level;
    public float maxHP, currentHP;
    public float maxMP, currentMP;
    
    // Abilities
    public PlayerAbilities abilities; // swim, jump, climb, fly, magic
    public List<AbilityId> unlockedAbilities;
    public List<SpellId> unlockedSpells;
    public SpellId leftSpellId, rightSpellId, specialSpellId;
    
    // Flags
    public List<string> flags; // Quest states, cinematics seen, etc.
    
    // Apariencia
    public List<AppearanceEntry> appearance;
    public List<string> unlockedWardrobeIds;
    
    // Progreso
    public List<InventoryItemSave> inventoryItems;
    public List<string> defeatedBossIds;
    public List<string> consumedInteractableIds;
    
    // NPCs
    public List<NpcPosEntry> npcPositions;
    
    // Narrativa
    public List<NarrativeBlackboardSnapshot> narrativeBlackboards;
}
```

---

### 8.7 Reglas de Persistencia

#### ✅ **CORRECTO**

```csharp
// 1. Obtener preset activo
var preset = GameBootProfile.Instance.GetActivePresetResolved();

// 2. Modificar preset durante el juego
preset.currentHP -= damage;
preset.flags.Add("QUEST_ELDRAN_COMPLETED");
preset.inventoryItems.Add(new InventoryItemSave { itemId = "POTION", count = 1 });

// 3. Guardar manualmente cuando el jugador lo pida
GameBootProfile.Instance.SaveCurrentGameState(SaveSystem.Instance, SaveRequestContext.Manual);
```

#### ❌ **INCORRECTO**

```csharp
// ❌ NO guardar directamente en JSON sin pasar por GameBootProfile
SaveSystem.Instance.Save(data);

// ❌ NO modificar defaultPlayerPreset en runtime
defaultPlayerPreset.currentHP = 50; // NUNCA hacer esto

// ❌ NO crear tu propio sistema de persistencia
PlayerPrefs.SetFloat("HP", currentHP); // Usar GameBootProfile
```

---

### 8.8 Sincronización de Sistemas

Cuando se guarda manualmente, `UpdateRuntimePresetFromCurrentState()` sincroniza:

| Sistema | Fuente | Destino en runtimePreset |
|---------|--------|--------------------------|
| **Spawn** | `SpawnManager.CurrentAnchorId` | `spawnAnchorId` |
| **Health** | `PlayerHealthSystem` | `currentHP`, `maxHP` |
| **Mana** | `ManaPool` | `currentMP`, `maxMP` |
| **Quests** | `QuestManager.ExportFlags()` | `flags` |
| **Abilities** | `PlayerActionManager` | `abilities` |
| **Inventory** | `Inventory.GetSaveSnapshot()` | `inventoryItems` |
| **Appearance** | `ModularAutoBuilder.GetSelection()` | `appearance` |
| **Bosses** | `BossProgressTracker.GetSnapshot()` | `defeatedBossIds` |
| **NPCs** | `NPCBehaviourManagerV2.lastPosition` | `npcPositions` |
| **Narrativa** | `NarrativeGraphHub.CaptureBlackboards()` | `narrativeBlackboards` |

---

### 8.9 Testing del Sistema

#### Desde Inspector (Testing)

```
1. GameBootProfile → Boot Settings
   └─ ☑ Use Preset Instead Of Save
   └─ Boot Preset: TestPreset_Level10
2. Play → El juego arranca con ese preset (ignora JSON)
```

#### Debugging con GameBootProfileDebugger

```
1. Añadir GameBootProfileDebugger al GameObject con GameBootService
2. Play → Presionar F4
3. Ver:
   - Estado actual del runtimePreset
   - Comparación con sistemas vivos
   - Historial de Save/Load
   - Detectar desincronización
```

---

### 8.10 Puntos de Guardado Manuales

#### Implementación Típica

```csharp
public class SavePoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Mostrar UI de confirmación
            ShowSavePrompt();
        }
    }
    
    private void OnPlayerConfirm()
    {
        var profile = GameBootService.Profile;
        var saveSystem = SaveSystem.Instance;
        
        if (profile.SaveCurrentGameState(saveSystem, SaveRequestContext.Manual))
        {
            Debug.Log("✅ Partida guardada exitosamente");
            ShowSaveSuccessUI();
        }
        else
        {
            Debug.LogError("❌ Error al guardar la partida");
            ShowSaveErrorUI();
        }
    }
}
```

---

### 8.11 Persistencia de NPCs

Los NPCs que tienen `persistLastPosition = true` guardan su posición:

```csharp
// En NPCBehaviourManagerV2
[SerializeField] public bool persistLastPosition = false;
[NonSerialized] public Vector3 lastPosition;

// Al mover un NPC programáticamente
public void SetLastPosition(Vector3 worldPosition)
{
    lastPosition = worldPosition;
}

// Al guardar
CaptureNpcPositionsFromScene(runtimePreset);
  └─ Recorre todos los NPCBehaviourManagerV2
  └─ Si persistLastPosition == true:
      └─ preset.npcPositions.Add(new NpcPosEntry {
            npcId = npc.gameObject.name,
            position = npc.lastPosition != default 
                ? npc.lastPosition 
                : npc.transform.position
         });

// Al cargar
ApplyNpcPositionsToScene(runtimePreset);
  └─ Por cada entry en preset.npcPositions:
      └─ Encuentra NPC por nombre
      └─ Aplica posición guardada
```

---

### 8.12 Resumen Visual

```
┌────────────────────────────────────────────────┐
│           FLUJO DE GUARDADO                    │
├────────────────────────────────────────────────┤
│                                                │
│  🎮 JUGADOR JUEGA                              │
│     └─> Sistemas modifican runtimePreset       │
│         (HP, MP, Quests, Inventory, etc.)     │
│                                                │
│  💾 JUGADOR GUARDA (Punto Manual)              │
│     └─> UpdateRuntimePresetFromCurrentState() │
│         (Sincroniza TODO → runtimePreset)     │
│     └─> BuildSaveDataFromProfile()            │
│         (runtimePreset → PlayerSaveData)      │
│     └─> saveSystem.Save(data)                 │
│         (PlayerSaveData → JSON disco)         │
│                                                │
│  📂 JUGADOR CARGA                              │
│     └─> saveSystem.Load(out data)             │
│         (JSON disco → PlayerSaveData)         │
│     └─> SetRuntimePresetFromSave(data)        │
│         (PlayerSaveData → runtimePreset)      │
│     └─> ApplySaveDataToProfile(data)          │
│         (runtimePreset → Sistemas vivos)      │
│                                                │
└────────────────────────────────────────────────┘
```

---

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

