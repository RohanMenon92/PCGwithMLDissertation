using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public struct LODInfo
{
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int lod;
    public float visibleDstThreshold;

    public float sqrVisibleDistanceThreshold
    {
        get
        {
            return visibleDstThreshold * visibleDstThreshold;
        }
    }
}

public class TerrainGenerator : MonoBehaviour
{
    static float terrainScale;
    const float viewerThresholdToUpdate = 25f;
    // square distances are easier to calculate
    const float sqrViewerThresholdToUpdate = viewerThresholdToUpdate * viewerThresholdToUpdate;

    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureSettings;

    public int colliderLODindex;
    public LODInfo[] detailLevels;

    public GameObject waterObject;

    public Transform viewer;
    public Material terrainMaterial;

    [Header("Chunk Collider Statistics (For Machine Learning)")]
    // ML STUFF: Calculate statistics for generated chunk colliders, if they are below a certain threshold, abandon generation
    public int chunkCollidersMade; //  chunks generated
    public float totNormalX; // To calculate Average X Normal 
    public float totNormalY; // To calculate Average Y Normal 
    public float totNormalZ; // To calculate Average Z Normal 
    public float totValidSlope; // To calculate Average valid slope

    Vector2 viewerPosition;
    Vector2 lastUpdateViewerPosition;
    int chunksVisibleInViewDst;
    float meshWorldSize;


    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    PlayerPlayScript playerPlayScript;
    ObjectCreator objectCreator;
    NavMeshSurface navMeshSurface;

    // Start is called before the first frame update
    void Start()
    {
        chunkCollidersMade = 0;
        totNormalX = 0;
        totNormalY = 0;
        totNormalZ = 0;
        totValidSlope = 0;        
        playerPlayScript = FindObjectOfType<PlayerPlayScript>();
        objectCreator = FindObjectOfType<ObjectCreator>();
        navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        navMeshSurface.collectObjects = CollectObjects.Children;
        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;


        waterObject.transform.position = new Vector3(0f, meshSettings.waterLevel, 0f);

        terrainScale = meshSettings.terrainScale;

        // Size of vertices in chunks is actually 1 less than this number
        meshWorldSize = meshSettings.meshWorldSize;

        textureSettings.UpdateMeshHeights(terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

        float maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / meshWorldSize);

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        // Call once at start
        UpdateVisibleChunks();
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

        if (viewerPosition != lastUpdateViewerPosition)
        {
            foreach (TerrainChunk chunk in visibleTerrainChunks)
            {
                chunk.UpdateCollisionMesh();
                chunk.UpdateTreeVisibility();
            }
        }

        if ((lastUpdateViewerPosition - viewerPosition).sqrMagnitude > sqrViewerThresholdToUpdate)
        {
            // Update whenever the viewer moves a certain threshold
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        // Prevent double update of chunk coordinates
        HashSet<Vector2> alreadyUpdateChunkCoords = new HashSet<Vector2>();

        lastUpdateViewerPosition = viewerPosition;
        // Check Removal of last few chunks
        // Go in reverse because we are removing from the list, prevents conflicts
        for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdateChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

        // Create new chunks
        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (!alreadyUpdateChunkCoords.Contains(viewedChunkCoord))
                {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        // Update chunk
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    }
                    else
                    {
                        TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, heightMapSettings, meshSettings, detailLevels, colliderLODindex, transform, viewer, terrainMaterial, objectCreator);
                        // Add new terrain chunk and parent it to this transform
                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
                        newChunk.OnVisibilityChanged += OnTerrainChunkVisibilityChanged;
                        newChunk.OnCreatedCollider += OnCreateColliderForChunk;

                        // subscribe to visibility changed before load
                        newChunk.Load();
                    }
                }
            }
        }
    }

    void OnCreateColliderForChunk(TerrainChunk chunk)
    {
        if(!playerPlayScript.thirdPersonPlayer)
        {
            // Update the surface because colliders are created for volume modifiers now
            navMeshSurface.BuildNavMesh();
        }

        // TODO: Create a function that returns these values
        chunkCollidersMade++;
        totNormalX += chunk.averageXNormal;
        totNormalY += chunk.averageYNormal;
        totNormalZ += chunk.averageZNormal;
        totValidSlope += chunk.averageValidSlope;

        chunk.CreateTrees();
    }

    void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible)
    {
        if (isVisible)
        {
            visibleTerrainChunks.Add(chunk);
        } else
        {
            visibleTerrainChunks.Remove(chunk);
        }
    }
}