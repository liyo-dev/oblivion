using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Narrativa/Graph", fileName = "NarrativeGraph")]
public class NarrativeGraph : ScriptableObject
{
    public string startNodeGuid;
    [SerializeReference] public List<NarrativeNode> nodes = new();
    [Tooltip("Escenas en las que aplica este grafo. Vacío = todas.")]
    public List<string> applicableSceneNames = new();

    private void OnValidate()
    {
        // Clean up null nodes that may have been created by serialization issues
        nodes?.RemoveAll(n => n == null);
    }

    public NarrativeNode FindNode(string guid)
        => nodes.Find(n => n != null && n.guid == guid);
}