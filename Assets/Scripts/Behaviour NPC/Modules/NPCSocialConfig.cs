using System;
using UnityEngine;

namespace Game.NPC.Modules
{
    [CreateAssetMenu(fileName = "NPC_Social_Config", menuName = "El Sendero/NPCs/Módulos/Social Config", order = 6)]
    public class NPCSocialConfig : NPCModuleConfigBase
    {
        [Header("Identidad Social")]
        [Tooltip("ID único de este NPC en el mundo social. Debe coincidir con el npcId que otros NPCs usen al definir sus relaciones.")]
        public string npcId;

        [Header("Personalidad")]
        public NPCPersonality personality = NPCPersonality.Default;

        [Header("Relaciones Conocidas")]
        [Tooltip("NPCs con los que este personaje tiene una relación establecida")]
        public NPCRelationshipEntry[] relationships = Array.Empty<NPCRelationshipEntry>();

        [Header("Comportamiento Social")]
        [Min(0f), Tooltip("Distancia máxima (metros) para detectar a otro NPC e iniciar un encuentro")]
        public float socialDetectionRange = 5f;

        [Min(0f), Tooltip("Tiempo mínimo (segundos) entre dos encuentros sociales consecutivos")]
        public float socialCooldown = 30f;

        [Min(0f), Tooltip("Distancia máxima (metros) para buscar un NPCWorldPoint al que caminar")]
        public float worldPointSearchRange = 20f;

        [ContextMenu("Randomizar Personalidad")]
        public void RandomizePersonality()
        {
            personality = NPCPersonality.CreateRandom();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            return true;
        }

        public NPCRelationType GetRelationshipWith(string otherId)
        {
            if (string.IsNullOrEmpty(otherId)) return NPCRelationType.Stranger;
            foreach (var r in relationships)
                if (r.npcId == otherId) return r.type;
            return NPCRelationType.Stranger;
        }

        // Devuelve 0-1: mayor valor = más afinidad
        public float GetAffinityWith(string otherId)
        {
            return GetRelationshipWith(otherId) switch
            {
                NPCRelationType.BestFriend   => 1.0f,
                NPCRelationType.Friend       => 0.75f,
                NPCRelationType.Acquaintance => 0.5f,
                NPCRelationType.Stranger     => 0.3f,
                NPCRelationType.Rival        => 0.15f,
                NPCRelationType.Enemy        => 0.0f,
                _                            => 0.3f,
            };
        }
    }
}
