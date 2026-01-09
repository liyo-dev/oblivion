﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿# 📘 El Sendero de las Estrellas - Documentación Técnica

**Proyecto:** El Sendero de las Estrellas  
**Motor:** Unity 2020.3+  
**Fecha:** Enero 2025  
**Versión del Documento:** 1.1

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
   - 7.1 [Sistema de Quests](#71-sistema-de-quests)
   - 7.1.1 [🆕 Sistema de Detección de Items del Wardrobe en Quests](#711-sistema-de-detección-de-items-del-wardrobe-en-quests)
   - 7.5 [Sistema de SpawnAnchor y Orientación](#75-sistema-de-spawnanchor-y-orientación)
8. [Sistema de Guardado](#8-sistema-de-guardado)
   - 8.9 [🆕 Sistema de Testing con Presets](#89-sistema-de-testing-con-presets)
9. [Sistema de Cinemáticas](#9-sistema-de-cinematicas)
10. [Solución de Problemas Comunes](#10-solución-de-problemas-comunes)
11. [Mejores Prácticas](#11-mejores-prácticas)
   - 11.12 [Sistemas Auxiliares](#1112-sistemas-auxiliares)
   - 11.13 [Problemas Conocidos y Soluciones](#1113-problemas-conocidos-y-soluciones-legacy)
12. [Sistema de Puzzles](#12-sistema-de-puzzles)
   - 12.1 [Burnable - Objetos Quemables](#121-burnable---objetos-quemables)
   - 12.2 [PressurePlate - Interruptor de Presión](#122-pressureplate---interruptor-de-presión)
13. [Sistema de Iconos en Diálogos](#13-sistema-de-iconos-en-diálogos)
   - 13.1 [Configuración de Sprites en TextMeshPro](#131-configuración-de-sprites-en-textmeshpro)
   - 13.2 [Troubleshooting](#132-troubleshooting)
14. [Sistema de Iluminación (Bake Nocturno)](#14-sistema-de-iluminación-bake-nocturno)
   - 14.1 [Configuración Optimizada de Lightmaps](#141-configuración-optimizada-de-lightmaps)
15. [Troubleshooting Adicional](#15-troubleshooting-adicional)
   - 15.1 [Errores del AI Toolkit (IGNORAR)](#151-errores-del-ai-toolkit-ignorar)
   - 15.2 [Problemas con PressurePlate](#152-problemas-con-pressureplate)
   - 15.3 [Problemas con Burnable](#153-problemas-con-burnable)
   - 15.4 [Problemas con Iconos en Diálogos](#154-problemas-con-iconos-en-diálogos)
16. [🆕 Changelog - Actualizaciones del Sistema](#16-changelog---actualizaciones-del-sistema)
   - 16.1 [Versión 1.1 - Enero 2025](#versión-11---enero-2025)

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
    
    public QuestChainEntry[] questChain;
    public bool enableItemDetection = true;
    public float detectionRadius = 3f;
    
    [Header("Persistent Icon")]  // ⭐ NUEVO (Dic 2025)
    public GameObject questIconPrefab;           // Prefab del icono (!)
    public Vector3 questIconOffset;              // Offset del icono
    public bool showIconWhenQuestAvailable;      // Mostrar cuando hay quest disponible
    public bool showIconWhenQuestInProgress;     // Mostrar cuando quest en progreso
    public bool showIconWhenQuestReadyToTurnIn;  // Mostrar cuando lista para entregar
    public GameObject turnInIconPrefab;          // Prefab alternativo (?) para entregar
    public bool hideIconWhenAllCompleted;        // Ocultar cuando todo completo
}
```

#### Sistema de Iconos de Quest (NUEVO Dic 2025)

Los NPCs con quests muestran automáticamente un icono sobre su cabeza:

**Estados del Icono:**
| Estado | Icono | Descripción |
|--------|-------|-------------|
| `Available` | `questIconPrefab` (!) | Quest disponible para iniciar |
| `InProgress` | `questIconPrefab` (!) | Quest activa, pasos pendientes |
| `ReadyToTurnIn` | `turnInIconPrefab` (?) | Quest lista para entregar |
| `None` | - | Todas completadas o sin quests |

**Componente NPCQuestIconManager:**
- Se añade automáticamente cuando el NPC tiene `questConfig`
- Se suscribe a eventos del QuestManager
- Actualiza el icono automáticamente cuando cambian las quests

**Uso:**
```
NPC Quest Giver:
└─ behaviourType: [Quest + Ambient]
    ├─ ambientConfig: NPC_Ambient_StandStill
    └─ questConfig: NPC_Quest_MainStory
        ├─ questIconPrefab: Canvas_QuestIcon (!)
        ├─ turnInIconPrefab: Canvas_TurnInIcon (?)
        ├─ showIconWhenQuestAvailable: ✅
        ├─ showIconWhenQuestInProgress: ✅
        ├─ showIconWhenQuestReadyToTurnIn: ✅
        └─ questChain:
            ├─ [0] Quest_FindCat
            └─ [1] Quest_DefeatBoss
```

---

### 3.5.1 Sistema de Huida Táctica y Cobertura ⭐ NUEVO

#### Resumen

Los NPCs pueden **huir estratégicamente** cuando están en desventaja, buscando **cobertura detrás de objetos** (árboles, rocas, edificios) para protegerse del jugador.

**Características:**
- ✅ Detecta situaciones de desventaja (salud baja, cooldowns activos)
- ✅ Busca cobertura automáticamente usando Raycast e IA
- ✅ Evalúa múltiples posiciones para encontrar la óptima
- ✅ Bloquea línea de visión con el jugador
- ✅ Sistema de cooldown para equilibrio
- ✅ Alternativa con escudo si no hay cobertura disponible

#### Problema Resuelto

**ANTES:** NPC con salud baja sigue atacando → Muere fácilmente

**AHORA:** NPC con salud baja busca cobertura detrás de árbol → Se esconde 4s → Vuelve al combate

#### Sistema de Decisión

**Condiciones para activar huida:**
1. Salud baja: `HP <= 30%` (configurable)
2. Sin recursos: Todos los ataques en cooldown Y escudo en cooldown
3. Estado defensivo: `CombatState.Defensive`

**Prioridades:** Cobertura → Escudo → Fallback cobertura

#### NPCTacticalRetreat.cs

**Ubicación:** `Assets/Scripts/Behaviour NPC/NPCTacticalRetreat.cs`

**Configuración:**
```csharp
[Header("Configuración de Cobertura")]
public float coverSearchRadius = 15f;      // Radio de búsqueda
public LayerMask coverLayerMask = -1;      // Capas de cobertura
public float coverStayDuration = 4f;       // Tiempo en cobertura
public bool showDebugGizmos = true;        // Gizmos en Scene view
```

**Propiedades:**
- `bool IsRetreating` - Estado de huida
- `bool IsBehindCover` - Si llegó a cobertura
- `Vector3? CoverPosition` - Posición de cobertura
- `Transform CoverObject` - Objeto de cobertura

**Algoritmo:**
1. Busca objetos cercanos con `Physics.OverlapSphere`
2. Evalúa cada objeto con scoring (distancia, tamaño, dirección)
3. Verifica que bloquee línea de visión
4. Navega hacia la mejor posición

#### Integración en NPCCombatBrain

**Nuevos campos:**
```csharp
public bool useTacticalRetreat = false;      // Activar sistema
public float retreatHealthThreshold = 0.3f;  // 30% salud
public float retreatCooldown = 15f;          // Cooldown
public bool preferShieldOverCover = false;   // Priorizar escudo
```

**Ejemplo de Configuración:**
```
NPCCombatConfig:
  ├─ useTacticalRetreat: ☑
  ├─ retreatHealthThreshold: 0.3 (30%)
  ├─ retreatCooldown: 15s
  ├─ coverSearchRadius: 15m
  ├─ preferShieldOverCover: ☐
  └─ coverStayDuration: 4s
```

#### Debugging

**Gizmos:**
- 🟢 Radio de búsqueda
- 🔵 Objetos candidatos
- 🟡 Cobertura seleccionada
- 🔴 Línea de visión

**Logs:**
```log
[NPCTacticalRetreat] 🔍 Buscando cobertura (radio: 15m)
[NPCTacticalRetreat] ✅ Cobertura seleccionada: Tree_Oak_03
[NPCCombatBrain] 🏃 Retirándose (HP: 25%)
[NPCTacticalRetreat] 🛡️ Detrás de cobertura - Esperando 4.0s
```

---

### 3.6 Sistema de Narrativa Interactiva (REFACTORIZADO Dic 2025)

#### Arquitectura Simplificada

El sistema de narrativa interactiva ha sido **refactorizado** para eliminar redundancias:

- **Antes:** Configuración global + configuración por narrativa (redundante)
- **Ahora:** Cada `ConditionalNarrative` controla su propio comportamiento

#### NPCInteractiveNarrativeConfig (ScriptableObject)

**Configuración compartida del módulo:**

```csharp
[CreateAssetMenu(menuName = "NPC/Módulos/Interactive Narrative Config")]
public class NPCInteractiveNarrativeConfig : NPCModuleConfigBase
{
    [Header("Narrativas Condicionales")]
    public ConditionalNarrative[] conditionalNarratives;  // Lista de narrativas
    
    [Header("Persistencia")]
    public bool persistState = true;           // Guardar estado en save
    public string persistenceId;               // Auto-generado: "NombreAsset_hash8"
    
    [Header("Comportamiento General")]
    public bool rotateToPlayerOnInteract = true;
    public float rotationDuration = 0.3f;
    
    [Header("Layer Management")]
    public LayerMode initialLayer = LayerMode.Interactable;
    public bool switchToEnemyLayerOnCombat = true;
    
    [Header("Detección del Jugador")]
    public float detectionRange = 10f;         // Rango compartido
    public GameObject alertIconPrefab;         // Icono de alerta (!)
    public float alertIconDuration = 1f;
    public bool walkTowardsPlayerOnAlert = true;
    public float stopDistanceFromPlayer = 2f;
}
```

#### ConditionalNarrative (Por Narrativa)

**Cada narrativa controla su propio comportamiento:**

```csharp
[Serializable]
public class ConditionalNarrative
{
    [Header("Identificación")]
    public string description;                 // Nombre descriptivo
    public int priority = 0;                   // Mayor = evalúa primero
    
    [Header("Condición")]
    public NarrativeCondition condition;       // Cuándo se activa
    
    [Header("Narrativa")]
    public NarrativeChainEntry[] narrativeChain;  // Acciones a ejecutar
    
    [Header("Comportamiento de Ejecución")]
    public bool singleUse = true;              // ¿Una sola vez?
    public bool autoStartOnDetection = false;  // ¿Auto-iniciar al detectar jugador?
    
    [Header("Estado Post-Narrativa")]
    public PostNarrativeState postNarrativeState = PostNarrativeState.None;
    public NPCAmbientConfig postNarrativeAmbientConfig;
    
    [Header("Icono Persistente")]
    public bool showPersistentIcon = false;    // Mostrar icono sobre cabeza
    public GameObject persistentIconPrefab;    // Prefab del icono
    
    [Header("Evento al Grafo Narrativo")]
    public bool sendNarrativeEvent = false;
    public string narrativeEventKey;
}
```

#### PostNarrativeState

```csharp
public enum PostNarrativeState
{
    None,              // No hacer nada especial
    Idle,              // Forzar estado Idle
    Wander,            // Activar comportamiento Wander
    SwitchToAmbient,   // Cambiar a NPCAmbientConfig específico
    Disable            // Desactivar el GameObject
}
```

#### Ejemplo: NPC con Intro + Diálogo Repetible

```
NPC Oliver:
└─ NPCInteractiveNarrativeConfig
    ├─ persistenceId: "oliver-narrative-xyz123"
    └─ conditionalNarratives:
        ├─ [0] Narrativa "Intro" (prioridad 10)
        │   ├─ singleUse: ✅
        │   ├─ autoStartOnDetection: ✅
        │   ├─ postNarrativeState: None
        │   ├─ showPersistentIcon: ❌
        │   └─ narrativeChain: [Diálogo, Movimiento]
        │
        └─ [1] Narrativa "Diálogo Repetible" (prioridad 0)
            ├─ singleUse: ❌
            ├─ autoStartOnDetection: ❌
            ├─ postNarrativeState: None
            ├─ showPersistentIcon: ✅
            └─ narrativeChain: [Diálogo]
```

**Flujo:**
1. Nueva partida → Oliver detecta jugador → ejecuta "Intro" (autoStart)
2. "Intro" se marca como ejecutada (singleUse)
3. "Diálogo Repetible" se activa (muestra icono)
4. Jugador interactúa → ejecuta "Diálogo Repetible"
5. Al cargar partida → "Intro" ya ejecutada → solo "Diálogo Repetible" disponible

#### Tipos de Acciones Narrativas

```csharp
public enum NarrativeActionType
{
    Dialogue,       // Mostrar diálogo
    Move,           // Mover a punto (anchor o transform)
    PlayAnimation,  // Reproducir animación
    StartQuest,     // Iniciar quest
    StartCombat,    // Iniciar combate
    Wait            // Esperar X segundos
}
``````

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
4. ⏱️ Cooldown post-ejecución (0.5s) ⭐ NUEVO
```

**Sistema de Cooldown Post-Narrativa (v1.1 - Enero 2025):**

Para evitar que el jugador pueda interactuar inmediatamente después de que termine una narrativa (causando diálogos duplicados o comportamientos inesperados), se implementó un **cooldown de 0.5 segundos**.

```csharp
// Estado interno
private float _lastExecutionEndTime = -999f;
private const float POST_EXECUTION_COOLDOWN = 0.5f;

// La propiedad IsExecuting incluye el cooldown
public bool IsExecuting => _isExecuting || 
                          (Time.time - _lastExecutionEndTime < POST_EXECUTION_COOLDOWN);

// Al finalizar la narrativa
private IEnumerator ExecuteNarrativeChain(...)
{
    // ... ejecución de acciones ...
    
    _isExecuting = false;
    _lastExecutionEndTime = Time.time; // ✅ Activa cooldown
}
```

**Comportamiento:**
- Durante 0.5s después de terminar una narrativa, `IsExecuting` devuelve `true`
- `Interactable.CanInteract()` verifica `IsExecuting`, bloqueando la interacción
- Esto previene que el jugador pulse el botón de interacción dos veces rápidamente
- El cooldown es transparente para el jugador (0.5s es imperceptible)

**Log:**
```
[NarrativeExecutor:Victoria] ⏱️ Narrativa finalizada - Cooldown activo hasta 123.45s
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
| **Quest** | `Quest` | NPCQuestConfig | NPCQuestActionExecutor, **NPCQuestIconManager** | Quest givers con iconos |
| **Narrative** | `Narrative` | NPCNarrativeConfig | - | NPCs con grafo narrativo |
| **Interactive Narrative** | `InteractiveNarrative` | NPCInteractiveNarrativeConfig | NPCInteractiveNarrativeExecutor, NPCAlertIconController | Secuencias guiadas |

#### Combinaciones Comunes

| Tipo de NPC | Flags | Comportamiento |
|-------------|-------|----------------|
| **Villager** | `Ambient` | Vaga por el pueblo |
| **Enemy** | `Ambient + Combat` | Patrulla y ataca |
| **Quest Giver** | `Quest + Ambient` | Da misiones con icono (!), puede moverse |
| **Boss** | `Combat` | Solo combate, estático |
| **Tutorial Guide** | `InteractiveNarrative` | Secuencia guiada con auto-detección |
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

#### Sistema de Persistencia de Narrativas ⭐ IMPORTANTE

**¿Cómo funciona `singleUse` y `persistState`?**

El sistema de persistencia de narrativas está diseñado para funcionar **por partida guardada**, no entre sesiones de Unity.

##### Configuración

```csharp
[Header("Configuración")]
public bool singleUse = true;      // ¿Se ejecuta solo una vez por partida?
public bool persistState = true;   // ¿Guardar el estado en el preset?
public string persistenceId;       // ID único generado automáticamente
```

##### Comportamiento Detallado

**`singleUse = true`:**
- La narrativa se ejecuta **UNA VEZ por partida**
- Después de completarse, **no se puede repetir** en esa partida
- Al crear una **NUEVA PARTIDA** → Se resetea y vuelve a estar disponible ✅

**`persistState = true`:**
- El estado de "completado" se guarda en `PlayerPresetSO`
- Se guarda cuando el jugador hace **SAVE** (F5 o menú de guardado)
- Al **CARGAR** esa partida guardada → Se restaura el estado
- Al crear una **NUEVA PARTIDA** → El preset es limpio, las narrativas vuelven a estar disponibles ✅

##### Flujo Completo Paso a Paso

```
1️⃣ NUEVA PARTIDA "Partida1"
   ┌────────────────────────────────────────┐
   │ PlayerPresetSO (limpio)                │
   │ completedInteractiveNarratives = []    │
   │                                        │
   │ Todas las narrativas disponibles ✅    │
   └────────────────────────────────────────┘

2️⃣ Ejecutar narrativa "IntroVillage"
   ┌────────────────────────────────────────┐
   │ Narrativa se completa                  │
   │ _hasBeenUsed = true (en memoria)       │
   │                                        │
   │ NO se puede volver a ejecutar ❌       │
   └────────────────────────────────────────┘

3️⃣ Guardar partida (SAVE - F5)
   ┌────────────────────────────────────────┐
   │ PlayerPresetSO actualizado:            │
   │ completedInteractiveNarratives =       │
   │   ["IntroVillage"]                     │
   │                                        │
   │ Estado persistido en archivo JSON ✅   │
   └────────────────────────────────────────┘

4️⃣ Cargar partida (LOAD "Partida1")
   ┌────────────────────────────────────────┐
   │ Se carga PlayerPresetSO guardado       │
   │ RestoreState() lee:                    │
   │   completedInteractiveNarratives =     │
   │     ["IntroVillage"]                   │
   │                                        │
   │ _hasBeenUsed = true                    │
   │ La narrativa sigue completada ✅       │
   └────────────────────────────────────────┘

5️⃣ CREAR NUEVA PARTIDA "Partida2"
   ┌────────────────────────────────────────┐
   │ Se crea PlayerPresetSO NUEVO           │
   │ completedInteractiveNarratives = []    │
   │                                        │
   │ La narrativa vuelve a estar            │
   │ disponible de nuevo ✅                 │
   └────────────────────────────────────────┘
```

##### Dónde se Guarda

```
📂 PlayerPresetSO.cs
  ├─ completedInteractiveNarratives: List<string>
  │  └─ Contiene los persistenceId de narrativas completadas
  │
  └─ Se serializa en JSON cuando:
     ├─ El jugador hace SAVE (F5)
     ├─ Desde el menú de guardado
     └─ Auto-save (si está configurado)

📂 NPCInteractiveNarrativeExecutor.cs
  ├─ _hasBeenUsed: bool (en memoria)
  │  └─ Se marca en CompleteNarrative()
  │
  ├─ SaveState()
  │  └─ preset.completedInteractiveNarratives.Add(persistenceId)
  │
  └─ RestoreState()
     └─ _hasBeenUsed = preset.completedInteractiveNarratives.Contains(persistenceId)
```

##### Logs Esperados

```log
// Al iniciar con narrativa disponible
[NPCInteractiveNarrativeExecutor:NPC_Eldran] ✅ Narrativa 'IntroVillage' disponible para ejecutar

// Al completar narrativa
[NPCInteractiveNarrativeExecutor:NPC_Eldran] ✅ Narrativa 'IntroVillage' completada
[NPCInteractiveNarrativeExecutor:NPC_Eldran] 💾 Estado guardado: IntroVillage

// Al cargar partida guardada
[NPCInteractiveNarrativeExecutor:NPC_Eldran] 🔄 Narrativa 'IntroVillage' ya completada (del último guardado manual)

// Al crear nueva partida
[NPCInteractiveNarrativeExecutor:NPC_Eldran] ✅ Narrativa 'IntroVillage' disponible para ejecutar
```

##### Casos de Uso Comunes

**1. Narrativa Única por Partida (Cinemática Intro)**
```csharp
singleUse = true;       // Solo se ve una vez
persistState = true;    // Se guarda en la partida
```

**2. Narrativa Repetible (Comerciante)**
```csharp
singleUse = false;      // Siempre disponible
persistState = false;   // No se guarda
```

**3. Entrega de Ítem Importante**
```csharp
// Narrativa 1: Antes de quest
singleUse = false;      // Repetible (saludo)
persistState = false;

// Narrativa 2: Entregar ítem (quest activa)
singleUse = true;       // Solo una vez
persistState = true;    // Se guarda
```

##### Solución de Problemas

**Problema:** "La narrativa no se resetea al crear nueva partida"

**Posibles causas:**
1. ❌ El `PlayerPresetSO` no se está creando limpio
2. ❌ Se está compartiendo el mismo preset entre partidas
3. ❌ `RestoreState()` se llama antes de limpiar el preset

**Solución:** Verificar en `GameBootService`:
```csharp
// Al crear nueva partida, debe crear un nuevo PlayerPresetSO
public void CreateNewGame(string presetName)
{
    var newPreset = ScriptableObject.CreateInstance<PlayerPresetSO>();
    newPreset.completedInteractiveNarratives = new List<string>();  // Limpio ✅
    // ...
}
```

**Problema:** "La narrativa se repite en la misma partida"

**Causa:** `singleUse = false` o `_hasBeenUsed` no se está marcando correctamente

**Solución:** Verificar logs:
```log
[NPCInteractiveNarrativeExecutor] ⚠️ singleUse=false → La narrativa es repetible
```

##### Herramientas de Debug

**NPCNarrativeStateManager** (Developer Menu)
```
📋 Limpiar Estado de Narrativas
├─ Limpia PlayerPrefs (sistema antiguo)
├─ Limpia completedInteractiveNarratives del preset actual
└─ Útil para testing y debugging
```

**Uso desde código:**
```csharp
// Resetear narrativa específica
NPCNarrativeStateManager.ClearNarrativeState("IntroVillage");

// Limpiar todas las narrativas
NPCNarrativeStateManager.ClearAllNarrativeStates();
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

### 7.1.1 Sistema de Detección de Items del Wardrobe en Quests

**Versión:** 1.1 (Enero 2025)
**Propósito:** Permitir que las quests detecten automáticamente cuando el jugador obtiene items del wardrobe (ropa, capas, accesorios) y completen steps automáticamente.

#### Arquitectura

El sistema funciona mediante **suscripción a eventos**:

1. **QuestManager** se suscribe a `WardrobeInventory.OnWardrobeChanged`
2. Cuando el jugador desbloquea un item del wardrobe, se dispara el evento
3. QuestManager verifica todas las quests activas
4. Si alguna quest requiere ese item, marca el step correspondiente como completado

#### Configuración en Unity

**1. Configurar la Quest (ScriptableObject):**

```
Click derecho → Create → Quests → Quest Data
Quest ID: "victoria_get_cloak"
Title Key: "QUEST_VICTORIA_CLOAK_TITLE"
Description Key: "QUEST_VICTORIA_CLOAK_DESC"
Steps:
  [0] conditionId: "" (o dejar vacío)
      description: "Obtener la capa de mago"
```

**2. Configurar el NPC que da la Quest:**

```csharp
// En el GameObject del NPC (ej: Victoria)
NPCBehaviourManagerV2 → Configuration → Quest Config → Quest Chain:

[0] Quest Data: Quest_VictoriaGetCloak
    Completion Mode: Manual
    
    ✨ NUEVO: Required Wardrobe Items:
    [0] Item: Cloak02 (WardrobeItemSO)
        Step Index: 0
        Step Condition Id: (dejar vacío si usas Step Index)
    
    Dialogues:
      - Before: DG_VICTORIA_BEFORE_QUEST
      - In Progress: DG_VICTORIA_IN_PROGRESS
      - Turn In: DG_VICTORIA_TURN_IN
      - Completed: DG_VICTORIA_COMPLETED
```

**3. Nodo Narrativo que Desbloquea el Item:**

```csharp
// En el grafo narrativo de Victoria
Nodo: UnlockWardrobeItemNode
  - Wardrobe Item: Cloak02
  - Show Popup: ✓
```

#### Clases Nuevas

**WardrobeItemRequirement (en QuestChainEntry.cs):**

```csharp
[Serializable]
public class WardrobeItemRequirement
{
    [Tooltip("El item de wardrobe requerido")]
    public WardrobeItemSO item;
    
    [Tooltip("ID de la condición del step de la quest. OPCIONAL - Si se deja vacío y stepIndex >= 0, se usa el índice directamente.")]
    public string stepConditionId = "";
    
    [Tooltip("Índice del step de la quest que corresponde a este item. Si es >= 0, se usa directamente sin necesidad de Condition Id.")]
    public int stepIndex = -1;
    
    /// <summary>
    /// Obtiene el stepConditionId con prioridad: stepIndex > conditionId manual > auto-generado
    /// </summary>
    public string GetStepConditionId()
    {
        // Prioridad 1: Si stepIndex es válido, retornar null para usar el índice
        if (stepIndex >= 0) return null;
        
        // Prioridad 2: Usar el valor manual si existe
        if (!string.IsNullOrEmpty(stepConditionId)) return stepConditionId;
        
        // Prioridad 3: Auto-generar basado en WardrobeId
        if (item != null && !string.IsNullOrEmpty(item.WardrobeId))
            return $"WARDROBE_{item.WardrobeId}";
        
        return "";
    }
}
```

#### Flujo Completo

```
1. Jugador habla con Victoria
   └─ QuestManager.StartQuest("victoria_get_cloak")
   └─ Quest entra en estado Active

2. Victoria ejecuta narrativa que desbloquea la capa
   └─ UnlockWardrobeItemNode ejecuta WardrobeService.UnlockWardrobeItem(Cloak02)
   └─ WardrobeInventory.Unlock() se ejecuta
   └─ Se dispara OnWardrobeChanged

3. QuestManager detecta el cambio
   └─ OnWardrobeChanged() es llamado
   └─ CheckWardrobeForQuest() verifica todas las quests activas
   └─ Encuentra que "victoria_get_cloak" requiere Cloak02
   └─ Marca step 0 como completado
   └─ OnQuestsChanged se dispara → UI se actualiza

4. Jugador vuelve a hablar con Victoria
   └─ NPCQuestConfig detecta que todos los steps están completados
   └─ Ejecuta diálogo de "Turn In"
   └─ Completa la quest
```

#### Métodos Relevantes del QuestManager

```csharp
// Suscripción al wardrobe (llamado automáticamente)
private void TrySubscribeToWardrobe()
{
    if (_isSubscribedToWardrobe) return;
    
    if (!PlayerService.TryGetComponent(out WardrobeInventory wardrobe, ...))
        return;
    
    _cachedWardrobe = wardrobe;
    _cachedWardrobe.OnWardrobeChanged += OnWardrobeChanged;
    _isSubscribedToWardrobe = true;
}

// Callback cuando cambia el wardrobe
private void OnWardrobeChanged()
{
    var activeQuests = _runtime.Values.Where(rq => rq.State == QuestState.Active).ToList();
    if (activeQuests.Count == 0) return;
    
    foreach (var rq in activeQuests)
    {
        CheckWardrobeForQuest(rq);
    }
}

// Verifica si el wardrobe cumple requisitos de una quest
private void CheckWardrobeForQuest(RuntimeQuest rq)
{
    var questEntry = FindQuestChainEntry(rq.Id);
    if (questEntry == null || questEntry.requiredWardrobeItems == null) return;
    
    foreach (var wardrobeReq in questEntry.requiredWardrobeItems)
    {
        bool hasItem = _cachedWardrobe.TryGetEntry(
            wardrobeReq.item.Category, 
            wardrobeReq.item.PartName, 
            out _
        );
        
        if (hasItem)
        {
            int stepIdx = FindStepIndex(rq, wardrobeReq.GetStepConditionId(), wardrobeReq.stepIndex);
            if (stepIdx >= 0 && !rq.Steps[stepIdx].completed)
            {
                MarkStepDone(rq.Id, stepIdx);
            }
        }
    }
}
```

#### Debugging

**Logs esperados cuando funciona correctamente:**

```
[QuestManager] ✅ Suscrito a WardrobeInventory.OnWardrobeChanged
[WardrobeService] ✅ Item 'Cloak02' desbloqueado correctamente
[QuestManager] 👗 Wardrobe cambió - Verificando quests activas...
[QuestManager] 🔍 Quest 'victoria_get_cloak' requiere 1 items de wardrobe
[QuestManager] 🎯 Verificando item de wardrobe 'Cloak02'
[QuestManager] ✅ Jugador tiene 'Cloak02' desbloqueado
[QuestManager] ✅ Usando stepIndex directo: 0
[QuestManager] 🎉 Completando step 0 de quest 'victoria_get_cloak' por item de wardrobe 'Cloak02'
```

#### Diferencias con Items Normales

| Aspecto | Items Normales (Inventory) | Items Wardrobe |
|---------|---------------------------|----------------|
| **Evento** | `Inventory.OnItemAdded` | `WardrobeInventory.OnWardrobeChanged` |
| **Configuración** | `requiredItems` | `requiredWardrobeItems` |
| **Clase** | `ItemRequirement` | `WardrobeItemRequirement` |
| **Cantidad** | Sí (`amount`) | No (binario: tiene/no tiene) |
| **Consumible** | Sí (`consumeOnComplete`) | No (nunca se consume) |

#### Mejores Prácticas

✅ **Usa stepIndex cuando sea posible** - Más simple para quests de un solo paso
✅ **Usa stepConditionId para quests complejas** - Mejor para quests con múltiples pasos
✅ **No mezcles ambos** - Si usas stepIndex, deja stepConditionId vacío
✅ **Verifica en Play Mode** - Activa los logs del QuestManager para debug

#### Troubleshooting

**Problema: El step no se completa al obtener el item del wardrobe**

1. Verifica que `requiredWardrobeItems` está configurado (no `requiredItems`)
2. Verifica que el `WardrobeItemSO` asignado es el correcto
3. Verifica que `stepIndex` apunta al step correcto (0-based)
4. Revisa los logs del `[QuestManager]` para ver si detectó el cambio

**Problema: La quest no encuentra el item**

1. Verifica que el `item.Category` y `item.PartName` son correctos
2. Abre el `WardrobeItemSO` y confirma los valores
3. Usa el método `WardrobeInventory.TryGetEntry()` en debug para verificar

---

#### Tips

## 7.5 Sistema de SpawnAnchor y Orientación

### 7.5.1 Filosofía del Sistema

> **"Coloca el anchor con el eje Z (flecha azul en Unity) apuntando donde quieres que mire el personaje. `faceDoor` solo invierte si lo marcas."**

El sistema de `SpawnAnchor` define **puntos de aparición y orientación** para jugadores y NPCs en cinemáticas y teletransporte.

---

### 7.5.2 Componente SpawnAnchor

**Ubicación:** `Assets/Scripts/World/SpawnAnchor.cs`

```csharp
public class SpawnAnchor : MonoBehaviour
{
    [Tooltip("ID único del anchor (ej: 'House_FrontDoor', 'Town_Plaza')")]
    public string anchorId;
    
    [Tooltip("Por defecto (false): El personaje mira en la dirección del eje Z del anchor (forward azul).\n" +
             "Si está marcado (true): El personaje mira en dirección OPUESTA al eje Z del anchor (-forward), es decir, da la vuelta 180°.\n\n" +
             "CONVENCIÓN DE DISEÑO:\n" +
             "- Coloca el anchor con el eje Z apuntando donde quieres que mire el jugador.\n" +
             "- Marca faceDoor=true solo si quieres invertir esa dirección.")]
    public bool faceDoor = false;
}
```

---

### 7.5.3 Convención de Diseño en Unity

#### Regla de Oro

| Configuración | Colocación del Anchor | Comportamiento |
|--------------|----------------------|----------------|
| **`faceDoor = false`** (por defecto) | Coloca la **flecha azul (eje Z)** apuntando donde quieres que mire el personaje | El personaje mira en dirección del **`forward`** del anchor |
| **`faceDoor = true`** | Coloca la **flecha azul** apuntando donde quieres que mire el personaje | El personaje mira en dirección **`-forward`** del anchor (180° invertido) |

#### Ejemplo Práctico: Puerta de Casa

**Escenario:** Tienes una casa con una puerta. Quieres dos puntos de spawn:
- Uno para **entrar** (mirar hacia adentro)
- Otro para **salir** (mirar hacia afuera)

**Configuración:**

```
1. Crea SpawnAnchor "House_FrontDoor_Inside"
   └─ Coloca la flecha azul apuntando HACIA EL INTERIOR de la casa
   └─ faceDoor = false
   └─ RESULTADO: Jugador mira HACIA ADENTRO ✅

2. Crea SpawnAnchor "House_FrontDoor_Outside"
   └─ Coloca la flecha azul apuntando HACIA AFUERA de la casa
   └─ faceDoor = false
   └─ RESULTADO: Jugador mira HACIA AFUERA ✅
```

**Alternativa con faceDoor=true:**

```
1. Crea un solo SpawnAnchor "House_FrontDoor"
   └─ Coloca la flecha azul apuntando HACIA EL INTERIOR
   └─ faceDoor = false → Jugador entra mirando hacia adentro
   └─ faceDoor = true → Jugador sale mirando hacia afuera (180° invertido)
```

---

### 7.5.4 Uso en el Código

#### TeleportService

Cuando teletransportas al jugador a un anchor:

```csharp
// Teletransporte simple
TeleportService.TeleportToAnchor(player, "House_FrontDoor");

// El sistema automáticamente:
// 1. Busca el SpawnAnchor con ID "House_FrontDoor"
// 2. Coloca al jugador en la posición del anchor
// 3. Aplica la orientación según faceDoor:
//    - faceDoor = false → rot = Quaternion.LookRotation(anchor.forward)
//    - faceDoor = true  → rot = Quaternion.LookRotation(-anchor.forward)
```

**Implementación interna:**

```csharp
// TeleportService.cs (líneas 118-138)
if (sa != null)
{
    // CONVENCIÓN: El SpawnAnchor se coloca con el eje Z (forward) apuntando
    // hacia donde quieres que mire el jugador POR DEFECTO
    if (sa.faceDoor)
    {
        // faceDoor = true → Invertir la dirección (mirar al lado contrario)
        // Usamos -forward para dar la vuelta 180°
        rot = Quaternion.LookRotation(-anchor.forward, Vector3.up);
    }
    else
    {
        // faceDoor = false (por defecto) → Usar la dirección del anchor tal cual
        // El jugador mira en la dirección del eje Z del anchor
        rot = Quaternion.LookRotation(anchor.forward, Vector3.up);
    }
}
```

#### CinematicState (NPCs en Cinemáticas)

Los NPCs también usan la misma lógica cuando se mueven a un anchor en cinemáticas:

```csharp
// CinematicState.cs - MoveToPositionSequence
// Al llegar al destino, busca un SpawnAnchor cercano
SpawnAnchor anchor = FindNearbySpawnAnchor(targetPosition);

if (anchor != null)
{
    // Aplica la misma lógica de faceDoor
    if (anchor.faceDoor)
    {
        direction = -anchor.transform.forward; // Invertir
    }
    else
    {
        direction = anchor.transform.forward; // Directo
    }
    
    context.Transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
}
```

---

### 7.5.5 Casos de Uso Comunes

#### 1. Puertas y Salidas

```
📦 House_FrontDoor (SpawnAnchor)
├─ anchorId: "House_FrontDoor"
├─ faceDoor: false
└─ Transform Forward (Z): Apuntando HACIA EL INTERIOR

💡 Uso: TeleportService.TeleportToAnchor(player, "House_FrontDoor")
✅ Resultado: Jugador aparece mirando hacia adentro (entrando)
```

#### 2. Puntos de Diálogo (NPCs)

```
📦 Elder_DialogueSpot (SpawnAnchor)
├─ anchorId: "Elder_DialogueSpot"
├─ faceDoor: false
└─ Transform Forward (Z): Apuntando HACIA EL ELDER

🎬 Cinemática:
   - NPC camina al anchor
   - Al llegar, mira hacia donde apunta la flecha (hacia Elder)
   - Inicia diálogo mirándose de frente
```

#### 3. Puntos de Spawn Iniciales

```
📦 Village_Entry (SpawnAnchor)
├─ anchorId: "Village_Entry"
├─ faceDoor: false
└─ Transform Forward (Z): Apuntando HACIA LA PLAZA DEL PUEBLO

🎮 Nueva Partida:
   - Player spawns en "Village_Entry"
   - Mira hacia la plaza (orientación natural)
   - Jugador ve el objetivo inmediatamente
```

---

### 7.5.6 Debugging y Visualización

#### Gizmos en Scene View

Puedes añadir gizmos visuales para ver la orientación del anchor en la escena:

```csharp
// SpawnAnchor.cs - Añadir método
private void OnDrawGizmos()
{
    // Dibujar flecha indicando dirección de mirada
    Gizmos.color = faceDoor ? Color.red : Color.green;
    Vector3 direction = faceDoor ? -transform.forward : transform.forward;
    Gizmos.DrawRay(transform.position, direction * 2f);
    
    // Dibujar esfera en el punto de spawn
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(transform.position, 0.5f);
}
```

**Interpretación:**
- **Flecha Verde:** `faceDoor = false` → Mira en dirección del forward
- **Flecha Roja:** `faceDoor = true` → Mira en dirección opuesta (-forward)
- **Esfera Amarilla:** Punto exacto de spawn

---

### 7.5.7 Archivos Relacionados

| Archivo | Función |
|---------|---------|
| **SpawnAnchor.cs** | Componente que define el punto de spawn y orientación |
| **TeleportService.cs** | Teletransporta al jugador aplicando orientación del anchor |
| **CinematicState.cs** | Mueve NPCs a anchors en cinemáticas con orientación |
| **NPCInteractiveNarrativeExecutor.cs** | Orienta NPCs según anchor cercano al cargar narrativa |
| **SpawnManager.cs** | Gestiona registro y búsqueda de anchors en escena |

---

### 7.5.8 Mejores Prácticas

✅ **DO:**
- Coloca el anchor con la flecha azul apuntando donde **intuyes** que debe mirar el personaje
- Usa nombres descriptivos: `House_FrontDoor_Inside`, `Elder_TalkSpot`, `BossArena_Center`
- Deja `faceDoor = false` por defecto (más intuitivo)
- Usa `faceDoor = true` solo cuando necesites invertir explícitamente

❌ **DON'T:**
- NO coloques el anchor rotado aleatoriamente esperando que `faceDoor` lo arregle
- NO uses `faceDoor` para rotaciones no relacionadas con inversión de 180°
- NO confíes en rotaciones del Transform sin mirar la flecha azul en la escena

---

### 7.5.9 Troubleshooting

#### Problema: "El jugador aparece mirando hacia el lado contrario"

**Causa:** La flecha azul del anchor apunta en dirección opuesta a la deseada.

**Solución:**
1. Selecciona el SpawnAnchor en la escena
2. Mira la **flecha azul (eje Z)** en el Gizmo
3. Rota el anchor hasta que la flecha apunte donde quieres que mire el jugador
4. Deja `faceDoor = false`

#### Problema: "Necesito que mire al revés de lo que tengo configurado"

**Solución rápida:** Marca `faceDoor = true` → Invierte 180°

**Solución limpia:** Rota el anchor 180° en el eje Y y deja `faceDoor = false`

#### Problema: "El NPC no se orienta correctamente en la cinemática"

**Causa:** No hay SpawnAnchor cerca del punto de destino.

**Solución:**
1. Crea un SpawnAnchor en el punto de destino de la cinemática
2. Orienta la flecha azul según donde debe mirar el NPC
3. El sistema lo detectará automáticamente (radio de búsqueda: 2 unidades)

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

### 8.9 Sistema de Testing con Presets

**Versión:** 2.0 (Enero 2025)

#### Filosofía

> **"En modo testing, el preset actúa COMO SI FUERA una partida cargada completa, inicializando TODOS los sistemas (quests, NPCs, blackboards, etc.) igual que LoadProfile()."**

El sistema permite crear **múltiples presets de testeo** para diferentes escenarios del juego sin necesidad de jugar hasta ese punto o modificar save files manualmente.

---

#### Diferencias entre Modos

| Aspecto | Modo Normal | Modo Testing (`usePresetInsteadOfSave = true`) |
|---------|-------------|-----------------------------------------------|
| **Fuente de datos** | JSON save file | `bootPreset` (ScriptableObject) |
| **Prioridad** | Save > Default Preset | **Boot Preset tiene PRIORIDAD ABSOLUTA** |
| **Sistemas inicializados** | Todos (desde save) | **Todos (desde preset)** ✨ NUEVO |
| **Interfiere con saves** | No | **No - Ignora saves completamente** |
| **Uso típico** | Producción | Testing/Debug |

---

#### Configuración del Modo Testing

**1. Crear Preset de Testeo:**

```
Click derecho → Create → Player/Player Preset SO
Nombre: TestPreset_VictoriaPostCloak

Configurar:
├─ spawnAnchorId: "MainWorld_Plaza"
├─ level: 5
├─ maxHP: 150, currentHP: 150
├─ maxMP: 100, currentMP: 100
├─ unlockedAbilities: [Swim, Jump, Magic]
├─ unlockedSpells: [Fireball, IceBlast]
├─ flags:
│  ├─ "QUEST_COMPLETED:victoria_get_cloak"
│  ├─ "QUEST_ACTIVE:next_main_quest"
│  └─ "BOSS_DEFEATED:forest_guardian"
├─ inventoryItems:
│  ├─ { itemId: "health_potion", count: 5 }
│  └─ { itemId: "mana_potion", count: 3 }
├─ unlockedWardrobeIds: ["Cloak02", "Hat01"]
├─ defeatedBossIds: ["forest_guardian"]
├─ completedInteractiveNarratives:
│  └─ "victoria-narrative-f5e4d3c2-unique_CN1"
└─ npcPositions:
   └─ { npcId: "Victoria", position: (10, 0, 5), isActive: true }
```

**2. Activar en GameBootProfile:**

```
GameBootProfile Asset:
├─ [Boot Settings]
│  ├─ ☑ Use Preset Instead Of Save
│  └─ Boot Preset: TestPreset_VictoriaPostCloak
```

**3. Iniciar el Juego:**

```
Play → El juego inicia con el preset como si fuera una partida cargada:
✅ Jugador aparece en "MainWorld_Plaza"
✅ Stats configurados (nivel 5, 150 HP, 100 MP)
✅ Quest "victoria_get_cloak" completada
✅ Quest "next_main_quest" activa
✅ Boss "forest_guardian" derrotado
✅ Victoria en posición específica y con narrativa ya ejecutada
✅ Items en inventario
✅ Wardrobe items desbloqueados
```

---

#### Sistemas Inicializados Automáticamente

Cuando el modo testing está activo, `GameBootService.ApplyPresetAsLoadedGame()` inicializa:

```csharp
1. SpawnManager
   └─ SetCurrentAnchor(preset.spawnAnchorId)
   └─ Jugador aparece en el punto correcto

2. BossProgressTracker
   └─ LoadFromSnapshot(preset.defeatedBossIds)
   └─ Bosses marcados como derrotados

3. QuestManager
   └─ RestoreFromProfileFlags(preset.flags)
   └─ Quests restauradas (active/completed/steps)

4. NPCs (GameBootProfile.ApplyNpcPositionsToScene)
   └─ Posiciones y estados aplicados desde preset.npcPositions

5. Sistema Narrativo
   └─ NarrativeAutoSetup.ResetForLoadedProfile()
   └─ NPCInteractiveNarrativeRegistry.Clear()
   └─ Narrativas completadas desde preset.completedInteractiveNarratives

6. Blackboards (preparado, pendiente implementación)
   └─ NarrativeGraphHub.RestoreBlackboards(preset.narrativeBlackboards)
```

---

#### Logs Esperados en Modo Testing

```
[GameBootService] ✅ Inicializado desde bootPreset (testing mode) - Aplicados todos los sistemas como si fuera una partida cargada
[GameBootService] 🎮 Aplicando preset de testeo como partida cargada...
[GameBootService]   ✅ Spawn anchor: MainWorld_Plaza
[GameBootService]   ✅ Bosses derrotados: 1
[GameBootService]   ✅ Quests restauradas desde 3 flags
[GameBootService]   ✅ Posiciones de NPCs: 1
[GameBootService]   ✅ NPCInteractiveNarrativeRegistry limpiado
[GameBootService] 🎮 Preset de testeo aplicado como partida cargada - Sistema completo inicializado
```

---

#### Ejemplos de Presets de Testing

**A. Inicio del Juego (Fresh Start):**
```
TestPreset_NewGame
├─ spawnAnchorId: "Bedroom"
├─ level: 1
├─ HP/MP: Básicos
├─ flags: []
├─ inventoryItems: []
└─ unlockedAbilities: [Jump] (solo lo básico)
```

**B. Medio Juego (Mid-Game):**
```
TestPreset_MidGame
├─ spawnAnchorId: "Town_Plaza"
├─ level: 10
├─ flags: 
│  ├─ Varias quests completadas
│  └─ 2-3 quests activas
├─ inventoryItems: Pociones, equipamiento
├─ unlockedAbilities: [Swim, Jump, Magic, Climb]
└─ defeatedBossIds: [boss_1, boss_2]
```

**C. Late Game (Casi al Final):**
```
TestPreset_LateGame
├─ spawnAnchorId: "FinalDungeon_Entrance"
├─ level: 20
├─ flags: Mayoría de quests completadas
├─ inventoryItems: Todo el equipamiento
├─ unlockedAbilities: Todas
├─ defeatedBossIds: Todos menos el final
└─ unlockedWardrobeIds: Todos los items cosméticos
```

**D. Testing Específico (ej: Bug de Victoria):**
```
TestPreset_VictoriaBugTest
├─ spawnAnchorId: "Town_VictoriaShop"
├─ flags: 
│  └─ "QUEST_ACTIVE:victoria_get_cloak"
│     (Quest activa pero sin completar)
├─ unlockedWardrobeIds: []
│  (Sin la capa todavía)
└─ completedInteractiveNarratives: []
   (Narrativa no ejecutada)
```

---

#### Ventajas del Sistema

✅ **Múltiples Escenarios:** Crea tantos presets como necesites
✅ **Reproducibilidad:** Siempre empiezas en el mismo estado
✅ **No Interfiere:** No afecta tus save files reales
✅ **Debug Rápido:** Salta directamente al escenario problemático
✅ **Sistema Completo:** TODOS los sistemas se inicializan correctamente
✅ **Fácil de Cambiar:** Solo cambia el `bootPreset` asignado

---

#### Workflow de Testing Recomendado

```
1. Detectas un bug en una situación específica
   └─ Ej: "Victoria no completa la quest de la capa"

2. Creas un preset que reproduce la situación
   ├─ TestPreset_VictoriaCloakBug
   └─ Configuras: Quest activa, sin capa, en la posición correcta

3. Activas modo testing
   ├─ usePresetInsteadOfSave = true
   └─ bootPreset = TestPreset_VictoriaCloakBug

4. Pruebas y debuggeas
   └─ El juego SIEMPRE inicia en ese estado
   └─ Puedes probar la solución múltiples veces

5. Una vez arreglado, desactivas modo testing
   └─ usePresetInsteadOfSave = false
   └─ El juego vuelve a usar saves normales
```

---

#### Troubleshooting Modo Testing

**Problema: El preset no se aplica correctamente**

1. Verifica que `usePresetInsteadOfSave` está marcado
2. Verifica que `bootPreset` tiene un preset asignado
3. Revisa los logs de `[GameBootService]` al iniciar

**Problema: Las quests no se restauran**

1. Verifica que `flags` en el preset tiene los flags correctos
2. Formato: `"QUEST_COMPLETED:quest_id"` o `"QUEST_ACTIVE:quest_id"`
3. Los flags son case-sensitive

**Problema: Los NPCs no están en la posición correcta**

1. Verifica que `npcPositions` está configurado
2. El `npcId` debe coincidir con el nombre del GameObject del NPC
3. Usa `NPCBehaviourManagerV2` en el NPC para que se aplique

**Problema: Las narrativas se ejecutan otra vez**

1. Añade el ID de la narrativa a `completedInteractiveNarratives`
2. Formato: `"npcname-narrative-uniqueid_CN1"`
3. Busca el ID en los logs cuando ejecutas la narrativa la primera vez

---

#### Debugging con GameBootProfileDebugger

```
1. Añadir GameBootProfileDebugger al GameObject con GameBootService
2. Play → Presionar F4
3. Ver:
   - ✅ Modo Testing activo
   - ✅ Preset usado: TestPreset_VictoriaPostCloak
   - Estado actual del runtimePreset
   - Comparación con sistemas vivos
   - Historial de eventos
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

### Problema: "Evento de derrota no se envía al grafo narrativo desde cadena narrativa"

**Contexto:** Cuando configuras un combate desde una `NarrativeChainEntry` con `sendEventOnDefeat = true` y `defeatEventKey`, el evento no se enviaba al derrotar al NPC.

**Causa:** Los valores de evento (`sendEventOnDefeat`, `defeatEventKey`, `sendDefeatEventBeforeDeath`) configurados en `NarrativeChainEntry` no se transferían al `NPCCombatConfig` que es lo que `NPCCombatLifecycleHandler` usa para enviar eventos.

**Solución (Fix aplicado en v1.1):**
En `NPCInteractiveNarrativeExecutor.ExecuteStartCombat()` ahora se transfieren los valores del entry al config:

```csharp
// ✅ FIX CRÍTICO: Transferir configuración de eventos de derrota
if (entry.sendEventOnDefeat && !string.IsNullOrEmpty(entry.defeatEventKey))
{
    entry.combatConfig.sendEventOnDefeat = entry.sendEventOnDefeat;
    entry.combatConfig.defeatEventKey = entry.defeatEventKey;
    entry.combatConfig.sendDefeatEventBeforeDeath = entry.sendDefeatEventBeforeDeath;
}
```

**Configuración correcta en Inspector:**
```
NarrativeChainEntry (StartCombat)
├─ Combat Config: → NPC_Combat_Config_Enemigo
├─ Send Event On Defeat: ✅
├─ Defeat Event Key: "BOSS_DEFEATED"
└─ Send Defeat Event Before Death: ☐ (opcional)
```

**Verificación:**
```
// En consola debe aparecer al configurar el combate:
[NarrativeExecutor] 📤 Configurado evento de derrota: 'BOSS_DEFEATED' (antes de muerte: False)

// Y al derrotar al NPC:
[Lifecycle] 📤 Enviando evento de derrota al grafo narrativo: 'BOSS_DEFEATED'
```

---

### Problema: "Narrativas de un solo uso no se restauran al cargar partida anterior"

**Contexto:** Cuando hablas con un NPC que tiene una narrativa condicional de un solo uso (singleUse=true), la narrativa se marca como ejecutada. Si luego cargas una partida **guardada antes** de esa interacción, la narrativa debería volver a estar disponible, pero permanecía como "ejecutada".

**Causa:** La lista `completedInteractiveNarratives` que rastrea qué narrativas se han completado **no se guardaba en el save**. Por lo tanto, al cargar un save anterior, el preset seguía teniendo la lista actualizada de la sesión actual en memoria, no la del save.

**Solución (Fix aplicado en v1.2):**

1. **Añadido campo a PlayerSaveData:**
```csharp
public List<string> completedInteractiveNarratives = new();
```

2. **Guardado al crear save:**
```csharp
d.completedInteractiveNarratives = preset.completedInteractiveNarratives != null 
    ? new List<string>(preset.completedInteractiveNarratives) 
    : new List<string>();
```

3. **Restaurado al cargar save (en SetRuntimePresetFromSave):**
```csharp
p.completedInteractiveNarratives = data.completedInteractiveNarratives != null 
    ? new List<string>(data.completedInteractiveNarratives) 
    : new List<string>();
```

4. **Mejorado NPCInteractiveNarrativeRegistry.Clear():**
   - Ya no limpia las listas de executors (que causaba que no se re-registraran)
   - Solo resetea los estados llamando a `ResetState()` en cada executor
   - Los executors restauran su estado desde el preset actualizado

**Verificación:**
```
// Al cargar partida, en consola debe aparecer:
[GameBootProfile] 📜 Restauradas X narrativas completadas desde save
[NPCInteractiveNarrativeRegistry] 🔄 Estados reseteados en Y executor(es), registro mantiene Y entradas

// Si la narrativa NO estaba completada en el save, el NPC mostrará el icono de nuevo
```

**Flujo correcto ahora:**
1. Jugador habla con Victoria → narrativa se ejecuta y guarda
2. Jugador NO guarda la partida
3. Jugador carga partida anterior (donde NO había hablado con Victoria)
4. `LoadProfile()` restaura `completedInteractiveNarratives` del save (sin la narrativa de Victoria)
5. `Clear()` resetea todos los executors y restauran desde el preset
6. Victoria muestra el icono de nuevo porque la narrativa NO está en `completedInteractiveNarratives`

---

### Problema: "Enemy marker no aparece en los enemigos"

**Contexto:** El marker de targeting del jugador (`PlayerTargeting`) no aparecía sobre los NPCs enemigos durante el combate.

**Solución implementada (v1.2):**

El sistema ahora tiene **dos modos** de funcionamiento:

#### 1. Auto-Target por Layer (Recomendado - Por defecto)
Configuración en `PlayerTargeting`:
```
autoTargetByLayer: ✅ (activo por defecto)
requireDamageableAlive: ✅
```

Con esta configuración, **cualquier objeto en el layer "Enemy"** que tenga un `Damageable` vivo será automáticamente targeteable. **No necesita componente `Targetable`**.

#### 2. Target Explícito con Targetable
Si un enemigo tiene el componente `Targetable`, se usa su configuración:
- `isInActiveCombat` debe ser `true` para ser targeteable
- `targetingRadius` permite personalizar el radio de detección

**Prioridad:**
1. Si tiene `Targetable` → usa su configuración (`isInActiveCombat` debe ser true)
2. Si NO tiene `Targetable` pero está en `enemyMask` → usa `autoTargetByLayer`

**Configuración en Inspector (PlayerTargeting):**
```
PlayerTargeting
├─ [Búsqueda]
│   ├─ radius: 8
│   ├─ scanRadius: 12
│   ├─ enemyMask: Enemy  ⭐ Importante
│   └─ fovDegrees: 140
│
├─ [Targeting Automático]
│   ├─ autoTargetByLayer: ✅   ⭐ NUEVO
│   └─ requireDamageableAlive: ✅
│
└─ [Feedback de Target]
    ├─ enableMarker: ✅
    └─ markerPrefab: → EnemyMarkerPrefab
```

**Resultado:**
- **Spider, Slime, etc.** (enemigos simples): Funcionan automáticamente por layer
- **Demon, Bosses** (enemigos con AI especial): Usan `Targetable` para control fino
- **NPCs en combate** (via CombatState): `isInActiveCombat` se activa automáticamente

---

### Problema: "Game Over con menú de botones"

**Contexto:** El Game Over mostraba un menú con botones (Cargar/Menú). Se simplificó para ser solo feedback visual.

**Nuevo comportamiento (v1.2):**
El `GameOverManager` ahora hace:
1. **Flash rojo de muerte** - Via `FeedbackService.ScreenFlash()` 🔴
2. **Slow motion progresivo** - De 1.0 a 0.1 en ~0.5s
3. **Zoom de cámara** - Reduce el FOV para acercar (via DOTween)
4. **Música de Game Over** - Reproduce el evento de audio configurado
5. **Transición automática** - Después de ~3s va al menú principal

**Usa FeedbackService para:**
- `ScreenFlash` - Flash rojo de muerte

**Configuración en Inspector:**
```
GameOverManager
├─ [Escenas]
│   └─ mainMenuScene: "MainMenu"
│
├─ [Slow Motion]
│   ├─ slowMotionScale: 0.1
│   ├─ slowMotionRampDuration: 0.5
│   └─ slowMotionHoldDuration: 2.5
│
├─ [Camera Zoom]
│   ├─ enableCameraZoom: ✅
│   ├─ zoomFactor: 0.6 (< 1 = acercar)
│   └─ zoomDuration: 1.5
│
├─ [Screen Flash]
│   ├─ enableDeathFlash: ✅
│   ├─ deathFlashColor: (0.5, 0, 0, 0.4)
│   └─ deathFlashDuration: 0.3
│
└─ [Audio]
    └─ gameOverAudioEvent: "GameOverMenu"
```

**Flujo visual:**
```
Jugador muere
   ↓
Flash rojo (FeedbackService.ScreenFlash)
Slow-mo comienza (1.0 → 0.1)
Cámara hace zoom (FOV se reduce)
Música de Game Over suena
   ↓
~3 segundos después
   ↓
Transición al menú principal
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
DialogueManager.OnDialogueClosed

// Input
GamepadInputReader.OnSubmit
GamepadInputReader.OnCancel
GamepadInputReader.OnMenuOpen
```

---

### 11.12 Sistemas Auxiliares

#### 11.12.1 Arquitectura del Sistema de Inputs

El sistema de inputs está **centralizado** para evitar código disperso y conflictos.

#### Componentes Principales

**PlayerInputManager (Cerebro Central):**
- Mantiene la instancia única de `PlayerControls`
- Gestiona el cambio entre modo **UI** y modo **Gameplay**
- Usa un contador de referencias para soportar contextos anidados
- Se registra en `ServiceLocator` para acceso global

```csharp
// Cambiar a modo UI
PlayerInputManager.Instance.PushUIMode();

// Restaurar modo Gameplay
PlayerInputManager.Instance.PopUIMode();

// Consultar modo actual
bool isInUI = PlayerInputManager.Instance.IsInUIMode;
```

**GamepadInputReader (Lector de Eventos):**
- Lee eventos de input del `PlayerControls`
- Expone eventos estáticos para que otros scripts se suscriban
- **NO gestiona el estado UI/Gameplay**

```csharp
void OnEnable()
{
    GamepadInputReader.OnInput += HandleInput;
}

void HandleInput(GamepadInputReader.InputEvent input)
{
    if (input.Type == GamepadInputReader.InputEventType.Submit)
    {
        // Hacer algo
    }
}
```

#### Reglas del Sistema de Input

**✅ HACER:**
- Usar `PlayerInputManager.PushUIMode()` al abrir cualquier menú/diálogo
- Usar `PlayerInputManager.PopUIMode()` al cerrar
- Suscribirse a `GamepadInputReader.OnInput` para leer eventos
- Usar `MenuManager.TryOpen()` antes de abrir un menú

**❌ NO HACER:**
- NO llamar directamente a `_controls.UI.Enable()` o `_controls.GamePlay.Disable()`
- NO crear instancias de `PlayerControls` manualmente
- NO gestionar el estado UI/Gameplay en scripts individuales

---

### 12.2 Sistema de Narrativa - Mejoras de Persistencia

#### Persistencia del Estado de Grafos

Los grafos narrativos ahora guardan correctamente su progreso:

- **WaitCustomEventNode**: Guarda si el evento ya fue recibido (`__event_{eventKey}_received`)
- **StartQuestNode**: Guarda si la quest ya fue iniciada (`__quest_{questId}_started`)
- Al recargar, estos nodos verifican el blackboard y avanzan automáticamente

#### NarrativeGraphValidator

Sistema de validación automática al registrar grafos:

```csharp
// Se ejecuta automáticamente al iniciar el juego
var validation = NarrativeGraphValidator.ValidateGraph(graph);
validation.LogResults("Mi Grafo");
```

**Detecta errores:**
- Falta de StartNode
- Nodos huérfanos sin conexiones
- GUIDs duplicados
- Configuraciones incorrectas

#### NarrativeGraphDebugger (F3)

Panel visual en pantalla para debugging:

- Estado en tiempo real de todos los grafos
- Información del blackboard
- Historial de nodos visitados
- Presionar **F3** para mostrar/ocultar

**Configuración:**
```csharp
public bool showDebugPanel = true;
public KeyCode toggleKey = KeyCode.F3;
public bool trackHistory = true;
public int maxHistoryEntries = 50;
```

#### Atributos de Nodos

```csharp
[SavePoint("Seguro guardar aquí")]
public sealed class WaitCustomEventNode : NarrativeNode { }

[UnsafeForSave("No guardar durante diálogo")]
public sealed class DialogueNode : NarrativeNode { }
```

---

### 12.3 GameBootProfile Debugger (F4)

Sistema de debugging visual para el sistema de partidas.

#### Características

- **Panel Visual (F4)**: Estado completo del preset y sistemas vivos
- **Historial**: Log cronológico de todas las operaciones de save/load
- **Detección de Desincronización**: Comparación visual preset vs sistemas
- **Botones de Testing**: Save/Load/Reset manual

#### Qué Muestra el Panel

```
Estado Runtime:
├── SpawnAnchor: "Bedroom"
├── Health: 45/100
├── Mana: 30/50
├── QuestFlags: 8
├── Abilities: S:True, J:True, C:False
├── Inventory: 3 items
└── Bosses derrotados: 1

Sistemas Vivos:
├── PlayerHealth: 100/100 ← ❌ Desincronizado!
├── PlayerMana: 50/50
└── Inventory: 3 items

Historial:
├── 14:35:22 ✅ Guardado exitoso (Manual)
├── 14:35:18 🔄 Runtime actualizado
└── 14:30:00 🆕 Nueva partida
```

#### Uso para Testing

```
1. F4 → Ver estado inicial
2. Hacer cambios en juego
3. "🔄 Update Runtime from State"
4. "💾 Force Save"
5. Cerrar y reiniciar
6. "📂 Load Save"
7. Comparar preset vs sistemas vivos
```

---

### 12.4 Barra de Vida del Boss (BossHealthBar)

Sistema de barra de vida para combates de boss.

#### Configuración del Prefab

```
Canvas (World Space)
└── BossHealthBar (Panel)
    ├── Background (Image)
    ├── HealthBarFill (Image - Filled, Horizontal)
    ├── BossIcon (Image - opcional)
    ├── BossNameText (TextMeshPro)
    └── HealthText (TextMeshPro)
```

#### Componente BossHealthBar

```csharp
[Header("Referencias del Boss")]
public Damageable bossDamageable;  // Auto-detecta si no asignado
public string bossName = "Boss";

[Header("UI - Barra de Vida")]
public Image healthBarFill;        // Image Type = Filled
public TextMeshProUGUI healthText; // "250/500"

[Header("Colores")]
public Color healthyColor = new Color(0.8f, 0.2f, 0.2f);
public Color warningColor = Color.yellow;   // 50% vida
public Color criticalColor = Color.red;     // 25% vida

[Header("Comportamiento")]
public bool autoShow = true;       // Mostrar al iniciar combate
public bool autoHideOnDeath = true;
public bool animateHealthChanges = true;
```

#### Características Automáticas

- ✅ Se muestra al iniciar combate
- ✅ Se actualiza en tiempo real
- ✅ Cambia de color según salud
- ✅ Se oculta cuando el boss muere
- ✅ Busca al boss automáticamente si no asignado

---

### 11.13 Problemas Conocidos y Soluciones

#### 11.13.1 Verificación de Items en Inventario al Iniciar Quests

**Problema:** Quest no detecta items que el jugador ya tiene.

**Solución:** `QuestManager.CheckExistingItemsForQuest()` verifica items al iniciar.

```csharp
// Automático - verifica steps con conditionId "ITEM_*"
foreach (var step in rq.Steps)
{
    if (step.conditionId.StartsWith("ITEM_"))
    {
        string itemId = step.conditionId.Substring(5);
        if (inventory.Count(itemId) > 0)
            step.completed = true;
    }
}
```

### 13.2 Convenciones de Código

**❌ NUNCA usar:**
```csharp
var inventory = FindObjectOfType<Inventory>();  // PROHIBIDO
```

**✅ SIEMPRE usar:**
```csharp
if (PlayerService.TryGetComponent(out Inventory inventory))
{
    // Usar inventory aquí
}

// O para managers
var dm = DialogueManager.Instance;
var qm = QuestManager.Instance;
```

### 13.3 NPCs - Layer Enemy para Targeting

**Problema:** Los hechizos del jugador no apuntan al NPC.

**Solución:** El GameObject del NPC **DEBE** estar en la Layer `Enemy`.

```
NPC GameObject:
├── Layer: Enemy ✅
├── Targetable (script)
└── NPCBehaviourManagerV2
```

### 13.4 Rotación de NPCs Durante Diálogos

**Arquitectura:**
- `DialogueManager` emite eventos: `OnDialogueStarted` y `OnDialogueClosed`
- `NPCSimpleAnimator` se suscribe a estos eventos
- El NPC controla su propia rotación internamente
- El DialogueManager NO manipula NPCs directamente

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
- ✅ Sistema de Quests: Funcional con postActions e iconos
- ✅ Sistema de Narrativa Interactiva: Refactorizado (por narrativa)
- ✅ Sistema de Localización: ES/EN completo
- ✅ Arquitectura de Escenas: START como núcleo
- ✅ Cinemáticas: Sin desactivar GameObjects
- ✅ Errores de compilación: 0
- ✅ Race conditions: Resueltas

**Sistemas Nuevos (Dic 2025):**
- ✨ Sistema de fade para cinemáticas
- ✨ NPCQuestActionExecutor con postActions
- ✨ NPCQuestIconManager para iconos de quest
- ✨ ConditionalNarrative con control individual
- ✨ Auto-generación de persistenceId

**Scripts legacy eliminados:**
- ❌ `SimpleNPCWander` (migrado a WanderState)
- ❌ `SimpleNPCCombat` (migrado a CombatState)
- ❌ `NPCAmbientBrain` (migrado a NPCConfiguration)
- ❌ `NPCBehaviourManager` (v1 → NPCBehaviourManagerV2)

---

## 📝 Historial de Cambios Mayores

### Diciembre 2025 (30 Dic) - Refactorización Sistema Narrativa + Iconos Quest

**Sistema de Narrativa Interactiva (REFACTORIZADO):**
- ✨ Eliminadas redundancias de configuración global vs por narrativa
- ✨ Cada `ConditionalNarrative` ahora controla:
  - `singleUse` - ¿Ejecutar una sola vez?
  - `autoStartOnDetection` - ¿Auto-iniciar al detectar jugador?
  - `postNarrativeState` - Estado post-narrativa individual
- ✨ Nuevo enum `PostNarrativeState.None` para no ejecutar estado post
- ✨ `persistenceId` se auto-genera basado en nombre del asset + hash único
- ✨ Detección de IDs duplicados con `Debug.LogError` detallado

**Sistema de Iconos de Quest (NUEVO):**
- ✨ Nuevo componente `NPCQuestIconManager`
- ✨ Se añade automáticamente a NPCs con `questConfig`
- ✨ Configuración en `NPCQuestConfig`:
  - `questIconPrefab` - Icono de quest disponible (!)
  - `turnInIconPrefab` - Icono de entregar quest (?)
  - `questIconOffset` - Posición del icono
  - Flags para mostrar en cada estado
- ✨ Actualización automática vía eventos del QuestManager

**Sistema de Guardado/Carga:**
- ✨ Limpieza explícita de `NPCInteractiveNarrativeRegistry` al cargar/nueva partida
- ✨ Los executors restauran su estado correctamente desde el preset

**Bug Fixes:**
- ✅ Corregido: IDs de persistencia duplicados entre NPCs (Victoria/Erika/Oliver)
- ✅ Corregido: Estado de narrativa de un NPC afectaba a otro
- ✅ Corregido: PostNarrativeState se ejecutaba después de cada narrativa

**Documentación:**
- 📚 Consolidados todos los archivos MD en `docs/DOCUMENTACION_TECNICA.md`
- 📚 Eliminados 11 archivos MD redundantes
- 📚 README.md simplificado con referencia a documentación principal

---

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

## 12. Sistema de Puzzles

### 12.1 Burnable - Objetos Quemables

**Script:** `Assets/Scripts/Puzzle/Burnable.cs`

Componente para objetos que pueden ser destruidos con elementos mágicos (fuego, hielo, etc.).

#### Características

- ✅ Detecta proyectiles mágicos por elemento
- ✅ Destruye hijos con mesh (útil para enredaderas)
- ✅ Feedback: VFX, SFX, animación
- ✅ Sistema de cadena (opcional para bombas)

#### Configuración

```csharp
[Header("Configuración")]
public MagicElement[] acceptedElements = { MagicElement.Fire };
public bool destroyOnlyChildrenWithMesh = true; // Para enredaderas
public bool destroyImmediately = false;
public float destroyDelay = 2f;

[Header("Objetos en Cadena (opcional)")]
public Burnable[] objectsToBurn;
public float chainBurnDelay = 0.3f;

[Header("Efectos")]
public GameObject burnVFX;
public float vfxLifetime = 3f;
public string burnSfxKey = "Object_Burn";
```

#### Método Principal

```csharp
public void OnHitByMagic(MagicElement element, Vector3 hitPoint)
{
    // Verifica si el elemento es aceptado
    // Reproduce VFX y SFX
    // Reproduce animación si existe
    // Desactiva collider
    // Destruye hijos con mesh O destruye objeto completo
    // Activa cadena de objetos (opcional)
}
```

#### Ejemplo de Uso: Enredadera

```
Jerarquía:
VineBlocker (GameObject padre)
├─ Collider (para detectar impactos)
├─ Burnable (script)
│  └─ destroyOnlyChildrenWithMesh: ✅
└─ Hijos:
   ├─ Vine_Mesh_01 ✅ (se destruye)
   ├─ Vine_Mesh_02 ✅ (se destruye)
   └─ Vine_Mesh_03 ✅ (se destruye)

Resultado:
- Al impactar con fuego → Destruye meshes
- Collider se desactiva → Jugador puede pasar
- GameObject padre permanece
```

---

### 12.2 PressurePlate - Interruptor de Presión

**Scripts:** `Assets/Scripts/Puzzle/PressurePlate.cs` + `PlatformElevator.cs`

Sistema completo de interruptores que se activan al colocar objetos encima.

#### Características de PressurePlate

- ✅ Detección de objetos con `PickupObject` y `Rigidbody`
- 🔽 Animación de hundimiento de la placa
- 📹 Feedback de cámara (shake) y sonido usando `FeedbackService`
- ⬆️ Elevar plataformas
- ⬇️ Hundir plataformas
- 🔥 Desactivar GameObjects con VFX
- ✨ Instanciar recompensas o enemigos

#### Configuración de PressurePlate

```csharp
[Header("Configuración del Interruptor")]
public bool onlyPickupObjects = true;
public bool lockWhenActivated = false;
public float minimumMass = 0.1f;

[Header("Feedback Visual")]
public float sinkAmount = 0.2f;
public float animationSpeed = 5f;
public Transform plateVisual;

[Header("Feedback de Cámara y Audio")]
public float cameraShakeIntensity = 0.3f;
public float cameraShakeDuration = 0.2f;
public string activateSfxKey = "PressurePlate_Activate";
public string deactivateSfxKey = "PressurePlate_Deactivate";

[Header("Acciones")]
public PlatformElevator[] platformsToRaise;
public PlatformElevator[] platformsToLower;
public GameObject[] objectsToDeactivate;
public GameObject deactivateVFX;
public GameObject[] prefabsToSpawn;
public Transform[] spawnPoints;
```

#### Métodos Principales

```csharp
private void Activate()
{
    // Hunde la placa visualmente
    // FeedbackService.CameraShake()
    // AudioService.PlaySFX()
    // Ejecuta todas las acciones configuradas
}

private void Deactivate()
{
    // Revierte el estado (si no está bloqueado)
}

public void ForceActivate() // API pública
public void ForceDeactivate() // API pública
```

#### PlatformElevator

Componente para plataformas móviles:

```csharp
[Header("Configuración de Movimiento")]
public float raiseHeight = 3f;
public float moveSpeed = 2f;
public AnimationCurve movementCurve;

[Header("Encadenamiento")]
public float delayBeforeMoving = 0f;
public PlatformElevator[] chainedPlatforms;

[Header("Feedback")]
public string movementStartSfxKey = "Platform_Move_Start";
public string movementStopSfxKey = "Platform_Move_Stop";
public GameObject movementVFX;
public Transform vfxSpawnPoint;
```

#### Métodos de PlatformElevator

```csharp
public void Raise() // Eleva la plataforma
public void Lower() // Hunde la plataforma
public void TeleportToRaised() // Sin animación
public void TeleportToLowered() // Sin animación
public void SetRaiseHeight(float height) // Cambiar en runtime

// Propiedades públicas
public bool IsMoving { get; }
public bool IsRaised { get; }
```

#### Flujo de Trabajo

```
1. Crear PressurePlate:
   ├─ GameObject con Collider (trigger)
   ├─ Script PressurePlate
   └─ Hijo con mesh visual (plateVisual)

2. Crear Plataformas:
   ├─ GameObject con PlatformElevator
   └─ Configurar raiseHeight y moveSpeed

3. Conectar:
   ├─ Arrastra plataformas a platformsToRaise/Lower
   ├─ Configura feedback (shake, SFX)
   └─ Opcionalmente: objectsToDeactivate, prefabsToSpawn

4. Probar:
   ├─ Coloca objeto con PickupObject + Rigidbody
   └─ El interruptor se activa automáticamente
```

#### Gizmos en el Editor

Ambos scripts dibujan Gizmos para visualizar conexiones:

**PressurePlate:**
- 🟡/🟢 Área de detección (color según estado)
- 🟢 Líneas verdes → plataformas que suben
- 🔴 Líneas rojas → plataformas que bajan
- 🟣 Líneas magenta → objetos a desactivar
- 🔵 Líneas cyan → spawn points

**PlatformElevator:**
- 🟡 Cubo amarillo = posición inicial
- 🟢 Cubo verde = posición elevada
- 🔵 Flecha = dirección y distancia
- 🟣 Líneas = plataformas encadenadas

#### Ejemplo Completo

```
Setup de Puzzle "Puente sobre Lava":

1. PressurePlate_RockButton
   ├─ onlyPickupObjects: ✅
   ├─ lockWhenActivated: ✅ (permanente)
   ├─ platformsToRaise: [Bridge_Platform]
   ├─ objectsToDeactivate: [MagicWall]
   └─ deactivateVFX: VFX_Explosion

2. Bridge_Platform (PlatformElevator)
   ├─ raiseHeight: 5
   ├─ moveSpeed: 2
   └─ chainedPlatforms: [Platform_Step2, Platform_Step3]

3. Rock_Pickup
   ├─ PickupObject (script)
   ├─ Rigidbody (mass: 10)
   └─ Interactable (para coger)

Resultado:
- Jugador coge roca
- Suelta roca en interruptor
- 🔽 Placa se hunde + camera shake
- ⬆️ Puente sube en cascada (3 plataformas)
- 🔥 Muro mágico desaparece con VFX
- ✅ Jugador cruza el puente
```

---

## 13. Sistema de Iconos en Diálogos

### 13.1 Configuración de Sprites en TextMeshPro

Para usar iconos (botones, items, etc.) en los textos de diálogo.

#### Paso 1: Crear Sprite Asset

1. **Window → TextMeshPro → Sprite Importer**
2. **Source:** Arrastra todos tus sprites de iconos
3. **Sprite Data Source:** Selecciona `Sprite Asset`
4. **Asignar nombres descriptivos:**
   - Botones: `ButtonA`, `ButtonB`, `ButtonX`, `ButtonY`
   - D-Pad: `DpadUp`, `DpadDown`, `DpadLeft`, `DpadRight`
   - Sticks: `LeftStick`, `RightStick`
   - Items: `Heart`, `Star`, `Coin`, `Key`, `Sword`, `Shield`
   - UI: `Potion`, `Chest`, `Lock`

5. **Save Sprite Asset** en:
   ```
   Assets/TextMesh Pro/Resources/Sprite Assets/DialogueIcons.asset
   ```

#### Paso 2: Configurar en DialogueManager

**Herramienta:** `Tools → Dialogue → Setup Icons`

La herramienta configura automáticamente:
- Asigna el Sprite Asset al DialogueManager
- Configura el componente TextMeshProUGUI del diálogo
- Valida la configuración

#### Paso 3: Usar en Diálogos

**Sintaxis:**
```
<sprite name="NombreDelSprite">
```

**Ejemplos:**
```
Texto del diálogo:
Pulsa <sprite name="ButtonA"> para saltar
Usa <sprite name="LeftStick"> para moverte
Abre el inventario con <sprite name="DpadDown">
Tu vida <sprite name="Heart"> está baja (30/100)
Has conseguido <sprite name="Coin"> x5
```

**Opciones avanzadas:**
```
Cambiar tamaño: <sprite name="ButtonA" size=150%>
Cambiar color: <sprite name="Heart" color=#FF0000>
Usar índice: <sprite=0>
```

### 13.2 Troubleshooting

**Problema:** No se ve el icono (cuadrado vacío)
- Verifica que el nombre coincide exactamente (case-sensitive)
- Reconfigura con `Tools → Dialogue → Setup Icons`
- Comprueba que el Sprite Asset está en la ruta correcta

**Problema:** Icono muy pequeño
- Usa `<sprite name="ButtonA" size=150%>`

**Problema:** Añadir más iconos después
1. `Window → TextMeshPro → Sprite Importer`
2. `Load Sprite Asset` (cargar el existente)
3. Añadir nuevos sprites
4. `Update Sprite Asset`

---

## 14. Sistema de Iluminación (Bake Nocturno)

### 14.1 Configuración Optimizada de Lightmaps

**Archivo:** `Assets/MainWorldLightSettings.lighting`

Para generar lightmaps de calidad optimizada en 2-4 horas.

#### Valores Configurados

```
Configuración Actual (Optimizada):
├─ Lightmap Max Size: 2048
├─ Direct Samples: 64 (suficiente precisión)
├─ Indirect Samples: 512 (buena calidad GI)
├─ Environment Samples: 256 (suficiente skylight)
├─ Bake Resolution: 25 (balance detalle/tiempo)
├─ Bounces: 2 (balance perfecto)
├─ Light Probe Multiplier: 4
├─ Compression: Normal (compresión razonable)
├─ Filtering Mode: Auto (automático optimizado)
├─ Denoiser: OpenImageDenoise (balance calidad/tiempo)
├─ AO: ON (Ambient Occlusion activo)
└─ Gauss Radius Indirect: 2 (suavizado optimizado)
```

#### Cómo Iniciar el Bake

1. Abre **MainWorld.unity**
2. **Window → Rendering → Lighting**
3. Verifica que **Lighting Settings** esté en "MainWorldLightSettings"
4. Click en **"Generate Lighting"** (abajo a la derecha)
5. Unity comenzará el bake automáticamente

---

## 16. Changelog - Actualizaciones del Sistema

### Versión 1.1 - Enero 2025

#### 🎯 Sistema de Quests - Detección de Items del Wardrobe

**Problema Resuelto:** Las quests no detectaban cuando el jugador obtenía items del wardrobe (capas, ropa, accesorios), causando que los steps no se completaran automáticamente.

**Cambios Implementados:**

1. **QuestManager.cs**
   - ✅ Añadida suscripción a `WardrobeInventory.OnWardrobeChanged`
   - ✅ Nuevo método `TrySubscribeToWardrobe()` - Suscripción automática al wardrobe
   - ✅ Nuevo método `OnWardrobeChanged()` - Callback cuando cambia el wardrobe
   - ✅ Nuevo método `CheckWardrobeForQuest()` - Verifica items del wardrobe vs requisitos
   - ✅ Actualizado `CheckExistingItemsForQuest()` - Verifica inventario + wardrobe
   - ✅ Métodos helper refactorizados: `CheckInventoryItemsForQuest()`, `CheckWardrobeItemsForQuest()`, `FindStepIndex()`

2. **QuestChainEntry.cs**
   - ✅ Nuevo campo `requiredWardrobeItems` (array de `WardrobeItemRequirement`)
   - ✅ Nueva clase `WardrobeItemRequirement`:
     - Campo `item` (WardrobeItemSO) - Item del wardrobe requerido
     - Campo `stepConditionId` (string, opcional) - ID de condición
     - Campo `stepIndex` (int, opcional) - Índice directo del step
     - Método `GetStepConditionId()` - Prioridad: stepIndex > conditionId > auto-generado

**Uso:**
```csharp
// En el NPC que da la quest
NPCBehaviourManagerV2 → Configuration → Quest Config → Quest Chain:
[0] Required Wardrobe Items:
    [0] Item: Cloak02 (WardrobeItemSO)
        Step Index: 0
```

**Documentación:** Ver sección [7.1.1 Sistema de Detección de Items del Wardrobe en Quests](#711-sistema-de-detección-de-items-del-wardrobe-en-quests)

---

#### 🎮 GameBootService - Modo Testing con Presets (v2.0)

**Problema Resuelto:** El modo testing (`usePresetInsteadOfSave = true`) solo inicializaba el `runtimePreset` pero NO aplicaba todos los sistemas (quests, NPCs, blackboards, etc.) como lo haría una carga de partida real.

**Cambios Implementados:**

1. **GameBootService.cs**
   - ✅ Nuevo método `ApplyPresetAsLoadedGame()` - Aplica preset como partida cargada completa
   - ✅ Actualizado `PrepareActivePreset()` - Llama a `ApplyPresetAsLoadedGame()` en modo testing
   - ✅ Inicializa TODOS los sistemas:
     - SpawnManager → Anchor de spawn
     - BossProgressTracker → Bosses derrotados
     - QuestManager → Estado completo de quests
     - NPCs → Posiciones y estados
     - NarrativeSystem → Reseteo y registro limpio
     - Blackboards → Preparación (pendiente implementación)

2. **GameBootProfile.cs**
   - ✅ Método `ApplyNpcPositionsToScene()` ahora es público (antes era privado)
   - ✅ Permite que `GameBootService` aplique posiciones de NPCs desde presets

**Ventajas:**
- ✅ Múltiples presets para diferentes escenarios de prueba
- ✅ No interfiere con save files reales
- ✅ Reproducibilidad perfecta - siempre el mismo estado
- ✅ Debug rápido - salta al escenario problemático
- ✅ Sistema completo - TODOS los sistemas inicializados correctamente

**Uso:**
```csharp
// Crear preset de testeo (PlayerPresetSO)
TestPreset_VictoriaPostCloak:
├─ spawnAnchorId: "MainWorld_Plaza"
├─ flags: ["QUEST_COMPLETED:victoria_get_cloak"]
├─ unlockedWardrobeIds: ["Cloak02"]
├─ completedInteractiveNarratives: ["victoria-narrative-f5e4d3c2-unique_CN1"]
└─ npcPositions: [...]

// Activar en GameBootProfile
GameBootProfile:
├─ usePresetInsteadOfSave: ✓
└─ bootPreset: TestPreset_VictoriaPostCloak
```

**Logs Esperados:**
```
[GameBootService] ✅ Inicializado desde bootPreset (testing mode)
[GameBootService] 🎮 Aplicando preset de testeo como partida cargada...
[GameBootService]   ✅ Spawn anchor: MainWorld_Plaza
[GameBootService]   ✅ Bosses derrotados: 2
[GameBootService]   ✅ Quests restauradas desde 15 flags
[GameBootService]   ✅ Posiciones de NPCs: 5
[GameBootService] 🎮 Preset de testeo aplicado - Sistema completo inicializado
```

**Documentación:** Ver sección [8.9 Sistema de Testing con Presets](#89-sistema-de-testing-con-presets)

---

#### ⏱️ NPCInteractiveNarrativeExecutor - Cooldown Post-Narrativa (v1.1)

**Problema Resuelto:** El jugador podía interactuar con un NPC inmediatamente después de terminar una narrativa, causando que se activara el diálogo por defecto dos veces seguidas.

**Cambios Implementados:**

1. **NPCInteractiveNarrativeExecutor.cs**
   - ✅ Nuevo campo `_lastExecutionEndTime` - Timestamp de finalización
   - ✅ Nueva constante `POST_EXECUTION_COOLDOWN = 0.5f` - Duración del cooldown
   - ✅ Actualizada propiedad `IsExecuting` - Incluye el cooldown en la verificación
   - ✅ Actualizado `ExecuteNarrativeChain()` - Establece timestamp al finalizar

**Comportamiento:**
- Durante 0.5 segundos después de terminar una narrativa, `IsExecuting` devuelve `true`
- `Interactable.CanInteract()` verifica `IsExecuting`, bloqueando la interacción
- El cooldown es imperceptible para el jugador pero previene inputs dobles

**Log:**
```
[NarrativeExecutor:Victoria] ⏱️ Narrativa finalizada - Cooldown activo hasta 123.45s
```

**Documentación:** Ver sección actualizada en [3.6 Sistema de Narrativa Interactiva](#36-sistema-de-narrativa-interactiva)

---

#### 🛠️ PlayerPresetSOEditor - Herramienta de Testing (v1.0)

**Problema Resuelto:** Era muy difícil crear presets de testeo con el estado correcto de quests, ya que requería conocer el formato exacto de los flags y era propenso a errores.

**Solución Implementada:**

1. **PlayerPresetSOEditor.cs** (NUEVO)
   - ✅ Editor personalizado para `PlayerPresetSO`
   - ✅ Botón "📸 Capturar Estado Actual del Juego"
     - Captura TODO el estado en Play Mode
     - Incluye quests, inventario, wardrobe, bosses, narrativas
     - 100% preciso - estado real del juego
   - ✅ Panel "📋 Ayudante de Quests"
     - Muestra formato correcto de cada tipo de flag
     - Ejemplos copiables
   - ✅ Panel "🔍 Análisis de Flags"
     - Analiza flags del preset actual
     - Muestra iconos visuales por tipo
     - Separa flags de quests de otros
   - ✅ Botones "⚡ Ejemplos Rápidos"
     - Añade templates de quests activas/completadas

**Uso:**
```csharp
// En Unity
1. Crea un PlayerPresetSO
2. Entra en Play Mode y avanza hasta el estado deseado
3. Pausa el juego
4. Selecciona el preset en el Inspector
5. Click "📸 Capturar Estado Actual del Juego"
6. ¡Listo! Todo el estado capturado automáticamente
```

**Ventajas:**
- ✅ No necesitas conocer formatos de flags
- ✅ Captura TODO automáticamente (quests, inventario, etc.)
- ✅ 100% preciso
- ✅ Ahorra 10-20 minutos por preset
- ✅ Reduce errores a cero

**Logs:**
```
[PlayerPresetSOEditor] ✅ Estado capturado en preset 'TestPreset_Victoria':
  ├─ Spawn Anchor: MainWorld_Plaza
  ├─ HP: 150/150
  ├─ MP: 100/100
  ├─ Flags: 15
  ├─ Inventory Items: 8
  ├─ Wardrobe Items: 12
  ├─ Defeated Bosses: 3
  └─ Completed Narratives: 5
```

**Documentación:** Ver sección [8.9 Sistema de Testing con Presets - Herramienta de Editor](#89-sistema-de-testing-con-presets)

---

#### 📊 Sistema de Quests en Presets - Aclaración

**Aclaración Importante:** El sistema de quests SÍ se guarda en los presets mediante los flags. El problema anterior era que:

1. Era difícil crear presets manualmente con el formato correcto de flags
2. No había forma fácil de capturar el estado actual de las quests

**Solución:**
- ✅ Herramienta `PlayerPresetSOEditor` captura automáticamente todos los flags de quests
- ✅ Panel de ayuda muestra el formato correcto si se quiere hacer manual
- ✅ Análisis visual de flags para verificar que el preset está correcto

**Formato de Flags (Documentado):**
```
Quest Activa:
• QUEST_ACTIVE:quest_id

Quest Completada:
• QUEST_COMPLETED:quest_id

Step Completado:
• QUEST_STEP_DONE:quest_id:0

Quest Archivada:
• QUEST_ARCHIVED:quest_id

Quest Tracked:
• QUEST_FOLLOWED:quest_id
```

---

### Resumen de Archivos Modificados

| Archivo | Cambios | Versión |
|---------|---------|---------|
| `QuestManager.cs` | Sistema de detección de wardrobe | 1.1 |
| `QuestChainEntry.cs` | Clase `WardrobeItemRequirement` | 1.1 |
| `GameBootService.cs` | Método `ApplyPresetAsLoadedGame()` | 2.0 |
| `GameBootProfile.cs` | `ApplyNpcPositionsToScene()` público | 2.0 |
| `NPCInteractiveNarrativeExecutor.cs` | Cooldown post-ejecución | 1.1 |
| `PlayerPresetSOEditor.cs` | **Herramienta de editor para presets** | **1.0 (NUEVO)** ✨ |

---

### Testing y Validación

**Tests Realizados:**
- ✅ Quest con item de wardrobe se completa automáticamente
- ✅ Preset de testeo inicializa todos los sistemas correctamente
- ✅ NPCs no permiten doble interacción después de narrativas
- ✅ Múltiples presets de testeo funcionan sin interferir con saves

**Logs de Debug Añadidos:**
- `[QuestManager]` - Detección de items del wardrobe
- `[GameBootService]` - Aplicación de presets como partida cargada
- `[NarrativeExecutor]` - Cooldown post-narrativa

---

### Notas de Migración

**Para proyectos existentes:**

1. **Quests con items de wardrobe:**
   - Actualizar `QuestChainEntry` con `requiredWardrobeItems` en lugar de `requiredItems`
   - El sistema detectará automáticamente cuando el jugador obtiene los items

2. **Testing con presets:**
   - Los presets antiguos siguen funcionando
   - Para usar la nueva funcionalidad completa, configurar todos los campos del preset
   - Ver ejemplos en la documentación sección 8.9

3. **NPCs con narrativas:**
   - No requiere cambios
   - El cooldown se aplica automáticamente

---

**Fecha de Actualización:** 9 de Enero de 2025  
**Autor:** Sistema de Documentación Técnica  
**Revisión:** 1.1

**Opción alternativa:** Activa **Auto Generate** para baking automático

#### Tiempo Estimado

- **Escena pequeña:** 30 min - 1 hora
- **MainWorld (grande):** 2-4 horas ⭐ (perfecto para noche)
- **Muy compleja:** 4-6 horas

#### Configuración del PC (Checklist Pre-Bake)

Antes de iniciar el bake nocturno:

- [ ] ✅ Guarda todo (Ctrl+S)
- [ ] ✅ Cierra otras aplicaciones pesadas
- [ ] ✅ Desactiva ahorro de energía:
  - Panel de Control → Energía → Alto rendimiento
  - Suspender: Nunca
  - Apagar pantalla: Nunca
- [ ] ✅ Pausa actualizaciones de Windows (7 días)
- [ ] ✅ Conecta el PC a corriente (no batería)
- [ ] ✅ Opcional: Cierra Discord, Chrome, etc.

#### Monitoreo del Progreso

**En Unity:**
- Barra de progreso abajo a la derecha
- Indica "Baking..." con porcentaje
- Puedes minimizar Unity - seguirá trabajando

**Cancelar:**
- Barra de progreso tiene botón de cancelación

#### Calidad Resultante

Con esta configuración optimizada obtendrás:

✓ Sombras suaves y detalladas  
✓ Global Illumination realista  
✓ Ambient Occlusion preciso  
✓ Sin artifacts ni noise (gracias al denoiser)  
✓ Rebotes de luz naturales (2 bounces)  
✓ Light probes de calidad  
✓ Compresión razonable (ahorra espacio)  

**Perfecto para:** Builds de producción, trailers, demos

#### Después del Bake

Al terminar (mañana):
1. Unity habrá terminado (desaparece barra de progreso)
2. Verás los nuevos lightmaps aplicados en la escena
3. **Guarda la escena** (Ctrl+S)
4. **Guarda el proyecto** (Ctrl+Shift+S)
5. Los lightmaps estarán en: `Assets/Scenes/Main World/MainWorld/`
6. Commit a Git si quieres guardar el resultado

---

## 15. Troubleshooting Adicional

### 15.1 Errores del AI Toolkit (IGNORAR)

Los errores que aparecen como:
```
ArgumentException: Requested value 'Textures' was not found
Error converting value "Textures" to type 'SuperProxyClientV1Namespace.CategoryEnumV1'
```

Son del **Unity AI Toolkit** (paquete de Unity) y son **completamente inofensivos**:

- ❌ NO afectan al juego
- ❌ NO causan crashes
- ❌ NO afectan MainWorld ni ninguna escena
- ❌ Solo ensucian la consola

**Soluciones:**

**Opción 1: Ignorarlos (Recomendado)**
- Simplemente ignóralos, no hacen nada malo

**Opción 2: Filtrar la consola**
- Click en el menú de hamburguesa (≡) en la consola
- Usa la barra de búsqueda para filtrar

**Opción 3: Desactivar AI Toolkit**
- Si no usas las funciones de IA de Unity:
  1. **Window → Package Manager**
  2. Busca: **AI Toolkit**
  3. Click en **Remove**

### 15.2 Problemas con PressurePlate

**Interruptor no detecta objetos:**
- ✅ Verifica que el Collider sea **Trigger**
- ✅ El objeto debe tener **Rigidbody**
- ✅ Si `onlyPickupObjects = true`, debe tener componente `PickupObject`
- ✅ Verifica que la masa sea mayor que `minimumMass`

**La placa no se hunde visualmente:**
- ✅ Arrastra el mesh hijo a `plateVisual`
- ✅ Verifica que `sinkAmount > 0`
- ✅ Verifica que `animationSpeed > 0`

**Las plataformas no se mueven:**
- ✅ Verifica que `raiseHeight > 0`
- ✅ Verifica que `moveSpeed > 0`
- ✅ Asegúrate de arrastrar las plataformas correctas al array

**Camera shake no funciona:**
- ✅ Verifica que `FeedbackService` esté inicializado en Start.unity
- ✅ Verifica que haya una Main Camera en la escena
- ✅ Ajusta `cameraShakeIntensity` y `cameraShakeDuration`

### 15.3 Problemas con Burnable

**No se destruyen los meshes:**
- ✅ Verifica que los hijos tengan `MeshRenderer` o `MeshFilter`
- ✅ `destroyOnlyChildrenWithMesh` debe estar en `true`
- ✅ El collider del padre debe detectar el impacto

**No detecta proyectiles mágicos:**
- ✅ El proyectil debe llamar a `OnHitByMagic(element, hitPoint)`
- ✅ Verifica que el `MagicElement` esté en `acceptedElements[]`

### 15.4 Problemas con Iconos en Diálogos

**Iconos no aparecen (cuadrado vacío):**
1. Verifica que el nombre coincide exactamente (case-sensitive)
2. `Tools → Dialogue → Setup Icons` y reconfigura
3. Comprueba que el Sprite Asset está en la ruta correcta:
   ```
   Assets/TextMesh Pro/Resources/Sprite Assets/DialogueIcons.asset
   ```

**Icono está muy pequeño:**
- Usa `<sprite name="ButtonA" size=150%>`

**Quiero añadir más iconos después:**
1. `Window → TextMeshPro → Sprite Importer`
2. `Load Sprite Asset` (cargar el existente)
3. Añade los nuevos sprites
4. `Update Sprite Asset`

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

