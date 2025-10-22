using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Narrative/Graph", fileName = "NarrativeGraph")]
public class NarrativeGraph : ScriptableObject
{
    public string startNodeGuid;
    [SerializeReference] public List<NarrativeNode> nodes = new();

    public NarrativeNode FindNode(string guid)
        => nodes.Find(n => n != null && n.guid == guid);
}