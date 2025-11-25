using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CelestialCyclesSystem
{
    [CreateAssetMenu(fileName = "Celestial Cycles Settings", menuName = "Celestial Cycles/Settings", order = 1)]
    public class CelestialCyclePeriod : ScriptableObject
    {

        [Header("Skybox Shader Property")]
        [ColorUsage(true, true)]
        public Color Tint = Color.white;
        [Range(0, 8)]
        public float Exposure = 1f;
        [Range(0, 10)]
        public float Contrast = 1f;
        public Color LightColor = Color.white;
        [ColorUsage(true, true)]
        public Color SunColor = Color.white;
        [Range(0.05f, .8f)]
        public float SunSize = .2f;
        [Header("Skybox Gradient Setting")]

        [Range(0, 1)]
        public float SkyColorBlend;
        [ColorUsage(true, true)]
        public Color SkyColor = Color.white;
        [Range(0.01f, 1)]
        public float SkyBlend = 1f;
        [Range(0, 3)]
        public float EquatorBrightness = 1f;
        [Range(0.01f, 1)]
        public float EquatorBlend = .1f;
        [Range(0f, .1f)]
        public float EquatorSmoothness = 0.05f;
        [Range(-20f, 20f)]
        public float EquatorOffset = -10f;
        public Color GroundColor = Color.black;
        [Range(0.01f, 1)]
        public float GroundBlend = 1f;

        [Header("Lighting")]

        [SerializeField] [Range(0, 8)] public float SkyboxIntensityMultiplier = 1;

        [Header("Environment Reflections")]
        [SerializeField] [Range(0, 1)] public float reflectionIntensity = 1;

        [Header("Fog Settings")]
        public Color fogColor = Color.grey;
        public float fogDensity = 0.01f;
        public float fogStartDistance = 500f;
        public float fogEndDistance = 2000f;

    }
}