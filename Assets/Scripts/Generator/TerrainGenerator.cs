using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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

    public Transform viewer;
    public Material terrainMaterial;

    Vector2 viewerPosition;
    Vector2 lastUpdateViewerPosition;
    int chunksVisibleInViewDst;
    float meshWorldSize;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    ObjectCreator objectCreator;

    // Start is called before the first frame update
    void Start()
    {
        objectCreator = FindObjectOfType<ObjectCreator>();

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
                        // subscribe to visibility changed before load
                        newChunk.Load();
                    }
                }
            }
        }
    }

    void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible)
    {
        if(isVisible)
        {
            visibleTerrainChunks.Add(chunk);
        } else
        {
            visibleTerrainChunks.Remove(chunk);
        }
    }
}