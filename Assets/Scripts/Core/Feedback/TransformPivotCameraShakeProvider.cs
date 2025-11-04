namespace Sendero.Core.Feedback
{
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Proveedor por defecto de Camera Shake: crea un pivot padre de la Main Camera
    /// y sacude ese pivot sin mover al Player. Compatible con URP camera stacking
    /// (afecta a la cámara base; las overlay no se sacuden salvo que se cuelguen del mismo pivot).
    /// </summary>
    public class TransformPivotCameraShakeProvider : ICameraShakeProvider
    {
        public void Shake(MonoBehaviour runner, float intensity, float duration)
        {
            if (!runner || intensity <= 0f || duration <= 0f) return;
            runner.StartCoroutine(Co_Shake(runner, intensity, duration));
        }

        private IEnumerator Co_Shake(MonoBehaviour runner, float intensity, float duration)
        {
            var cam = Camera.main;
            if (!cam) yield break;

            var pivot = EnsurePivot(cam.transform);
            if (!pivot) yield break;

            Vector3 originalLocalPos = pivot.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                pivot.localPosition = originalLocalPos + new Vector3(x, y, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            pivot.localPosition = originalLocalPos;
        }

        private Transform EnsurePivot(Transform camT)
        {
            if (!camT) return null;

            var parent = camT.parent;
            // Si ya hay un pivot marcado, úsalo
            if (parent && parent.GetComponent<FeedbackCameraShakePivot>())
                return parent;

            // Crear pivot como padre del MainCamera conservando la pose
            var pivotGo = new GameObject("FS_ShakePivot");
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
