using UnityEngine;

public enum DrawMode
{
    NoiseMap,
    FalloffMap,
    Mesh
};

public class MapPreview : MonoBehaviour
{
    public DrawMode drawMode;

    public bool autoUpdate;

    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int editorLevelOfDetail;

    //  ML STUFF: This can be randomized by the ML agent when generating types of forests, plains, deserts, mountains, islands, plateaus etc
    // Could be based on the noise parameters or certain limits can be assigned to noise parameters based on the kind of reion to generate
    // This can also be used to make sure all the regions appear in the generations(make sure mountains have snowy peaks for example)
    // regions can be randomized

    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureData;

    public Material terrainMaterial;

    public Renderer textureRender;

    public MeshFilter meshFilter;
    public MeshRenderer meshRednerer;

    private void Start()
    {
        // Hide preview Assets
        textureRender.gameObject.SetActive(false);
        meshRednerer.gameObject.SetActive(false);
    }

    public void DrawTexture(Texture2D texture)
    {
        textureRender.sharedMaterial.mainTexture = texture;
        textureRender.transform.localScale = new Vector3(texture.width, 1, texture.height) / 100;
    }

    public void DrawMesh(MeshData meshData)
    {
        meshFilter.sharedMesh = meshData.CreateMesh();
    }

    public void ShowTexturePreview()
    {
        if (!textureRender.gameObject.activeSelf)
        {
            textureRender.gameObject.SetActive(true);
            meshRednerer.gameObject.SetActive(false);
        }
    }

    public void ShowMeshPreview()
    {
        if (!meshRednerer.gameObject.activeSelf)
        {
            meshRednerer.gameObject.SetActive(true);
            textureRender.gameObject.SetActive(false);
        }
    }


    void OnValuesUpdated()
    {
        // Redraw the map in editor if values are updated
        if (!Application.isPlaying)
        {
            DrawMapInEditor();
        }
    }

    private void OnValidate()
    {
        // Subscribe to auto update values when something changes in terrain data or noise data
        // Unsubscribe and resubscribe because you don't want to keep adding the same event
        if (meshSettings != null)
        {
            meshSettings.OnValuesUpdated -= OnValuesUpdated;
            meshSettings.OnValuesUpdated += OnValuesUpdated;
        }
        if (heightMapSettings != null)
        {
            heightMapSettings.OnValuesUpdated -= OnValuesUpdated;
            heightMapSettings.OnValuesUpdated += OnValuesUpdated;
        }
        //if (textureData != null)
        //{
        //    textureData.OnValuesUpdated -= OnTextureValuesUpdated;
        //    textureData.OnValuesUpdated += OnTextureValuesUpdated;
        //}
    }

    public void DrawMapInEditor()
    {
        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, Vector2.zero);

        if (drawMode == DrawMode.NoiseMap)
        {
            DrawTexture(TextureGenerator.TextureFromHeightMap(heightMap));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            DrawTexture(TextureGenerator.TextureFromHeightMap(new HeightMap(FalloffGenerator.GenerateFallOfMap(meshSettings.numVertsPerLine, heightMapSettings.falloffCurve), 0, 1)));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            DrawMesh(MeshGenerator.GenerateTerrainMesh(heightMap.values, editorLevelOfDetail, meshSettings));
        }

        textureData.UpdateMeshHeights(terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);
    }
}
