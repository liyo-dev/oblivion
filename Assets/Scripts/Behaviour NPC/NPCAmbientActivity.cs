/// <summary>
/// Actividad ambiental que realiza un NPC en estado Idle.
/// Requiere enableWander = false en NPCAmbientConfig.
/// </summary>
public enum NPCAmbientActivity
{
    None,
    SitGround,
    SitLow,
    SitMedium,
    SitHigh,
    Eat,
    Drink,
    Sleep
}
