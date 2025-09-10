using UnityEngine;
using Invector.vCharacterController; 

[DisallowMultipleComponent]
public class AbilityGateBridge : MonoBehaviour, IAbilityGate
{
    [Header("Refs")]
    public PlayerState playerState;
    public ManaPool mana;

    [Header("MP Cost")]
    public float magicCostBasic = 8f;
    public float magicCostCombo = 15f;

    void Awake()
    {
        if (!playerState) playerState = GetComponent<PlayerState>() ?? FindFirstObjectByType<PlayerState>();
        if (!mana)        mana        = GetComponent<ManaPool>();
    }

    public bool CanUsePhysical()
    {
        return playerState == null || playerState.HasAbility(AbilityId.PhysicalAttack);
    }

    public bool CanUseMagic(MagicCastType type)
    {
        if (playerState && !playerState.HasAbility(AbilityId.MagicAttack))
            return false;

        if (!mana) return true;
        float cost = (type == MagicCastType.Basic) ? magicCostBasic : magicCostCombo;
        return mana.Current >= cost;
    }

    public bool TrySpendMagic(MagicCastType type)
    {
        if (!mana) return true;
        float cost = (type == MagicCastType.Basic) ? magicCostBasic : magicCostCombo;
        return mana.TrySpend(cost);
    }
}