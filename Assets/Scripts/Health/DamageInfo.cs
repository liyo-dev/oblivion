using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public DamageKind kind;
    public GameObject source;     // el objeto que causa el daño (proyectil, espada...)
    public GameObject instigator; // quién lo lanzó/empuña (el player)
    public Vector3 point;         // punto de impacto
    public Vector3 normal;        // normal del impacto

    public DamageInfo(float amount, DamageKind kind, GameObject source, GameObject instigator, Vector3 point, Vector3 normal)
    {
        this.amount = amount;
        this.kind = kind;
        this.source = source;
        this.instigator = instigator;
        this.point = point;
        this.normal = normal;
    }
}