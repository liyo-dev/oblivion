using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MeleeHitbox : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private DamageKind kind = DamageKind.Physical;
    [SerializeField] private LayerMask enemyMask;

    [Header("Ventana de golpe")]
    [SerializeField] private float defaultActiveTime = 0.25f;

    private readonly HashSet<IDamageable> _hitThisSwing = new();
    private bool _armed;
    private Collider _col;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true; // importante
    }

    public void ArmForSeconds(float time)
    {
        StopAllCoroutines();
        StartCoroutine(Co_Arm(time <= 0f ? defaultActiveTime : time));
    }

    public void ArmBegin()
    {
        _armed = true;
        _hitThisSwing.Clear();
    }

    public void ArmEnd()
    {
        _armed = false;
        _hitThisSwing.Clear();
    }

    private IEnumerator Co_Arm(float t)
    {
        ArmBegin();
        yield return new WaitForSeconds(t);
        ArmEnd();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;
        if (((1 << other.gameObject.layer) & enemyMask) == 0) return;

        if (!other.TryGetComponent<IDamageable>(out var dmg) )
            dmg = other.GetComponentInParent<IDamageable>();

        if (dmg == null) return;
        if (_hitThisSwing.Contains(dmg)) return;

        Vector3 p = other.ClosestPoint(transform.position);
        Vector3 n = (other.transform.position - transform.position).normalized;

        var info = new DamageInfo(damage, kind, this.gameObject, null, p, n);
        dmg.ApplyDamage(in info);
        _hitThisSwing.Add(dmg);
    }
}