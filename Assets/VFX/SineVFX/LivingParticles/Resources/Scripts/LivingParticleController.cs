using UnityEngine;

public class LivingParticleController : MonoBehaviour {

    private Transform _playerTransform;
    private ParticleSystemRenderer _psr;

    void Start () {
        _psr = GetComponent<ParticleSystemRenderer>();

        // Obtener la referencia al jugador desde el ServiceLocator
        _playerTransform = ServiceLocator.Get<Transform>();
        if (_playerTransform == null) {
            Debug.LogError("No se pudo encontrar el Transform del jugador en el ServiceLocator.");
        }
    }

    void Update () {
        if (_playerTransform != null) {
            _psr.material.SetVector(Shader.PropertyToID("_Affector"), _playerTransform.position);
        }
    }
}
