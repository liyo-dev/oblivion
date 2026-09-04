namespace Sendero.Core.Feedback
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Cinemachine;
    using UnityEngine;

    /// <summary>
    /// Proveedor por defecto de Camera Shake: crea un pivot padre de la cámara objetivo
    /// y sacude ese pivot sin mover al Player. Compatible con URP camera stacking.
    /// </summary>
    public class TransformPivotCameraShakeProvider : ICameraShakeProvider
    {
        private class ActiveShake
        {
            public MonoBehaviour   Runner;
            public Coroutine       Coroutine;
            public Transform       Pivot;
            public CinemachineBrain Brain; // null si la camara de este shake no tiene Brain (p.ej. _stageCamera de cinematicas)
        }

        // FIX M5 (auditoría 2026-08-07): antes cada shake capturaba pivot.localPosition como su
        // "posición original" en el momento de arrancar. Con dos shakes solapados sobre el mismo
        // pivot, el segundo capturaba una posición ya desplazada por el ruido del primero; al
        // terminar, cada uno restauraba a SU captura (incorrecta) en vez de a la pose real de
        // reposo → offset residual permanente en el pivot de cámara. Ahora la posición base se
        // guarda una sola vez por pivot (cuando no hay ningún shake activo sobre él) y solo se
        // restaura cuando el último shake de ese pivot termina (ref-count).
        private class PivotState
        {
            public Vector3 BaseLocalPos;
            public int     RefCount;
        }

        private readonly List<ActiveShake> _active = new List<ActiveShake>();
        private readonly Dictionary<Transform, PivotState> _pivotStates = new Dictionary<Transform, PivotState>();

        // FIX (1 sep 2026): recuento de referencias de Brains pausados mientras dura un shake -
        // ver comentario en Shake() para el porque.
        private readonly Dictionary<CinemachineBrain, int> _brainRefCounts = new Dictionary<CinemachineBrain, int>();

        public void Shake(MonoBehaviour runner, float intensity, float duration)
        {
            Shake(runner, Camera.main, intensity, duration);
        }

        public void Shake(MonoBehaviour runner, Camera targetCamera, float intensity, float duration)
        {
            if (!runner || !targetCamera || intensity <= 0f || duration <= 0f) return;
            var pivot = EnsurePivot(targetCamera.transform);
            if (!pivot) return;

            if (!_pivotStates.TryGetValue(pivot, out var state))
            {
                state = new PivotState { BaseLocalPos = pivot.localPosition, RefCount = 0 };
                _pivotStates[pivot] = state;
            }
            state.RefCount++;

            // FIX (1 sep 2026): si la camara tiene un CinemachineBrain activo (el caso normal en
            // gameplay, donde Camera.main sigue al vcam de juego via PlayerBattleModeController/
            // el resto del rig de Cinemachine), el Brain reescribe camT.position/rotation en cada
            // LateUpdate con el blend del vcam activo - cancelando en el mismo frame cualquier
            // offset que este pivot le imponga en Update. Resultado: el shake se ejecuta (el pivot
            // se mueve) pero nunca llega a renderizarse - exactamente el sintoma reportado ("no he
            // notado ninguna mejoria" tras anadir camera shake a los cambios de fase de jefe).
            // Se pausa el Brain mientras dura el shake -mismo patron ya usado en
            // SimpleCinematicDirector para tomar control directo de Camera.main durante
            // cinematicas- con recuento de referencias por si hay shakes solapados sobre el mismo
            // Brain. Camaras sin Brain (p.ej. _stageCamera de las cinematicas) no se ven afectadas.
            var brain = targetCamera.GetComponent<CinemachineBrain>();
            AcquireBrain(brain);

            var entry = new ActiveShake
            {
                Runner = runner,
                Pivot  = pivot,
                Brain  = brain,
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
                ReleasePivot(s.Pivot, restoreImmediately: false);
                ReleaseBrain(s.Brain);
            }
            _active.Clear();

            // Todos los shakes cancelados: restaurar cada pivot a su pose base y limpiar estado.
            foreach (var kvp in _pivotStates)
            {
                if (kvp.Key) kvp.Key.localPosition = kvp.Value.BaseLocalPos;
            }
            _pivotStates.Clear();
        }

        private IEnumerator Co_Shake(ActiveShake entry, float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (!entry.Pivot) { ReleasePivot(entry.Pivot, restoreImmediately: true); ReleaseBrain(entry.Brain); RemoveEntry(entry.Coroutine); yield break; }

                if (_pivotStates.TryGetValue(entry.Pivot, out var state))
                {
                    float x = Random.Range(-1f, 1f) * intensity;
                    float y = Random.Range(-1f, 1f) * intensity;
                    entry.Pivot.localPosition = state.BaseLocalPos + new Vector3(x, y, 0f);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ReleasePivot(entry.Pivot, restoreImmediately: true);
            ReleaseBrain(entry.Brain);
            RemoveEntry(entry.Coroutine);
        }

        // Decrementa el ref-count del pivot; si era el último shake activo sobre él, restaura la
        // posición base y limpia el estado.
        private void ReleasePivot(Transform pivot, bool restoreImmediately)
        {
            if (!pivot) return;
            if (!_pivotStates.TryGetValue(pivot, out var state)) return;

            state.RefCount--;
            if (state.RefCount <= 0)
            {
                if (restoreImmediately) pivot.localPosition = state.BaseLocalPos;
                _pivotStates.Remove(pivot);
            }
        }

        // FIX (1 sep 2026): ver comentario en Shake(). Pausa el Brain la primera vez que se le
        // pide un shake y lo reactiva solo cuando el ultimo shake que lo necesitaba termina -
        // mismo patron de recuento que PivotState/ReleasePivot de mas arriba.
        private void AcquireBrain(CinemachineBrain brain)
        {
            if (!brain || !brain.enabled) return; // null, o ya pausado por otra razon ajena al shake
            _brainRefCounts.TryGetValue(brain, out var count);
            if (count == 0) brain.enabled = false;
            _brainRefCounts[brain] = count + 1;
        }

        private void ReleaseBrain(CinemachineBrain brain)
        {
            if (!brain) return;
            if (!_brainRefCounts.TryGetValue(brain, out var count)) return;
            count--;
            if (count <= 0)
            {
                _brainRefCounts.Remove(brain);
                brain.enabled = true;
            }
            else
            {
                _brainRefCounts[brain] = count;
            }
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
