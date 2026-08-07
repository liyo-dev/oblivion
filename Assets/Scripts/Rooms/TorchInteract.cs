using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TorchInteract : MonoBehaviour
{
    [Header("Estado")]
    public bool isLit = false;          // arranca apagada

    [Header("Visual (solo Particle Systems)")]
    public ParticleSystem flameFX;      // llama
    public ParticleSystem lightFX;      // halo/brillo (otro PS)

    [Header("Ignición por capa")]
    [Tooltip("Selecciona aquí la(s) capa(s) que encienden la antorcha. Marca 'Projectile'.")]
    public LayerMask igniteLayers;
    [Tooltip("Si está activo, destruimos el proyectil al encender.")]
    public bool consumeProjectileOnHit = true;

    [Header("Audio (clave del Audio Graph Profile)")]
    public string sfxIgniteKey = "Torch_Ignite";

    public System.Action<bool> onTorchToggled;

    void Start() => ApplyVisuals();

    // ---------- Público ----------
    public void Ignite()
    {
        if (isLit) return;
        isLit = true;
        ApplyVisuals();
        if (!string.IsNullOrWhiteSpace(sfxIgniteKey)) AudioService.Instance?.PlaySFX(sfxIgniteKey, 1f, transform.position);
        onTorchToggled?.Invoke(isLit);
    }

    public void Extinguish()
    {
        if (!isLit) return;
        isLit = false;
        ApplyVisuals();
        onTorchToggled?.Invoke(isLit);
    }

    // ---------- Detección (trigger o colisión física) ----------
    void OnTriggerEnter(Collider other)
    {
        if (IsIgniter(other.gameObject))
        {
            Ignite();
            if (consumeProjectileOnHit) SafeDestroy(GetRootRigidbodyOrSelf(other));
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        var hitGO = GetRootRigidbodyOrSelf(collision.collider);
        if (IsIgniter(hitGO))
        {
            Ignite();
            if (consumeProjectileOnHit) SafeDestroy(hitGO);
        }
    }

    // ---------- Utils ----------
    bool IsIgniter(GameObject go)
    {
        return (igniteLayers.value & (1 << go.layer)) != 0;
    }

    GameObject GetRootRigidbodyOrSelf(Collider col)
    {
        return col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
    }

    void SafeDestroy(GameObject go)
    {
        if (!go || go == gameObject) return;
        Destroy(go);
    }

    void ApplyVisuals()
    {
        // Importante: en los PS desactiva "Stop Action = Destroy", así podemos reusar Play/Stop.
        if (flameFX)
        {
            if (isLit) { if (!flameFX.isPlaying) flameFX.Play(); }
            else       { if (flameFX.isPlaying)  flameFX.Stop(); }
        }
        if (lightFX)
        {
            if (isLit) { if (!lightFX.isPlaying) lightFX.Play(); }
            else       { if (lightFX.isPlaying)  lightFX.Stop(); }
        }
    }
}
