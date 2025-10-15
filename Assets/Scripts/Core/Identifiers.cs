public enum AbilityId { PhysicalAttack, MagicAttack, Dash, Block }
public enum SpellId { None, Fireball, IceSpike, Storm, Lightning, FireSpecial }
public enum DamageKind { Physical, Magic, Special }
[System.Obsolete("Use QuestState instead")] public enum QuesStateEnum { NotStarted = 0, Active = 1, Completed = 2 }
public enum MagicSlot { Left, Right, Special }
public enum MagicKind { Projectile, Special }
public enum MagicElement { Fire, Ice, Storm, Light }
public enum SpellSlotType { Any, SpecialOnly }
public enum QuestState { Inactive = 0, Active = 1, Completed = 2, Failed = 3 }

// Enumerados para localización
public enum UITextId { MainMenu_NewGame, MainMenu_Continue, MainMenu_Settings, MainMenu_Exit, Settings_Language, Settings_Audio, Settings_Graphics, Settings_Controls, Settings_Back, UI_Health, UI_Mana, UI_Level, UI_Experience, Dialogue_Continue, Dialogue_Skip, Dialogue_End, Interact_Press, Interact_Talk, Interact_Examine, Interact_PickUp, Interact_Open, System_Loading, System_Saving, System_GameSaved, System_Error }

public enum DialogueId { NPC_Villager_01, NPC_Merchant_01, NPC_Guard_01, Object_Sign_01, Object_Book_01, Object_Chest_01, Tutorial_Movement, Tutorial_Combat, Tutorial_Magic }

public enum PartCategory { Body, Cloak, Head, Hair, Eyes, Mouth, Hat, Eyebrow, Accessory, WeaponL, WeaponR, ShieldR, Bow, Arrows, Spear, Wand, THS_Sword, OHS_Sword, Axe, Hammer }
[System.Obsolete("Use PartCategory instead")] public enum PartCat { Body, Cloak, Accessory, Eyes, Mouth, Hair, Head, Hat, Eyebrow, Bow, OHS, Shield, Arrows }

public enum WeaponStance { None = 0, SingleSword = 1, SwordAndShield = 2, TwoHandSword = 3, BowAndArrow = 4, Spear = 5, MagicWand = 6, DoubleSword = 7 }

public enum ObjectType { Caja, Barril, Bolsa, Paquete, Otro }

// --- Enums centralizados añadidos --- (movidos desde varios scripts para tener un único punto de verdad)
public enum ActionMode { Default, Carrying, Casting, Cinematic, Stunned, Swimming }
public enum PlayerAbility { Move, Jump, Sprint, Roll, Attack, Magic, Interact, Carry, Aim }

// Interactable
public enum InteractableMode { OpenDialogue, HandOffToTarget }
public enum SessionSelect { UseField, AutoFirstOnThisGO, ByTypeName }

// Quests
public enum QuestCompletionMode { Manual, AutoCompleteOnTalk, CompleteOnTalkIfStepsReady }
public enum TargetMode { FirstFound, Specific, ByName }

// Rooms
public enum RoomDifficulty { Easy, Medium, Hard, Boss }
public enum RoomKind { Puzzle, Combat, Mixed, Boss }

// Requirement modes moved from RoomExitBlocker
public enum RequirementMode { AnyQuestStartedOrCompleted, AnyQuestStarted, SpecificQuestsStarted, SpecificQuestsCompleted }

// Characters
public enum Hand { None, Left, Right }

// Environment
public enum EnvironmentMode { Unknown, Exterior, Interior }
