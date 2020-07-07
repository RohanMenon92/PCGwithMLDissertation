using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TerrainType {
    public string terrainName;
    public float height;
    public Color color;
}

public enum DrawMode {
    NoiseMap,
    ColorMap
};

public class MapGenerator : MonoBehaviour
{
    public DrawMode drawMode;

    public bool autoUpdate;

    [Min(10)]
    public int mapWidth;
    [Min(10)]
    public int mapHeight;
    public float noiseScale;

    // ML STUFF: These Values will be modified by the ML agent to create different terrain maps
    // The ML agent will define which terrain is better based on % that is navigable, sloping, etc
    [Min(0)]
    public int octaves;
    [Range(0, 1)]
    public float persistence;
    [Min(1)]
    public float lacunarity;
    public int seed;
    public Vector2 offset;

    MapDisplay mapDisplay;

    //  ML STUFF: This can be randomized by the ML agent when generating types of terrain, plains, deserts, mountains, etc 
    public TerrainType[] regions;

    public void GenerateMap()
    {
        if(mapDisplay == null)
        {
            mapDisplay = this.GetComponent<MapDisplay>();
        }

        // Generate Initial Noisemap
        float[,] noiseMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, noiseScale, octaves, persistence, lacunarity, seed, offset);

        // Create Color map
        Color[] colorMap = new Color[mapWidth * mapHeight]; 
        for(int y = 0; y < mapHeight; y++)
        {
            for(int x = 0; x < mapWidth; x++)
            {
                float currentHeight = noiseMap[x, y];
                for(int i = 0; i < regions.Length; i++)
                {
                    if(currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapWidth + x] = regions[i].color;
                        break;
                    }
                }
            }
        }


        if(drawMode == DrawMode.NoiseMap)
        {
            mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));    
        } else if(drawMode == DrawMode.ColorMap)
        {
            mapDisplay.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        mapDisplay = this.GetComponent<MapDisplay>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
