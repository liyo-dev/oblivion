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
    IceSpike,
    Storm,
    Lightning,
    FireSpecial
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
    Special
}

/// <summary>Elementos mágicos disponibles.</summary>
public enum MagicElement
{
    Fire,
    Ice,
    Storm,
    Light
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
    Stunned,
    Swimming
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
    Aim
}

// Interactable / Session
/// <summary>Modos de interacción disponibles para objetos interactuables.</summary>
public enum InteractableMode
{
    OpenDialogue,
    HandOffToTarget
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
    CompleteOnTalkIfStepsReady
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

/// <summary>Modos de requerimiento para desbloqueos de salida.</summary>
public enum RequirementMode
{
    AnyQuestStartedOrCompleted,
    AnyQuestStarted,
    SpecificQuestsStarted,
    SpecificQuestsCompleted
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
