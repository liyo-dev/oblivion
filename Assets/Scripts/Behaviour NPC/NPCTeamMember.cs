using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Componente auxiliar que marca a un NPC como miembro de un equipo de combate.
    /// Se añade automáticamente por NPCCombatTeam a todos los miembros del equipo.
    /// </summary>
    [DisallowMultipleComponent]
    public class NPCTeamMember : MonoBehaviour
    {
        private NPCCombatTeam _team;
        private bool _isLeader;
        private NPCBehaviourManagerV2 _manager;
        private bool _hasNotifiedTeam; // Flag para evitar notificaciones repetidas
    
    /// <summary>
    /// El equipo al que pertenece este NPC.
    /// </summary>
    public NPCCombatTeam Team => _team;
    
    /// <summary>
    /// Indica si este NPC es el líder del equipo.
    /// </summary>
    public bool IsLeader => _isLeader;
    
    /// <summary>
    /// Indica si este NPC pertenece a un equipo.
    /// </summary>
    public bool HasTeam => _team != null;
    
    /// <summary>
    /// Indica si este NPC ya notificó al equipo de la presencia del jugador.
    /// </summary>
    public bool HasNotifiedTeam => _hasNotifiedTeam;
    
    void Awake()
    {
        _manager = GetComponent<NPCBehaviourManagerV2>();
    }
    
    /// <summary>
    /// Configura el equipo de este miembro.
    /// Llamado por NPCCombatTeam durante la inicialización.
    /// </summary>
    public void SetTeam(NPCCombatTeam team, bool isLeader)
    {
        _team = team;
        _isLeader = isLeader;
        
        // Debug.Log($"[NPCTeamMember] {name}: Asignado al equipo de {team.name} (Líder: {isLeader})");
    }
    
    /// <summary>
    /// Llamado cuando este NPC detecta al jugador.
    /// Si pertenece a un equipo, notifica al equipo en lugar de actuar solo.
    /// </summary>
    public bool TryNotifyTeamOfPlayer(Transform player)
    {
        if (_team == null) return false;

        // ✅ FIX: Evitar notificaciones repetidas que causan bucle infinito
        if (_hasNotifiedTeam) return true; // Retornamos true para indicar que ya se manejó

        // ✅ FIX (auditoría combate, 15 ago 2026): antes se marcaba _hasNotifiedTeam=true de forma
        // incondicional, ANTES de saber si el equipo aceptó la notificación. Si OnPlayerDetected
        // la rechazaba (p.ej. otro equipo ya en combate global), el flag se quemaba igual — y como
        // el único reset es tras derrota+resurrección del equipo, este NPC quedaba sordo a la
        // detección del jugador para siempre (IdleState.CheckPlayerDetection corta en cuanto ve
        // HasNotifiedTeam=true). Ahora solo se quema si la notificación realmente prendió.
        // ✅ REDISEÑO (15 ago 2026, a petición de Raúl): se pasa `_manager` (este mismo NPC) como
        // "detector" para que el equipo sepa QUIÉN vio primero al jugador — ese NPC habla primero
        // en la secuencia de frases de entrada (ver NPCCombatTeam.Co_DetectAndEngage).
        bool accepted = _team.OnPlayerDetected(player, _manager);
        if (accepted)
            _hasNotifiedTeam = true;

        return accepted;
    }
    
    /// <summary>
    /// Resetea el flag de notificación (útil para respawn o reinicio).
    /// </summary>
    public void ResetNotificationFlag()
    {
        _hasNotifiedTeam = false;
    }
    
    /// <summary>
    /// Llamado cuando este NPC es derrotado.
    /// Notifica al equipo si pertenece a uno.
    /// </summary>
    public void NotifyDefeated()
    {
        if (_team != null && _manager != null)
        {
            _team.OnMemberDefeated(_manager);
        }
    }
}
}

