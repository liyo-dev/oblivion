using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CelestialCyclesSystem
{
    [ExecuteInEditMode]
    public class CelestialClockObject : MonoBehaviour
    {
        public CelestialTimeManager timeManager;
        public float timeSpeed;
        public RotationAxis rotationAxis = RotationAxis.Z; // Default rotation axis
        public bool flip = false;
        public float currentTime = 0; // Time component, ranges from 0 to 24
        public Transform hourNeedle;
        public Transform minuteNeedle;



        // Start is called before the first frame update
        public enum RotationAxis
        {
            X,
            Y,
            Z
        }
        private void Start()
        {
            timeManager = FindObjectOfType<CelestialTimeManager>();
        }
        // Update is called once per frame
        void Update()
        {
            if (timeManager != null)
            {
                currentTime = timeManager.currentTimeOfDay;
                timeWorks(currentTime);
            }
            else
            {
                float actualSpeed = timeSpeed / 60f;
                // Simulate the passage of time
                currentTime += Time.deltaTime * actualSpeed;
                currentTime %= 24f; // Loop back to 0 after hitting 24 hours
                timeWorks(currentTime);
            }

        }

        void timeWorks(float currentTime)
        {
            if (flip)
            {
                float hourRotation = (currentTime / 24) * -720; // 2 full rotations in 24 hours
                float minuteRotation = (currentTime / 24) * -8640; // 24 full rotations in 24 hours
                RotateNeedle(hourNeedle, hourRotation);
                RotateNeedle(minuteNeedle, minuteRotation);
            }
            else
            {
                float hourRotation = (currentTime / 24) * 720; // 2 full rotations in 24 hours
                float minuteRotation = (currentTime / 24) * 8640; // 24 full rotations in 24 hours
                RotateNeedle(hourNeedle, hourRotation);
                RotateNeedle(minuteNeedle, minuteRotation);
            }
        }

        void RotateNeedle(Transform needle, float rotation)
        {
            if (needle != null)
            {
                switch (rotationAxis)
                {
                    case RotationAxis.X:
                        needle.localEulerAngles = new Vector3(rotation, 0, 0);
                        break;
                    case RotationAxis.Y:
                        needle.localEulerAngles = new Vector3(0, rotation, 0);
                        break;
                    case RotationAxis.Z:
                        needle.localEulerAngles = new Vector3(0, 0, rotation);
                        break;
                }
            }
        }
    }
}
