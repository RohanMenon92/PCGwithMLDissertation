using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// True because we want inherited values to also use this editor
[CustomEditor(typeof(UpdatableData), true)]
public class UpdatableDataEditor : Editor
{
    public override void OnInspectorGUI ()
    {
        base.OnInspectorGUI();

        UpdatableData data = (UpdatableData)target;
        if(GUILayout.Button("Update"))
        {
            data.NotifyOfUpdatedValues();
        }
    }

}
