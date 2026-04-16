using System.Collections.Generic;
using System;
using UnityEngine;

public enum GamePhase
{
    Gameplay,
    PauseMenu,
    MainMenu,
    Dialogue,
    SavePrompt,
    Inventory,
    Equipment,
    QuestMenu,
    Map,
    Shop,
    Cutscene,
    Loading,
    GameOver
}

public static class GameState
{
    private static readonly Dictionary<GamePhase, int> _stack = new Dictionary<GamePhase, int>();
    public static event Action OnChanged;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _stack.Clear();
        OnChanged = null;
    }
    #endif

    public static void Push(GamePhase phase)
    {
        if (!_stack.ContainsKey(phase)) _stack[phase] = 0;
        _stack[phase]++;
        OnChanged?.Invoke();
    }

    public static void Pop(GamePhase phase)
    {
        if (!_stack.ContainsKey(phase)) return;
        _stack[phase]--;
        if (_stack[phase] <= 0) _stack.Remove(phase);
        OnChanged?.Invoke();
    }

    public static bool Is(GamePhase phase) => _stack.ContainsKey(phase);

    public static bool IsAny(params GamePhase[] phases)
    {
        for (int i = 0; i < phases.Length; i++)
            if (Is(phases[i])) return true;
        return false;
    }

    // ===== Derived permissions =====
    public static bool CanProcessGameplayInput
        => !IsAny(GamePhase.PauseMenu,
                  GamePhase.MainMenu,
                  GamePhase.Dialogue,
                  GamePhase.SavePrompt,
                  GamePhase.Cutscene,
                  GamePhase.Loading,
                  GamePhase.GameOver,
                  GamePhase.Inventory,
                  GamePhase.Equipment,
                  GamePhase.QuestMenu);

    public static bool CanInteractGlobally
        => !IsAny(GamePhase.PauseMenu,
                  GamePhase.MainMenu,
                  GamePhase.Dialogue,
                  GamePhase.SavePrompt,
                  GamePhase.Cutscene,
                  GamePhase.Loading,
                  GamePhase.Inventory,
                  GamePhase.Equipment,
                  GamePhase.Shop,
                  GamePhase.QuestMenu);

    public static bool CanOpenPause
    {
        get
        {
            bool result = !IsAny(GamePhase.PauseMenu,
                                 GamePhase.MainMenu,
                                 GamePhase.Dialogue,
                                 GamePhase.SavePrompt,
                                 GamePhase.Cutscene,
                                 GamePhase.Loading,
                                 GamePhase.GameOver,
                                 GamePhase.Inventory,
                                 GamePhase.Equipment,
                                 GamePhase.QuestMenu);
            return result;
        }
    }

    public static bool CanOpenInventory
        => CanProcessGameplayInput && !Is(GamePhase.Inventory) && !Is(GamePhase.Equipment) && !Is(GamePhase.QuestMenu);
}
