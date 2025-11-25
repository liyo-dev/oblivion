using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

namespace CelestialCyclesSystem
{
    [ExecuteAlways]
    public class CelestialTimeManager : MonoBehaviour
    {
        public bool Trigger = true;
        [Header("Performance")]
        public bool updateGI = true;
        [Range(0f, 2f)]
        public float giUpdateInterval = 0.2f;
        [Range(0f, 2f)]
        public float shaderUpdateInterval = 0.1f; // Updates every 0.1 seconds
        public Slider timeOfDaySlider;
        public TextMeshProUGUI timeText;

        [Header("Current Time")]
        [Range(0, 24)]
        public float currentTimeOfDay = 8f;
        [Range(0, 100)]
        public float timeSpeed = 0f;
        private float actualSpeed = 1f;
        private bool isNightTime;
        private float celestialAngleY;

        [Header("Sunrise Time")]
        [Range(4, 10)]
        public float sunriseTime = 5; // Time when sun starts rising
        [Range(0.1f, 3f)]
        public float sunriseDuration = 1f; // Duration of sunrise
        [Header("Sunset Time")]
        [Range(15, 22)]
        public float sunsetTime = 19; // Time when sun starts setting
        [Range(0.1f, 3f)]
        public float sunsetDuration = 1f; // Duration of sunset

        [Header("Light Intensities")]
        public float sunIntensity = 1f;
        public float shadowIntensity = .85f;
        public float moonIntensity = .3f;

        [Header("Wind Direction Property")]
        public bool shouldWindChange = false;
        [Range(1, 24)]
        public float windChangeInterval = 1f;
        [Range(0, 360)]
        public float currentWindDirection = 0f;
        [Range(0, 180)]
        public float maxWindDirectionChange = 0f;
        private float nextWindChangeTime = 0f;
        private float lastWindDirection = -1f; // Stores last value to detect changes

        [Header("Sun & Sky Settings")]
        public Transform sunTransform;
        public Material sceneSkybox;
        private Light directionalLight;
        [Range(-180f, 180f)]
        public float sunAzimuth = -30f;
        [SerializeField]
        [Range(-90f, 90f)]
        public float sunriseOffset = 30f;
        [SerializeField]
        [Range(-90f, 90f)]
        public float sunsetOffset = 30f;
                [HideInInspector]
        public float skyboxIntensityModifier = 1f;
        [HideInInspector]
        public float weatherModifier = 1f;
        [HideInInspector]
        public float seasonModifier = 1f;
        [Header("Fog Settings (Manager Controlled)")]
        public bool fogEnabled = true;
        public FogMode fogMode = FogMode.Linear;
        [Tooltip("Multiplies the fog settings from the CelestialCyclePeriod profiles.")]
        [Range(0.1f, 20f)]
        public float fogDistance = 5f;

        [Header("Cloud Control (TimeManager Only)")]

        public bool overrideCloudControl = true;

        public Color CloudColor = Color.gray;
        [Range(0f, 1f)] public float DarkSky = 0.2f;
        [Range(0f, 1f)] public float BlendCloud = 0.4f;
        public Vector4 AmbientCloudProperty = new Vector4(1f, 1f, 1f, 1f);
        [Range(0f, 2f)] public float AmbientCloudSpeed = 0.5f;
        [Range(0.1f, 5f)] public float CloudScale_01 = 1f;
        [Range(0.1f, 5f)] public float CloudScale_02 = 1f;
        [Range(0f, 1f)] public float CloudAmount = 0.5f;
        [Range(0f, 2f)] public float CloudIntensity = 1f;
        [Range(0f, 2f)] public float CloudLayerIntensity01 = 1f;
        [Range(0f, 2f)] public float CloudLayerIntensity02 = 1f;
        [Range(0f, 2f)] public float CloudSpeed = 1f;


        public CelestialCycleDay timeOfDayController;

        private bool noonApplied = false;
        private bool nightApplied = false;
        private float lastGIUpdateTime = 0f;

        private float nextShaderUpdateTime = 0f;

        private Coroutine windCoroutine;

        private int lastDisplayedMinute = -1;

        // Called when any serialized property changes in the Editor.
        private void OnValidate()
        {
            if (timeOfDaySlider != null)
            {
                timeOfDaySlider.maxValue = 60f;
                timeOfDaySlider.value = timeSpeed;
            }
            // ☁️ Apply cloud settings only if WeatherManager is NOT assigned
            if (!Application.isPlaying && overrideCloudControl)
            {
                ApplyCloudSettings();
            }
        }
        void Start()
        {
            if (Application.isPlaying)
            {
                Trigger = true;
                // Apply initial lighting settings based on currentTimeOfDay
                CheckSunriseOrSunset(currentTimeOfDay);
                // Set lastGIUpdateTime to prevent immediate re-updates
                lastGIUpdateTime = currentTimeOfDay;
                // Update the time text to reflect the initial time
                if (timeText != null)
                    UpdateTimeText(currentTimeOfDay);
            }
            if (timeOfDaySlider != null)
            {
                timeOfDaySlider.maxValue = 60f;
                timeOfDaySlider.value = timeSpeed;
                timeOfDaySlider.onValueChanged.AddListener(UpdateTimeSpeed);
            }

            // Cache directional light from sunTransform
            if (sunTransform != null && directionalLight == null)
                directionalLight = sunTransform.GetComponent<Light>();
        }

        void Update()
        {

            // Allow manual control when Trigger is false
            if (Application.isPlaying && timeOfDaySlider != null && !Trigger)
            {
                timeSpeed = timeOfDaySlider.value;
          
            }
            if(!Application.isPlaying && timeOfDaySlider != null && Trigger)
            {
                timeOfDaySlider.value = timeSpeed;
            }


            if (Trigger)
            {
                // Time progresses automatically
                actualSpeed = timeSpeed / 60f;
                currentTimeOfDay += Time.deltaTime * actualSpeed;
                currentTimeOfDay %= 24f;

                if (shouldWindChange)
                    UpdateWindOverTime();

                if (currentWindDirection != lastWindDirection)
                {
                    Shader.SetGlobalFloat("WindDirection", currentWindDirection);
                    lastWindDirection = currentWindDirection;
                }

                updateDynamicGI();
            }

                UpdateSunDirection();
                CheckSunriseOrSunset(currentTimeOfDay);

                if (currentWindDirection != lastWindDirection)
                {
                    Shader.SetGlobalFloat("WindDirection", currentWindDirection);
                    lastWindDirection = currentWindDirection;
                }

                // Determine boundaries for full-day and full-night
                float sunriseEnd = sunriseTime + (sunriseDuration / 2f);
                float sunsetStart = sunsetTime - (sunsetDuration / 2f);

                if (!Application.isPlaying && sceneSkybox != null && RenderSettings.skybox != null)
                {
                    CheckSunriseOrSunset(currentTimeOfDay); // Handle transitions like sunrise and sunset
                    sunriseEnd = sunriseTime + (sunriseDuration / 2f);
                    sunsetStart = sunsetTime - (sunsetDuration / 2f);

                    if (currentTimeOfDay >= sunriseEnd && currentTimeOfDay < sunsetStart)
                    {
                        ApplyNoonSettings();
                    }
                    else if (currentTimeOfDay < sunriseTime - (sunriseDuration / 2f) || currentTimeOfDay >= sunsetTime + (sunsetDuration / 2f))
                    {
                        ApplyNightSettings();
                    }
                }
                else
                {
                    // In play mode, update only if not already applied to avoid unnecessary calls.
                    if (currentTimeOfDay >= sunriseEnd && currentTimeOfDay < sunsetStart)
                    {
                        if (!noonApplied)
                        {
                            ApplyNoonSettings();
                            noonApplied = true;
                            nightApplied = false;
                        }
                    }
                    else if (currentTimeOfDay < sunriseTime || currentTimeOfDay >= sunsetStart)
                    {
                        if (!nightApplied)
                        {
                            ApplyNightSettings();
                            nightApplied = true;
                            noonApplied = false;
                        }
                    }
                }

                // Only update time text if minute changed
                int currentMinute = Mathf.FloorToInt((currentTimeOfDay - Mathf.FloorToInt(currentTimeOfDay)) * 60);
                if (timeText != null && currentMinute != lastDisplayedMinute)
                {
                    UpdateTimeText(currentTimeOfDay);
                    lastDisplayedMinute = currentMinute;
                }
        }

        public void updateDynamicGI()
        {
            if (updateGI && ShouldUpdateGlobalIllumination(currentTimeOfDay))
            {
                DynamicGI.UpdateEnvironment();
                lastGIUpdateTime = currentTimeOfDay;
            }
        }



        bool ShouldUpdateGlobalIllumination(float time)
        {
            if (giUpdateInterval == 0) return true;

            float timeDiff = Mathf.Min(Mathf.Abs(time - lastGIUpdateTime), 24f - Mathf.Abs(time - lastGIUpdateTime));
            return timeDiff >= giUpdateInterval;
        }



        private void UpdateWindOverTime()
        {
            if (currentTimeOfDay >= nextWindChangeTime)
            {
                nextWindChangeTime = currentTimeOfDay + windChangeInterval;
                float newDirection = currentWindDirection + UnityEngine.Random.Range(-maxWindDirectionChange, maxWindDirectionChange);
                newDirection = Mathf.Repeat(newDirection, 360f);

                if (windCoroutine != null)
                    StopCoroutine(windCoroutine);
                windCoroutine = StartCoroutine(SmoothWindTransition(newDirection));
            }
        }

        IEnumerator SmoothWindTransition(float targetDirection)
        {
            float t = 0;
            float startDirection = currentWindDirection;

            while (t < 1)
            {
                t += Time.deltaTime * 0.1f;
                currentWindDirection = Mathf.LerpAngle(startDirection, targetDirection, t);
                Shader.SetGlobalFloat("WindDirection", currentWindDirection);
                yield return null;
            }
        }
        void CheckSunriseOrSunset(float time)
        {

            if (Time.time < nextShaderUpdateTime)
                return; // ⛔ Skip this frame’s shader update

            nextShaderUpdateTime = Time.time + shaderUpdateInterval;


            // Define explicit boundaries for sunrise transition.
            float sunriseStart = sunriseTime - (sunriseDuration / 2f);
            float sunriseStayStart = sunriseTime - (sunriseDuration / 4f);
            float sunriseStayEnd = sunriseTime + (sunriseDuration / 4f);
            float sunriseEnd = sunriseTime + (sunriseDuration / 2f);
            // Define explicit boundaries for sunset transition.
            float sunsetStart = sunsetTime - (sunsetDuration / 2f);
            float sunsetStayStart = sunsetTime - (sunsetDuration * 0.3f);  // first 30% ramp-up ends
            float sunsetStayEnd = sunsetTime + (sunsetDuration * 0.3f);    // last 30% ramp-down starts
            float sunsetEnd = sunsetTime + (sunsetDuration / 2f);
            float progress = 0f;

            // Determine if we’re within transition windows
            bool isSunriseTransition = currentTimeOfDay >= sunriseTime && currentTimeOfDay <= sunriseEnd;
            bool isSunsetTransition = currentTimeOfDay >= sunsetStart && currentTimeOfDay <= sunsetTime;

            // 🌄 SUNRISE: Only lerp after sun hits horizon
            if (currentTimeOfDay >= sunriseTime && currentTimeOfDay <= sunriseEnd)
            {
                float sunProgress = Mathf.InverseLerp(sunriseTime, sunriseEnd, currentTimeOfDay);
                Shader.SetGlobalColor("_Sun_Color", Color.Lerp(timeOfDayController.morningSettings.SunColor, timeOfDayController.noonSettings.SunColor, sunProgress));
                Shader.SetGlobalFloat("_Sun_Size", Mathf.Lerp(timeOfDayController.morningSettings.SunSize, timeOfDayController.noonSettings.SunSize, sunProgress));
            }
            // 🌇 SUNSET: Only lerp until sun hits horizon
            else if (currentTimeOfDay >= sunsetStart && currentTimeOfDay <= sunsetTime)
            {
                float sunProgress = Mathf.InverseLerp(sunsetStart, sunsetTime, currentTimeOfDay);
                Shader.SetGlobalColor("_Sun_Color", Color.Lerp(timeOfDayController.noonSettings.SunColor, timeOfDayController.eveningSettings.SunColor, sunProgress));
                Shader.SetGlobalFloat("_Sun_Size", Mathf.Lerp(timeOfDayController.noonSettings.SunSize, timeOfDayController.eveningSettings.SunSize, sunProgress));
            }
            // 🌞 DAY / 🌙 NIGHT: lock to full profiles
            else
            {
                bool isDay = currentTimeOfDay > sunriseEnd && currentTimeOfDay < sunsetStart;
                Shader.SetGlobalColor("_Sun_Color", isDay ? timeOfDayController.noonSettings.SunColor : timeOfDayController.nightSettings.SunColor);
                Shader.SetGlobalFloat("_Sun_Size", isDay ? timeOfDayController.noonSettings.SunSize : timeOfDayController.nightSettings.SunSize);
            }

            if (currentTimeOfDay >= sunriseStart && currentTimeOfDay < sunriseEnd)
            {
                if (currentTimeOfDay < sunriseStayStart)
                {
                    // Phase 1: Ramp from night to morning.
                    progress = Mathf.InverseLerp(sunriseStart, sunriseStayStart, currentTimeOfDay);
                    UpdateSceneLightingAndSkybox(timeOfDayController.nightSettings, timeOfDayController.morningSettings, progress);
                }
                else if (currentTimeOfDay >= sunriseStayStart && currentTimeOfDay < sunriseStayEnd)
                {
                    // Phase 2: Plateau – hold the morning profile.
                    UpdateSceneLightingAndSkybox(timeOfDayController.morningSettings, timeOfDayController.morningSettings, 0f);

                }
                else if (currentTimeOfDay >= sunriseStayEnd && currentTimeOfDay < sunriseEnd)
                {
                    // Phase 3: Ramp from morning to noon.
                    progress = Mathf.InverseLerp(sunriseStayEnd, sunriseEnd, currentTimeOfDay);
                    UpdateSceneLightingAndSkybox(timeOfDayController.morningSettings, timeOfDayController.noonSettings, progress);

                }
            }


            if (currentTimeOfDay >= sunsetStart && currentTimeOfDay < sunsetEnd)
            {
                if (currentTimeOfDay < sunsetStayStart)
                {
                    // Phase 1: Ramp from noon to evening.
                    progress = Mathf.InverseLerp(sunsetStart, sunsetStayStart, currentTimeOfDay);
                    UpdateSceneLightingAndSkybox(timeOfDayController.noonSettings, timeOfDayController.eveningSettings, progress);

                }
                else if (currentTimeOfDay >= sunsetStayStart && currentTimeOfDay < sunsetStayEnd)
                {
                    // Phase 2: Plateau – hold the evening profile.
                    UpdateSceneLightingAndSkybox(timeOfDayController.eveningSettings, timeOfDayController.eveningSettings, 0f);

                }
                else if (currentTimeOfDay >= sunsetStayEnd && currentTimeOfDay < sunsetEnd)
                {
                    // Phase 3: Ramp from evening to night.
                    progress = Mathf.InverseLerp(sunsetStayEnd, sunsetEnd, currentTimeOfDay);
                    UpdateSceneLightingAndSkybox(timeOfDayController.eveningSettings, timeOfDayController.nightSettings, progress);

                }
            }
        }


        public void UpdateTimeSpeed(float newValue)
        {

            timeSpeed = Mathf.Clamp(newValue, 0f, 60f);

        }

        void ApplyNoonSettings()
        {
            UpdateSceneLightingAndSkybox(timeOfDayController.noonSettings, timeOfDayController.noonSettings, 0f);
            directionalLight.intensity = sunIntensity * weatherModifier;
            Shader.SetGlobalFloat("_SunLightIntensity", sunIntensity);
        }

        // Helper function to apply night settings
        void ApplyNightSettings()
        {
            UpdateSceneLightingAndSkybox(timeOfDayController.nightSettings, timeOfDayController.nightSettings, 1f);
            directionalLight.intensity = moonIntensity * weatherModifier;
            Shader.SetGlobalFloat("_MoonLightIntensity", moonIntensity);
        }

        // Skybox & Fog updates only during sunrise and sunset
        private void UpdateSceneLightingAndSkybox(CelestialCyclePeriod startSettings, CelestialCyclePeriod endSettings, float progress)
        {
            Material skyboxMaterial = RenderSettings.skybox;
            if (skyboxMaterial == null)
            {
                Debug.LogWarning("Skybox material is not assigned in RenderSettings.");
                return;
            }


            // Lerp skybox properties
            Shader.SetGlobalColor("_Tint", Color.Lerp(startSettings.Tint, endSettings.Tint, progress));
            Shader.SetGlobalFloat("_Exposure", Mathf.Lerp(startSettings.Exposure, endSettings.Exposure, progress));
            Shader.SetGlobalFloat("_Contrast", Mathf.Lerp(startSettings.Contrast, endSettings.Contrast, progress));
            Shader.SetGlobalFloat("_Color_Blend", Mathf.Lerp(startSettings.SkyColorBlend, endSettings.SkyColorBlend, progress));
            Shader.SetGlobalColor("_Sky_Color", Color.Lerp(startSettings.SkyColor, endSettings.SkyColor, progress));
            Shader.SetGlobalFloat("_Sky_Blend", Mathf.Lerp(startSettings.SkyBlend, endSettings.SkyBlend, progress));
            Shader.SetGlobalFloat("_Equator_Brightness", Mathf.Lerp(startSettings.EquatorBrightness, endSettings.EquatorBrightness, progress));
            Shader.SetGlobalFloat("_Equator_Blend", Mathf.Lerp(startSettings.EquatorBlend, endSettings.EquatorBlend, progress));
            Shader.SetGlobalFloat("_Equator_Smoothness", Mathf.Lerp(startSettings.EquatorSmoothness, endSettings.EquatorSmoothness, progress));
            Shader.SetGlobalFloat("_Equator_Offset", Mathf.Lerp(startSettings.EquatorOffset, endSettings.EquatorOffset, progress));
            Shader.SetGlobalColor("_Ground_Color", Color.Lerp(startSettings.GroundColor, endSettings.GroundColor, progress));
            Shader.SetGlobalFloat("_Ground_Blend", Mathf.Lerp(startSettings.GroundBlend, endSettings.GroundBlend, progress));

            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = fogMode;
            // Lerp fog and ambient settings
            RenderSettings.fogColor = Color.Lerp(startSettings.fogColor, endSettings.fogColor, progress);

            float fogStartDistanceSetting = Mathf.Lerp(startSettings.fogStartDistance, endSettings.fogStartDistance, progress);
            fogStartDistanceSetting *= fogDistance* seasonModifier;
            RenderSettings.fogStartDistance = fogStartDistanceSetting;
            float fogEndDistanceSetting = Mathf.Lerp(startSettings.fogEndDistance, endSettings.fogEndDistance, progress);
            fogEndDistanceSetting *= fogDistance * seasonModifier;
            RenderSettings.fogEndDistance = fogEndDistanceSetting;
            float fogDensitySetting = Mathf.Lerp(startSettings.fogDensity, endSettings.fogDensity, progress);
            fogDensitySetting *= Mathf.Pow(0.5f, fogDistance * seasonModifier - 1f);
            RenderSettings.fogDensity = fogDensitySetting;
            RenderSettings.ambientIntensity = (Mathf.Lerp(startSettings.SkyboxIntensityMultiplier, endSettings.SkyboxIntensityMultiplier, progress)) * weatherModifier;
            RenderSettings.reflectionIntensity = (Mathf.Lerp(startSettings.reflectionIntensity, endSettings.reflectionIntensity, progress)) * weatherModifier;

    


            // Update directional light if available (lerp sun color if needed)
            if (directionalLight != null)
            {
                directionalLight.color = Color.Lerp(startSettings.LightColor, endSettings.LightColor, progress);
            }
        }

        public void ApplyCloudSettings()
        {
            if (!overrideCloudControl) return;

            Material skyboxMaterial = RenderSettings.skybox;
            if (skyboxMaterial == null)
            {
                Debug.LogWarning("Skybox material is not assigned in RenderSettings.");
                return;
            }

            Shader.SetGlobalColor("_Cloud_Color", CloudColor);
            Shader.SetGlobalFloat("_Dark_Sky", DarkSky);
            Shader.SetGlobalFloat("_Blend_Cloud", BlendCloud);
            Shader.SetGlobalVector("_AmbientCloudProperty", AmbientCloudProperty);
            Shader.SetGlobalFloat("_AmbientCloudSpeed", AmbientCloudSpeed);
            Shader.SetGlobalFloat("_Cloud_01_Scale", CloudScale_01);
            Shader.SetGlobalFloat("_Cloud_02_Scale", CloudScale_02);
            Shader.SetGlobalFloat("_Cloud_Amount", CloudAmount);
            Shader.SetGlobalFloat("_Cloud_Intensity", CloudIntensity);
            Shader.SetGlobalFloat("_Cloud_Layer_1_Intensity", CloudLayerIntensity01);
            Shader.SetGlobalFloat("_Cloud_Layer_2_Intensity", CloudLayerIntensity02);
            Shader.SetGlobalFloat("_Cloud_Speed", CloudSpeed);
        }

        void UpdateTimeText(float time)
        {
            // Convert 'time' to hours and minutes
            int hours = Mathf.FloorToInt(time);
            int minutes = Mathf.FloorToInt((time - hours) * 60);

            // Format and update the TextMeshPro text
            timeText.text = string.Format("{0:00}:{1:00}", hours, minutes);
        }


        void UpdateSunDirection()
        {
            bool isDay = currentTimeOfDay >= sunriseTime && currentTimeOfDay < sunsetTime;

            float celestialAngleX;
            float celestialAngleY;

            float threshold = sunriseOffset + 10f;

            if (isDay)
            {
                float normalizedDayTime = Mathf.InverseLerp(sunriseTime, sunsetTime, currentTimeOfDay);
                celestialAngleX = Mathf.Lerp(sunriseOffset, 180f - sunriseOffset, normalizedDayTime);
                celestialAngleY = sunAzimuth;

                Shader.SetGlobalFloat("_Shift", celestialAngleX <= threshold ? 1f : 0f);
                Shader.SetGlobalFloat("_isNight", celestialAngleX <= threshold ? 1f : 0f);
            }
            else
            {
                float nightStart = sunsetTime;
                float nightEnd = sunriseTime + 24f;
                float currentTimeWrapped = currentTimeOfDay >= sunsetTime ? currentTimeOfDay : currentTimeOfDay + 24f;
                float normalizedNightTime = Mathf.InverseLerp(nightStart, nightEnd, currentTimeWrapped);
                celestialAngleX = sunsetOffset;
                celestialAngleY = Mathf.Lerp(sunAzimuth + 180f, sunAzimuth, normalizedNightTime);

                Shader.SetGlobalFloat("_Shift", celestialAngleX <= threshold ? 0f : 1f); // Reverse logic for moon
                Shader.SetGlobalFloat("_isNight", celestialAngleX <= threshold ? 0f : 1f);
            }

            if (directionalLight != null)
            {
                directionalLight.transform.rotation = Quaternion.Lerp(
                    directionalLight.transform.rotation,
                    Quaternion.Euler(celestialAngleX, celestialAngleY, 0f),
                    Time.deltaTime * 5f
                );
            }
        }

        public bool IsNightTime()
        {
            return isNightTime;
        }
        public float returnSunYRotation()
        {
            return celestialAngleY;
        }
    }
}