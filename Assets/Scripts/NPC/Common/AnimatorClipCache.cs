using System.Collections.Generic;
using UnityEngine;

namespace Alex.NPC.Common
{
    /// <summary>
    /// Utility to cache AnimationClip lengths to avoid repeated lookups through the controller.
    /// </summary>
    public sealed class AnimatorClipCache
    {
        readonly Animator _animator;
        readonly Dictionary<string, float> _lengthCache = new();

        public AnimatorClipCache(Animator animator)
        {
            _animator = animator;
            Prewarm();
        }

        public float GetLength(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return 0f;

            if (_lengthCache.TryGetValue(clipName, out var len))
                return len;

            len = ResolveLength(clipName);
            if (len > 0f)
                _lengthCache[clipName] = len;
            return len;
        }

        void Prewarm()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (!_lengthCache.ContainsKey(clip.name))
                    _lengthCache.Add(clip.name, clip.length);
            }
        }

        float ResolveLength(string clipName)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return 0f;

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == clipName)
                    return clip.length;
            }

            return 0f;
        }
    }
}
