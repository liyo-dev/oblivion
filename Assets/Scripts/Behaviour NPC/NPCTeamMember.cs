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
        
        // Notificar al equipo
        _team.OnPlayerDetected(player);
        return true;
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

