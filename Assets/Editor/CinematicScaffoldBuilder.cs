#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

namespace Editor
{
    public static class CinematicScaffoldBuilder
    {
        [MenuItem("Tools/Cinematics/Create Cinematic (Scaffold)")]
        public static void CreateCinematicScaffold()
        {
            // === 1) Carpeta y Asset ===
            string defaultName = "NewCinematicSequence";
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Cinematic Sequence",
                defaultName,
                "asset",
                "Elige dónde guardar el ScriptableObject de la cinemática."
            );
            if (string.IsNullOrEmpty(path)) return;

            var seq = ScriptableObject.CreateInstance<CinematicSequence>();
            seq.name = Path.GetFileNameWithoutExtension(path);
            seq.timeScale = 1f;
            seq.skippable = true;
            seq.skipKey = KeyCode.Escape;
            seq.lockPlayerInput = true;
            seq.handOffToGameplayOnEnd = true;
            AssetDatabase.CreateAsset(seq, path);
            AssetDatabase.SaveAssets();

            // === 2) Jerarquía de escena ===
            // Runner + Director
            var runner = new GameObject("CinematicRunner");
            var director = runner.AddComponent<CinematicDirector>();
            director.sequence = seq;

            // Camera Rig
            var rig = new GameObject("CameraRig").transform;
            Camera mainCam = Camera.main;
            if (!mainCam)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                mainCam = camGo.GetComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            mainCam.transform.SetParent(rig, false);
            director.targetCamera = mainCam;
            director.cameraRig = rig;

            // Player (opcional): intenta auto-detectar algo que parezca input
            var player = GameObject.FindWithTag("Player");
            if (player)
            {
                director.playerRoot = player;
                // Busca algún componente de input habilitable (PlayerActionManager)
                Behaviour input = player.GetComponent<PlayerActionManager>();
                if (!input)
                {
                    // Intentar buscar otros componentes comunes de input por nombre
                    var components = player.GetComponents<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        if (comp != null && comp.GetType().Name.Contains("Input"))
                        {
                            input = comp;
                            break;
                        }
                    }
                }
                director.playerInput = input;
            }

            // === 3) Waypoints y LookAt ===
            var worldRefs = new GameObject("CinematicRefs").transform;

            // PathRoot (P0 cielo, P1 ciudad, P2 casa)
            var pathRoot = new GameObject("PathRoot").transform;
            pathRoot.SetParent(worldRefs, false);

            var p0 = new GameObject("P0_CieloAlto").transform;   p0.SetParent(pathRoot, false); p0.position = new Vector3(0, 80, -80);
            var p1 = new GameObject("P1_SobreCiudad").transform; p1.SetParent(pathRoot, false); p1.position = new Vector3(0, 40, -20);
            var p2 = new GameObject("P2_FrenteCasaAlex").transform; p2.SetParent(pathRoot, false); p2.position = new Vector3(12, 6, -5);

            // LookAts
            var cityLook = new GameObject("LookAt_Ciudad").transform; cityLook.SetParent(worldRefs, false); cityLook.position = new Vector3(0, 10, 0);
            var casaAlex = new GameObject("LookAt_CasaAlex").transform; casaAlex.SetParent(worldRefs, false); casaAlex.position = new Vector3(12, 6, 0);
            var camaAlex = new GameObject("LookAt_CamaAlex").transform; camaAlex.SetParent(worldRefs, false); camaAlex.position = new Vector3(13, 2, 2);

            // === 4) Shots de ejemplo (editables) ===
            // Shot 1: CameraMove por path desde cielo a ciudad
            {
                var s = new CinematicSequence.Shot
                {
                    name = "01_FlyOverCiudad",
                    type = CinematicSequence.ShotType.CameraMove,
                    duration = 7f,
                    ease = AnimationCurve.EaseInOut(0, 0, 1, 1),
                    pathRoot = pathRoot,
                    lookAt = cityLook,
                    targetFOV = 58f,
                    doFadeIn = true,
                    fadeDuration = 0.75f,
                    subtitleText = "Amanece sobre el Reino...",
                    subtitleLeadIn = 0.3f,
                    subtitleHold = 2.5f,
                    subtitleFade = 0.25f
                };
                seq.shots.Add(s);
            }
            // Shot 2: Focus a casa de Alex
            {
                var s = new CinematicSequence.Shot
                {
                    name = "02_EnfocarCasaAlex",
                    type = CinematicSequence.ShotType.FocusTarget,
                    duration = 1.8f,
                    ease = AnimationCurve.Linear(0, 0, 1, 1),
                    lookAt = casaAlex
                };
                seq.shots.Add(s);
            }
            // Shot 3: CameraMove acercándose a la ventana/cama
            {
                var s = new CinematicSequence.Shot
                {
                    name = "03_AcercamientoInterior",
                    type = CinematicSequence.ShotType.CameraMove,
                    duration = 2.8f,
                    ease = AnimationCurve.EaseInOut(0, 0, 1, 1),
                    to = camaAlex,           // posición final aproximada = cerca de la cama
                    lookAt = camaAlex,
                    targetFOV = 55f
                };
                seq.shots.Add(s);
            }
            // Shot 4: Texto “Alex duerme…”
            {
                var s = new CinematicSequence.Shot
                {
                    name = "04_Texto_Duerme",
                    type = CinematicSequence.ShotType.ShowText,
                    duration = 0f, // no se usa en ShowText
                    subtitleText = "Alex duerme profundamente...",
                    subtitleLeadIn = 0.2f,
                    subtitleHold = 2.5f,
                    subtitleFade = 0.25f
                };
                seq.shots.Add(s);
            }
            // Shot 5: Fundido rápido a negro (corte)
            {
                var s = new CinematicSequence.Shot
                {
                    name = "05_FadeToBlack",
                    type = CinematicSequence.ShotType.FadeOnly,
                    duration = 0.2f,
                    doFadeOut = true,
                    fadeDuration = 0.5f
                };
                seq.shots.Add(s);
            }
            // Shot 6: Volver desde negro dentro de la casa
            {
                var s = new CinematicSequence.Shot
                {
                    name = "06_FadeFromBlack",
                    type = CinematicSequence.ShotType.FadeOnly,
                    duration = 0.2f,
                    doFadeIn = true,
                    fadeDuration = 0.5f
                };
                seq.shots.Add(s);
            }
            // Shot 7: Pausa mínima antes del handoff
            {
                var s = new CinematicSequence.Shot
                {
                    name = "07_Pausa",
                    type = CinematicSequence.ShotType.Wait,
                    duration = 0.5f
                };
                seq.shots.Add(s);
            }

            // === 5) Cableado final ===
            director.cameraRig = rig;
            EditorUtility.SetDirty(seq);
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            // Seleccionar y enfocar en Hierarchy/Project
            Selection.activeObject = runner;
            EditorGUIUtility.PingObject(seq);

            Debug.Log($"[Cinematics] Creado scaffold de cinemática:\n- Asset: {path}\n- Runner/Director y refs en escena listos.\nEdita los shots en el asset para ajustarlo a tu mundo.");
        }
    }
}
#endif
