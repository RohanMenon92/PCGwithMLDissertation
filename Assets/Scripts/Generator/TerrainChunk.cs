using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TerrainChunk
{
    const float colliderGenrationThreshold = 5f;
    public event System.Action<TerrainChunk, bool> OnVisibilityChanged;
    
    public Vector2 coord;
    public Vector2 chunkPosition;
    Vector2 sampleCenter;
    Bounds bounds;

    GameObject meshObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;

    // LOD Data
    LODInfo[] detailLevels;
    LODMesh[] lodMeshes;
    int colliderLODindex;

    HeightMap heightMap;
    bool heightMapReceived;
    bool hasSetCollider = false;

    float maxViewDst;

    int prevLODIndex = -1;
    HeightMapSettings heightMapSettings;
    MeshSettings meshSettings;
    Transform viewer;
    Material meshMaterial;
    ObjectCreator objectCreator;


    Vector2 viewerPosition
    {
        get
        {
            return new Vector2(viewer.position.x, viewer.position.z);
        }
    }

    public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODindex, Transform parent, Transform viewer, Material meshMaterial, ObjectCreator objectCreator)
    {
        this.coord = coord;
        this.detailLevels = detailLevels;
        this.colliderLODindex = colliderLODindex;
        this.objectCreator = objectCreator;

        this.heightMapSettings = heightMapSettings;
        this.meshSettings = meshSettings;
        this.viewer = viewer;
        this.meshMaterial = meshMaterial;

        // max view distance should be last detail level
        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
        sampleCenter = coord * meshSettings.meshWorldSize / meshSettings.terrainScale;
        chunkPosition = coord * meshSettings.meshWorldSize;
        bounds = new Bounds(chunkPosition, Vector2.one * meshSettings.meshWorldSize);

        // Create plane
        meshObject = new GameObject("TerrainChunk_" + coord.x + ":" + coord.y);
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshCollider = meshObject.AddComponent<MeshCollider>();

        meshObject.transform.parent = parent;
        meshObject.transform.position = new Vector3(chunkPosition.x, 0, chunkPosition.y);
        SetVisible(false);

        // Create LOD meshes for all levels of detail
        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i++)
        {
            lodMeshes[i] = new LODMesh(detailLevels[i].lod);
            if (i == colliderLODindex)
            {
                lodMeshes[i].updateCallback += UpdateCollisionMesh;
            }
            lodMeshes[i].updateCallback += UpdateTerrainChunk;
        }
    }

    public void Load()
    {
        // Add request for data in mapGenererator and create a thread
        // Lamda expression required because parameters cannot be passed
        ThreadDataRequester.RequestData(() => HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, sampleCenter), OnHeightMapReceived);
    }


    void OnHeightMapReceived(object mapData)
    {
        this.heightMap = (HeightMap)mapData;
        heightMapReceived = true;

        // Create color map for this map data
        //Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
        // Set smoothness to zero because lit instances will be used
        meshRenderer.material = meshMaterial;
        meshRenderer.material.SetFloat("_Smoothness", 0f);

        UpdateTerrainChunk();
        //print("Map Data received");
        //mapGen.RequestMeshData(mapData, OnMeshDataReceived);
    }

    public void UpdateTerrainChunk()
    {
        if (!heightMapReceived)
        {
            return;
        }

        // Effecient way to check chunk distance
        float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));

        // To check for removal
        bool wasVisible = IsVisible();
        bool visible = viewerDstFromNearestEdge <= maxViewDst;

        if (visible)
        {
            int lodIndex = 0;

            for (int i = 0; i < detailLevels.Length - 1; i++)
            {
                // In the last case, chunk will not be visible
                if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold)
                {
                    lodIndex = i + 1;
                }
                else
                {
                    break;
                }
            }

            if (lodIndex != prevLODIndex)
            {
                LODMesh lodMesh = lodMeshes[lodIndex];
                if (lodMesh.hasMesh)
                {
                    prevLODIndex = lodIndex;
                    meshFilter.mesh = lodMesh.mesh;
                }
                else if (!lodMesh.hasRequestedMesh)
                {
                    lodMesh.RequestMesh(heightMap, meshSettings);
                }
            }
        }

        if (wasVisible != visible)
        {
            SetVisible(visible);
            if (OnVisibilityChanged != null)
            {
                OnVisibilityChanged.Invoke(this, visible);
            }
        }
    }

    // Check that is called more frequently than updating the mesh
    public void UpdateCollisionMesh()
    {
        if (hasSetCollider)
        {
            return;
        }

        float sqrDistnceFromViewerEdge = bounds.SqrDistance(viewerPosition);

        if (sqrDistnceFromViewerEdge < detailLevels[colliderLODindex].sqrVisibleDistanceThreshold)
        {
            if (!lodMeshes[colliderLODindex].hasRequestedMesh)
            {
                lodMeshes[colliderLODindex].RequestMesh(heightMap, meshSettings);
            }
        }

        if (sqrDistnceFromViewerEdge > colliderGenrationThreshold * colliderGenrationThreshold || sqrDistnceFromViewerEdge == 0)
        {
            if (lodMeshes[colliderLODindex].hasMesh)
            {
                meshCollider.sharedMesh = lodMeshes[colliderLODindex].mesh;
                hasSetCollider = true;

                objectCreator.OnCreateObjectsForChunk(this, new Vector2(bounds.size.x, bounds.size.y));
            }
        }
    }

    public void SetVisible(bool visible)
    {
        meshObject.SetActive(visible);
    }

    public bool IsVisible()
    {
        return meshObject.activeSelf;
    }
}

class LODMesh
{
    public Mesh mesh;
    public bool hasRequestedMesh;
    public bool hasMesh;
    int lod;
    public event System.Action updateCallback;


    public LODMesh(int lod)
    {
        this.lod = lod;
    }

    void OnMeshDataReceived(object meshDataObject)
    {

        MeshData meshData = (MeshData)meshDataObject;
        mesh = meshData.CreateMesh();
        hasMesh = true;
        updateCallback();
    }

    public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings)
    {
        hasRequestedMesh = true;
        ThreadDataRequester.RequestData(() => MeshGenerator.GenerateTerrainMesh(heightMap.values, lod, meshSettings), OnMeshDataReceived);
    }
}
