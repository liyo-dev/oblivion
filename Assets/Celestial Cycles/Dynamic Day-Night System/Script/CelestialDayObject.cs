using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CelestialCyclesSystem
{
    public class CelestialDayObject : MonoBehaviour
    {
        private CelestialTimeManager timeManager;
        public Light[] childLights;
        public MeshRenderer[] meshRenderers;
        public ParticleSystem[] particleSystems;
        private bool lastDayState = false;

        private void Start()
        {
            timeManager = FindObjectOfType<CelestialTimeManager>();
            childLights = GetComponentsInChildren<Light>();
            meshRenderers = GetComponentsInChildren<MeshRenderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        private void Update()
        {
            // Determine if it is day or night based on the current time and some threshold.
            bool currentDayState = !timeManager.IsNightTime(); // Assuming IsNightTime() is a method in CelestialTimeManager that determines if it's night.

            // Check if the day-night state has changed
            if (lastDayState != currentDayState)
            {
                lastDayState = currentDayState;
                ToggleLights(currentDayState);
                ToggleMeshRenderersLitProperty(currentDayState);
                ToggleParticleSystems(currentDayState);
            }
        }

        private void ToggleParticleSystems(bool isNight)
        {
            foreach (ParticleSystem ps in particleSystems)
            {
                if (isNight)
                {
                    ps.Play(); // Start the Particle System if it's night
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // Stop and clear the Particle System if it's day
                }
            }
        }

        private void ToggleLights(bool isNight)
        {
            foreach (Light light in childLights)
            {
                light.enabled = isNight; // Enable lights at night, disable them during the day
            }
        }


        private void ToggleMeshRenderersLitProperty(bool night)
        {
            foreach (MeshRenderer renderer in meshRenderers)
            {
                if (renderer.material.HasProperty("_EmissiveIntensity")) // Assuming "_EmissiveIntensity" is the property to toggle.
                {
                    // Toggle the "_Lit" property based on whether it's night.
                    renderer.material.SetFloat("_EmissiveIntensity", night ? 1f : 0f);
                }
            }
        }
    }
}
