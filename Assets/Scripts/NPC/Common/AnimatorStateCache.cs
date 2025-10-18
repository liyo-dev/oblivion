using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alex.NPC.Common
{
    /// <summary>
    /// Lightweight cache around Animator state hashes/layers so we avoid repeated resolution work.
    /// Designed to keep MonoBehaviours slim by delegating state handling here.
    /// </summary>
    [Serializable]
    public sealed class AnimatorStateCache
    {
        [Serializable]
        public struct CachedState
        {
            public int Hash;
            public int Layer;

            public CachedState(int hash, int layer)
            {
                Hash = hash;
                Layer = layer;
            }
        }

        readonly Animator _animator;
        readonly Dictionary<string, CachedState> _cache = new();

        public AnimatorStateCache(Animator animator)
        {
            _animator = animator;
        }

        public bool TryResolve(string stateNameOrPath, out CachedState state)
        {
            state = default;
            if (_animator == null || string.IsNullOrEmpty(stateNameOrPath))
                return false;

            if (_cache.TryGetValue(stateNameOrPath, out state))
                return true;

            foreach (var candidate in EnumerateCandidates(stateNameOrPath))
            {
                if (TryResolveInternal(candidate, out state))
                {
                    _cache[stateNameOrPath] = state;
                    return true;
                }
            }

            return false;
        }

        public bool CrossFade(string stateNameOrPath, float fadeDuration, float normalizedTime = 0f)
        {
            if (!TryResolve(stateNameOrPath, out var state))
                return false;

            _animator.CrossFadeInFixedTime(state.Hash, fadeDuration, state.Layer, normalizedTime);
            return true;
        }

        public void Preload(params string[] stateNames)
        {
            if (stateNames == null) return;
            foreach (var stateName in stateNames)
            {
                TryResolve(stateName, out _);
            }
        }

        IEnumerable<string> EnumerateCandidates(string raw)
        {
            yield return raw;

            if (!raw.Contains('.'))
            {
                yield return $"Base Layer.{raw}";
                yield return $"Base Layer.Locomotion.{raw}";
            }
        }

        bool TryResolveInternal(string stateName, out CachedState cached)
        {
            cached = default;
            if (_animator == null || string.IsNullOrEmpty(stateName))
                return false;

            int hash = Animator.StringToHash(stateName);
            for (int layer = 0; layer < _animator.layerCount; layer++)
            {
                if (!_animator.HasState(layer, hash)) continue;
                cached = new CachedState(hash, layer);
                return true;
            }

            return false;
        }
    }
}
