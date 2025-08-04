using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher instance;
    private readonly Queue<System.Action> actions = new Queue<System.Action>();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (instance == null)
            {
                CreateInstance();
            }
            return instance;
        }
    }

    private static void CreateInstance()
    {
        GameObject go = new GameObject("MainThreadDispatcher");
        instance = go.AddComponent<UnityMainThreadDispatcher>();
        DontDestroyOnLoad(go);
    }

    public void Enqueue(System.Action action)
    {
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }

    void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
            {
                try
                {
                    actions.Dequeue().Invoke();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Main thread task error: {e.Message}");
                }
            }
        }
    }
}