using System;
using UnityEngine;

namespace Alex.NPC.Common
{
    public static class PlayerLocator
    {
        public static Transform ResolvePlayer(bool allowSceneLookup = true)
        {
            if (PlayerService.TryGetComponent(out Transform player, includeInactive: true))
                return ResolveMotionRoot(player);

            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup) && playerGo)
                return ResolveMotionRoot(playerGo.transform);

            if (!allowSceneLookup)
                return null;

            var fallback = GameObject.FindGameObjectWithTag("Player");
            if (fallback != null)
            {
                PlayerService.RegisterPlayer(fallback, false);
                return ResolveMotionRoot(fallback.transform);
            }

            return null;
        }

        public static Transform ResolvePlayerCamera()
        {
            if (Camera.main)
                return Camera.main.transform;

            return null;
        }

        static Transform ResolveMotionRoot(Transform candidate)
        {
            if (!candidate)
                return null;

            var invector = FindInvectorController(candidate);
            if (invector != null && invector != candidate)
                return invector;

            var characterController = candidate.GetComponentInChildren<CharacterController>(true);
            if (characterController && characterController.transform != candidate)
                return characterController.transform;

            var rigidbody = candidate.GetComponentInChildren<Rigidbody>(true);
            if (rigidbody && rigidbody.transform != candidate)
                return rigidbody.transform;

            return candidate;
        }

        static Transform FindInvectorController(Transform root)
        {
            var type = Type.GetType("Invector.vCharacterController.vThirdPersonController, Invector-3rdPersonController_LITE", false)
                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonController, Invector-3rdPersonController", false)
                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonController", false);

            if (type == null)
                return null;

            var component = root.GetComponentInChildren(type, true) as Component;
            return component ? component.transform : null;
        }
    }
}
