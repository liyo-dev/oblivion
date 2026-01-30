namespace Sendero.Core.Feedback
{
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Proveedor por defecto de Camera Shake: crea un pivot padre de la cámara objetivo
    /// y sacude ese pivot sin mover al Player. Compatible con URP camera stacking.
    /// </summary>
    public class TransformPivotCameraShakeProvider : ICameraShakeProvider
    {
        public void Shake(MonoBehaviour runner, float intensity, float duration)
        {
            Shake(runner, Camera.main, intensity, duration);
        }

        public void Shake(MonoBehaviour runner, Camera targetCamera, float intensity, float duration)
        {
            if (!runner || !targetCamera || intensity <= 0f || duration <= 0f) return;
            runner.StartCoroutine(Co_Shake(targetCamera, intensity, duration));
        }

        private IEnumerator Co_Shake(Camera targetCamera, float intensity, float duration)
        {
            if (!targetCamera) yield break;

            var pivot = EnsurePivot(targetCamera.transform);
            if (!pivot) yield break;

            Vector3 originalLocalPos = pivot.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (!pivot) yield break; // Seguridad por si se destruye el objeto

                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                pivot.localPosition = originalLocalPos + new Vector3(x, y, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (pivot) pivot.localPosition = originalLocalPos;
        }

        private Transform EnsurePivot(Transform camT)
        {
            if (!camT) return null;

            var parent = camT.parent;
            // Si ya hay un pivot marcado, úsalo
            if (parent && parent.GetComponent<FeedbackCameraShakePivot>())
                return parent;

            // Crear pivot como padre de la cámara conservando la pose
            var pivotGo = new GameObject($"FS_ShakePivot_{camT.name}");
            var pivotT = pivotGo.transform;

            pivotT.SetParent(parent, false);
            pivotT.position = camT.position;
            pivotT.rotation = camT.rotation;
            pivotT.localScale = Vector3.one;

            camT.SetParent(pivotT, true);
            camT.localPosition = Vector3.zero;
            camT.localRotation = Quaternion.identity;

            pivotGo.AddComponent<FeedbackCameraShakePivot>();
            return pivotT;
        }

        // Marcador para identificar pivots de shake creados por el provider
        private class FeedbackCameraShakePivot : MonoBehaviour {}
    }
}
