using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    [System.Serializable]
    public struct LODInfo
    {
        [Range(0, MeshGenerator.numSupportedLODs - 1)]
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

        void OnMeshDataReceived(MeshData meshData)
        {

            mesh = meshData.CreateMesh();
            hasMesh = true;
            updateCallback();
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGen.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }
    }

    static float terrainScale;
    const float viewerThresholdToUpdate = 25f;
    const float colliderGenrationThreshold = 5f;
    // square distances are easier to calculate
    const float sqrViewerThresholdToUpdate = viewerThresholdToUpdate * viewerThresholdToUpdate;

    public static MapGenerator mapGen;

    public int colliderLODindex;
    public LODInfo[] detailLevels;
    public static float maxViewDst;
    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 lastUpdateViewerPosition;
    int chunkSize;
    int chunksVisibleInViewDst;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    // Start is called before the first frame update
    void Start()
    {
        // max view distance should be last detail level
        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;

        // Set map Generator
        mapGen = FindObjectOfType<MapGenerator>();
        terrainScale = mapGen.terrainData.terrainScale;

        // Size of vertices in chunks is actually 1 less than this number
        chunkSize = mapGen.mapChunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        // Call once at start
        UpdateVisibleChunks();
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / terrainScale;

        if(viewerPosition != lastUpdateViewerPosition)
        {
            foreach(TerrainChunk chunk in visibleTerrainChunks)
            {
                chunk.UpdateCollisionMesh();
            }
        }

        if((lastUpdateViewerPosition - viewerPosition).sqrMagnitude > sqrViewerThresholdToUpdate)
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
        for (int i= visibleTerrainChunks.Count-1; i>=0; i--)
        {
            alreadyUpdateChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

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
                        // Add new terrain chunk and parent it to this transform
                        terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, colliderLODindex, transform, mapMaterial));
                    }
                }
            }
        }
    }

    public class TerrainChunk {
        public Vector2 coord;
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        // LOD Data
        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        int colliderLODindex;

        MapData mapData;        
        bool mapDataReceived;
        bool hasSetCollider = false;

        int prevLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, int colliderLODindex, Transform parent, Material meshMaterial)
        {
            this.coord = coord;
            this.detailLevels = detailLevels;
            this.colliderLODindex = colliderLODindex;

            position = coord * size;
            Vector3 chunkPosition = new Vector3(position.x, 0, position.y);
            bounds = new Bounds(position, Vector2.one * size);


            // Create plane
            meshObject = new GameObject("TerrainChunk_" + coord.x + ":" + coord.y);
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            meshObject.transform.parent = parent;
            meshObject.transform.position = chunkPosition * terrainScale;
            meshObject.transform.localScale = Vector3.one * terrainScale;
            SetVisible(false);

            // Create LOD meshes for all levels of detail
            lodMeshes = new LODMesh[detailLevels.Length];
            for(int i = 0; i< detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod);
                if (i == colliderLODindex)
                {
                    lodMeshes[i].updateCallback += UpdateCollisionMesh;
                }
                lodMeshes[i].updateCallback += UpdateTerrainChunk;
            }

            //print("RequestMapData");
            // Add request for data in mapGenererator and create a thread
            mapGen.RequestMapData(coord * size, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            // Create color map for this map data
            //Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            // Set smoothness to zero because lit instances will be used
            meshRenderer.material = mapGen.terrainMaterial;
            meshRenderer.material.SetFloat("_Smoothness", 0f);

            UpdateTerrainChunk();
            //print("Map Data received");
            //mapGen.RequestMeshData(mapData, OnMeshDataReceived);
        }

        public void UpdateTerrainChunk()
        {
            if(!mapDataReceived)
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

                for(int i = 0; i<detailLevels.Length - 1; i++)
                {
                    // In the last case, chunk will not be visible
                    if(viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold)
                    {
                        lodIndex = i + 1;
                    } else
                    {
                        break;
                    }
                }

                if(lodIndex != prevLODIndex)
                {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if(lodMesh.hasMesh)
                    {
                        prevLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    } else if(!lodMesh.hasRequestedMesh)
                    {
                        lodMesh.RequestMesh(mapData);
                    }
                }
            }

            if(wasVisible != visible)
            {
                if(visible)
                {
                    visibleTerrainChunks.Add(this);
                } else
                {
                    visibleTerrainChunks.Remove(this);
                }

                SetVisible(visible);
            }
        }

        // Check that is called more frequently than updating the mesh
        public void UpdateCollisionMesh()
        {
            if(hasSetCollider)
            {
                return;
            }
            float sqrDistnceFromViewerEdge = bounds.SqrDistance(viewerPosition);

            if(sqrDistnceFromViewerEdge < detailLevels[colliderLODindex].sqrVisibleDistanceThreshold)
            {
                if(!lodMeshes[colliderLODindex].hasRequestedMesh)
                {
                    lodMeshes[colliderLODindex].RequestMesh(mapData);
                }
            }

            if (sqrDistnceFromViewerEdge > colliderGenrationThreshold * colliderGenrationThreshold || sqrDistnceFromViewerEdge == 0)
            {
                if (lodMeshes[colliderLODindex].hasMesh) {
                    meshCollider.sharedMesh = lodMeshes[colliderLODindex].mesh;
                    hasSetCollider = true;
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
}
