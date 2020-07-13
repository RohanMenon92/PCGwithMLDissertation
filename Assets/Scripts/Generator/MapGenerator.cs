using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;

public enum DrawMode
{
    NoiseMap,
    FalloffMap,
    Mesh
};

public struct MapData
{
    // readonly so that they can't be modified after creation
    public readonly float[,] heightMap;

    public MapData(float[,] heightMap)
    {
        this.heightMap = heightMap;
    }
}

public class MapGenerator : MonoBehaviour
{
    public DrawMode drawMode;

    public bool autoUpdate;

    [Range(0, MeshGenerator.numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, MeshGenerator.numSupportedFlatShadedChunkSizes - 1)]
    public int flatShadedChunkSizeIndex;

    [Range(0, MeshGenerator.numSupportedLODs - 1)]
    public int editorLevelOfDetail;

    //  ML STUFF: This can be randomized by the ML agent when generating types of forests, plains, deserts, mountains, islands, plateaus etc
    // Could be based on the noise parameters or certain limits can be assigned to noise parameters based on the kind of reion to generate
    // This can also be used to make sure all the regions appear in the generations(make sure mountains have snowy peaks for example)
    //public TerrainType[] regions;

    public MapDisplay mapDisplay;
    float[,] fallOffMap;

    public TerrainData terrainData;
    public NoiseData noiseData;
    public TextureData textureData;

    public Material terrainMaterial;

    // Struct to handle map data and thread data
    // Generic so that it can handle both
    struct MapThreadInfo<T>
    {
        // readonly so that they can't be modified after creation
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    // Return different values for mapChunkSize when using flatShading
    public int mapChunkSize
    {
        get
        {
            if (terrainData.useFlatShading)
            {
                return MeshGenerator.supportedFlatShadedMeshSizes[flatShadedChunkSizeIndex] - 1;
            } else
            {
                return MeshGenerator.supportedMeshSizes[chunkSizeIndex] - 1;
            }
        }
    }

    void OnTextureValuesUpdated()
    {
        //textureData.ApplyToMaterial(terrainMaterial);
    }

    void OnValuesUpdated()
    {
        if(terrainData.useFalloff)
        {
            fallOffMap = FalloffGenerator.GenerateFallOfMap(mapChunkSize + 2, terrainData.falloffCurve);
        }

        // Redraw the map in editor if values are updated
        if(!Application.isPlaying)
        {
            DrawMapInEditor();
        }
    }

    private void OnValidate()
    {
        // Subscribe to auto update values when something changes in terrain data or noise data
        // Unsubscribe and resubscribe because you don't want to keep adding the same event
        if (terrainData != null) 
        {
            terrainData.OnValuesUpdated -= OnValuesUpdated;
            terrainData.OnValuesUpdated += OnValuesUpdated;
        }
        if (noiseData != null)
        {
            noiseData.OnValuesUpdated -= OnValuesUpdated;
            noiseData.OnValuesUpdated += OnValuesUpdated;
        }
        if(textureData != null)
        {
            textureData.OnValuesUpdated -= OnTextureValuesUpdated;
            textureData.OnValuesUpdated += OnTextureValuesUpdated;
        }
    }

    void Awake()
    {
    }

    // Start is called before the first frame update
    void Start()
    {
        mapDisplay = this.GetComponent<MapDisplay>();
        // Update minimum and maximmum mesh heights
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }

    public void DrawMapInEditor()
    {
        if (mapDisplay == null)
        {
            mapDisplay = FindObjectOfType<MapDisplay>();
        }
        MapData mapData = GenerateMapData(Vector2.zero);

        if (drawMode == DrawMode.NoiseMap)
        {
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFallOfMap(mapChunkSize, terrainData.falloffCurve)));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            mapDisplay.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.heightMultiplier, terrainData.heightCurve, editorLevelOfDetail, terrainData.useFlatShading));
        }

        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }

    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        // Start thrread for generating map data
        ThreadStart threadStart = delegate
        {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        // Start Generate Map on a thread
        MapData mapData = GenerateMapData(center);

        // Do not let multiple threads access mapDataThreadInfoQueue at the same time
        // Prevent queue being unordered, etc
        lock (mapDataThreadInfoQueue)
        {
            // Add thread to the queue to request data after
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int levelOfDetail, Action<MeshData> callback)
    {
        // Start thrread for generating mesh data
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, levelOfDetail, callback);
        };

        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int levelOfDetail, Action<MeshData> callback)
    {
        // Generate MeshData
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.heightMultiplier, terrainData.heightCurve, levelOfDetail, terrainData.useFlatShading);

        // Do not let multiple threads access mapDataThreadInfoQueue at the same time
        // Prevent queue being unordered, etc
        lock (meshDataThreadInfoQueue)
        {
            // Add thread to the queue to request data after
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Look at map thread Info Queue and request data
        if(mapDataThreadInfoQueue.Count > 0)
        {
            for(int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                // Get the next set of thread info by taking it out from the queue
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                // Call the thread info callback with the relevant data
                threadInfo.callback(threadInfo.parameter);
            }
        }

        // Look at mesh thread Info Queue and request data
        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                // Get the next set of thread info by taking it out from the queue
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                // Call the thread info callback with the relevant data
                threadInfo.callback(threadInfo.parameter);
            }
        }

    }

    MapData GenerateMapData(Vector2 center)
    {
        // Generate Initial Noisemap 
        // with additional padding for normal calculation
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize + 2, mapChunkSize + 2, noiseData.noiseScale, noiseData.octaves, noiseData.persistence, noiseData.lacunarity, noiseData.seed, center + noiseData.offset, noiseData.noiseNormalized, noiseData.noiseEstimatorVariable);

        if (terrainData.useFalloff)
        {
            if(fallOffMap == null)
            {
                fallOffMap = FalloffGenerator.GenerateFallOfMap(mapChunkSize + 2, terrainData.falloffCurve);
            }

            for (int y = 0; y < mapChunkSize + 2; y++)
            {
                for (int x = 0; x < mapChunkSize + 2; x++)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - fallOffMap[x, y]);
                    float currentHeight = noiseMap[x, y];
                }
            }
        }

        return new MapData(noiseMap);
    }
}
