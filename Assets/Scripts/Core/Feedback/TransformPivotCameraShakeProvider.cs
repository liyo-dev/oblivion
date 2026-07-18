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
        private class ActiveShake
        {
            public MonoBehaviour Runner;
            public Coroutine     Coroutine;
            public Transform     Pivot;
            public Vector3       OriginalLocalPos;
        }

        private readonly System.Collections.Generic.List<ActiveShake> _active =
            new System.Collections.Generic.List<ActiveShake>();

        public void Shake(MonoBehaviour runner, float intensity, float duration)
        {
            Shake(runner, Camera.main, intensity, duration);
        }

        public void Shake(MonoBehaviour runner, Camera targetCamera, float intensity, float duration)
        {
            if (!runner || !targetCamera || intensity <= 0f || duration <= 0f) return;
            var pivot = EnsurePivot(targetCamera.transform);
            if (!pivot) return;
            var entry = new ActiveShake
            {
                Runner          = runner,
                Pivot           = pivot,
                OriginalLocalPos = pivot.localPosition,
            };
            entry.Coroutine = runner.StartCoroutine(Co_Shake(entry, intensity, duration));
            _active.Add(entry);
        }

        public void CancelAll()
        {
            foreach (var s in _active)
            {
                if (s.Runner && s.Coroutine != null)
                    s.Runner.StopCoroutine(s.Coroutine);
                if (s.Pivot)
                    s.Pivot.localPosition = s.OriginalLocalPos;
            }
            _active.Clear();
        }

        private IEnumerator Co_Shake(ActiveShake entry, float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (!entry.Pivot) yield break;

                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                entry.Pivot.localPosition = entry.OriginalLocalPos + new Vector3(x, y, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (entry.Pivot) entry.Pivot.localPosition = entry.OriginalLocalPos;
            RemoveEntry(entry.Coroutine);
        }

        private void RemoveEntry(Coroutine c)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i].Coroutine == c) { _active.RemoveAt(i); return; }
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
