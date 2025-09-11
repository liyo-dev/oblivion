using UnityEngine;

[CreateAssetMenu(menuName="Magic/Spell")]
public class MagicSpellSO : ScriptableObject
{
    [Header("Identidad")]
    public string       displayName = "Fireball";
    public MagicKind    kind        = MagicKind.Projectile;
    public MagicElement element     = MagicElement.Fire;

    [Header("Prefab / Física")]
    public GameObject prefab;
    public float      initialSpeed = 18f;
    public bool       useGravity   = false;

    [Header("Costes / CD")]
    public float manaCost = 5f;
    public float cooldown = 0.25f;
}
