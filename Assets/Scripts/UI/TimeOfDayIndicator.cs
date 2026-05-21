using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sendero.UI
{
    /// <summary>
    /// Muestra un icono en el HUD que cambia según el período del día.
    /// Cuando llueve, sobreescribe el icono con rainSprite hasta que para la lluvia.
    /// Busca DayNightCycle automáticamente cada vez que se carga una escena nueva.
    /// </summary>
    public class TimeOfDayIndicator : MonoBehaviour
    {
        [Serializable]
        public struct TimeIconEntry
        {
            public DayNightCycle.TimeOfDay timeOfDay;
            public Sprite sprite;
        }

        [Header("Referencias")]
        [Tooltip("La Image de UI donde se mostrará el icono")]
        [SerializeField] private Image iconImage;

        [Header("Mapeado de iconos")]
        [Tooltip("Un sprite por cada período de DayNightCycle.TimeOfDay")]
        [SerializeField] private TimeIconEntry[] icons;

        [Tooltip("Icono que se muestra mientras llueve (sobreescribe el del período actual)")]
        [SerializeField] private Sprite rainSprite;

        private DayNightCycle _dayNight;
        private DayNightCycle.TimeOfDay _currentPeriod;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromCycle();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Si ya tenemos referencia válida no hace falta buscar de nuevo
            if (_dayNight != null) return;

            var found = FindObjectOfType<DayNightCycle>();
            if (found == null) return;

            _dayNight = found;
            _dayNight.TimeOfDayChanged += OnTimeOfDayChanged;
            _dayNight.RainStarted      += OnRainStarted;
            _dayNight.RainStopped      += OnRainStopped;

            OnTimeOfDayChanged(_dayNight.CurrentTimeOfDay);
            if (_dayNight.IsRaining) OnRainStarted();
        }

        private void UnsubscribeFromCycle()
        {
            if (_dayNight == null) return;
            _dayNight.TimeOfDayChanged -= OnTimeOfDayChanged;
            _dayNight.RainStarted      -= OnRainStarted;
            _dayNight.RainStopped      -= OnRainStopped;
            _dayNight = null;
        }

        private void OnTimeOfDayChanged(DayNightCycle.TimeOfDay time)
        {
            _currentPeriod = time;

            // Si está lloviendo mantenemos el rainSprite, no cambiamos
            if (_dayNight != null && _dayNight.IsRaining) return;

            ApplySprite(SpriteForPeriod(time));
        }

        private void OnRainStarted()
        {
            if (rainSprite != null)
                ApplySprite(rainSprite);
        }

        private void OnRainStopped()
        {
            ApplySprite(SpriteForPeriod(_currentPeriod));
        }

        private Sprite SpriteForPeriod(DayNightCycle.TimeOfDay time)
        {
            foreach (var entry in icons)
                if (entry.timeOfDay == time) return entry.sprite;
            return null;
        }

        private void ApplySprite(Sprite sprite)
        {
            if (iconImage == null) return;
            iconImage.sprite  = sprite;
            iconImage.enabled = sprite != null;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { }
#endif
    }
}
