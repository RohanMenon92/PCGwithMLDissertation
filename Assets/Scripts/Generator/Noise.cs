using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    public enum NormalizeMode {
        Local,
        Global
    }

    // These are what the ML agent will control for terrain generation
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, float scale, int octaves, float persistance, float lacunarity, int seed, Vector2 offset, NormalizeMode normalizeMode, float globalNoiseEstimator = 1f)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        float amplitude = 1;
        float frequency = 1;
        float maxPossibleHeight = 0;

        System.Random prng = new System.Random(seed);
        // This is generated from seed so that we sample diffferent points from different positions,
        // Should remain constant for ML agent generators for proper learning
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i< octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            // Unity coordinate system will cause issues for Y, needs to be subtracted
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }


        if (scale <= 0)
        {
            scale = 0.0001f;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;


        // Iterate through map
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                // For each octave, amplitude and frequency should be reset to 1
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = 0f, sampleY = 0f;
                    if(normalizeMode == NormalizeMode.Global) {
                        sampleX = (x - halfWidth + octaveOffsets[i].x) / scale * frequency;
                        sampleY = (y - halfHeight + octaveOffsets[i].y) / scale * frequency;
                    } else if(normalizeMode == NormalizeMode.Local)
                    {
                        sampleX = (x - halfWidth) / scale * frequency + octaveOffsets[i].x;
                        sampleY = (y - halfHeight) / scale * frequency + octaveOffsets[i].y;
                    }

                    // Multiply by 2 and subtract by 1 to convert limits from [-0.5, 0.5] to [0, 1] 
                    float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) * 2) - 1;

                    // Define noiseHeight
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                // update minimum and maximum noiseHeight
                if (normalizeMode == NormalizeMode.Local)
                {
                    if (noiseHeight > maxLocalNoiseHeight)
                    {
                        maxLocalNoiseHeight = noiseHeight;
                    }
                    else if (noiseHeight < minLocalNoiseHeight)
                    {
                        minLocalNoiseHeight = noiseHeight;
                    }
                }
                noiseMap[x, y] = noiseHeight;
            }
        }

        // Normalization of noiseMap according to minNoiseHeight and maxNoiseHeight
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                if(normalizeMode == NormalizeMode.Local)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                } else if(normalizeMode == NormalizeMode.Global)
                {
                    // ML STUFF: Analyze the noise map and make sure the hills area/ and troughts area falls within a reasonable limit 
                    // ML STUFF: Analyze the global noise map, and make sure no parts fall out of range 
                    // make a few chunks and change the estimator variable so that there is no noisemap value for which goes above 1f 
                    // put a limit to the globalNoiseEstimator maybe and check if the generated chunks at different noisemaps have a good amount of mountains 
                    // Would probably need a limit as well later
                    float normalizedHeight = (noiseMap[x, y] + 1) / (2f * maxPossibleHeight / globalNoiseEstimator);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }


        return noiseMap;
    }
}
