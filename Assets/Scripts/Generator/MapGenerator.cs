using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;

public enum DrawMode
{
    NoiseMap,
    ColorMap,
    FalloffMap,
    Mesh
};

public struct MapData
{
    // readonly so that they can't be modified after creation
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}

[System.Serializable]
public struct TerrainType {
    public string terrainName;
    public float height;
    public Color color;
}


public class MapGenerator : MonoBehaviour
{
    public DrawMode drawMode;

    public bool autoUpdate;

    // Replaced by chunk size
    //[Header("Map Size")]
    //[Min(10)]
    //public int mapWidth;
    //[Min(10)]
    //public int mapHeight;

    [Range(0, 4)]
    public int editorLevelOfDetail;

    //  ML STUFF: This can be randomized by the ML agent when generating types of forests, plains, deserts, mountains, islands, plateaus etc
    // Could be based on the noise parameters or certain limits can be assigned to noise parameters based on the kind of reion to generate
    // This can also be used to make sure all the regions appear in the generations(make sure mountains have snowy peaks for example)
    public TerrainType[] regions;

    public MapDisplay mapDisplay;
    float[,] fallOffMap;

    public TerrainData terrainData;
    public NoiseData noiseData;

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

    static MapGenerator instance;

    // Return different values for mapChunkSize when using flatShading
    public static int mapChunkSize
    {
        get
        {
            if (instance == null) {
                instance = FindObjectOfType<MapGenerator>();
            }
            if (instance.terrainData.useFlatShading)
            {
                // Minimum Chunk size for flat shading
                // 96 - 1 = 95 is not divisible by 5,  
                // limit for vertex chunk size (255) but we create a lot more vertexes when using flat shading(each triangle is independent)
                return 95;
            } else
            {
                // Chunk Size for regular terrain
                // 241 - 1 = 240 is divisible by a lot more factors than the unity limit for vertex chunk size (255)
                // 239 Because we are adding 2 vertices for the padding
                return 239;
            }
        }
    }

    void OnValuesUpdated()
    {
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
    }

    void Awake()
    {
        fallOffMap = FalloffGenerator.GenerateFallOfMap(mapChunkSize, terrainData.falloffCurve);
    }

    // Start is called before the first frame update
    void Start()
    {
        mapDisplay = this.GetComponent<MapDisplay>();
    }

    public void DrawMapInEditor()
    {
        if (mapDisplay == null)
        {
            mapDisplay = FindObjectOfType<MapDisplay>();
        }
        if (terrainData.useFalloff)
        {
            fallOffMap = FalloffGenerator.GenerateFallOfMap(mapChunkSize, terrainData.falloffCurve);
        }
        MapData mapData = GenerateMapData(Vector2.zero);

        if (drawMode == DrawMode.NoiseMap)
        {
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColorMap)
        {
            mapDisplay.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFallOfMap(mapChunkSize, terrainData.falloffCurve)));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            mapDisplay.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.heightMultiplier, terrainData.heightCurve, editorLevelOfDetail, terrainData.useFlatShading), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
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

        // Create Color map
        Color[] colorMap = new Color[mapChunkSize * mapChunkSize]; 
        for(int y = 0; y < mapChunkSize; y++)
        {
            for(int x = 0; x < mapChunkSize; x++)
            {
                if(terrainData.useFalloff)
                {
                    noiseMap[x, y] = Mathf.Clamp(noiseMap[x, y] - fallOffMap[x, y], 0 ,1);
                }
                float currentHeight = noiseMap[x, y];
                for(int i = 0; i < regions.Length; i++)
                {
                    if(currentHeight >= regions[i].height)
                    {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                    } else
                    {
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colorMap);
    }
}
