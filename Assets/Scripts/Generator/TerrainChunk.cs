using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TerrainChunk
{
    float colliderGenrationThreshold = 194f;
    public event System.Action<TerrainChunk, bool> OnVisibilityChanged;
    public event System.Action<TerrainChunk> OnCreatedCollider;

    public GameObject meshObject;
    public Vector2 coord;
    public Vector2 chunkPosition;
    public Bounds bounds;

    public bool hasSetCollider = false;
    public bool hasTrees = false;
    public bool hasCreatedTrees = false;

    // ML STUFF: Use this for noise generation analysis (TODO :: Should also check if the terrain generation is taking into account water vertices?)
    // Average Y should be at least greater than 4.5 (For navigable terrain) but less than 5.75, (too flat)
    // Average X and Z should be greater than 1.5 (otherwise terrain is too flat) but preferably less than 2.5 (otherwise terrain is not navigable)
    // Average valid slope should be at least half of the generated terrain for interesting navigable meshes
    public float averageYNormal;
    public float averageXNormal;
    public float averageZNormal;
    public float averageValidSlope;
    public float averageWaterAmount;

    Vector2 sampleCenter;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;
    NavMeshModifierVolume navMeshModifier;

    // LOD Data
    LODInfo[] detailLevels;
    LODMesh[] lodMeshes;
    int colliderLODindex;

    HeightMap heightMap;
    bool heightMapReceived;
    float maxViewDst;

    int prevLODIndex = -1;
    HeightMapSettings heightMapSettings;
    MeshSettings meshSettings;
    Transform viewer;
    Material meshMaterial;
    ObjectCreator objectCreator;
    List<Vector2> treePoints;

    Vector2 viewerPosition
    {
        get
        {
            return new Vector2(viewer.position.x, viewer.position.z);
        }
    }

    public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODindex, Transform parent, Transform viewer, Material meshMaterial, ObjectCreator objectCreator, float collisionGenerationThreshold)
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

        colliderGenrationThreshold = collisionGenerationThreshold;

        // Create plane
        meshObject = new GameObject(GameConstants.TerrainChunkPrefix + coord.x + ":" + coord.y);
        meshObject.tag = GameConstants.TerrainChunkTag;
        meshObject.transform.parent = parent;
        meshObject.transform.position = new Vector3(chunkPosition.x, 0, chunkPosition.y);

        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshCollider = meshObject.AddComponent<MeshCollider>();

        navMeshModifier = meshObject.AddComponent<NavMeshModifierVolume>();
        //navMeshSurface.collectObjects = CollectObjects.Volume;
        //navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navMeshModifier.center = Vector3.zero;
        navMeshModifier.size = new Vector3(meshSettings.meshWorldSize * 1.1f, heightMapSettings.maxHeight * 2, meshSettings.meshWorldSize * 1.1f);
        SetVisible(false);

        // Create LOD meshes for all levels of detail
        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i++)
        {
            lodMeshes[i] = new LODMesh(detailLevels[i].lod);
            if (i == colliderLODindex)
            {
                lodMeshes[i].updateCallback += UpdateCollisionMesh;
                lodMeshes[i].updateCallback += UpdateTreeVisibility;
            }
            lodMeshes[i].updateCallback += UpdateTerrainChunk;
        }
    }

    public void UpdateTreeVisibility()
    {
        if(!hasCreatedTrees)
        {
            return;
        }

        // Show trees if within half the distance of colliderGeneration Threshold or within the terrain mesh
        bool showTrees = bounds.SqrDistance(viewerPosition) < (colliderGenrationThreshold * colliderGenrationThreshold)/4 || bounds.SqrDistance(viewerPosition) == 0;

        if (showTrees && !hasTrees)
        {
            CreateTrees();
        }
        else if (!showTrees && hasTrees)
        {
            objectCreator.ReturnChunkObjectsToPool(this, meshObject.transform);
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
        UpdateCollisionMesh();
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

        if (sqrDistnceFromViewerEdge < colliderGenrationThreshold * colliderGenrationThreshold || sqrDistnceFromViewerEdge == 0)
        {
            if (lodMeshes[colliderLODindex].hasMesh)
            {
                meshCollider.sharedMesh = lodMeshes[colliderLODindex].mesh;

                averageXNormal = 0f;
                averageYNormal = 0f;
                averageZNormal = 0f;
                averageValidSlope = 0f;

                int vertexIndex = 0;
                int waterVertices = 0;
                Vector3[] vertices = meshCollider.sharedMesh.vertices;

                // Calculate data for ML statistics
                foreach (Vector3 meshNormal in meshCollider.sharedMesh.normals)
                {
                    // Check if it is a water mesh, dont take into account if it is
                    if(vertices[vertexIndex].y >= meshSettings.waterLevel)
                    {
                        // Add metrics to calculate
                        averageYNormal += Math.Abs(meshNormal.y);
                        averageXNormal += Math.Abs(meshNormal.x);
                        averageZNormal += Math.Abs(meshNormal.z);
                        // If slope is less than 45 degrees
                        if (Vector3.Angle(meshNormal, Vector3.up) < 45)
                        {
                            averageValidSlope += 1;
                        }
                    } else
                    {
                        waterVertices++;
                    }

                    vertexIndex++;
                }

                int totalVertices = meshCollider.sharedMesh.normals.Length;
                int nonWaterVertices = totalVertices - waterVertices;
                //Debug.Log("Non water vertexes " + nonWaterVertexes + " total mesh Length:" + meshCollider.sharedMesh.normals.Length);

                if(nonWaterVertices != 0)
                {
                    averageWaterAmount = (float)waterVertices / totalVertices;
                    averageYNormal = averageYNormal / nonWaterVertices;
                    averageXNormal = averageXNormal / nonWaterVertices;
                    averageZNormal = averageZNormal / nonWaterVertices;
                    averageValidSlope = averageValidSlope / nonWaterVertices;
                } else
                {
                    averageWaterAmount = 1;
                    averageYNormal = 0;
                    averageXNormal = 0;
                    averageZNormal = 0;
                    averageValidSlope = 0;
                }


                //Debug.Log("AverageX " + averageXNormal + " :: AverageY " + averageYNormal + " :: AverageZ " + averageZNormal + " :: Average Valid Slope " + averageValidSlope);

                hasSetCollider = true;
                OnCreatedCollider.Invoke(this);
            }
        }
    }

    public void CreateTrees()
    {
        if(!hasCreatedTrees)
        {
            // If has not created trees before, set them and store them
            treePoints = objectCreator.OnCreateNewTreesForChunk(this);
        }
        // If has created trees before, set them
        objectCreator.BuildChunkTreesFromPoints(this, treePoints);
    }

    public void SetVisible(bool visible)
    {
        meshObject.SetActive(visible);
    }

    public bool IsVisible()
    {
        return meshObject.activeSelf;
    }

    public void ClearAll()
    {
        detailLevels = null;

        foreach(LODMesh LODmesh in lodMeshes)
        {
            LODmesh.Clear();
        }
        lodMeshes = null;

        OnVisibilityChanged = null;
        OnCreatedCollider = null;
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

    public void Clear()
    {
        mesh.Clear();
        updateCallback = null;
    }
}
