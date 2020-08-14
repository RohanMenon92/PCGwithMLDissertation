using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    public enum NormalizeMode {
        Local,
        Global
    }

    // These can be what the ML agent will control for terrain/map generation
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, NoiseSettings settings, Vector2 sampleCenter)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        float amplitude = 1;
        float frequency = 1;
        float maxPossibleHeight = 0;

        System.Random prng = new System.Random(settings.seed);

        // This is generated from seed so that we sample diffferent points from different positions,
        // Should remain constant for ML agent generators for proper learning
        Vector2[] octaveOffsets = new Vector2[settings.octaves];
        for (int i = 0; i< settings.octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + settings.offset.x + sampleCenter.x;
            // Unity coordinate system will cause issues for Y, needs to be subtracted
            float offsetY = prng.Next(-100000, 100000) - settings.offset.y - sampleCenter.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= settings.persistence;
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

                for (int i = 0; i < octaveOffsets.Length; i++)
                {
                    float sampleX = 0f, sampleY = 0f;
                    if(settings.normalizeMode == NormalizeMode.Global) {
                        sampleX = (x - halfWidth + octaveOffsets[i].x) / settings.scale * frequency;
                        sampleY = (y - halfHeight + octaveOffsets[i].y) / settings.scale * frequency;
                    } else if(settings.normalizeMode == NormalizeMode.Local)
                    {
                        sampleX = (x - halfWidth) / settings.scale * frequency + octaveOffsets[i].x;
                        sampleY = (y - halfHeight) / settings.scale * frequency + octaveOffsets[i].y;
                    }

                    // Multiply by 2 and subtract by 1 to convert limits from [-0.5, 0.5] to [0, 1] 
                    float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) * 2) - 1;

                    // Define noiseHeight
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= settings.persistence;
                    frequency *= settings.lacunarity;
                }

                // update minimum and maximum noiseHeight
                if (settings.normalizeMode == NormalizeMode.Local)
                {
                    if (noiseHeight > maxLocalNoiseHeight)
                    {
                        maxLocalNoiseHeight = noiseHeight;
                    }
                    if (noiseHeight < minLocalNoiseHeight)
                    {
                        minLocalNoiseHeight = noiseHeight;
                    }
                }
                noiseMap[x, y] = noiseHeight;

                if (settings.normalizeMode == NormalizeMode.Global)
                {
                    // ML STUFF: Analyze the noise map and make sure the hills area/ and troughts area falls within a reasonable limit 
                    // ML STUFF: Analyze the global noise map, and make sure no parts fall out of range 
                    // make a few chunks and change the estimator variable so that there is no noisemap value for which goes above 1f 
                    // put a limit to the globalNoiseEstimator maybe and check if the generated chunks at different noisemaps have a good amount of mountains 
                    // Would probably need a limit as well later
                    float normalizedHeight = (noiseMap[x, y] + 1) / (2f * maxPossibleHeight / settings.noiseEstimatorVariable);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }

        if (settings.normalizeMode == NormalizeMode.Local)
        {
            // Normalization of noiseMap according to minNoiseHeight and maxNoiseHeight
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                }
            }
        }

        return noiseMap;
    }
}

[System.Serializable]
public class NoiseSettings
{
    [Header("Noise Parameters")]
    // ML STUFF: These Values will be modified by the ML agent to create different terrain maps
    // Generate Multiple Terrain Chunks after setting noiseNormalized to GLOBAL
    // The ML agent will define which terrain is better based on % that is navigable, sloping, less percentage of areas accessible, etc
    // This can be done by just the "noiseEstimatorVariable" or changing all the NoiseMap parameters as well
    // Make sure there aren't too many plateaus or cut offs
    // Will ensure generated areas are better for navigation and also to showcase all regions
    public Noise.NormalizeMode normalizeMode;
    public float noiseEstimatorVariable;
    [Min(0.01f)]
    public float scale = 50;

    [Min(1)]
    public int octaves = 6;
    [Range(0, 1)]
    public float persistence = 0.5f;
    [Range(1, 5)]
    public float lacunarity = 1.5f;
    public int seed;
    public Vector2 offset;

#if UNITY_EDITOR
    public void ValidateValues()
    {
        // For further validation of noise scripts
    }
#endif
}
