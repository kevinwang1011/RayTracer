using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EventManager
{
    private static EventManager _instance = null;
    public static EventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new EventManager();
            }
            return _instance;
        }
    }

    public enum EventType
    {
        Render
    }

    private Dictionary<EventType, UnityEvent> eventDictionary = new Dictionary<EventType, UnityEvent>();

    public void AddListener(EventType eventType, UnityAction listener)
    {
        UnityEvent thisEvent = null;
        if (Instance.eventDictionary.TryGetValue(eventType, out thisEvent))
        {
            thisEvent.AddListener(listener);
        }
        else
        {
            thisEvent = new UnityEvent();
            thisEvent.AddListener(listener);
            eventDictionary.Add(eventType, thisEvent);
        }
    }

    public void RemoveListener(EventType eventType, UnityAction listener)
    {
        UnityEvent thisEvent = null;
        if (eventDictionary.TryGetValue(eventType, out thisEvent))
        {
            thisEvent.RemoveListener(listener);
        }
    }

    public void TriggerEvent(EventType eventType)
    {
        UnityEvent thisEvent = null;
        if (eventDictionary.TryGetValue(eventType, out thisEvent))
        {
            thisEvent.Invoke();
        }
    }

}
