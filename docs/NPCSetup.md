# NPC combat setup quick checklist

The `NPCBehaviourManager` enforces three components via `[RequireComponent]`:

- `NavMeshAgent` for pathfinding and approach.
- `NPCSimpleAnimator` to drive the animator states for idle, walk, challenge, etc.
- `Interactable` so the NPC can trigger dialogues or custom interactions.

At runtime it also expects or will auto-create the following pieces:

- An `Animator` (usually on the character rig). `NPCSimpleAnimator` locates it with `GetComponentInChildren<Animator>()` if you do not assign one in the inspector.
- A `Damageable` component to hold battle health; if the GameObject does not have one the manager adds it automatically when the battle starts.
- A collider (e.g., `CapsuleCollider`) so the NPC can be targeted/hit and the `Interactable` can detect clicks.
- A `Rigidbody` is optional; if present and `forceKinematicRigidbody` is enabled, the manager sets it to kinematic and freezes rotation to avoid physics pushes.

Common optional helpers for combat feedback:

- Assign `healthBarPrefab` (or `healthBarCanvasOverride`) in the Combat module to render the NPC HP bar.
- Set `projectilePrefab` and optionally `projectileOrigin` if the NPC should shoot.
- Provide `dialogueOnChallenge` so the challenge flow blocks the player and starts dialogue before battle.

If any of these references are missing the manager logs debug information, but the required components above must be present on the NPC GameObject.
