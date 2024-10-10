using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventEmitter : MonoBehaviour
{
    public void EmitEvent()
    {
        EventManager.Instance.TriggerEvent(EventManager.EventType.Render);
    }
}
