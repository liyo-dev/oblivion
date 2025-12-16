using UnityEngine;
using Core;

public class CharacterSpinWithGamepad : MonoBehaviour
{
    public float rotateSpeed = 120f;
    public float deadZone = 0.15f;
    private PlayerControls input;
    private bool ownsControls;

    void Awake()
    {
        input = Core.PlayerInputManager.GetSharedOrNew(out ownsControls);
        if (ownsControls)
            input?.Enable();
    }

    void OnDestroy()
    {
        if (ownsControls)
            input?.Disable();
    }

    void Update()
    {
        Vector2 look = input.GamePlay.CameraLook.ReadValue<Vector2>();
        float x = Mathf.Abs(look.x) > deadZone ? look.x : 0f;
        if (x != 0f)
            transform.Rotate(0f, x * rotateSpeed * Time.unscaledDeltaTime, 0f, Space.World);
    }
}