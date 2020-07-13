using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdatableData : ScriptableObject
{
    public event System.Action OnValuesUpdated;
    public bool autoUpdate;

    // This should be called from the derived UpdatableData
    protected virtual void OnValidate()
    {
        // For calling an event when update happens
        if(autoUpdate)
        {
            UnityEditor.EditorApplication.update += NotifyOfUpdatedValues;
        }
    }

    public void NotifyOfUpdatedValues()
    {
        UnityEditor.EditorApplication.update -= NotifyOfUpdatedValues;

        if (OnValuesUpdated != null)
        {
            OnValuesUpdated();
        }
    }
}
