using UnityEngine;
using System.Collections;

/// <summary>
/// Componente simple para hacer shake de una cámara específica.
/// Se añade dinámicamente cuando se necesita hacer shake en cámaras que no son Camera.main.
/// </summary>
public class SimpleCameraShaker : MonoBehaviour
{
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Coroutine _shakeCoroutine;

    void Awake()
    {
        _originalPosition = transform.localPosition;
        _originalRotation = transform.localRotation;
    }

    public void Shake(float intensity, float duration)
    {
        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);
        
        _shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));
    }

    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percentComplete = elapsed / duration;
            float damper = 1f - Mathf.Clamp01(percentComplete);
            
            // Shake de posición
            float x = Random.Range(-1f, 1f) * intensity * damper;
            float y = Random.Range(-1f, 1f) * intensity * damper;
            float z = Random.Range(-1f, 1f) * intensity * damper;
            
            transform.localPosition = _originalPosition + new Vector3(x, y, z);
            
            // Shake de rotación (más sutil)
            float rotX = Random.Range(-1f, 1f) * intensity * damper * 5f;
            float rotY = Random.Range(-1f, 1f) * intensity * damper * 5f;
            float rotZ = Random.Range(-1f, 1f) * intensity * damper * 5f;
            
            transform.localRotation = _originalRotation * Quaternion.Euler(rotX, rotY, rotZ);
            
            yield return null;
        }
        
        // Restaurar posición y rotación originales
        transform.localPosition = _originalPosition;
        transform.localRotation = _originalRotation;
        
        _shakeCoroutine = null;
    }
}
