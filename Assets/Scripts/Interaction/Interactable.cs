using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;
using Core.InputGlyphs;

[DisallowMultipleComponent]
public class Interactable : MonoBehaviour
{
    // Cooldown global breve para evitar re-disparar interacción justo al cerrar prompts/opciones
    static float s_globalCooldownUntil = 0f;
    [Header("Modo")]
    [SerializeField] private InteractableMode mode = InteractableMode.OpenDialogue;

    [Header("Hint (icono botón)")]
    [SerializeField] private GameObject hint;
    [SerializeField] private bool hideHintAtStart = true;
    [SerializeField] private float hintAnimDuration = 0.25f;
    [Tooltip("Set de sprites (uno por familia de mando/teclado) para el icono de interactuar. " +
             "Referencia DIRECTA (sin Resources.Load) — arrastrar el asset compartido " +
             "InteractionHintIconSet. Si se deja vacío, el icono se queda con el sprite que ya " +
             "tenga puesto a mano en el prefab (comportamiento anterior, sin romper nada).")]
    [SerializeField] private InteractionHintIconSet iconSet;

    [Header("Uso")]
    [SerializeField] private bool singleUse = false;
    [SerializeField] private bool initiallyEnabled = true;

    [Header("Persistencia single-use")]
    [SerializeField] private string persistentIdOverride;
    [SerializeField] private bool includeSceneInPersistentId = true;

    [Header("Abrir diálogo")]
    [SerializeField] private string dialogueCharacterId;
    [SerializeField] private DialogueAsset dialogue;
    [SerializeField] private DialogueAsset yesOption;
    [SerializeField] private DialogueAsset noOption;
    [SerializeField] private DialogueAsset confirmFollowUp;
    [SerializeField] private DialogueAsset cancelFollowUp;

    [Header("Opciones (UnityEvent)")]
    public UnityEvent OnConfirm;

    [Header("Eventos opcionales")]
    public UnityEvent<GameObject> OnInteract;
    public UnityEvent OnStarted;
    public UnityEvent OnFinished;
    public UnityEvent OnConsumed;

    bool used, enabledForUse;
    NPCBehaviourManagerV2 _npcManager;
    NPCPartyMember _partyMember;
    Game.NPC.CompanionFollowPrompt _followPrompt;
    NPCWorldPoint _worldPoint;
    bool _hintVisible;
    Image _hintIcon;

    public bool IsHintVisible => _hintVisible;
    Tweener _hintTween;
    Vector3 _hintOriginalScale;

    [Header("Línea de visión")]
    [Tooltip("Altura sobre la base de este objeto usada como SightPoint cuando no hay 'hint' " +
             "asignado (ver SightPoint). Sin efecto si hint sí está asignado.")]
    [SerializeField] private float defaultSightHeight = 1.4f;

    // FIX (2026-08-12): InteractionDetector.FindNearest() comprobaba línea de visión contra
    // transform.position (la base/los pies del NPC). Cualquier mueble de altura media entre el
    // player y el NPC (mostrador de tienda, mesa...) queda justo a esa altura y se detectaba
    // como obstrucción real aunque el jugador viera al NPC perfectamente por encima — el hint
    // nunca llegaba a mostrarse en ningún vendedor con mostrador delante. En vez de re-etiquetar
    // el layer de cada mueble de cada tienda (frágil y con efectos secundarios en renderizado/
    // física), se expone aquí el punto que SÍ debe comprobarse: por defecto la posición del
    // propio hint, que ya está a la altura de la cabeza/por encima del NPC por diseño (es un
    // icono flotante). Si el jugador puede ver ese punto, un mostrador bajo deja de bloquear.
    //
    // FIX (2026-08-12b): la asunción de arriba ("el hint ya está a la altura de la cabeza") no
    // se cumple para todos los prefabs de NPC — p.ej. Patricia tiene el Canvas del hint colgando
    // directamente de la raíz del prefab con offset local (0,0,0), así que hint.transform.position
    // queda prácticamente a la misma altura que transform.position (por eso el mostrador de su
    // tienda seguía bloqueando el hint incluso después del fix anterior). En vez de depender de
    // que cada prefab tenga el Canvas bien posicionado, se garantiza aquí una altura mínima
    // (defaultSightHeight) sobre la base del NPC: si el hint ya está más alto que eso (el caso
    // "bien configurado"), se usa su posición tal cual; si no, se eleva hasta el mínimo.
    public Vector3 SightPoint
    {
        get
        {
            Vector3 point = hint != null ? hint.transform.position : transform.position + Vector3.up * defaultSightHeight;
            float minY = transform.position.y + defaultSightHeight;
            if (point.y < minY)
                point.y = minY;
            return point;
        }
    }

    void Awake()
    {
        enabledForUse = initiallyEnabled;
        if (hint)
        {
            _hintOriginalScale = hint.transform.localScale;
            _hintIcon = hint.GetComponentInChildren<Image>(true);
            if (hideHintAtStart)
            {
                hint.transform.localScale = Vector3.zero;
                hint.SetActive(false);
            }
        }
        _npcManager = GetComponent<NPCBehaviourManagerV2>();
        _partyMember = GetComponent<NPCPartyMember>() ?? GetComponentInParent<NPCPartyMember>();
        _worldPoint = GetComponent<NPCWorldPoint>();
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += RestoreSingleUseStateFromPreset;
        // Re-aplicar estado single-use también cuando el preset se re-aplique en runtime (tras cargar save)
        PlayerPresetService.OnPresetApplied += HandlePresetApplied;

        if (GameBootService.IsAvailable)
            RestoreSingleUseStateFromPreset();

        // Icono según mando/teclado (sprites baked en iconSet, sin Resources.Load).
        InputGlyphService.FamilyChanged += HandleInputFamilyChanged;
        RefreshHintIcon();
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= RestoreSingleUseStateFromPreset;
        PlayerPresetService.OnPresetApplied -= HandlePresetApplied;
        InputGlyphService.FamilyChanged -= HandleInputFamilyChanged;

        // Si el jugador murió o la escena se descargó mientras un diálogo estaba activo,
        // limpiar las fases que este Interactable pudo haber empujado sin hacer Pop.
        if (GameState.Is(GamePhase.Dialogue)) GameState.Pop(GamePhase.Dialogue);
        if (GameState.Is(GamePhase.SavePrompt)) GameState.Pop(GamePhase.SavePrompt);
    }

    void HandlePresetApplied()
    {
        // Cuando el PlayerPresetService re-aplica el preset (por ejemplo tras Load),
        // refrescar el estado single-use desde el preset activo.
        RestoreSingleUseStateFromPreset();
    }

    void HandleInputFamilyChanged(InputGlyphDeviceFamily _) => RefreshHintIcon();

    /// <summary>
    /// Actualiza el sprite del icono de interactuar según la familia de mando/teclado activa,
    /// usando el set de sprites baked en <see cref="iconSet"/> (referencia directa, sin
    /// Resources.Load). Si no hay iconSet asignado, o le falta el sprite de la familia actual,
    /// no toca nada — se queda con el sprite que ya tuviera el prefab a mano.
    /// </summary>
    void RefreshHintIcon()
    {
        if (iconSet == null || _hintIcon == null) return;

        var sprite = iconSet.GetSprite(InputGlyphService.CurrentFamily);
        if (sprite == null) return;

        _hintIcon.sprite = sprite;
        _hintIcon.preserveAspect = true;
    }

    public void SetHintVisible(bool visible, GameObject interactor = null)
    {
        if (hint == null) return;
        
        // Solo mostrar hint si puede interactuar
        bool canShow = visible && !used && enabledForUse && CanInteract(interactor);
        
        // Si el estado no cambia, no hacer nada
        if (canShow == _hintVisible) return;
        
        _hintVisible = canShow;
        
        // Matar cualquier tween anterior
        _hintTween?.Kill();
        
        if (canShow)
        {
            // Mostrar con animación
            hint.SetActive(true);
            hint.transform.localScale = Vector3.zero;
            _hintTween = hint.transform.DOScale(_hintOriginalScale, hintAnimDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true); // Usar tiempo no escalado por si está en pausa
        }
        else
        {
            // Ocultar con animación
            _hintTween = hint.transform.DOScale(Vector3.zero, hintAnimDuration * 0.7f)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (hint != null)
                        hint.SetActive(false);
                });
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (_partyMember == null)
            _partyMember = GetComponent<NPCPartyMember>() ?? GetComponentInParent<NPCPartyMember>();

        var dm = DialogueManager.Instance;
        if (dm != null && dm.IsOpen)
            return false;
        if (!GameState.CanInteractGlobally)
            return false;
        if (Time.unscaledTime < s_globalCooldownUntil)
            return false;
        if (!enabledForUse)
            return false;
        if (singleUse && used)
            return false;
        // Los NPCs en el equipo no deben ser interactuables (sin botón A ni acción),
        // excepto cuando están en estado "Sígueme"/"Dejar de seguir".
        // Usamos CanShowFollowPrompt() en lugar de IsShowingPrompt para evitar dependencia circular:
        // IsShowingPrompt depende de IsHintVisible, que a su vez depende de CanInteract().
        if (_partyMember != null && _partyMember.IsInParty)
        {
            // Lazy-init: CompanionFollowPrompt se añade dinámicamente y puede no existir en Awake.
            if (_followPrompt == null)
                _followPrompt = GetComponent<Game.NPC.CompanionFollowPrompt>();
            if (_followPrompt == null || !_followPrompt.CanShowFollowPrompt())
                return false;
        }

        // Bloqueo central: durante combate/diálogo/cinemática no permitir nuevas interacciones
        if (IsInteractionBlockedByCurrentState(interactor))
            return false;
        
        // Bloquear interacción si el NPC está ejecutando una narrativa
        var narrativeExecutor = GetComponent<Game.NPC.Modules.NPCInteractiveNarrativeExecutor>();
        if (narrativeExecutor != null && narrativeExecutor.IsExecuting)
            return false;

        // No mostrar el hint si el NPCWorldPoint ya está ocupado (por otro NPC o por el propio player)
        if (_worldPoint != null && _worldPoint.IsOccupied)
            return false;

        // Comprobar proximidad al punto de interacción exacto (evita activar desde lejos)
        if (_worldPoint != null && interactor != null)
        {
            float dist = Vector3.Distance(interactor.transform.position, _worldPoint.InteractionPosition);
            if (dist > _worldPoint.requiredProximity)
                return false;
        }

        return true;
    }

    public void Interact(GameObject interactor)
    {
        // Verificar si este NPC requiere Will como personaje activo
        var willOnly = GetComponent<WillOnlyInteractable>();
        if (willOnly != null && !willOnly.IsWillActive())
        {
            willOnly.ShowFeedback();
            return;
        }

        if (!CanInteract(interactor)) return;

        // IMPORTANTE: Establecer cooldown en PlayerActionManager para evitar que el botón A
        // se procese como salto inmediatamente después de interactuar
        PlayerActionManager actionManager = null;
        if (interactor != null)
        {
            actionManager = interactor.GetComponent<PlayerActionManager>() ??
                            interactor.GetComponentInParent<PlayerActionManager>();
        }
        if (actionManager == null)
        {
            PlayerService.TryGetComponent(out actionManager, includeInactive: true, allowSceneLookup: true);
        }
        if (actionManager != null)
        {
            actionManager.SetInteractCooldown();
        }

        OnInteract?.Invoke(interactor);

        // Si el NPC es miembro del equipo, la interacción fue manejada por CompanionFollowPrompt.
        // No continuar (sin diálogo ni narrativa).
        if (_partyMember != null && _partyMember.IsInParty)
            return;

        // Modo HandOffToTarget: delegar al NPCBehaviourManagerV2 si existe
        if (mode == InteractableMode.HandOffToTarget && _npcManager != null)
        {
            _npcManager.HandleInteraction(interactor);
            return;
        }
        
        // ✅ PRIORIDAD: Si hay un NPCInteractiveNarrativeExecutor con narrativas condicionales,
        // delegar a él en lugar de ejecutar el diálogo por defecto
        var narrativeExecutor = GetComponent<Game.NPC.Modules.NPCInteractiveNarrativeExecutor>();
        if (narrativeExecutor != null)
        {
            var config = narrativeExecutor.GetConfiguration();
            if (config != null && config.HasAvailableNarrative())
            {
                Debug.Log($"[Interactable:{name}] 🎭 Delegando a NPCInteractiveNarrativeExecutor (tiene narrativas condicionales disponibles)");
                bool success = narrativeExecutor.TryExecuteNarrative();
                if (success)
                {
                    Debug.Log($"[Interactable:{name}] ✅ Narrativa condicional ejecutada exitosamente");
                    return; // ✅ Salir - no ejecutar el diálogo por defecto
                }
                else
                {
                    Debug.LogWarning($"[Interactable:{name}] ⚠️ TryExecuteNarrative() falló, usando diálogo por defecto");
                }
            }
            else
            {
                Debug.Log($"[Interactable:{name}] ℹ️ NPCInteractiveNarrativeExecutor existe pero no hay narrativas disponibles, usando diálogo por defecto");
            }
        }
        
        // Si no hay NPCInteractiveNarrativeExecutor o no tiene narrativas disponibles,
        // ejecutar el comportamiento por defecto del Interactable
        switch (mode)
        {
            case InteractableMode.OpenDialogue:
                StartDialogue();
                break;
            case InteractableMode.OpenDialogueWithOptions:
                StartDialogueWithOptions();
                break;
            case InteractableMode.UseWorldPoint:
                StartWorldPointActivity(interactor);
                break;
            case InteractableMode.ConfirmationPopup:
                StartConfirmationPopup();
                break;
        }
    }

    public void InteractWithPlayer()
    {
        TryInteractWithPlayer();
    }

    public bool TryInteractWithPlayer()
    {
        if (!PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) || playerGo == null)
        {
            Debug.LogWarning("[Interactable] Could not locate Player for interaction via PlayerService.");
            return false;
        }

        Interact(playerGo);
        return true;
    }

    private bool IsInteractionBlockedByCurrentState(GameObject interactor)
    {
        // Regla global: si existe combate activo en el mundo, no se permite interactuar.
        if (ActiveCombatRegistry.Count > 0)
        {
            ActiveCombatRegistry.CleanupDestroyedNPCs();
            if (ActiveCombatRegistry.Count > 0)
                return true;
        }

        PlayerActionManager actionManager = null;
        if (interactor != null)
        {
            actionManager = interactor.GetComponent<PlayerActionManager>() ??
                            interactor.GetComponentInParent<PlayerActionManager>();
        }
        if (actionManager == null)
        {
            PlayerService.TryGetComponent(out actionManager, includeInactive: true, allowSceneLookup: true);
        }
        if (actionManager != null && !actionManager.CanInteract())
            return true;

        Game.Player.PlayerBattleModeController battleController = null;
        if (interactor != null)
        {
            battleController = interactor.GetComponent<Game.Player.PlayerBattleModeController>() ??
                               interactor.GetComponentInParent<Game.Player.PlayerBattleModeController>();
        }
        if (battleController == null)
        {
            PlayerService.TryGetComponent(out battleController, includeInactive: true, allowSceneLookup: true);
        }
        if (battleController != null && battleController.IsInBattleMode)
            return true;

        return false;
    }

    void StartDialogue()
    {
        Debug.Log($"[Interactable:{name}] 📖 StartDialogue - dialogue={dialogue?.name}");
        
        // ✅ REPRODUCIR ANIMACIÓN DE INTERACCIÓN (si no es batalla)
        var npcAnimator = GetComponent<NPCSimpleAnimator>();
        if (npcAnimator != null && _npcManager != null)
        {
            // Solo reproducir animación si NO está en combate
            if (_npcManager.Context == null || !_npcManager.Context.IsInCombat)
            {
                StartCoroutine(PlayInteractionAnimation(npcAnimator));
                Debug.Log($"[Interactable:{name}] 🎭 Reproduciendo animación de interacción");
            }
        }
        
        var dm = DialogueManager.Instance;
        if (dialogue && dm != null)
        {
            Debug.Log($"[Interactable:{name}] ✅ Iniciando diálogo: {dialogue.name}");
            OnStarted?.Invoke();
            GameState.Push(GamePhase.Dialogue);

            // ✅ FIX: bloquear Wander/Idle mientras dura el diálogo por defecto.
            // Sin esto, un NPC con enableWander sigue caminando durante todo el diálogo
            // porque este flujo (a diferencia de NPCInteractiveNarrativeExecutor) nunca
            // pasaba por el Brain ni tocaba Context.IsInteracting.
            SetNpcInteracting(true);

            // ✅ NPCSimpleAnimator maneja la rotación del NPC (suscrito a eventos de DialogueManager)
            dm.StartDialogue(dialogue, transform, () =>
            {
                Debug.Log($"[Interactable:{name}] 🔚 Diálogo terminado");
                SetNpcInteracting(false);
                OnFinished?.Invoke();
                if (GameState.Is(GamePhase.Dialogue)) GameState.Pop(GamePhase.Dialogue);
                AfterUse();
            });
        }
        else
        {
            Debug.LogWarning($"[Interactable] No DialogueAsset o DialogueManager en {name}.");
            AfterUse();
        }
    }
    
    /// <summary>
    /// Reproduce la animación de interacción en el NPC
    /// </summary>
    private System.Collections.IEnumerator PlayInteractionAnimation(NPCSimpleAnimator npcAnimator)
    {
        // Reproducir animación de interacción
        npcAnimator.PlayOneShot("InteractWithPeople_NoWeapon", 0, onComplete: null);
        
        // Esperar un frame
        yield return null;
    }

    void StartDialogueWithOptions()
    {
        // ✅ REPRODUCIR ANIMACIÓN DE INTERACCIÓN (si no es batalla)
        var npcAnimator = GetComponent<NPCSimpleAnimator>();
        if (npcAnimator != null && _npcManager != null)
        {
            // Solo reproducir animación si NO está en combate
            if (_npcManager.Context == null || !_npcManager.Context.IsInCombat)
            {
                StartCoroutine(PlayInteractionAnimation(npcAnimator));
                Debug.Log($"[Interactable:{name}] 🎭 Reproduciendo animación de interacción (con opciones)");
            }
        }
        
        var dm = DialogueManager.Instance;
        if (dm == null)
        {
            Debug.LogWarning("[Interactable] DialogueManager no disponible.");
            return;
        }

        // Resolver textos desde DialogueAssets (localizados por el propio manager o por LocalizationManager en SavePoint, según tu flujo)
        string prompt = ResolveDialogueText(dialogue, string.Empty);
        string yes = ResolveDialogueText(yesOption, "Sí");
        string no = ResolveDialogueText(noOption, "No");

        OnStarted?.Invoke();
        // Ocultar el hint del interactable mientras aparecen las opciones
        SetHintVisible(false);
        // Bloquear otros menús mientras se muestran las opciones
        GameState.Push(GamePhase.SavePrompt);

        // ✅ FIX: mismo motivo que StartDialogue() - evitar que el NPC siga vagando
        // mientras se muestra el diálogo con opciones. Se libera en HandleChoiceResult().
        SetNpcInteracting(true);

        // NPCSimpleAnimator maneja la rotación del NPC (suscrito a eventos de DialogueManager)

        try
        {
            dm.ShowWithChoices(prompt, yes, no,
                onYes: () => {
                    HandleChoiceResult(confirmFollowUp, invokeConfirm: true);
                },
                onNo:  () => {
                    HandleChoiceResult(cancelFollowUp, invokeConfirm: false);
                }
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Interactable] ShowWithChoices failed: {ex.Message}\n{ex.StackTrace}");
            if (GameState.Is(GamePhase.SavePrompt)) GameState.Pop(GamePhase.SavePrompt);
            SetNpcInteracting(false);
        }
    }

    /// <summary>
    /// FIX INC-048: variante de StartDialogueWithOptions() que usa el popup de confirmación
    /// unificado (ConfirmationPopupUI) en vez del cuadro de diálogo con opciones. Reutiliza los
    /// mismos campos serializados (dialogue = mensaje, confirmFollowUp/cancelFollowUp = diálogo
    /// opcional posterior, OnConfirm = evento al confirmar) para no duplicar configuración en el
    /// Inspector ni romper los Interactables existentes en modo OpenDialogueWithOptions.
    /// </summary>
    void StartConfirmationPopup()
    {
        var npcAnimator = GetComponent<NPCSimpleAnimator>();
        if (npcAnimator != null && _npcManager != null)
        {
            if (_npcManager.Context == null || !_npcManager.Context.IsInCombat)
                StartCoroutine(PlayInteractionAnimation(npcAnimator));
        }

        var popup = ConfirmationPopupUI.Instance;
        if (popup == null)
        {
            Debug.LogWarning($"[Interactable:{name}] ConfirmationPopupUI.Instance es null (¿Start.unity no está cargada?). Usando diálogo con opciones como fallback.");
            StartDialogueWithOptions();
            return;
        }

        string prompt = ResolveDialogueText(dialogue, string.Empty);

        OnStarted?.Invoke();
        SetHintVisible(false);
        GameState.Push(GamePhase.SavePrompt);

        // ✅ FIX: mismo motivo que StartDialogue() - ver comentario allí.
        SetNpcInteracting(true);

        popup.Show(prompt,
            onConfirm: () => HandleChoiceResult(confirmFollowUp, invokeConfirm: true),
            onCancel:  () => HandleChoiceResult(cancelFollowUp, invokeConfirm: false));
    }

    string ResolveDialogueText(DialogueAsset asset, string fallback)
    {
        if (asset == null || asset.lines == null || asset.lines.Length == 0) return fallback;
        var line = asset.lines[0];
        string textId = line.textId;
        string text = line.text;
        if (!string.IsNullOrEmpty(textId) && LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.Get(textId, string.IsNullOrEmpty(text) ? fallback : text);
        }
        // Si text parece una clave (MAYÚS_CON_GUIONES), intentar localizarla
        if (!string.IsNullOrEmpty(text) && LocalizationManager.Instance != null && LooksLikeKey(text))
        {
            return LocalizationManager.Instance.Get(text, text);
        }
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    bool LooksLikeKey(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return false;
        bool hasUnderscore = trimmed.Contains('_');
        bool isUpper = trimmed.ToUpperInvariant() == trimmed;
        return hasUnderscore && isUpper;
    }

    void HandleChoiceResult(DialogueAsset followUp, bool invokeConfirm)
    {
        if (invokeConfirm)
            OnConfirm?.Invoke();

        if (GameState.Is(GamePhase.SavePrompt))
            GameState.Pop(GamePhase.SavePrompt);

        // Armar un pequeño cooldown tras cerrar el prompt para evitar reabrir al instante
        s_globalCooldownUntil = Time.unscaledTime + 0.25f;

        var dm = DialogueManager.Instance;
        if (followUp != null && dm != null)
        {
            // Sigue habiendo diálogo (follow-up): mantener IsInteracting hasta que también termine.
            dm.StartDialogue(followUp, transform, () =>
            {
                SetNpcInteracting(false);
                OnFinished?.Invoke();
                AfterUse();
            });
        }
        else
        {
            if (dm != null)
                dm.FinalizeChoiceNoFollowUp();
            SetNpcInteracting(false);
            OnFinished?.Invoke();
            AfterUse();
        }
    }

    /// <summary>
    /// Bloquea/libera el movimiento ambiental (Wander/Idle) del NPC mientras dura un diálogo
    /// que no pasa por NPCBrain (StartDialogue, StartDialogueWithOptions, StartConfirmationPopup).
    /// Sin esto, un NPC con enableWander sigue caminando durante el diálogo.
    /// </summary>
    void SetNpcInteracting(bool interacting)
    {
        if (_npcManager?.Context != null)
            _npcManager.Context.IsInteracting = interacting;
    }

    void StartWorldPointActivity(GameObject interactor)
    {
        if (_worldPoint == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[Interactable:{name}] UseWorldPoint configurado pero no hay NPCWorldPoint en este objeto.");
#endif
            return;
        }

        var handler = interactor != null
            ? interactor.GetComponent<PlayerAmbientActivityHandler>()
              ?? interactor.GetComponentInParent<PlayerAmbientActivityHandler>()
            : null;

        if (handler == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[Interactable:{name}] No se encontró PlayerAmbientActivityHandler en el interactor.");
#endif
            return;
        }

        if (!_worldPoint.TryOccupy(interactor.transform))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Interactable:{name}] NPCWorldPoint ya ocupado — no se puede iniciar actividad.");
#endif
            return;
        }

        handler.StartActivity(_worldPoint);
    }

    void AfterUse()
    {
        if (singleUse && !used)
        {
            used = true;
            MarkConsumedInPreset();
            OnConsumed?.Invoke();
        }
        SetHintVisible(false);
    }

    public void EnableInteraction(bool enable)
    {
        enabledForUse = enable;
        if (!enable) SetHintVisible(false);
    }

    public string DialogueCharacterId => dialogueCharacterId;
    public void SetDialogue(DialogueAsset asset) => dialogue = asset;
    public void SetMode(InteractableMode newMode) => mode = newMode;

    internal void RegisterNPCManager(NPCBehaviourManagerV2 manager)
    {
        _npcManager = manager;
    }

    internal void UnregisterNPCManager(NPCBehaviourManagerV2 manager)
    {
        if (_npcManager == manager)
            _npcManager = null;
    }

    string GetPersistentId()
    {
        // Si hay un override explícito, usarlo directamente
        if (!string.IsNullOrEmpty(persistentIdOverride))
            return persistentIdOverride;

        // Construir ID único basado en escena + nombre + posición
        var sceneName = includeSceneInPersistentId && gameObject.scene.IsValid()
            ? gameObject.scene.name
            : string.Empty;

        // Incluir posición para garantizar unicidad entre objetos con el mismo nombre
        var pos = transform.position;
        var posKey = $"{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";

        if (string.IsNullOrEmpty(sceneName))
            return $"{gameObject.name}_{posKey}";
        else
            return $"{sceneName}:{gameObject.name}_{posKey}";
    }

    void MarkConsumedInPreset()
    {
        if (!GameBootService.IsAvailable || !singleUse)
            return;

        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null)
            return;

        var id = GetPersistentId();
        if (string.IsNullOrEmpty(id))
            return;

        preset.consumedInteractableIds ??= new List<string>();
        if (!preset.consumedInteractableIds.Contains(id))
            preset.consumedInteractableIds.Add(id);
    }

    void RestoreSingleUseStateFromPreset()
    {
        if (!singleUse || used)
            return;

        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null || preset.consumedInteractableIds == null)
            return;

        var id = GetPersistentId();
        if (string.IsNullOrEmpty(id))
            return;

        if (preset.consumedInteractableIds.Contains(id))
        {
            used = true;
            SetHintVisible(false);
        }
    }
}
