using UnityEngine;

public class AmbientInhibitor : MonoBehaviour
{
    public AmbientAgent agent;
    int _locks;

    public void Lock()
    {
        _locks++;
        if (agent) { agent.enabled = false; var nav = agent.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (nav) nav.isStopped = true; }
    }
    public void Unlock()
    {
        _locks = Mathf.Max(0, _locks-1);
        if (_locks == 0 && agent) { var nav = agent.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (nav) nav.isStopped = false; agent.enabled = true; }
    }
}