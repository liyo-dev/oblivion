using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente que controla las expresiones faciales de un NPC mediante meshes intercambiables.
/// Los personajes tienen múltiples GameObjects para ojos (Eye01, Eye02...) y bocas (Mouth01, Mouth02...)
/// y este componente activa/desactiva el correspondiente según la emoción.
/// </summary>
public class NPCEmotionController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Configuración")]
    [Tooltip("Perfil de emociones que define el mapeo emoción -> meshes")]
    [SerializeField] private EmotionProfile emotionProfile;

    [Header("Estado Original (Antes de Hablar)")]
    [Tooltip("Mesh de ojos que tiene el NPC por defecto (antes de cualquier diálogo)")]
    [SerializeField] private GameObject originalEyeMesh;
    
    [Tooltip("Mesh de boca que tiene el NPC por defecto (antes de cualquier diálogo)")]
    [SerializeField] private GameObject originalMouthMesh;
    
    [Header("Búsqueda Automática")]
    [Tooltip("Si no se asignan meshes originales, buscar meshes que contengan estos prefijos")]
    [SerializeField] private string eyePrefix = "Eye";
    [SerializeField] private string mouthPrefix = "Mouth";
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    #endregion
    
    #region Private Fields
    
    private NPCSimpleAnimator _npcAnimator;
    private Game.NPC.NPCBehaviourManagerV2 _npcManager;

    // Cache de GameObjects de ojos indexados por nombre
    private Dictionary<string, GameObject> _eyeMeshes = new Dictionary<string, GameObject>();

    // Cache de GameObjects de boca indexados por nombre
    private Dictionary<string, GameObject> _mouthMeshes = new Dictionary<string, GameObject>();
    
    // Nombres de los meshes activos antes de empezar el diálogo
    private string _originalEyeMeshName;
    private string _originalMouthMeshName;
    
    // Emoción actual
    private NPCEmotion _currentEmotion = NPCEmotion.Neutral;
    
    // Flag para saber si estamos en diálogo
    private bool _isInDialogue = false;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Awake()
    {
        _npcAnimator = GetComponent<NPCSimpleAnimator>();
        _npcManager = GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        CacheFacialMeshes();
    }
    
    void OnEnable()
    {
        // Suscribirse a eventos del DialogueManager
        DialogueManager.OnDialogueStarted += OnDialogueStarted;
        DialogueManager.OnDialogueClosed += OnDialogueClosed;
        DialogueManager.OnDialogueLineChanged += OnDialogueLineChanged;
    }
    
    void OnDisable()
    {
        // Desuscribirse de eventos
        DialogueManager.OnDialogueStarted -= OnDialogueStarted;
        DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        DialogueManager.OnDialogueLineChanged -= OnDialogueLineChanged;
    }
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Busca y cachea todos los meshes de ojos y boca en el personaje.
    /// </summary>
    private void CacheFacialMeshes()
    {
        _eyeMeshes.Clear();
        _mouthMeshes.Clear();
        
        // Buscar todos los meshes en la jerarquía
        CacheMeshesRecursive(transform);
        
        // Si hay meshes originales asignados, guardar sus nombres
        if (originalEyeMesh != null)
        {
            _originalEyeMeshName = originalEyeMesh.name;
        }
        
        if (originalMouthMesh != null)
        {
            _originalMouthMeshName = originalMouthMesh.name;
        }
        
        if (debugMode)
        {
            Debug.Log($"[NPCEmotionController:{name}] ✅ Encontrados {_eyeMeshes.Count} meshes de ojos, {_mouthMeshes.Count} meshes de boca");
            Debug.Log($"[NPCEmotionController:{name}] 💾 Estado original configurado - Ojos: {_originalEyeMeshName ?? "auto"}, Boca: {_originalMouthMeshName ?? "auto"}");
            
            foreach (var kvp in _eyeMeshes)
                Debug.Log($"  👁️ {kvp.Key} -> {kvp.Value.name}");
            
            foreach (var kvp in _mouthMeshes)
                Debug.Log($"  👄 {kvp.Key} -> {kvp.Value.name}");
        }
    }
    
    private void CacheMeshesRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            // Buscar ojos
            if (child.name.StartsWith(eyePrefix) && !_eyeMeshes.ContainsKey(child.name))
            {
                _eyeMeshes[child.name] = child.gameObject;
            }
            
            // Buscar bocas
            if (child.name.StartsWith(mouthPrefix) && !_mouthMeshes.ContainsKey(child.name))
            {
                _mouthMeshes[child.name] = child.gameObject;
            }
            
            // Buscar recursivamente
            CacheMeshesRecursive(child);
        }
    }
    
    #endregion
    
    #region Dialogue Events
    
    /// <summary>
    /// Callback cuando se inicia un diálogo.
    /// </summary>
    private void OnDialogueStarted(Transform npcInvolved)
    {
        // Solo procesar si este NPC es el involucrado
        if (npcInvolved != transform)
            return;
        
        // Si ya estamos en diálogo, ignorar (evitar doble guardado)
        if (_isInDialogue)
            return;
        
        if (debugMode)
            Debug.Log($"[NPCEmotionController:{name}] 📢 OnDialogueStarted - Guardando estado original");
        
        _isInDialogue = true;
        
        // Guardar los meshes activos actualmente (ANTES de cualquier cambio de emoción)
        SaveOriginalMeshes();
    }
    
    /// <summary>
    /// Callback cuando se cierra un diálogo.
    /// </summary>
    private void OnDialogueClosed(Transform npcInvolved)
    {
        // Restaurar si participamos en este diálogo (como NPC principal o como hablante secundario)
        if (!_isInDialogue)
            return;

        if (debugMode)
            Debug.Log($"[NPCEmotionController:{name}] 📢 OnDialogueClosed - Restaurando estado original");

        _isInDialogue = false;

        // Restaurar meshes originales
        RestoreOriginalMeshes();
    }
    
    /// <summary>
    /// Callback cuando cambia la línea de diálogo.
    /// </summary>
    private void OnDialogueLineChanged(DialogueLine line, Transform npcInvolved)
    {
        bool isMainNpc = npcInvolved == transform;
        string effectiveId = _npcManager?.DialogueCharacterId;
        bool matchesById = !string.IsNullOrEmpty(effectiveId) && line.speakerNameId == effectiveId;
        if (!isMainNpc && !matchesById)
            return;
        
        // ✅ Si es la primera línea y aún no hemos guardado el estado original, hacerlo ahora
        if (!_isInDialogue)
        {
            if (debugMode)
                Debug.Log($"[NPCEmotionController:{name}] 📢 Primera línea detectada - Guardando estado original ANTES de aplicar emoción");
            
            _isInDialogue = true;
            SaveOriginalMeshes();
        }
        
        if (debugMode)
            Debug.Log($"[NPCEmotionController:{name}] 📢 OnDialogueLineChanged - Emoción: {line.emotion}");

        // La cara cambia si hay emoción explícita; None mantiene la expresión actual
        // La animación corporal (Talk01/02/03, Angry, Cheer, etc.) la gestiona DialogueManager directamente
        if (line.emotion != NPCEmotion.None)
            SetEmotion(line.emotion);
    }
    
    #endregion
    
    #region Emotion Control
    
    /// <summary>
    /// Establece la emoción del NPC, activando los meshes correspondientes.
    /// </summary>
    public void SetEmotion(NPCEmotion emotion)
    {
        if (emotionProfile == null)
        {
            if (debugMode)
                Debug.LogWarning($"[NPCEmotionController:{name}] ⚠️ No hay EmotionProfile asignado");
            return;
        }
        
        if (_currentEmotion == emotion)
            return;
        
        if (debugMode)
            Debug.Log($"[NPCEmotionController:{name}] 🎭 Cambiando emoción: {_currentEmotion} -> {emotion}");
        
        _currentEmotion = emotion;
        
        // Obtener datos de la emoción
        var emotionData = emotionProfile.GetEmotionData(emotion);
        
        // Activar mesh de ojos correspondiente
        ActivateMesh(_eyeMeshes, emotionData.eyeMeshName, "ojos");
        
        // Activar mesh de boca correspondiente
        ActivateMesh(_mouthMeshes, emotionData.mouthMeshName, "boca");
    }
    
    /// <summary>
    /// Activa un mesh específico y desactiva los demás del mismo tipo.
    /// </summary>
    private void ActivateMesh(Dictionary<string, GameObject> meshCache, string meshName, string meshType)
    {
        if (string.IsNullOrEmpty(meshName))
            return;
        
        if (!meshCache.TryGetValue(meshName, out GameObject targetMesh))
        {
            if (debugMode)
                Debug.LogWarning($"[NPCEmotionController:{name}] ⚠️ Mesh de {meshType} '{meshName}' no encontrado");
            return;
        }
        
        // Desactivar todos los meshes de este tipo
        foreach (var kvp in meshCache)
        {
            bool shouldBeActive = kvp.Key == meshName;
            if (kvp.Value.activeSelf != shouldBeActive)
            {
                kvp.Value.SetActive(shouldBeActive);
                
                if (debugMode && shouldBeActive)
                    Debug.Log($"[NPCEmotionController:{name}] ✅ Activado {meshType}: {kvp.Key}");
            }
        }
    }
    
    /// <summary>
    /// Guarda los meshes activos actualmente como estado original.
    /// Si hay meshes originales configurados en el inspector, usa esos.
    /// </summary>
    private void SaveOriginalMeshes()
    {
        // Si hay mesh original de ojos configurado en inspector, usarlo
        if (originalEyeMesh != null)
        {
            _originalEyeMeshName = originalEyeMesh.name;
        }
        else
        {
            // Si no, detectar cuál está activo actualmente
            _originalEyeMeshName = null;
            foreach (var kvp in _eyeMeshes)
            {
                if (kvp.Value.activeSelf)
                {
                    _originalEyeMeshName = kvp.Key;
                    break;
                }
            }
        }
        
        // Si hay mesh original de boca configurado en inspector, usarlo
        if (originalMouthMesh != null)
        {
            _originalMouthMeshName = originalMouthMesh.name;
        }
        else
        {
            // Si no, detectar cuál está activo actualmente
            _originalMouthMeshName = null;
            foreach (var kvp in _mouthMeshes)
            {
                if (kvp.Value.activeSelf)
                {
                    _originalMouthMeshName = kvp.Key;
                    break;
                }
            }
        }
        
        if (debugMode)
            Debug.Log($"[NPCEmotionController:{name}] 💾 Estado original guardado - Ojos: {_originalEyeMeshName ?? "ninguno"}, Boca: {_originalMouthMeshName ?? "ninguno"}");
    }
    
    /// <summary>
    /// Restaura los meshes originales.
    /// </summary>
    private void RestoreOriginalMeshes()
    {
        if (!string.IsNullOrEmpty(_originalEyeMeshName))
        {
            ActivateMesh(_eyeMeshes, _originalEyeMeshName, "ojos");
        }
        
        if (!string.IsNullOrEmpty(_originalMouthMeshName))
        {
            ActivateMesh(_mouthMeshes, _originalMouthMeshName, "boca");
        }
        
        _currentEmotion = NPCEmotion.Neutral;
        
        if (debugMode)
            Debug.Log($"[NPCEmotionController:{name}] ↩️ Estado original restaurado");
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Obtiene la emoción actual del NPC.
    /// </summary>
    public NPCEmotion CurrentEmotion => _currentEmotion;
    
    /// <summary>
    /// Indica si el NPC está actualmente en un diálogo.
    /// </summary>
    public bool IsInDialogue => _isInDialogue;
    
    /// <summary>
    /// Fuerza el reset de la expresión facial a la original.
    /// </summary>
    public void ForceReset()
    {
        RestoreOriginalMeshes();
    }
    
    /// <summary>
    /// Establece el perfil de emociones en runtime.
    /// </summary>
    public void SetEmotionProfile(EmotionProfile profile)
    {
        emotionProfile = profile;
    }
    
    /// <summary>
    /// Obtiene la cantidad de meshes de ojos encontrados.
    /// </summary>
    public int EyeMeshCount => _eyeMeshes.Count;
    
    /// <summary>
    /// Obtiene la cantidad de meshes de boca encontrados.
    /// </summary>
    public int MouthMeshCount => _mouthMeshes.Count;
    
    #endregion
}
