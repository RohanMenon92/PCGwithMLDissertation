using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ThreadDataRequester : MonoBehaviour
{
    static ThreadDataRequester instance;
    Queue<ThreadInfo> threadDataQueue = new Queue<ThreadInfo>();

    // Struct to handle map data and thread data
    // Generic so that it can handle both
    struct ThreadInfo
    {
        // readonly so that they can't be modified after creation
        public readonly Action<object> callback;
        public readonly object parameter;

        public ThreadInfo(Action<object> callback, object parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
    private void Awake()
    {
        instance = FindObjectOfType<ThreadDataRequester>();
    }

    // Ask for data with function to generate it
    public static void RequestData(Func<object> generateData, Action<object> callback)
    {
        // Start thrread for generating map data
        ThreadStart threadStart = delegate
        {
            instance.DataThread(generateData, callback);
        };

        new Thread(threadStart).Start();
    }


    // Update is called once per frame
    void Update()
    {
        // Look at map thread Info Queue and request data
        if (threadDataQueue.Count > 0)
        {
            for (int i = 0; i < threadDataQueue.Count; i++)
            {
                // Get the next set of thread info by taking it out from the queue
                ThreadInfo threadInfo = threadDataQueue.Dequeue();
                // Call the thread info callback with the relevant data
                threadInfo.callback(threadInfo.parameter);
            }
        }

        //// Look at mesh thread Info Queue and request data
        //if (meshDataThreadInfoQueue.Count > 0)
        //{
        //    for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
        //    {
        //        // Get the next set of thread info by taking it out from the queue
        //        MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
        //        // Call the thread info callback with the relevant data
        //        threadInfo.callback(threadInfo.parameter);
        //    }
        //}
    }

    void DataThread(Func<object> generateData, Action<object> callback)
    {
        // Start Generate on a thread
        object data = generateData();

        // Do not let multiple threads access threadDataQueue at the same time
        // Prevent queue being unordered, etc
        lock (threadDataQueue)
        {
            // Add thread to the queue to request data after
            threadDataQueue.Enqueue(new ThreadInfo(callback, data));
        }
    }

    public static void ClearDataQueue()
    {
        instance.threadDataQueue.Clear();
    }
}
