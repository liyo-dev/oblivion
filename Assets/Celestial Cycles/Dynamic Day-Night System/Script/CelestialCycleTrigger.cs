using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CelestialCyclesSystem
{
    public class CelestialCycleTrigger : MonoBehaviour
    {
        public CelestialTimeManager celestialCycleManager; // Assign this in the inspector
        [Range(0,24)]
        public float newTime = 12f; // The new time to set in the cycle manager, e.g., noon
        [Range(0, 4)]
        public float transitionDuration = 1f; // How long will it take to change to new time
        [Range(0, 10)]
        public float newTimeSpeed = 0f; // if you want to change TimeSpeed as well

        [HideInInspector]
        public string playerTag = "Player"; // Now a serialized field, hidden from the default inspector

        private void Update()
        {
            if (celestialCycleManager == null)
            {
                celestialCycleManager = FindObjectOfType<CelestialTimeManager>();
            }

        }
        void OnTriggerEnter(Collider other)
        {
            // Check if the object entering the trigger has the correct tag
            if (other.CompareTag(playerTag) && celestialCycleManager != null)
            {
                celestialCycleManager.currentTimeOfDay = newTime;
                celestialCycleManager.timeSpeed = newTimeSpeed;
            }
        }
    }
}
