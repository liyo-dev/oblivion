using System;
using UnityEngine;

namespace ithappy
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCharacterInput : PlayerCharacterInputBase
    {
        protected override void Update()
        {
            base.Update();
            
            if (Input.GetKeyDown(KeyCode.Space))
            {
                HandleJump();
            }
            
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                HandleAnimations();
            }
            
            cachedMouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            cachedMouseY = Input.GetAxis("Mouse Y") * rotationSpeed;
            cachedHorizontal = Input.GetAxis("Horizontal");
            cachedVertical = Input.GetAxis("Vertical");
        }
    }
}