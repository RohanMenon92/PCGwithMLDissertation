using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDstThreshold;
    }

    const float viewerThresholdToUpdate = 25f;
    // square distances are easier to calculate
    const float sqrViewerThresholdToUpdate = viewerThresholdToUpdate * viewerThresholdToUpdate;

    public static MapGenerator mapGen;

    public LODInfo[] detailLevels;

    public static float maxViewDst;

    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 lastUpdateViewerPosition;
    int chunkSize;
    int chunksVisibleInViewDst;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    // Start is called before the first frame update
    void Start()
    {
        // max view distance should be last detail level
        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;

        // Set map Generator
        mapGen = FindObjectOfType<MapGenerator>();

        // Size of vertices in chunks is actually 1 less than this number
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        // Call once at start
        UpdateVisibleChunks();
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

        if((lastUpdateViewerPosition - viewerPosition).sqrMagnitude > sqrViewerThresholdToUpdate)
        {
            // Update whenever the viewer moves a certain threshold
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        lastUpdateViewerPosition = viewerPosition;
        // Check Removal of last few chunks
        for (int i=0; i<terrainChunksVisibleLastUpdate.Count; i++)
        {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        // Create new chunks
        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if(terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    // Update chunk
                    terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    if(terrainChunkDictionary[viewedChunkCoord].IsVisible())
                    {
                        terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
                    }
                } else
                {
                    // Add new terrain chunk and parent it to this transform
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
                }
            }
        }
    }

    public class TerrainChunk {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;

        // LOD Data
        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;

        MapData mapData;        
        bool mapDataReceived;

        int prevLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material meshMaterial)
        {
            this.detailLevels = detailLevels;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);

            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            // Create plane
            meshObject = new GameObject("TerrainChunk_" + coord.x + ":" + coord.y);
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer = meshObject.AddComponent<MeshRenderer>();

            meshObject.transform.parent = parent;
            meshObject.transform.position = positionV3;
            SetVisible(false);

            // Create LOD meshes for all levels of detail
            lodMeshes = new LODMesh[detailLevels.Length];
            for(int i = 0; i< detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
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
            Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            // Set smoothness to zero because lit instances will be used
            meshRenderer.material.SetFloat("_Smoothness", 0f);
            meshRenderer.material.mainTexture = texture;

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
                        meshFilter.mesh = lodMesh.mesh;
                    } else if(!lodMesh.hasRequestedMesh)
                    {
                        lodMesh.RequestMesh(mapData);
                    }
                }
            }

            SetVisible(visible);
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
        System.Action updateCallback;


        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
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
}
