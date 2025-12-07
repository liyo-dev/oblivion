using System;
using UnityEngine;
using UnityEngine.Events;

public class OnTriggerExit_Event : MonoBehaviour
{
    public UnityEvent OnTriggerExit;
    public Action ActionAfterTrigger;
    public string ElementToCompare;
    public bool OneTimeEvent;
    private bool m_IsTriggerEnter;
    private bool isEnabled;

    private void Start()
    {
        isEnabled = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(ElementToCompare) && isEnabled)
        {
            if(OneTimeEvent) m_IsTriggerEnter = true;
            if (OneTimeEvent && m_IsTriggerEnter) return;

            OnTriggerExit?.Invoke();
            ActionAfterTrigger?.Invoke();
        }
    }

    public void CanTrigger() => isEnabled = true;
    public void CanNotTrigger() => isEnabled = false;
}
