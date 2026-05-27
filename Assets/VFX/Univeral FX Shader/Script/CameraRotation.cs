using UnityEngine;

namespace RDR.Camera
{
    public class CameraRotation : MonoBehaviour
    {
        public Transform target; // The object around which the camera will rotate
        public float rotationSpeed = 200.0f; // Camera rotation speed
        public float zoomSpeed = 2.0f; // Zoom speed
        public float minDistance = 2.0f; // Minimum distance to the object
        public float maxDistance = 20.0f; // Maximum distance to the object
        public float smoothTime = 0.2f; // Smoothing time for rotation and zoom

        private float currentDistance; // Current distance to the object
        private float targetDistance; // Target distance to the object
        private float distanceVelocity; // Speed for smoothing zoom
        private Vector3 currentVelocity; // Variable for smoothing rotation
        private Vector3 currentRotation; // Current rotation angle
        private Vector3 targetRotation; // Target rotation angle

        void Start()
        {
            if (target == null)
            {
                Debug.LogError("Target object for the camera is not set!");
                return;
            }

            // Initialize distance and rotation angle
            currentDistance = targetDistance = Vector3.Distance(transform.position, target.position);
            Vector3 angles = transform.eulerAngles;
            currentRotation = new Vector3(angles.x, angles.y, 0);
            targetRotation = currentRotation;
        }

        void Update()
        {
            if (target == null) return;

            // Handle rotation when the right mouse button is held down
            if (Input.GetMouseButton(1)) // 1 - right mouse button
            {
                targetRotation.y += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
                targetRotation.x -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
                targetRotation.x = Mathf.Clamp(targetRotation.x, -80f, 80f); // Limit vertical angle
            }

            // Zoom (in/out) using the mouse scroll wheel
            float scroll = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance - scroll, minDistance, maxDistance);

            // Smoothly update the current rotation angle
            currentRotation = Vector3.SmoothDamp(currentRotation, targetRotation, ref currentVelocity, smoothTime);

            // Smoothly update the distance to the object (zoom)
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, smoothTime);

            // Calculate the new camera position
            Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
            Vector3 direction = rotation * Vector3.forward;
            transform.position = target.position - direction * currentDistance;

            // The camera always looks at the object
            transform.LookAt(target);
        }
    }
}
