using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UIController : MonoBehaviour
{

    public Light directionalLight;
    public ReflectionProbe reflectionProbe;
    public Material daySkyboxMaterial;
    public Material nightSkyboxMaterial;
    public Transform prefabHolder;
    public Text text;

    private Transform[] prefabs;
    private List<Transform> lt;
    private int activeNumber = 0;

    void Start()
    {
#if ENABLE_INPUT_SYSTEM
        // Check for Standalone Input Module and replace it with Input System UI Input Module
        var standaloneInputModule = FindFirstObjectByType<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standaloneInputModule != null)
        {
            Debug.Log("Replacing Standalone Input Module with Input System UI Input Module.");
            var eventSystemGameObject = standaloneInputModule.gameObject;
            Destroy(standaloneInputModule);
            var inputSystemUIModule = eventSystemGameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
#endif

        // Existing initialization logic
        lt = new List<Transform>();
        prefabs = prefabHolder.GetComponentsInChildren<Transform>(true);

        foreach (Transform tran in prefabs)
        {
            if (tran.parent == prefabHolder)
            {
                lt.Add(tran);
            }
        }

        prefabs = lt.ToArray();
        EnableActive();
    }

    // Turn On active VFX Prefab
    public void EnableActive()
    {
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (i == activeNumber)
            {
                prefabs[i].gameObject.SetActive(true);
                text.text = prefabs[i].name;
            }
            else
            {
                prefabs[i].gameObject.SetActive(false);
            }
        }
    }

    // Change active VFX
    public void ChangeEffect(bool bo)
    {
        if (bo == true)
        {
            activeNumber++;
            if (activeNumber == prefabs.Length)
            {
                activeNumber = 0;
            }
        }
        else
        {
            activeNumber--;
            if (activeNumber == -1)
            {
                activeNumber = prefabs.Length - 1;
            }
        }

        EnableActive();
    }

    public void SetDay()
    {
        directionalLight.enabled = true;
        RenderSettings.skybox = daySkyboxMaterial;
        reflectionProbe.RenderProbe();
    }

    public void SetNight()
    {
        directionalLight.enabled = false;
        RenderSettings.skybox = nightSkyboxMaterial;
        reflectionProbe.RenderProbe();
    }


    // TEMP
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // New Input System
        if (Keyboard.current[Key.Q].wasPressedThisFrame)
        {
            SetDay();
        }
        if (Keyboard.current[Key.E].wasPressedThisFrame)
        {
            SetNight();
        }
        if (Keyboard.current[Key.X].wasPressedThisFrame)
        {
            ChangeEffect(true);
        }
        if (Keyboard.current[Key.Z].wasPressedThisFrame)
        {
            ChangeEffect(false);
        }
#else
        // Old Input System
        if (Input.GetKeyDown(KeyCode.Q))
        {
            SetDay();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            SetNight();
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            ChangeEffect(true);
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            ChangeEffect(false);
        }
#endif
    }
}
