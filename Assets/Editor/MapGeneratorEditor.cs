using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapGenerator mapGen = (MapGenerator)target;

        // Auto update on change
        if (DrawDefaultInspector())
        {
            if (mapGen.autoUpdate)
            {
                mapGen.DrawMapInEditor();
            }
        }

        if(mapGen.mapDisplay)
        {
            if (mapGen.drawMode == DrawMode.FalloffMap || mapGen.drawMode == DrawMode.NoiseMap)
            {
                mapGen.mapDisplay.ShowTexturePreview();
            }

            if (mapGen.drawMode == DrawMode.Mesh)
            {
                mapGen.mapDisplay.ShowMeshPreview();
            }
        }

        if (GUILayout.Button("Generate"))
        {
            mapGen.DrawMapInEditor();
        }
    }
}
