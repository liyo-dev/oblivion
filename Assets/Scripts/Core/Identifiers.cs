using System;

/// <summary>
/// Conjunto centralizado de identificadores y enumerados usados por el juego.
/// Mantener todos los enums en este archivo facilita su localización y evita
/// duplicados en el proyecto. No se introduce un namespace para mantener
/// compatibilidad con referencias existentes en el código base.
/// </summary>

/// Combat / Abilities
/// <summary>Identificadores de habilidades y ataques disponibles.</summary>
public enum AbilityId
{
    PhysicalAttack,
    MagicAttack,
    Dash,
    Block
}

/// <summary>Tipos de daño que pueden aplicarse a entidades.</summary>
public enum DamageKind
{
    Physical,
    Magic,
    Special
}

/// <summary>Posturas/estilos de manejo de armas.</summary>
public enum WeaponStance
{
    None = 0,
    SingleSword = 1,
    SwordAndShield = 2,
    TwoHandSword = 3,
    BowAndArrow = 4,
    Spear = 5,
    MagicWand = 6,
    DoubleSword = 7
}

// Magic / Spells
/// <summary>Identificadores de hechizos y habilidades mágicas.</summary>
public enum SpellId
{
    None,
    Fireball,
    Plasmaball,
    CorazonEstelar,
    Levitation,
    AuraEstelar,
    Cycloneburst,
    // NUEVO (23/08/2026): hechizo de Liam — ver Assets/_SPELLS/GarraDelPacto.asset,
    // creado por el menú "El Sendero/Magia/Crear Hechizo de Liam (Garra del Pacto)".
    GarraDelPacto,
    // NUEVO (23/08/2026): hechizo de retaguardia creado por Raúl — ver Assets/_SPELLS/Huracan.asset.
    // Antes de este ID, Huracan.asset tenía spellId:6 (Cycloneburst) duplicado con Tornado.asset.
    Huracan,
    // NUEVO (30/08/2026): hechizo de zona de Liam — ver Assets/_SPELLS/SelloDelPacto.asset,
    // creado por el menú "El Sendero/Magia/Crear Hechizo de Liam (Sello del Pacto)". Sale de la
    // mano (mismo VFX/timing de casteo que un hechizo Projectile normal) pero, en vez de volar,
    // se materializa al instante como una zona fija en el suelo — ver MagicKind.Zone.
    SelloDelPacto,
    // NUEVO (30/08/2026): hechizos del Mago Oscuro para la Batalla Final — ver
    // Assets/_SPELLS/MagoOscuroGolpe.asset y MagoOscuroGrieta.asset, creados por el menu
    // "El Sendero/Magia/Crear Hechizos del Mago Oscuro (Batalla Final)".
    MagoOscuroGolpe,
    MagoOscuroGrieta
}

/// <summary>Ranuras de magia (mano izquierda, derecha, o especial).</summary>
public enum MagicSlot
{
    Left,
    Right,
    Special
}

/// <summary>Tipos de comportamiento de un hechizo.</summary>
public enum MagicKind
{
    Projectile,
    Special,
    Levitation,
    // NUEVO (30/08/2026): el hechizo sale de la mano igual que un Projectile (mismo spawnVFX y
    // castDelaySeconds, para aprovechar la animación de casteo existente), pero en vez de
    // instanciar algo que viaja, se materializa al instante como una zona fija que aplica daño
    // periódico a quien esté dentro mientras dura. Ver MagicZoneEffect.cs y
    // MagicProjectileSpawner.SpawnZoneNow().
    Zone
}

/// <summary>Elementos mágicos disponibles.</summary>
public enum MagicElement
{
    Fire,
    Ice,
    Storm,
    Light,
    Mind,
    // NUEVO (30/08/2026): magia de corrupcion del Mago Oscuro / el Sendero corrompido —
    // ver guion-tecnico-batalla-final-2026-08-30.md. Antes de este cambio no habia un
    // elemento propio para la magia oscura del jefe final; reutilizar Mind o Storm habria
    // sido enganoso (esos elementos ya representan a Liam/Estela).
    Dark
}

/// <summary>Tipos de ranuras para asignar hechizos.</summary>
public enum SpellSlotType
{
    Any,
    SpecialOnly
}

// Player / Actions
/// <summary>Modos de acción globales del sistema de personajes.</summary>
public enum ActionMode
{
    Default,
    Carrying,
    Casting,
    Cinematic,
    Inventory,
    Stunned,
    Swimming,
    Flying,
    Climbing,
    Combat,
    Minigame,
    UsingWorldPoint
}

/// <summary>Habilidades básicas que puede usar el jugador.</summary>
public enum PlayerAbility
{
    Move,
    Jump,
    Sprint,
    Roll,
    Attack,
    Magic,
    Interact,
    Carry,
    Aim,
    Fly,
    Climb
}

// Interactable / Session
/// <summary>Modos de interacción disponibles para objetos interactuables.</summary>
public enum InteractableMode
{
    OpenDialogue,
    OpenDialogueWithOptions,
    HandOffToTarget,
    UseWorldPoint,
    // FIX INC-048: confirmación Sí/No usando el popup unificado (ConfirmationPopupUI) en lugar
    // del cuadro de diálogo con opciones. Pensado para casos simples como confirmar el guardado
    // en un SavePoint, sin diálogo de seguimiento narrativo.
    ConfirmationPopup
}

/// <summary>Cómo seleccionar una sesión: por campo, automáticamente por GameObject, o por nombre de tipo.</summary>
public enum SessionSelect
{
    UseField,
    AutoFirstOnThisGameObject,
    ByTypeName
}

// Quests / Objectives
/// <summary>Estado de una misión.</summary>
public enum QuestState
{
    Inactive = 0,
    Active = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>Obsoleto: use <see cref="QuestState"/> en su lugar.</summary>
[Obsolete("Use QuestState instead")]
public enum QuestStateEnum
{
    NotStarted = 0,
    Active = 1,
    Completed = 2
}

/// <summary>Modo de finalización de una quest.</summary>
public enum QuestCompletionMode
{
    Manual,
    AutoCompleteOnTalk,
    CompleteOnTalkIfStepsReady,
    AutoCompleteWhenStepsReady
}

/// <summary>Modo de objetivo (como encontrar el primero, uno específico o por nombre).</summary>
public enum TargetMode
{
    FirstFound,
    Specific,
    ByName
}

// Rooms / Encounters
/// <summary>Dificultad de una sala/encuentro.</summary>
public enum RoomDifficulty
{
    Easy,
    Medium,
    Hard,
    Boss
}

/// <summary>Tipo de sala o encuentro.</summary>
public enum RoomKind
{
    Puzzle,
    Combat,
    Mixed,
    Boss
}

// Localization / UI
/// <summary>Identificadores de texto usados por el sistema de localización/UI.</summary>
public enum UITextId
{
    MainMenuNewGame,
    MainMenuContinue,
    MainMenuSettings,
    MainMenuExit,
    SettingsLanguage,
    SettingsAudio,
    SettingsGraphics,
    SettingsControls,
    SettingsBack,
    UIHealth,
    UIMana,
    UILevel,
    UIExperience,
    DialogueContinue,
    DialogueSkip,
    DialogueEnd,
    InteractPress,
    InteractTalk,
    InteractExamine,
    InteractPickUp,
    InteractOpen,
    SystemLoading,
    SystemSaving,
    SystemGameSaved,
    SystemError
}

/// <summary>Identificadores para diálogos y nodos de conversación.</summary>
public enum DialogueId
{
    NpcVillager01,
    NpcMerchant01,
    NpcGuard01,
    ObjectSign01,
    ObjectBook01,
    ObjectChest01,
    TutorialMovement,
    TutorialCombat,
    TutorialMagic
}

// Parts / Character customization
/// <summary>Categorías de piezas/partes para personajes (cosmética/armado).</summary>
public enum PartCategory
{
    Body,
    Cloak,
    Head,
    Hair,
    Eyes,
    Mouth,
    Hat,
    Eyebrow,
    Accessory,
    WeaponL,
    WeaponR,
    ShieldR,
    Bow,
    Arrows,
    Spear,
    Wand,
    ThsSword,
    OhsSword,
    Axe,
    Hammer,
    Ohs
}

// World objects
/// <summary>Tipos de objetos en el mundo (nombres en español usados por el diseñador).</summary>
public enum ObjectType
{
    Caja,
    Barril,
    Bolsa,
    Paquete,
    Otro
}

// Characters
/// <summary>Mano usada (none/left/right).</summary>
public enum Hand
{
    None,
    Left,
    Right
}

/// <summary>Modo de entorno (interior/exterior/unknown).</summary>
public enum EnvironmentMode
{
    Unknown,
    Exterior,
    Interior
}
