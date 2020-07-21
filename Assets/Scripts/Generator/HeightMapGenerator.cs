using UnityEngine;

public static class HeightMapGenerator
{
    public static HeightMap GenerateHeightMap(int width, int height, HeightMapSettings settings, Vector2 sampleCenter)
    {
        float[,] values = Noise.GenerateNoiseMap(width, height, settings.noiseSettings, sampleCenter);
        // Create a new height Curve because curve evaluation will cause problems when accessed from different threads
        // Alternatively, lock the thread but it is slower
        AnimationCurve heightCurve = new AnimationCurve(settings.heightCurve.keys);

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        float[,] fallOffValues = FalloffGenerator.GenerateFallOfMap(width, settings.falloffCurve);

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                values[i, j] *= (heightCurve.Evaluate(values[i, j]) * settings.heightMultiplier);

                if (values[i, j] > maxValue)
                {
                    maxValue = values[i, j];
                }
                if (values[i, j] < minValue)
                {
                    minValue = values[i, j];
                }

                if (settings.useFalloff)
                {
                    values[i, j] *= fallOffValues[i, j];
                }
            }
        }

        return new HeightMap(values, minValue, maxValue);
    }
}
public struct HeightMap
{
    // readonly so that they can't be modified after creation
    public readonly float[,] values;
    public readonly float minValue;
    public readonly float maxValue;

    public HeightMap(float[,] values, float minValue, float maxValue)
    {
        this.values = values;
        this.minValue = minValue;
        this.maxValue = maxValue;
    }
}