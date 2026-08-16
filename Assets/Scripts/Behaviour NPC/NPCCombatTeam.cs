using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// Sistema de Equipo de Combate para NPCs.
    /// Permite que múltiples NPCs luchen juntos como un equipo coordinado.
    /// 
    /// Uso: Añadir este componente al NPC líder del equipo y arrastrar los compañeros.
    /// Cuando el líder detecte al jugador, todo el equipo entrará en combate.
    /// </summary>
    [DisallowMultipleComponent]
    public class NPCCombatTeam : MonoBehaviour
    {
        #region Configuration
        
        [Header("Equipo de Combate")]
        [Tooltip("NPCs que forman parte de este equipo. El NPC con este componente es el líder.")]
        [SerializeField] private List<NPCBehaviourManagerV2> teamMembers = new List<NPCBehaviourManagerV2>();
    
    // NOTA (15 ago 2026): regroupDistance/maxRegroupTime/waitForAllMembers se eliminaron —
    // pertenecían a Co_RegroupAndStartCombat/Co_MoveMemberToPosition, borrados al migrar a
    // Co_DetectAndEngage (la detección ahora dispara diálogo+combate al instante, sin caminar a
    // una formación antes). Si algún encuentro concreto necesita ese comportamiento, reintroducir
    // los campos entonces en vez de mantenerlos sin uso.
    [Tooltip("Distancia alrededor del líder donde se posicionarán los compañeros (usado por GetFormationPosition, p.ej. al resucitar como equipo).")]
    [SerializeField] private float formationRadius = 2.5f;

    [Header("Resurrección Grupal")]
    [Tooltip("Si está marcado, todos los miembros del equipo se levantan cuando el combate termina (si aplica).")]
    [SerializeField] private bool resurrectAsTeam = true;
    
    [Tooltip("Delay entre la resurrección de cada miembro (para efecto visual).")]
    [SerializeField] private float resurrectDelay = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    #endregion
    
    #region State
    
    private NPCBehaviourManagerV2 _leaderManager;
    private bool _isTeamInCombat;
    private bool _isRegrouping;
    private bool _isResurrecting; // ✅ FIX #21: guard contra Co_ResurrectTeam concurrentes
    private int _defeatedCount;
    private List<NPCBehaviourManagerV2> _allMembers = new List<NPCBehaviourManagerV2>(); // Líder + compañeros
    private Transform _currentTarget; // ✅ FIX: Referencia al jugador para calcular formación

    // Ancla invisible en el punto medio de los miembros vivos del equipo, usada como "npc" al
    // llamar a DialogueManager.StartBattleDialogue para que la cámara de diálogo (legacy o
    // cinemática) encuadre a TODO el equipo en vez de solo al líder. Creada una vez y reutilizada.
    private Transform _groupFocusAnchor;
    
    // Evento para notificar cuando todo el equipo ha sido derrotado
    public System.Action OnTeamDefeated;
    
    // Evento para notificar cuando el equipo inicia combate
    public System.Action OnTeamCombatStarted;
    
    // Estado de diálogo post-derrota
    public bool IsPostDefeatDialogueFinished { get; private set; }
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// Indica si el equipo está actualmente en combate.
    /// </summary>
    public bool IsTeamInCombat => _isTeamInCombat;
    
    /// <summary>
    /// Indica si el equipo está reagrupándose.
    /// </summary>
    public bool IsRegrouping => _isRegrouping;
    
    /// <summary>
    /// Número de miembros del equipo (incluyendo líder).
    /// </summary>
    public int TeamSize => _allMembers.Count;
    
    /// <summary>
    /// Número de miembros derrotados.
    /// </summary>
    public int DefeatedCount => _defeatedCount;
    
    /// <summary>
    /// Indica si todo el equipo ha sido derrotado.
    /// </summary>
    public bool IsTeamDefeated => _defeatedCount >= _allMembers.Count;
    
    /// <summary>
    /// Lista de todos los miembros del equipo (solo lectura).
    /// </summary>
    public IReadOnlyList<NPCBehaviourManagerV2> AllMembers => _allMembers;
    
    /// <summary>
    /// El líder del equipo (el NPC que tiene este componente).
    /// </summary>
    public NPCBehaviourManagerV2 Leader => _leaderManager;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Awake()
    {
        _leaderManager = GetComponent<NPCBehaviourManagerV2>();
        
        if (_leaderManager == null)
        {
            Debug.LogError($"[NPCCombatTeam] {name}: No se encontró NPCBehaviourManagerV2 en este GameObject!");
            enabled = false;
            return;
        }
        
        // Construir lista completa (líder + compañeros)
        _allMembers.Clear();
        _allMembers.Add(_leaderManager);
        
        foreach (var member in teamMembers)
        {
            if (member != null && member != _leaderManager && !_allMembers.Contains(member))
            {
                _allMembers.Add(member);
            }
        }
        
        if (showDebugLogs)
        {
            // Debug.Log($"[NPCCombatTeam] {name}: Equipo inicializado con {_allMembers.Count} miembros");
        }
    }
    
    void Start()
    {
        // Suscribirse a eventos de detección del líder
        if (_leaderManager != null)
        {
            // Registrar este equipo en cada miembro para que sepan que pertenecen a un equipo
            foreach (var member in _allMembers)
            {
                var teamLink = member.gameObject.GetComponent<NPCTeamMember>();
                if (teamLink == null)
                {
                    teamLink = member.gameObject.AddComponent<NPCTeamMember>();
                }
                teamLink.SetTeam(this, member == _leaderManager);
            }
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Llamado cuando CUALQUIER miembro del equipo detecta al jugador (da igual cuál — líder o
    /// no). Dispara la secuencia completa: todos se paran y miran al jugador, cada uno dice su
    /// propia frase de entrada (empezando por quien detectó) y combate para todos a la vez. Ver
    /// Co_DetectAndEngage.
    /// Devuelve true si la notificación prendió (o el equipo ya estaba en marcha), false si fue
    /// rechazada (bloqueo global de combate) — el llamador (NPCTeamMember.TryNotifyTeamOfPlayer)
    /// usa este valor para no marcar la notificación como "hecha" cuando en realidad no lo fue.
    /// </summary>
    /// <param name="detector">
    /// ✅ NUEVO (15 ago 2026, a petición de Raúl): el miembro del equipo que detectó al jugador.
    /// Habla primero en la secuencia de diálogo de entrada. Puede ser null (p.ej. si algún día se
    /// llama desde otro sitio que no sea NPCTeamMember) — en ese caso se usa el orden por defecto
    /// (líder primero).
    /// </param>
    public bool OnPlayerDetected(Transform player, NPCBehaviourManagerV2 detector = null)
    {
        if (_isTeamInCombat || _isRegrouping) return true; // ya en marcha, nada que reintentar

        // Bloqueo global: si ya hay un combate activo y ningún miembro de este equipo
        // está en combate todavía, no iniciar un segundo combate paralelo.
        ActiveCombatRegistry.CleanupDestroyedNPCs();
        if (ActiveCombatRegistry.Count > 0 && !IsAnyTeamMemberInCombat())
        {
            if (showDebugLogs)
            {
                Debug.Log($"[NPCCombatTeam] {name}: Combate global activo, se cancela nueva detección del equipo.");
            }
            // ✅ FIX (auditoría combate, 15 ago 2026): antes esto era void y el llamador se
            // quedaba con _hasNotifiedTeam=true para siempre aunque la notificación se
            // descartara aquí — el equipo quedaba sordo a la detección del jugador de por vida.
            // Ahora se devuelve false para que el llamador pueda reintentar más adelante.
            return false;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: ¡Jugador detectado! Iniciando secuencia de equipo...");
        }

        StartCoroutine(Co_DetectAndEngage(player, detector));
        return true;
    }

    private bool IsAnyTeamMemberInCombat()
    {
        for (int i = 0; i < _allMembers.Count; i++)
        {
            var member = _allMembers[i];
            if (member == null)
                continue;
            if (ActiveCombatRegistry.IsInCombat(member.gameObject))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Versión pública de <see cref="IsAnyTeamMemberInCombat"/>. Usada por IdleState para dejar
    /// pasar la detección propia de un NPC cuando un compañero de su mismo equipo ya está
    /// registrado en ActiveCombatRegistry (evita que el bloqueo global de combate deje al resto
    /// del equipo congelado en IdleState para siempre).
    /// </summary>
    public bool IsAnyMemberInCombat() => IsAnyTeamMemberInCombat();
    
    /// <summary>
    /// Notifica que el diálogo post-derrota ha terminado.
    /// Llamado por el líder desde NPCCombatLifecycleHandler.
    /// </summary>
    public void NotifyPostDefeatDialogueFinished()
    {
        if (IsPostDefeatDialogueFinished)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[NPCCombatTeam] {name}: NotifyPostDefeatDialogueFinished llamado múltiples veces - ignorando");
            }
            return;
        }
        
        IsPostDefeatDialogueFinished = true;
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Diálogo post-derrota finalizado. Notificando al equipo.");
        }
    }
    
    /// <summary>
    /// Fuerza la finalización del diálogo si el sistema se atasca.
    /// Útil para debugging o situaciones de emergencia.
    /// </summary>
    public void ForceFinishPostDefeatDialogue()
    {
        if (!IsPostDefeatDialogueFinished)
        {
            if (showDebugLogs) Debug.LogWarning($"[NPCCombatTeam] {name}: ⚠️ FORZANDO finalización de diálogo post-derrota");
            IsPostDefeatDialogueFinished = true;
            
            // Cancelar dizzy en todos los miembros para que procedan
            foreach (var member in _allMembers)
            {
                if (member == null) continue;
                var lifecycle = member.GetComponent<NPCCombatLifecycleHandler>();
                if (lifecycle != null)
                {
                    lifecycle.CancelDizzySequence();
                }
            }
        }
    }
    
    /// <summary>
    /// Resetea el estado del equipo (útil para resurrección).
    /// </summary>
    public void ResetTeamState()
    {
        IsPostDefeatDialogueFinished = false;
        _defeatedCount = 0;
        _isTeamInCombat = false;
        _isRegrouping = false;

        // ✅ FIX: Resetear el flag de notificación en todos los miembros
        foreach (var member in _allMembers)
        {
            if (member == null) continue;
            var teamMember = member.GetComponent<NPCTeamMember>();
            if (teamMember != null)
            {
                teamMember.ResetNotificationFlag();
            }
        }
    }
    
    /// <summary>
    /// Notifica que un miembro del equipo ha sido derrotado.
    /// </summary>
    public void OnMemberDefeated(NPCBehaviourManagerV2 member)
    {
        if (!_allMembers.Contains(member)) return;
        
        _defeatedCount++;
        
        // Desregistrar al miembro derrotado del combate activo
        ActiveCombatRegistry.UnregisterNPC(member.gameObject);
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Miembro {member.name} derrotado ({_defeatedCount}/{_allMembers.Count})");
        }
        
        // Verificar si todo el equipo ha sido derrotado
        if (IsTeamDefeated)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[NPCCombatTeam] {name}: ¡Todo el equipo ha sido derrotado!");
            }
            
            _isTeamInCombat = false;
            OnTeamDefeated?.Invoke();
        }
    }
    
    /// <summary>
    /// Resucita a todos los miembros del equipo (si está configurado).
    /// </summary>
    public void ResurrectTeam()
    {
        if (!resurrectAsTeam) return;
        if (_isResurrecting) return; // ✅ FIX #21: evita corrutinas Co_ResurrectTeam concurrentes

        StartCoroutine(Co_ResurrectTeam());
    }

    /// <summary>
    /// Fuerza a todos los miembros a entrar en combate inmediatamente (sin reagrupación).
    /// </summary>
    public void ForceTeamCombat(Transform player)
    {
        if (_isTeamInCombat) return;

        _isTeamInCombat = true;
        _defeatedCount = 0;
        IsPostDefeatDialogueFinished = false; // Resetear flag
        _currentTarget = player; // ✅ FIX #23: para que GetFormationPosition funcione también por esta ruta

        foreach (var member in _allMembers)
        {
            if (member != null && member.gameObject.activeInHierarchy)
            {
                TryForceMemberIntoCombat(member, player); // ✅ FIX #1
            }
        }

        OnTeamCombatStarted?.Invoke();
    }

    /// <summary>
    /// ✅ FIX #1 (CRÍTICO, auditoría combate 15 ago 2026): fuerza a un miembro a entrar en
    /// combate SOLO si tiene combatConfig asignado. Sin esta guarda, un miembro mal configurado
    /// (el caso real de Lety: combatConfig vacío) entraba igualmente en CombatState y se
    /// registraba en ActiveCombatRegistry, pero NPCBehaviourManagerV2 solo añade Damageable si
    /// HasBehaviour(Combat) && combatConfig != null — así que nunca podía recibir daño ni morir,
    /// _defeatedCount nunca llegaba a _allMembers.Count, e IsTeamDefeated quedaba en false para
    /// siempre: un solo NPC mal configurado bloqueaba la condición de victoria de TODO el equipo
    /// (sin celebración de victoria, sin gates narrativos enganchados a OnTeamDefeated). Ahora,
    /// si falta la config, el miembro se cuenta como derrotado de inmediato (no combate, pero
    /// tampoco bloquea al resto) y se loguea el error para detectarlo fácil en el Inspector.
    /// </summary>
    private void TryForceMemberIntoCombat(NPCBehaviourManagerV2 member, Transform player)
    {
        if (member.Configuration?.combatConfig == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[NPCCombatTeam] {name}: {member.name} no tiene combatConfig asignado — se excluye del combate de equipo (no podrá recibir daño). Revisa su NPCBehaviourManagerV2.");
#endif
            OnMemberDefeated(member);
            return;
        }

        member.ForceEnterCombat(player);
    }
    
    /// <summary>
    /// Obtiene las posiciones de formación frente al jugador.
    /// Los NPCs se colocan en semicírculo frente al jugador para la escena de confrontación.
    /// </summary>
    public Vector3 GetFormationPosition(int memberIndex)
    {
        if (memberIndex < 0 || _allMembers.Count <= 0) return transform.position;
        if (_currentTarget == null) return transform.position;
        
        // ✅ FIX: Calcular posición relativa al JUGADOR, no al líder
        // Esto asegura que todos los NPCs se agrupen frente al jugador para el diálogo
        
        Vector3 playerPos = _currentTarget.position;
        Vector3 dirFromPlayer = (transform.position - playerPos).normalized;
        dirFromPlayer.y = 0;
        
        // Si no hay dirección válida, usar forward del jugador
        if (dirFromPlayer.sqrMagnitude < 0.01f)
        {
            dirFromPlayer = _currentTarget.forward;
        }
        
        // Posición base: frente al jugador a distancia de formación
        float baseDistance = formationRadius + 2f; // Distancia base del jugador
        Vector3 formationCenter = playerPos + dirFromPlayer * baseDistance;
        
        // Si solo hay un miembro (el líder), ponerlo en el centro
        if (_allMembers.Count == 1 || memberIndex == 0)
        {
            return formationCenter;
        }
        
        // Distribuir en arco frente al jugador
        // El líder (index 0) va al centro, los demás a los lados
        float totalMembers = _allMembers.Count;
        float spreadAngle = 60f; // Ángulo total del arco (grados)
        
        // Calcular ángulo para este miembro
        // Index 0 = centro, index 1 = izquierda, index 2 = derecha, etc.
        float angleOffset;
        if (memberIndex == 0)
        {
            angleOffset = 0f;
        }
        else
        {
            // Alternar izquierda/derecha
            int sideIndex = (memberIndex + 1) / 2;
            bool isLeft = (memberIndex % 2) == 1;
            angleOffset = sideIndex * (spreadAngle / (totalMembers - 1)) * (isLeft ? -1f : 1f);
        }
        
        // Rotar la dirección por el ángulo calculado
        Vector3 memberDir = Quaternion.Euler(0, angleOffset, 0) * dirFromPlayer;
        Vector3 memberPos = playerPos + memberDir * baseDistance;
        
        return memberPos;
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Secuencia única de detección→diálogo→combate del equipo. Sustituye al antiguo
    /// Co_RegroupAndStartCombat (que hacía caminar a todos a una formación y luego intentaba
    /// disparar diálogo a través de NPCInteractiveNarrativeExecutor, el sistema narrativo
    /// LEGACY/CONGELADO — por eso nunca sonaba diálogo en un equipo nuevo como Lety+Vicky).
    ///
    /// Ahora se reutiliza exactamente el mismo mecanismo que ya funciona bien para NPCs solos:
    /// NPCCombatConfig.dialogueOnAlert + DialogueManager.StartBattleDialogue. Cero grafo
    /// narrativo, cero executor legacy — ninguno de los dos sistemas narrativos se toca aquí.
    ///
    /// Da igual qué miembro detectó al jugador: los dos (o más) se paran y miran de inmediato
    /// (sin caminar antes, "nada más detectarte"). Cada miembro con dialogueOnAlert configurado
    /// dice SU PROPIA frase, en orden — quien detectó primero, y luego el resto del equipo —
    /// con la misma cámara grupal (enfocando a todo el equipo) durante toda la secuencia. Al
    /// terminar (o de inmediato si nadie tiene diálogo configurado) entran en combate todos a la
    /// vez vía ForceEnterCombat — no dependen de que cada uno "descubra" el combate por su cuenta.
    /// </summary>
    private IEnumerator Co_DetectAndEngage(Transform player, NPCBehaviourManagerV2 detector = null)
    {
        _isRegrouping = true;
        _currentTarget = player;

        if (showDebugLogs)
            Debug.Log($"[NPCCombatTeam] {name}: ¡Jugador detectado! Deteniendo y encarando al equipo...");

        // 1. TODO el equipo se para y mira al jugador de inmediato. Reutiliza AlertState (icono
        // de detección, música de alerta, animación de "Sense") con skipDialogue=true: el
        // diálogo lo dispara este método, no cada miembro por separado.
        foreach (var member in _allMembers)
        {
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            if (member.Context != null) member.Context.Player = player;
            member.Brain?.ForceState(new AlertState(duration: 999f, walk: false, stopDist: 0f, skipDialogue: true));
        }

        // ✅ REDISEÑO (15 ago 2026, a petición de Raúl): "cada NPC con su propia frase; quien te
        // ve primero habla primero, luego el resto — así se siente como un equipo sin necesitar
        // un único diálogo compartido escrito a mano para cada combinación posible". Antes solo
        // sonaba leaderConfig.dialogueOnAlert (la frase del líder, siempre, sin importar quién
        // detectó); ahora se recorre el equipo completo empezando por el detector.
        //
        // 2. Orden de habla: detector primero (si está vivo y tiene dialogueOnAlert), luego el
        // resto del equipo en el orden en que fueron añadidos (líder incluido si no fue él quien
        // detectó). La cámara se mantiene enfocando a TODO el equipo durante toda la secuencia
        // (mismo _groupFocusAnchor, sin cortes) — solo cambia el ORDEN de las líneas, no el estilo
        // de cámara.
        if (DialogueManager.Instance != null)
        {
            UpdateGroupFocusAnchor();

            var speakOrder = new List<NPCBehaviourManagerV2>(_allMembers.Count);
            if (detector != null && _allMembers.Contains(detector) && detector.gameObject.activeInHierarchy)
                speakOrder.Add(detector);
            foreach (var member in _allMembers)
            {
                if (member == null || !member.gameObject.activeInHierarchy) continue;
                if (member == detector) continue; // ya añadido primero
                speakOrder.Add(member);
            }

            bool isFirstLine = true;
            foreach (var speaker in speakOrder)
            {
                var speakerConfig = speaker.Configuration?.combatConfig;
                if (speakerConfig == null || speakerConfig.dialogueOnAlert == null) continue;

                bool dialogueDone = false;
                DialogueManager.Instance.StartBattleDialogue(speakerConfig.dialogueOnAlert, _groupFocusAnchor,
                    () => dialogueDone = true, applyBattlePrep: isFirstLine);
                isFirstLine = false;
                yield return new WaitUntil(() => dialogueDone);
            }

            if (showDebugLogs && isFirstLine) // nadie llegó a hablar (ningún dialogueOnAlert configurado)
                Debug.Log($"[NPCCombatTeam] {name}: Ningún miembro tiene dialogueOnAlert configurado — se pasa directo a combate.");
        }
        else if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: DialogueManager no disponible — se pasa directo a combate.");
        }

        // 3. Combate para TODO el equipo a la vez.
        _isRegrouping = false;
        _isTeamInCombat = true;
        _defeatedCount = 0;
        IsPostDefeatDialogueFinished = false;

        foreach (var member in _allMembers)
        {
            if (member != null && member.gameObject.activeInHierarchy)
                TryForceMemberIntoCombat(member, player); // ✅ FIX #1
        }

        if (showDebugLogs)
            Debug.Log($"[NPCCombatTeam] {name}: Equipo en combate.");

        OnTeamCombatStarted?.Invoke();
    }

    /// <summary>
    /// Reposiciona (creándola la primera vez) un ancla invisible en el punto medio de los
    /// miembros vivos del equipo. Se pasa como "npc" a DialogueManager.StartBattleDialogue para
    /// que la cámara de diálogo encuadre a todo el equipo en vez de solo al líder.
    /// </summary>
    private void UpdateGroupFocusAnchor()
    {
        if (_groupFocusAnchor == null)
        {
            var go = new GameObject($"{name}_GroupDialogueFocus");
            go.hideFlags = HideFlags.HideInHierarchy;
            _groupFocusAnchor = go.transform;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var member in _allMembers)
        {
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            sum += member.transform.position;
            count++;
        }

        _groupFocusAnchor.position = count > 0 ? sum / count : transform.position;
    }
    
    /// <summary>
    /// Resucita a todos los miembros del equipo con delay.
    /// </summary>
    private IEnumerator Co_ResurrectTeam()
    {
        _isResurrecting = true; // ✅ FIX #21

        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Resucitando equipo...");
        }

        ResetTeamState();

        foreach (var member in _allMembers)
        {
            if (member == null) continue;

            // Llamar al método de resurrección del NPC
            var lifecycleHandler = member.GetComponent<NPCCombatLifecycleHandler>();
            if (lifecycleHandler != null)
            {
                lifecycleHandler.Resurrect();
            }

            yield return new WaitForSeconds(resurrectDelay);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: ¡Equipo resucitado!");
        }

        _isResurrecting = false; // ✅ FIX #21
    }
    
    #endregion
    
    #region Editor
    
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Dibujar radio de formación
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, formationRadius);
        
        // Dibujar posiciones de formación
        if (_allMembers == null || _allMembers.Count == 0)
        {
            // En editor sin play, simular posiciones
            int simulatedCount = teamMembers.Count + 1;
            for (int i = 1; i < simulatedCount; i++)
            {
                float angle = (360f / (simulatedCount - 1)) * (i - 1) + 180f;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * formationRadius;
                Vector3 pos = transform.position + offset;
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pos, 0.3f);
                Gizmos.DrawLine(transform.position, pos);
            }
        }
        else
        {
            for (int i = 1; i < _allMembers.Count; i++)
            {
                Vector3 pos = GetFormationPosition(i);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pos, 0.3f);
                Gizmos.DrawLine(transform.position, pos);
            }
        }
        
        // Líneas a los compañeros
        Gizmos.color = Color.green;
        foreach (var member in teamMembers)
        {
            if (member != null)
            {
                Gizmos.DrawLine(transform.position + Vector3.up, member.transform.position + Vector3.up);
            }
        }
    }
#endif
    
    #endregion
}
}
