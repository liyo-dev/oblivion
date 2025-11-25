using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CelestialCyclesSystem
{
    public class CelestialNightObject : MonoBehaviour
    {
        private CelestialTimeManager timeManager;
        public Light[] childLights;
        public MeshRenderer[] meshRenderers;
        public ParticleSystem[] particleSystems;
        private bool lastNightState = false;

        private void Start()
        {
            timeManager = FindObjectOfType<CelestialTimeManager>();
            childLights = GetComponentsInChildren<Light>();
            meshRenderers = GetComponentsInChildren<MeshRenderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();

            // Immediately determine the current day-night state and apply the appropriate settings
            lastNightState = timeManager.IsNightTime(); // Get the initial state

            // Toggle all components based on the initial state
            ToggleLights(lastNightState);
            ToggleMeshRenderersLitProperty(lastNightState);
            ToggleParticleSystems(lastNightState);
        }

        private void Update()
        {
            // Determine if it is day or night based on the current time and some threshold.
            bool currentNightState = timeManager.IsNightTime(); // Assuming IsNightTime() is a method in CelestialTimeManager that determines if it's night.

            // Check if the day-night state has changed
            if (lastNightState != currentNightState)
            {
                lastNightState = currentNightState;
                ToggleLights(currentNightState);
                ToggleMeshRenderersLitProperty(currentNightState);
                ToggleParticleSystems(currentNightState);
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
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            foreach (MeshRenderer renderer in meshRenderers)
            {
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    renderer.GetPropertyBlock(propBlock, i);

                    float emissiveIntensity = night ? 1f : 0f;
                    if (renderer.materials[i].HasProperty("_EmissiveIntensity"))
                    {
                        propBlock.SetFloat("_EmissiveIntensity", emissiveIntensity);
                        renderer.SetPropertyBlock(propBlock, i);
                    }
                }
            }
        }

    }
}
