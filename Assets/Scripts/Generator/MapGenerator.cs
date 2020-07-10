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

    // 241 - 1 = 240 is divisible by a lot more factors than the unity limit for chunk size (255)
    // 239 Because we are adding 2 vertices for the padding
    public const int mapChunkSize = 239;
    public float terrainScale = 1f;

    [Range(0, 6)]
    public int editorLevelOfDetail;

    [Header("Noise Parameters")]
    // ML STUFF: These Values will be modified by the ML agent to create different terrain maps
    // Generate Multiple Terrain Chunks after setting noiseNormalized to GLOBAL
    // The ML agent will define which terrain is better based on % that is navigable, sloping, less percentage of areas accessible, etc
    // This can be done by just the "noiseEstimatorVariable" or changing all the NoiseMap parameters as well
    // Make sure there aren't too many plateaus or cut offs
    // Will ensure generated areas are better for navigation and also to showcase all regions
    public Noise.NormalizeMode noiseNormalized;
    public float noiseEstimatorVariable;
    public float noiseScale;

    [Min(0)]
    public int octaves;
    [Range(0, 1)]
    public float persistence;
    [Min(1)]
    public float lacunarity;
    public int seed;
    public Vector2 offset;

    [Header("Curve Parameters")]
    public float heightMultiplier;
    public AnimationCurve heightCurve;

    public bool useFalloff;
    public AnimationCurve falloffCurve;

    //  ML STUFF: This can be randomized by the ML agent when generating types of forests, plains, deserts, mountains, islands, plateaus etc
    // Could be based on the noise parameters or certain limits can be assigned to noise parameters based on the kind of reion to generate
    // This can also be used to make sure all the regions appear in the generations(make sure mountains have snowy peaks for example)
    public TerrainType[] regions;

    MapDisplay mapDisplay;
    float[,] fallOffMap;
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

    void Awake()
    {
        fallOffMap = FalloffGenerator.GenerateFallOfMap(mapChunkSize, falloffCurve);
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
        if (useFalloff)
        {
            fallOffMap = FalloffGenerator.GenerateFallOfMap(mapChunkSize, falloffCurve);
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
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFallOfMap(mapChunkSize, falloffCurve)));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            mapDisplay.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, heightMultiplier, heightCurve, editorLevelOfDetail), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
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
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, heightMultiplier, heightCurve, levelOfDetail);

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
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize + 2, mapChunkSize + 2, noiseScale, octaves, persistence, lacunarity, seed, center + offset, noiseNormalized, noiseEstimatorVariable);

        // Create Color map
        Color[] colorMap = new Color[mapChunkSize * mapChunkSize]; 
        for(int y = 0; y < mapChunkSize; y++)
        {
            for(int x = 0; x < mapChunkSize; x++)
            {
                if(useFalloff)
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



    public void ShowTexturePreview()
    {
        if(!mapDisplay.textureRender.gameObject.activeSelf)
        {
            mapDisplay.textureRender.gameObject.SetActive(true);
            mapDisplay.meshRednerer.gameObject.SetActive(false);
        }
    }

    public void ShowMeshPreview()
    {
        if (!mapDisplay.meshRednerer.gameObject.activeSelf)
        {
            mapDisplay.meshRednerer.gameObject.SetActive(true);
            mapDisplay.textureRender.gameObject.SetActive(false);
        }
    }
}
