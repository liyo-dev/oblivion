using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Ventana de editor para previsualizar y ajustar combinaciones de cara (mesh de ojos/boca)
/// y animación corporal por emoción, sin tener que adivinar nombres a ciegas ni depender
/// del sistema de diálogo en Play Mode.
///
/// Funciona en dos modos:
///  - Edit Mode: usa <see cref="AnimationMode"/> para samplear el clip del estado elegido
///    directamente sobre el modelo (scrub/loop en tiempo real, sin pulsar Play).
///  - Play Mode: usa el Animator real (animator.Play) para que se vea exactamente como en juego.
///
/// También audita el EmotionProfile asignado: para cada emoción comprueba que el mesh de ojos,
/// el mesh de boca y el estado de animación corporal configurados existen de verdad en el NPC
/// y en el Animator Controller seleccionados, y avisa si falta alguna entrada (p.ej. una emoción
/// del enum sin fila en el asset).
///
/// Menú: El Sendero > NPCs > Testeo de Emociones y Animación
/// </summary>
public class NPCEmotionTesterWindow : EditorWindow
{
    [MenuItem("El Sendero/NPCs/Testeo de Emociones y Animación")]
    private static void Open()
    {
        GetWindow<NPCEmotionTesterWindow>("Emociones NPC").Show();
    }

    #region Estado de la ventana

    private GameObject _targetNpc;
    private Animator _animator;
    private EmotionProfile _profile;

    private string _eyePrefix = "Eye";
    private string _mouthPrefix = "Mouth";

    // Meshes detectados en el NPC objetivo (nombre -> GameObject)
    private readonly Dictionary<string, GameObject> _eyeMeshes = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _mouthMeshes = new Dictionary<string, GameObject>();

    // Snapshot de la cara original para poder restaurarla
    private Dictionary<string, bool> _originalEyeActive;
    private Dictionary<string, bool> _originalMouthActive;

    // Estados del Animator Controller efectivo, por capa (nombre de capa -> nombre de estado -> AnimatorState)
    private string[] _layerNames = new string[0];
    private List<Dictionary<string, AnimatorState>> _statesByLayer = new List<Dictionary<string, AnimatorState>>();
    private int _layerIndex;

    // Selección actual en los desplegables
    private NPCEmotion _selectedEmotion = NPCEmotion.Happy;
    private string _selectedEye;
    private string _selectedMouth;
    private string _selectedBodyState;

    // Previsualización de animación en Edit Mode
    private AnimationClip _previewClip;
    private float _previewTime;
    private double _lastEditorTime;
    private bool _isPreviewingAnim;

    private Vector2 _scroll;
    private bool _showAudit = true;

    #endregion

    private void OnDisable()
    {
        StopAnimationPreview();
    }

    private void OnDestroy()
    {
        StopAnimationPreview();
        RestoreOriginalFace();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Arrastra un NPC de la escena (con Animator, y opcionalmente NPCEmotionController) y, si quieres, " +
            "un EmotionProfile. Elige cara y animación en los desplegables y pulsa Previsualizar para verlo " +
            "directamente sobre el modelo, sin entrar en Play Mode.",
            MessageType.Info);

        EditorGUILayout.Space();
        DrawTargetSelection();

        if (_targetNpc == null)
        {
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        DrawFaceAndAnimationControls();

        EditorGUILayout.Space();
        DrawPreviewButtons();

        if (_profile != null)
        {
            EditorGUILayout.Space();
            DrawSaveToProfile();

            EditorGUILayout.Space();
            DrawAudit();
        }

        EditorGUILayout.EndScrollView();
    }

    #region Selección de objetivo

    private void DrawTargetSelection()
    {
        EditorGUI.BeginChangeCheck();
        var newTarget = (GameObject)EditorGUILayout.ObjectField("NPC (escena)", _targetNpc, typeof(GameObject), true);
        var newProfile = (EmotionProfile)EditorGUILayout.ObjectField("Emotion Profile", _profile, typeof(EmotionProfile), false);
        bool targetChanged = EditorGUI.EndChangeCheck();

        if (targetChanged)
        {
            if (newTarget != _targetNpc)
            {
                RestoreOriginalFace();
                StopAnimationPreview();
                _targetNpc = newTarget;
                RefreshFromTarget();
            }

            _profile = newProfile;
        }

        if (_targetNpc == null)
        {
            EditorGUILayout.HelpBox("Ningún NPC seleccionado.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Animator", _animator != null ? "OK" : "NO ENCONTRADO",
            _animator != null ? EditorStyles.label : EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Ojos: {_eyeMeshes.Count}   Boca: {_mouthMeshes.Count}   Capas: {_layerNames.Length}");
        if (GUILayout.Button("Refrescar", GUILayout.Width(80)))
        {
            RefreshFromTarget();
        }
        EditorGUILayout.EndHorizontal();

        if (_animator == null)
        {
            EditorGUILayout.HelpBox("El NPC no tiene componente Animator (ni en él ni en sus hijos).", MessageType.Error);
        }
    }

    private void RefreshFromTarget()
    {
        _eyeMeshes.Clear();
        _mouthMeshes.Clear();
        _originalEyeActive = null;
        _originalMouthActive = null;
        _statesByLayer.Clear();
        _layerNames = new string[0];
        _layerIndex = 0;
        _selectedEye = null;
        _selectedMouth = null;
        _selectedBodyState = null;

        if (_targetNpc == null)
            return;

        // Si el NPC tiene NPCEmotionController, respetar sus prefijos configurados
        var emotionController = _targetNpc.GetComponent<NPCEmotionController>();
        if (emotionController != null)
        {
            var so = new SerializedObject(emotionController);
            var eyePrefixProp = so.FindProperty("eyePrefix");
            var mouthPrefixProp = so.FindProperty("mouthPrefix");
            if (eyePrefixProp != null && !string.IsNullOrEmpty(eyePrefixProp.stringValue))
                _eyePrefix = eyePrefixProp.stringValue;
            if (mouthPrefixProp != null && !string.IsNullOrEmpty(mouthPrefixProp.stringValue))
                _mouthPrefix = mouthPrefixProp.stringValue;

            if (_profile == null)
            {
                var profileProp = so.FindProperty("emotionProfile");
                if (profileProp != null)
                    _profile = profileProp.objectReferenceValue as EmotionProfile;
            }
        }

        CacheMeshesRecursive(_targetNpc.transform);
        SnapshotOriginalFace();

        _animator = _targetNpc.GetComponentInChildren<Animator>(true);
        if (_animator != null)
        {
            CacheAnimatorStates();
        }
    }

    private void CacheMeshesRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith(_eyePrefix) && !_eyeMeshes.ContainsKey(child.name))
                _eyeMeshes[child.name] = child.gameObject;

            if (child.name.StartsWith(_mouthPrefix) && !_mouthMeshes.ContainsKey(child.name))
                _mouthMeshes[child.name] = child.gameObject;

            CacheMeshesRecursive(child);
        }
    }

    private void SnapshotOriginalFace()
    {
        _originalEyeActive = _eyeMeshes.ToDictionary(kv => kv.Key, kv => kv.Value != null && kv.Value.activeSelf);
        _originalMouthActive = _mouthMeshes.ToDictionary(kv => kv.Key, kv => kv.Value != null && kv.Value.activeSelf);
    }

    private void CacheAnimatorStates()
    {
        AnimatorController controller = ResolveEffectiveAnimatorController(_animator);
        if (controller == null)
            return;

        _layerNames = controller.layers.Select(l => l.name).ToArray();
        _statesByLayer = new List<Dictionary<string, AnimatorState>>(_layerNames.Length);

        foreach (var layer in controller.layers)
        {
            var dict = new Dictionary<string, AnimatorState>();
            if (layer.stateMachine != null)
                CollectStatesRecursive(layer.stateMachine, dict);
            _statesByLayer.Add(dict);
        }

        _layerIndex = Mathf.Clamp(_layerIndex, 0, Mathf.Max(0, _layerNames.Length - 1));
    }

    /// <summary>
    /// Resuelve el AnimatorController base de un Animator, desenvolviendo la cadena
    /// de AnimatorOverrideController si la hubiera. Reemplazo local porque
    /// AnimatorController.GetEffectiveAnimatorController ya no existe en esta versión de Unity.
    /// </summary>
    private static AnimatorController ResolveEffectiveAnimatorController(Animator animator)
    {
        if (animator == null)
            return null;

        RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
        while (runtimeController is AnimatorOverrideController overrideController)
            runtimeController = overrideController.runtimeAnimatorController;

        return runtimeController as AnimatorController;
    }

    private static void CollectStatesRecursive(AnimatorStateMachine sm, Dictionary<string, AnimatorState> outStates)
    {
        foreach (var childState in sm.states)
        {
            if (childState.state != null && !outStates.ContainsKey(childState.state.name))
                outStates[childState.state.name] = childState.state;
        }

        foreach (var childSm in sm.stateMachines)
        {
            if (childSm.stateMachine != null)
                CollectStatesRecursive(childSm.stateMachine, outStates);
        }
    }

    #endregion

    #region Controles de cara / animación

    private void DrawFaceAndAnimationControls()
    {
        EditorGUILayout.LabelField("Selección", EditorStyles.boldLabel);

        if (_profile != null)
        {
            EditorGUILayout.BeginHorizontal();
            _selectedEmotion = (NPCEmotion)EditorGUILayout.EnumPopup("Emoción (perfil)", _selectedEmotion);
            if (GUILayout.Button("Cargar desde perfil", GUILayout.Width(150)))
            {
                var data = _profile.GetEmotionData(_selectedEmotion);
                _selectedEye = data.eyeMeshName;
                _selectedMouth = data.mouthMeshName;
                _selectedBodyState = data.bodyAnimStateName;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        // Desplegable de mesh de ojos
        _selectedEye = DrawStringPopup("👁 Mesh de ojos", _selectedEye, _eyeMeshes.Keys.OrderBy(n => n).ToArray());

        // Desplegable de mesh de boca
        _selectedMouth = DrawStringPopup("👄 Mesh de boca", _selectedMouth, _mouthMeshes.Keys.OrderBy(n => n).ToArray());

        // Capa del Animator (Base Layer, UpperBody, etc.)
        if (_layerNames.Length > 0)
        {
            _layerIndex = EditorGUILayout.Popup("Capa del Animator", _layerIndex, _layerNames);
            var statesInLayer = _statesByLayer[_layerIndex].Keys.OrderBy(n => n).ToArray();
            _selectedBodyState = DrawStringPopup("🏃 Animación corporal", _selectedBodyState, statesInLayer);
        }
        else
        {
            EditorGUILayout.HelpBox("No se han detectado estados de Animator (¿falta el Animator o su controller?).", MessageType.Warning);
        }
    }

    private static string DrawStringPopup(string label, string current, string[] options)
    {
        if (options.Length == 0)
        {
            EditorGUILayout.LabelField(label, "(ninguno detectado)");
            return current;
        }

        var withEmpty = new string[options.Length + 1];
        withEmpty[0] = "(ninguno)";
        System.Array.Copy(options, 0, withEmpty, 1, options.Length);

        int currentIndex = string.IsNullOrEmpty(current) ? 0 : System.Array.IndexOf(withEmpty, current);
        if (currentIndex < 0) currentIndex = 0;

        int newIndex = EditorGUILayout.Popup(label, currentIndex, withEmpty);
        return newIndex == 0 ? string.Empty : withEmpty[newIndex];
    }

    #endregion

    #region Previsualización

    private void DrawPreviewButtons()
    {
        EditorGUILayout.LabelField("Previsualización", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("👁 Previsualizar cara"))
        {
            ApplyFacePreview();
        }

        if (GUILayout.Button("↩ Restaurar cara original"))
        {
            RestoreOriginalFace();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_selectedBodyState)))
        {
            if (GUILayout.Button("▶ Reproducir animación"))
            {
                PlaySelectedAnimation();
            }
        }

        using (new EditorGUI.DisabledScope(!_isPreviewingAnim && !EditorApplication.isPlaying))
        {
            if (GUILayout.Button("⏹ Detener animación"))
            {
                StopAnimationPreview();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (!EditorApplication.isPlaying && _isPreviewingAnim && _previewClip != null)
        {
            EditorGUILayout.LabelField($"Reproduciendo (Edit Mode): {_previewClip.name}  ({_previewTime:F2}s / {_previewClip.length:F2}s)");
        }
        else if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode activo: la animación se reproduce con el Animator real del NPC.", MessageType.None);
        }
    }

    private void ApplyFacePreview()
    {
        if (_originalEyeActive == null)
            SnapshotOriginalFace();

        SetActiveMesh(_eyeMeshes, _selectedEye);
        SetActiveMesh(_mouthMeshes, _selectedMouth);
    }

    private static void SetActiveMesh(Dictionary<string, GameObject> meshes, string nameToActivate)
    {
        foreach (var kvp in meshes)
        {
            if (kvp.Value == null) continue;
            bool shouldBeActive = kvp.Key == nameToActivate;
            if (kvp.Value.activeSelf != shouldBeActive)
                kvp.Value.SetActive(shouldBeActive);
        }
    }

    private void RestoreOriginalFace()
    {
        if (_originalEyeActive == null)
            return;

        foreach (var kvp in _eyeMeshes)
        {
            if (kvp.Value != null && _originalEyeActive.TryGetValue(kvp.Key, out bool active))
                kvp.Value.SetActive(active);
        }

        foreach (var kvp in _mouthMeshes)
        {
            if (kvp.Value != null && _originalMouthActive.TryGetValue(kvp.Key, out bool active))
                kvp.Value.SetActive(active);
        }
    }

    private void PlaySelectedAnimation()
    {
        if (_animator == null || string.IsNullOrEmpty(_selectedBodyState))
            return;

        if (EditorApplication.isPlaying)
        {
            // Play Mode: dejar que el Animator real lo reproduzca (se ve exactamente como en juego)
            _animator.Play(Animator.StringToHash(_selectedBodyState), _layerIndex, 0f);
            return;
        }

        // Edit Mode: resolver el clip del estado y samplearlo con AnimationMode
        if (!_statesByLayer[_layerIndex].TryGetValue(_selectedBodyState, out var state))
        {
            Debug.LogWarning($"[NPCEmotionTester] Estado '{_selectedBodyState}' no encontrado en la capa seleccionada.");
            return;
        }

        var clip = ResolveClip(state.motion);
        if (clip == null)
        {
            Debug.LogWarning($"[NPCEmotionTester] El estado '{_selectedBodyState}' no tiene un AnimationClip que se pueda previsualizar (¿Blend Tree vacío?).");
            return;
        }

        StartAnimationPreview(clip);
    }

    private static AnimationClip ResolveClip(Motion motion)
    {
        if (motion is AnimationClip clip)
            return clip;

        if (motion is BlendTree tree)
        {
            foreach (var child in tree.children)
            {
                var resolved = ResolveClip(child.motion);
                if (resolved != null)
                    return resolved;
            }
        }

        return null;
    }

    private void StartAnimationPreview(AnimationClip clip)
    {
        StopAnimationPreview();

        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        _previewClip = clip;
        _previewTime = 0f;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        _isPreviewingAnim = true;

        EditorApplication.update += TickAnimationPreview;

        // Muestra el primer frame inmediatamente
        SampleCurrentFrame();
    }

    private void TickAnimationPreview()
    {
        if (_previewClip == null || _targetNpc == null)
        {
            StopAnimationPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - _lastEditorTime);
        _lastEditorTime = now;
        _previewTime += delta;

        float length = Mathf.Max(0.01f, _previewClip.length);
        _previewTime = _previewClip.isLooping ? _previewTime % length : Mathf.Min(_previewTime, length);

        SampleCurrentFrame();
        Repaint();
    }

    private void SampleCurrentFrame()
    {
        if (_previewClip == null || _animator == null)
            return;

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_animator.gameObject, _previewClip, _previewTime);
        AnimationMode.EndSampling();
        SceneView.RepaintAll();
    }

    private void StopAnimationPreview()
    {
        EditorApplication.update -= TickAnimationPreview;
        _isPreviewingAnim = false;
        _previewClip = null;

        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
    }

    #endregion

    #region Guardado en el Emotion Profile

    private void DrawSaveToProfile()
    {
        EditorGUILayout.LabelField("Guardar en el Emotion Profile", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            $"Esto sobreescribe (o crea) la fila de '{_selectedEmotion}' en '{_profile.name}' con la cara y " +
            "animación seleccionadas arriba.",
            MessageType.None);

        if (GUILayout.Button($"💾 Guardar combinación para '{_selectedEmotion}'"))
        {
            SaveCurrentSelectionToProfile();
        }
    }

    private void SaveCurrentSelectionToProfile()
    {
        Undo.RecordObject(_profile, "Actualizar Emotion Profile desde Tester");

        var emotions = _profile.emotions ?? new EmotionMeshData[0];
        int index = System.Array.FindIndex(emotions, e => e.emotion == _selectedEmotion);

        var newData = new EmotionMeshData
        {
            emotion = _selectedEmotion,
            eyeMeshName = _selectedEye,
            mouthMeshName = _selectedMouth,
            bodyAnimStateName = _selectedBodyState
        };

        if (index >= 0)
        {
            emotions[index] = newData;
        }
        else
        {
            var list = emotions.ToList();
            list.Add(newData);
            emotions = list.ToArray();
        }

        _profile.emotions = emotions;

        EditorUtility.SetDirty(_profile);
        AssetDatabase.SaveAssets();

        Debug.Log($"[NPCEmotionTester] Guardado '{_selectedEmotion}' -> Ojos:{_selectedEye} Boca:{_selectedMouth} Anim:{_selectedBodyState} en {_profile.name}");
    }

    #endregion

    #region Auditoría

    private void DrawAudit()
    {
        _showAudit = EditorGUILayout.Foldout(_showAudit, "🔍 Auditoría del Emotion Profile", true);
        if (!_showAudit)
            return;

        EditorGUI.indentLevel++;

        var allEmotions = System.Enum.GetValues(typeof(NPCEmotion)).Cast<NPCEmotion>()
            .Where(e => e != NPCEmotion.None);

        var configured = new HashSet<NPCEmotion>((_profile.emotions ?? new EmotionMeshData[0]).Select(e => e.emotion));

        foreach (var emotion in allEmotions)
        {
            if (!configured.Contains(emotion))
            {
                EditorGUILayout.HelpBox($"'{emotion}' no tiene fila en el perfil. GetEmotionData() hará fallback a la primera entrada del array ({(_profile.emotions != null && _profile.emotions.Length > 0 ? _profile.emotions[0].emotion.ToString() : "vacío")}).", MessageType.Warning);
            }
        }

        foreach (var data in _profile.emotions ?? new EmotionMeshData[0])
        {
            var problems = new List<string>();

            if (!string.IsNullOrEmpty(data.eyeMeshName) && _eyeMeshes.Count > 0 && !_eyeMeshes.ContainsKey(data.eyeMeshName))
                problems.Add($"mesh de ojos '{data.eyeMeshName}' no existe en este NPC");

            if (!string.IsNullOrEmpty(data.mouthMeshName) && _mouthMeshes.Count > 0 && !_mouthMeshes.ContainsKey(data.mouthMeshName))
                problems.Add($"mesh de boca '{data.mouthMeshName}' no existe en este NPC");

            // Vacío es válido a propósito (emociones que solo cambian cara, sin tocar el cuerpo);
            // solo es un problema si apunta a un estado que no existe.
            if (!string.IsNullOrEmpty(data.bodyAnimStateName) && _statesByLayer.Count > 0
                && !_statesByLayer.Any(layer => layer.ContainsKey(data.bodyAnimStateName)))
            {
                problems.Add($"estado de animación '{data.bodyAnimStateName}' no existe en el Animator Controller de este NPC");
            }

            if (problems.Count > 0)
            {
                EditorGUILayout.HelpBox($"{data.emotion}: {string.Join("; ", problems)}.", MessageType.Error);
            }
        }

        EditorGUI.indentLevel--;
    }

    #endregion
}
