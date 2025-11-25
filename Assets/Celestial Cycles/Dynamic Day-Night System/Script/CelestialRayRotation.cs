using UnityEngine;
using System.Linq;
namespace CelestialCyclesSystem
{
    [ExecuteAlways]
    public class CeletialRayRotation : MonoBehaviour
    {
        private CelestialTimeManager timeManager;
        private float sunYRotation;
        private void Start()
        {
            timeManager = FindObjectOfType<CelestialTimeManager>();
        }

        void Update()
        {
            if (timeManager == null)
            {
                timeManager = FindObjectOfType<CelestialTimeManager>();
            }
            if (timeManager != null)
            {
                sunYRotation = timeManager.returnSunYRotation();

                // Create a new rotation Quaternion using the adjusted Y rotation
                Quaternion newRotation = Quaternion.Euler(transform.rotation.eulerAngles.x, sunYRotation, transform.rotation.eulerAngles.z);

                // Apply the new rotation to the transform
                transform.rotation = newRotation;
            }


        }
    }
}
