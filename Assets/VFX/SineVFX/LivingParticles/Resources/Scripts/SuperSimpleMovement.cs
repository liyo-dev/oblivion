using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SuperSimpleMovement : MonoBehaviour {

    public float movementSpeed = 3f;

    void Update() {
#if ENABLE_INPUT_SYSTEM
        // New Input System
        Vector2 input = new Vector2(
            Keyboard.current[Key.A].isPressed ? -1 : Keyboard.current[Key.D].isPressed ? 1 : 0,
            Keyboard.current[Key.S].isPressed ? -1 : Keyboard.current[Key.W].isPressed ? 1 : 0
        );
        transform.Translate(input.x * Time.deltaTime * movementSpeed, 0f, input.y * Time.deltaTime * movementSpeed);
#else
        // Old Input System
        transform.Translate(Input.GetAxis("Horizontal") * Time.deltaTime * movementSpeed, 0f, Input.GetAxis("Vertical") * Time.deltaTime * movementSpeed);
#endif
    }
}
