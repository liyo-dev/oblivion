using UnityEngine;
using Invector.vCharacterController;

[RequireComponent(typeof(MonoBehaviour))] 
public class MagicCaster : MonoBehaviour
{
    public FireballProjectile fireballBasic;
    public FireballProjectile fireballCombo;
    public LayerMask enemyMask;

    private IAbilityGate gate;
    private vThirdPersonController cc;

    void Awake()
    {
        gate = GetComponent<IAbilityGate>();                          // usa la interfaz
        cc   = GetComponent<vThirdPersonController>();
    }

    void OnEnable()  { if (cc) cc.OnMagicCast += HandleMagicCast; }
    void OnDisable() { if (cc) cc.OnMagicCast -= HandleMagicCast; }

    void HandleMagicCast(GameObject caster, Transform origin, Vector3 direction, MagicCastType type)
    {
        if (gate != null)
        {
            if (!gate.CanUseMagic(type) || !gate.TrySpendMagic(type)) return;
        }
        var prefab = (type == MagicCastType.Basic) ? fireballBasic : fireballCombo;
        if (!prefab || !origin) return;

        var proj = Instantiate(prefab, origin.position, Quaternion.LookRotation(direction, Vector3.up));
        proj.Instigator = caster;
    }
}