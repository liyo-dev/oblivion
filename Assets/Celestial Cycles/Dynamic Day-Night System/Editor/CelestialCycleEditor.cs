using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
namespace CelestialCyclesSystem
{
    public class CelestialCycleEditor : EditorWindow
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string celestialSettingsDirectoryPath;

        [SerializeReference] private string sceneName;
        [SerializeReference] private string scenePath;
        [SerializeReference] private string sceneDirectory;
        private string assetRelativePath;
        private string backupPath;
        private CelestialTimeManager timeManager;
        [SerializeReference] private CelestialCycleDay existingCCDay;
        private bool hasEnvironmentSetting = false;
        private Editor environmentManagerEditor;
        private bool isSceneBackedUp = false;
        private string backupFilePath = "";

        [MenuItem("Tools/Celestial Cycle/Manager/Celestial Time Manager")]
        static public void ShowWindow()
        {
            CelestialCycleEditor window = EditorWindow.GetWindow<CelestialCycleEditor>();
            window.titleContent.text = "Celestial Time Manager";

        }


        private void OnValidate()
        {
            if (!timeManager) timeManager = FindObjectOfType<CelestialTimeManager>();

        }
        void GeneratePath()
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            sceneDirectory = Path.GetDirectoryName(scenePath).Replace("\\", "/");

            string sceneFolderPath = sceneDirectory + "/" + sceneName;

            // Check if the scene folder exists
            if (!AssetDatabase.IsValidFolder(sceneFolderPath))
            {
                string guid = AssetDatabase.CreateFolder(sceneDirectory, sceneName);  // Create the scene folder only if it doesn't exist
                backupPath = AssetDatabase.GUIDToAssetPath(guid);  // Retrieve the folder path
            }
            else
            {
                backupPath = sceneFolderPath; // If folder exists, just assign its path
            }



            string folderPath = sceneFolderPath + "/Celestial Cycle";
    
    // Check if "Celestial Cycle" folder exists
    if (!AssetDatabase.IsValidFolder(folderPath))
    {
        string guid = AssetDatabase.CreateFolder(sceneDirectory + "/" + sceneName, "Celestial Cycle");  // Create if not present
        assetRelativePath = AssetDatabase.GUIDToAssetPath(guid);  // Retrieve the folder path
    }
    else
    {
        assetRelativePath = folderPath;  // Use the existing folder path
    }

    celestialSettingsDirectoryPath = sceneDirectory;  // This stores the directory of the scene (not "Celestial Cycle")
}

        private void OnGUI()
        {
            GUILayout.BeginVertical("box");
            {
                DrawHeader();
            }
            GUILayout.EndVertical();

            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.5f, 0.5f, 0.75f, 1f);
            headerStyle.margin = new RectOffset(0, 0, 10, 10);

            GUILayout.BeginVertical("box");
            GUILayout.Label("Celestial Time Manager", headerStyle);
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");
            if (!isSceneBackedUp)
            {
                EditorGUILayout.HelpBox("Warning: It is highly recommended to back up your current scene before proceeding. This tool will make changes to your scene's lighting and settings.", MessageType.Warning);
                if (GUILayout.Button("Backup Current Scene"))
                {
                    if (string.IsNullOrEmpty(assetRelativePath))
                    {
                        GeneratePath();
                    }
                    BackupCurrentScene();
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Backup created at: {backupFilePath}", MessageType.Info);
                if (GUILayout.Button("Show Backup in Project"))
                {
                    // Ping the object to highlight it in the project window.
                    Object backupAsset = AssetDatabase.LoadAssetAtPath<Object>(backupFilePath);
                    if (backupAsset != null)
                    {
                        EditorGUIUtility.PingObject(backupAsset);
                    }
                    else
                    {
                        Debug.LogWarning("Could not find backup asset at path: " + backupFilePath);
                    }
                }
            }
            GUILayout.EndVertical();

            if (Lightmapping.giWorkflowMode == Lightmapping.GIWorkflowMode.Iterative)
            {
                EditorGUILayout.HelpBox("Auto Generate Lighting is not recommended for time of day. Please disable it to avoid performance issues.", MessageType.Warning);
                if (GUILayout.Button("Turn Off Auto Generate"))
                {
                    Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
                }
            }


            // Always fetch latest Time Manager
            if (!timeManager)
                timeManager = FindObjectOfType<CelestialTimeManager>();

            bool setupDone = timeManager && timeManager.timeOfDayController && CheckSkybox();

            if (!setupDone)
            {
                if (!timeManager)
                {
                    if (GUILayout.Button("Spawn Celestial Time Manager"))
                    {
                        SpawnEnvironmentManager();
                    }
                    if (GUILayout.Button("Search and Assign Time Manager"))
                    {
                        var foundManager = FindObjectOfType<CelestialTimeManager>();
                        if (foundManager)
                        {
                            timeManager = foundManager;
                            RefreshEnvironmentManagerEditor(timeManager);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Not Found", "No Celestial Time Manager found.", "OK");
                        }
                    }
                }

                if (timeManager && existingCCDay == null)
                {
                    if (GUILayout.Button("Generate Celestial Cycle"))
                    {
                        GeneratePath();
                        GenerateEnvironmentSettings();
                    }
                }

                if (timeManager && !CheckSkybox())
                {
                    if (GUILayout.Button("Create Celestial Skybox Material"))
                    {
                        CreateSkyboxMaterial();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("✅ All settings have been applied.");

                if (environmentManagerEditor == null || environmentManagerEditor.target != timeManager)
                {
                    RefreshEnvironmentManagerEditor(timeManager);
                }

                if (environmentManagerEditor)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
                    environmentManagerEditor.OnInspectorGUI();
                    EditorGUILayout.EndScrollView();
                }

                if (GUILayout.Button("Refresh"))
                {
                    RefreshEnvironmentManagerEditor(timeManager);
                }
            }
        }

        void RefreshEnvironmentManagerEditor(CelestialTimeManager environmentManager)
        {
            if (environmentManagerEditor != null)
            {
                DestroyImmediate(environmentManagerEditor);
                environmentManagerEditor = null;
            }

            if (environmentManager != null)
            {
                environmentManagerEditor = Editor.CreateEditor(environmentManager);
            }
            else
            {
                Debug.LogWarning("Cannot refresh editor; environmentManager is null!");
            }
        }

        public static void SetupTimeManager()
        {
            CelestialCycleEditor instance = EditorWindow.GetWindow<CelestialCycleEditor>();
            instance.SetupTimeManagerInternal();
        }

        private void SetupTimeManagerInternal()
        {
            if (FindObjectOfType<CelestialTimeManager>() == null)
            {
                SpawnEnvironmentManager();
                GeneratePath();
                GenerateEnvironmentSettings();
                CreateSkyboxMaterial();
            }
        }
        bool CheckSkybox()
        {
            CelestialTimeManager sceneManager = FindObjectOfType<CelestialTimeManager>();

            if (sceneManager != null && sceneManager.sceneSkybox != null)
            {
                return true; // ✅ Scene Skybox exists, no need to regenerate.
            }

            return RenderSettings.skybox != null && RenderSettings.skybox.name.Contains("CelestialSkybox");
        }


        public static void DrawHeader()
        {
            /// 
            /// Info & Help
            /// 

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Asset Store", EditorStyles.miniButton, GUILayout.Width(120)))
            {
                Application.OpenURL("https://assetstore.unity.com/publishers/41999");
            }

            if (GUILayout.Button("Documentation", EditorStyles.miniButton))
            {
                Application.OpenURL("https://docs.google.com/document/d/1RwEpgVMH7uIRCWsu2b3Bi4yRK1iiSg-8dByGHPyc7hM/edit");
            }

            EditorGUILayout.EndHorizontal();

        }
        private void SpawnEnvironmentManager()
        {
            // Find the Directional Light in the scene
            Light sunLight = FindObjectsOfType<Light>().FirstOrDefault(light => light.type == LightType.Directional);

            if (sunLight == null)
            {
                Debug.LogError("No Directional Light found in the scene.");
                return;
            }

            // Spawn the GameObject and add the CelestialTimeManager script
            GameObject environmentManagerObj = new GameObject("Celestial Time Manager");
            CelestialTimeManager environmentManagerScript = environmentManagerObj.AddComponent<CelestialTimeManager>();

            // Set the sunTransform variable
            environmentManagerScript.sunTransform = sunLight.transform;

            Debug.Log("Celestial Time Manager spawned and configured successfully.");
        }

        private void GenerateEnvironmentSettings()
        {

            if (FindObjectOfType<CelestialTimeManager>() == null)
            {
                Debug.LogWarning("No CelestialTimeManager found. Creating one...");
                SpawnEnvironmentManager();
            }
            // We are only generating one season for time management – we'll use "Default".
            string folderName = sceneName + " Profile";
            int s = 0; // Index for Default season
                       // Profile types: index 0 = Manager, 1 = Morning, 2 = Noon, 3 = Evening, 4 = Night.
            string[] profileNames = { "Manager", "Morning", "Noon", "Evening", "Night" };

            // Create a folder for the current season under assetRelativePath.
            string seasonFolderPath = Path.Combine(assetRelativePath, folderName);
            if (!AssetDatabase.IsValidFolder(seasonFolderPath))
            {
                AssetDatabase.CreateFolder(assetRelativePath, folderName);
            }

            CelestialCyclePeriod[] environmentSettings = new CelestialCyclePeriod[4];
            for (int i = 1; i < profileNames.Length; i++)
            {
                string presetPath = $"Season_Time/{s}_{i} {profileNames[i]}";
                CelestialCyclePeriod preset = Resources.Load<CelestialCyclePeriod>(presetPath);
                if (preset == null)
                {
                    Debug.LogError($"Could not load preset for {folderName} {profileNames[i]} from {presetPath}");
                    continue;
                }
                CelestialCyclePeriod setting = Instantiate(preset);
                string assetName = $"{sceneName}_{profileNames[i]}.asset";
                string settingPath = Path.Combine(seasonFolderPath, assetName);
                settingPath = AssetDatabase.GenerateUniqueAssetPath(settingPath);
                AssetDatabase.CreateAsset(setting, settingPath);
                environmentSettings[i - 1] = setting;  // index 0 = Morning, etc.
            }

            // --- Generate the Manager asset (profile index 0) ---
            string managerPresetPath = $"Season_Time/{s}_0 {profileNames[0]}";
            CelestialCycleDay managerPreset = Resources.Load<CelestialCycleDay>(managerPresetPath);
            if (managerPreset == null)
            {
                Debug.LogError($"Could not load Manager preset for {folderName} from {managerPresetPath}");
                return;
            }
            CelestialCycleDay timeManagerAsset = Instantiate(managerPreset);
            string managerAssetName = $"{sceneName}_Manager.asset";
            string managerAssetPath = Path.Combine(seasonFolderPath, managerAssetName);
            managerAssetPath = AssetDatabase.GenerateUniqueAssetPath(managerAssetPath);
            AssetDatabase.CreateAsset(timeManagerAsset, managerAssetPath);

            // Assign the generated period profiles to the Manager asset.
            timeManagerAsset.morningSettings = environmentSettings[0];
            timeManagerAsset.noonSettings = environmentSettings[1];
            timeManagerAsset.eveningSettings = environmentSettings[2];
            timeManagerAsset.nightSettings = environmentSettings[3];

            // Assign the generated period profiles to the Manager asset.
            timeManagerAsset.morningSettings = environmentSettings[0];
            timeManagerAsset.noonSettings = environmentSettings[1];
            timeManagerAsset.eveningSettings = environmentSettings[2];
            timeManagerAsset.nightSettings = environmentSettings[3];

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            existingCCDay = timeManagerAsset;
            Debug.Log($"{folderName} Celestial Cycle Day Profile and Celestial Cycle Period Profiles created successfully.");

            // Now assign the generated Default profile to the CelestialTimeManager in the scene.
            CelestialTimeManager sceneManager = FindObjectOfType<CelestialTimeManager>();
            if (sceneManager != null)
            {
                sceneManager.timeOfDayController = timeManagerAsset;
                EditorUtility.SetDirty(sceneManager);

                // If your CelestialTimeManager has an ApplyTimeSettings() method, call it to refresh the settings.
                var applyMethod = sceneManager.GetType().GetMethod("ApplyTimeSettings");
                if (applyMethod != null)
                {
                    applyMethod.Invoke(sceneManager, null);
                }
                Debug.Log("Generated Default Celestial Cycle Day Profile assigned to Celestial Time Manager and settings applied.");
            }
            else
            {
                Debug.LogWarning("No CelestialTimeManager found in the scene to assign generated profiles.");
            }
        }

        private void CreateSkyboxMaterial()
        {
            CelestialTimeManager sceneManager = FindObjectOfType<CelestialTimeManager>();
            if (sceneManager == null)
            {
                Debug.LogError("CelestialTimeManager not found in the scene!");
                return;
            }

            Material originalSkyboxMaterial = Resources.Load<Material>("CelestialSkybox");
            if (originalSkyboxMaterial == null)
            {
                Debug.LogError("Skybox material 'CelestialSkybox' not found in Resources.");
                return;
            }

            // ✅ Create a new skybox material instance
            Material newSkybox = new Material(originalSkyboxMaterial);
            string skyboxPath = Path.Combine(assetRelativePath, sceneName + "_CelestialSkybox.mat");
            skyboxPath = AssetDatabase.GenerateUniqueAssetPath(skyboxPath);

            AssetDatabase.CreateAsset(newSkybox, skyboxPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ✅ Store in Time Manager
            sceneManager.sceneSkybox = newSkybox;
            EditorUtility.SetDirty(sceneManager);

            // ✅ First-time application to RenderSettings.skybox
            if (!RenderSettings.skybox.name.Contains("CelestialSkybox"))
            {
                RenderSettings.skybox = newSkybox;
            }

            Debug.Log("✅ New skybox material created and applied to CelestialTimeManager & RenderSettings.");
        }
        private void BackupCurrentScene()
        {
            var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(currentScene.path))
            {
                Debug.LogError("Please save the current scene before creating a backup.");
                return;
            }

            string baseName = sceneName + "_Backup";
            string extension = ".unity";
            string backUpPath = Path.Combine(assetRelativePath, baseName + extension);

            int counter = 1;
            while (File.Exists(backUpPath))
            {
                backUpPath = Path.Combine(assetRelativePath, $"{baseName}_{counter}{extension}");
                counter++;
            }

            bool saveOK = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene, backUpPath, true);
            if (saveOK)
            {
                isSceneBackedUp = true;
                backupFilePath = backUpPath;
                Debug.Log($"Scene backup saved to: {backUpPath}");
            }
            else
            {
                Debug.LogError($"Failed to save scene backup to: {backUpPath}");
            }
        }

    }
}
