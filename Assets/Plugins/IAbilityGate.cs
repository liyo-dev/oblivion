using Invector.vCharacterController; // para MagicCastType

// Nota: sin namespace, como tus IAttackHitbox/ITargetProvider.
public interface IAbilityGate
{
    bool CanUsePhysical();
    bool CanUseMagic(MagicCastType type);
    bool TrySpendMagic(MagicCastType type);
}