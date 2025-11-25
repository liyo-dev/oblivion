using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Control simple de ciclo día/noche que intercambia skybox, configura la luz
/// direccional y emite eventos cuando entra en cada fase.
/// </summary>
[DisallowMultipleComponent]
public class DayNightCycle : MonoBehaviour
{
    public enum State
    {
        Day,
        Night,
    }

    [Header("Skybox")]
    [Tooltip("Skybox a usar durante el día.")]
    [SerializeField] private Material daySkybox;

    [Tooltip("Skybox a usar durante la noche.")]
    [SerializeField] private Material nightSkybox;

    [Header("Luz direccional")]
    [SerializeField] private Light directionalLight;

    [SerializeField] private float dayLightIntensity = 1.2f;
    [SerializeField] private Color dayLightColor = Color.white;

    [SerializeField] private float nightLightIntensity = 0.25f;
    [SerializeField] private Color nightLightColor = new(0.6f, 0.7f, 1f);

    [Header("Ciclo")]
    [Tooltip("Duración del día en segundos.")]
    [SerializeField] private float dayDuration = 120f;

    [Tooltip("Duración de la noche en segundos.")]
    [SerializeField] private float nightDuration = 90f;

    [Tooltip("Estado inicial al arrancar la escena.")]
    [SerializeField] private bool startAtDay = true;

    [Tooltip("Si es falso, no avanza automáticamente el ciclo.")]
    [SerializeField] private bool autoAdvance = true;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onDayStarted;
    [SerializeField] private UnityEvent onNightStarted;

    public event Action DayStarted;
    public event Action NightStarted;

    public State CurrentState { get; private set; } = State.Day;

    float _timeLeft;

    void OnEnable()
    {
        InitializeCycle();
    }

    void Update()
    {
        if (!autoAdvance) return;

        _timeLeft -= Time.deltaTime;
        if (_timeLeft <= 0f)
        {
            SwitchState();
        }
    }

    public void SwitchState()
    {
        var next = CurrentState == State.Day ? State.Night : State.Day;
        ApplyState(next, invokeEvents: true);
    }

    public void SetDay()
    {
        ApplyState(State.Day, invokeEvents: true);
    }

    public void SetNight()
    {
        ApplyState(State.Night, invokeEvents: true);
    }

    void InitializeCycle()
    {
        var initialState = startAtDay ? State.Day : State.Night;
        ApplyState(initialState, invokeEvents: false);
    }

    void ApplyState(State state, bool invokeEvents)
    {
        CurrentState = state;

        ApplySkybox(state);
        ApplyDirectionalLight(state);
        ResetTimer(state);

        if (!invokeEvents) return;

        if (state == State.Day)
        {
            onDayStarted?.Invoke();
            DayStarted?.Invoke();
        }
        else
        {
            onNightStarted?.Invoke();
            NightStarted?.Invoke();
        }
    }

    void ApplySkybox(State state)
    {
        var skybox = state == State.Day ? daySkybox : nightSkybox;
        if (skybox) RenderSettings.skybox = skybox;
        DynamicGI.UpdateEnvironment();
    }

    void ApplyDirectionalLight(State state)
    {
        if (!directionalLight) return;

        if (state == State.Day)
        {
            directionalLight.color = dayLightColor;
            directionalLight.intensity = dayLightIntensity;
        }
        else
        {
            directionalLight.color = nightLightColor;
            directionalLight.intensity = nightLightIntensity;
        }
    }

    void ResetTimer(State state)
    {
        _timeLeft = state == State.Day ? Mathf.Max(0f, dayDuration) : Mathf.Max(0f, nightDuration);
    }
}
