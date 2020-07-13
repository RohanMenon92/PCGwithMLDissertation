using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapPreview))]
public class MapPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapPreview mapPreview = (MapPreview)target;

        // Auto update on change
        if (DrawDefaultInspector())
        {
            if (mapPreview.autoUpdate)
            {
                mapPreview.DrawMapInEditor();
            }
        }

        if(mapPreview)
        {
            if (mapPreview.drawMode == DrawMode.FalloffMap || mapPreview.drawMode == DrawMode.NoiseMap)
            {
                mapPreview.ShowTexturePreview();
            }

            if (mapPreview.drawMode == DrawMode.Mesh)
            {
                mapPreview.ShowMeshPreview();
            }
        }

        if (GUILayout.Button("Generate"))
        {
            mapPreview.DrawMapInEditor();
        }
    }
}
