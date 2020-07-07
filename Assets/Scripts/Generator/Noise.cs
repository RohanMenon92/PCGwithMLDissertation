using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    // These are what the ML agent will control for terrain generation
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, float scale, int octaves, float persistance, float lacunarity, int seed, Vector2 offset)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        System.Random prng = new System.Random(seed);
        // This is generated from seed so that we sample diffferent points from different positions,
        // Should remain constant for ML agent generators for proper learning
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i< octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }


        if (scale <= 0)
        {
            scale = 0.0001f;
        }

        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;


        // Iterate through map
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                // For each octave
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x-halfWidth) / scale * frequency + octaveOffsets[i].x + offset.x;
                    float sampleY = (y-halfHeight) / scale * frequency + octaveOffsets[i].y + offset.y;

                    float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) * 2) - 1;

                    // Define noiseHeight
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                // update minimum and maximum noiseHeight
                if (noiseHeight > maxNoiseHeight)
                {
                    maxNoiseHeight = noiseHeight;
                } else if (noiseHeight < minNoiseHeight)
                {
                    minNoiseHeight = noiseHeight;
                }

                noiseMap[x, y] = noiseHeight;
            }
        }

        // Normalization of noiseMap according to minNoiseHeight and maxNoiseHeight
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                noiseMap[x, y] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseMap[x, y]);
            }
        }


                return noiseMap;
    }
}
