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
    public int chunkCollidersMade = 0; //  chunks generated
    public float totNormalX = 0; // To calculate Average X Normal 
    public float totNormalY = 0; // To calculate Average Y Normal 
    public float totNormalZ = 0; // To calculate Average Z Normal 
    public float totValidSlope = 0; // To calculate Average valid slope
    public float totWaterAmount = 0; // To calculate water amount

    Vector2 viewerPosition;
    Vector2 lastUpdateViewerPosition;
    int chunksVisibleInViewDst;
    float meshWorldSize;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    GeneratorAgent genAgent;
    PlayerPlayScript playerPlayScript;
    ObjectCreator objectCreator;
    NavMeshSurface navMeshSurface;

    public bool isGeneratorTrainer = false;
    public bool trainerChunksGenerated = false;

    // Start is called before the first frame update
    void Start()
    {
        playerPlayScript = FindObjectOfType<PlayerPlayScript>();
        objectCreator = FindObjectOfType<ObjectCreator>();

        genAgent = GetComponent<GeneratorAgent>();
        if(genAgent == null)
        {
            navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        } else
        {
            isGeneratorTrainer = true;
        }

        InitTerrain();
    }

    void InitTerrain()
    {
        trainerChunksGenerated = false;

        chunkCollidersMade = 0;
        totNormalX = 0;
        totNormalY = 0;
        totNormalZ = 0;
        totValidSlope = 0;
        totWaterAmount = 0;

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
                        TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, heightMapSettings, meshSettings, detailLevels, colliderLODindex, transform, viewer, terrainMaterial, objectCreator, isGeneratorTrainer ? meshSettings.meshWorldSize * 2 : meshSettings.meshWorldSize);
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
        // TODO: Create a function that returns these values
        chunkCollidersMade++;

        totNormalX += chunk.averageXNormal;
        totNormalY += chunk.averageYNormal;
        totNormalZ += chunk.averageZNormal;
        totValidSlope += chunk.averageValidSlope;
        totWaterAmount += chunk.averageWaterAmount;

        if(isGeneratorTrainer && !genAgent.hasComputedReward)
        {
            if(chunkCollidersMade >= genAgent.minimumChunkColliders)
            {
                genAgent.ComputeRewards();
                genAgent.RequestDecision();
            }
            trainerChunksGenerated = chunkCollidersMade >= genAgent.minimumChunkColliders;
        }

        if (!isGeneratorTrainer)
        {
            chunk.CreateTrees();
        }

        if (!playerPlayScript.thirdPersonPlayer && !isGeneratorTrainer)
        {
            // Update the surface because colliders are created for volume modifiers now
            navMeshSurface.BuildNavMesh();
        }
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

    public void ResetGenerator()
    {
        ThreadDataRequester.ClearDataQueue();

        foreach(TerrainChunk chunk in terrainChunkDictionary.Values)
        {
            chunk.ClearAll();
        }

        terrainChunkDictionary.Clear();
        visibleTerrainChunks.Clear();

        // Delete Earlier Chunks
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform chunkTransform = transform.GetChild(i);
            GameObject.Destroy(chunkTransform.gameObject);
        }

        //// Call garbage collector
        System.GC.Collect();

        // Init Terrain
        InitTerrain();
    }
}